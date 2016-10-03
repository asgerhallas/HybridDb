using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Dapper;
using HybridDb.Commands;
using HybridDb.Config;
using HybridDb.Linq2;
using HybridDb.Linq2.Ast;
using HybridDb.Migrations;
using Serilog;
using ShinySwitch;
using Column = HybridDb.Config.Column;
using Switch = ShinySwitch.Switch;

namespace HybridDb
{
    public class DocumentStore : IDocumentStore
    {
        readonly bool testing;

        Guid lastWrittenEtag;
        long numberOfRequests;

        internal DocumentStore(Configuration configuration, TableMode mode, string connectionString, bool testing)
        {
            Configuration = configuration;
            Logger = configuration.Logger;

            switch (mode)
            {
                case TableMode.UseRealTables:
                    Database = new SqlServerUsingRealTables(this, connectionString);
                    break;
                case TableMode.UseTempTables:
                    Database = new SqlServerUsingTempTables(this, connectionString);
                    break;
                case TableMode.UseTempDb:
                    Database = new SqlServerUsingTempDb(this, connectionString);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("mode", mode, null);
            }

            this.testing = testing;
        }

        internal DocumentStore(DocumentStore store, Configuration configuration, bool testing)
        {
            Configuration = configuration;
            Database = store.Database;
            Logger = configuration.Logger;

            this.testing = testing;
        }

        public static IDocumentStore Create(string connectionString, Action<Configuration> configure = null)
        {
            configure = configure ?? (x => { });
            var configuration = new Configuration();
            configure(configuration);
            return new DocumentStore(configuration, TableMode.UseRealTables, connectionString, false);
        }

        public static IDocumentStore ForTesting(TableMode mode, Action<Configuration> configure = null)
        {
            return ForTesting(mode, null, configure);
        }

        public static IDocumentStore ForTesting(TableMode mode, string connectionString, Action<Configuration> configure = null)
        {
            configure = configure ?? (x => { });
            var configuration = new Configuration();
            configure(configuration);
            return new DocumentStore(configuration, mode, connectionString ?? "data source=.;Integrated Security=True", true);
        }

        public IDatabase Database { get; }
        public ILogger Logger { get; private set; }
        public Configuration Configuration { get; }
        public bool IsInitialized { get; private set; }

        public void Initialize()
        {
            if (IsInitialized)
                return;

            Configuration.Initialize();

            Logger = Configuration.Logger;

            new SchemaMigrationRunner(this, new SchemaDiffer()).Run();
            var documentMigration = new DocumentMigrationRunner().Run(this);
            if (testing) documentMigration.Wait();

            IsInitialized = true;
        }

        public IDocumentSession OpenSession()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("You must call Initialize() on the store before opening a session.");
            }

            return new DocumentSession(this);
        }

        public Guid Execute(IReadOnlyList<DatabaseCommand> commands)
        {
            commands = commands.ToList();

            if (!commands.Any())
                return LastWrittenEtag;

            var timer = Stopwatch.StartNew();
            using (var connectionManager = Database.Connect())
            {
                var i = 0;
                var etag = Guid.NewGuid();
                var sql = "";
                var parameters = new List<Parameter>();
                var numberOfParameters = 0;
                var expectedRowCount = 0;
                var numberOfInsertCommands = 0;
                var numberOfUpdateCommands = 0;
                var numberOfDeleteCommands = 0;
                foreach (var command in commands)
                {
                    var preparedCommand =
                        Switch<SqlDatabaseCommand>.On(command)
                            .Match<InsertCommand>(insert => 
                            {
                                numberOfInsertCommands++;
                                return PrepareInsertCommand(insert, etag, i++);
                            })
                            .Match<UpdateCommand>(update =>
                            {
                                numberOfUpdateCommands++;
                                return PrepareUpdateCommand(update, etag, i++);
                            })
                            .Match<DeleteCommand>(delete =>
                            {
                                numberOfDeleteCommands++;
                                return PrepareDeleteCommand(delete, i++);
                            })
                            .OrThrow();
    
                    var numberOfNewParameters = preparedCommand.Parameters.Count;

                    if (numberOfNewParameters >= 2100)
                        throw new InvalidOperationException("Cannot execute a command with more than 2100 parameters.");

                    if (numberOfParameters + numberOfNewParameters >= 2100)
                    {
                        InternalExecute(connectionManager, sql, parameters, expectedRowCount);

                        sql = "";
                        parameters = new List<Parameter>();
                        expectedRowCount = 0;
                        numberOfParameters = 0;
                    }

                    expectedRowCount += preparedCommand.ExpectedRowCount;
                    numberOfParameters += numberOfNewParameters;

                    sql += $"{preparedCommand.Sql};";
                    parameters.AddRange(preparedCommand.Parameters);
                }

                InternalExecute(connectionManager, sql, parameters, expectedRowCount);

                connectionManager.Complete();

                Logger.Information("Executed {0} inserts, {1} updates and {2} deletes in {3}ms",
                            numberOfInsertCommands,
                            numberOfUpdateCommands,
                            numberOfDeleteCommands,
                            timer.ElapsedMilliseconds);

                lastWrittenEtag = etag;
                return etag;
            }
        }

        // ReSharper disable once UnusedParameter.Local
        void InternalExecute(ManagedConnection managedConnection, string sql, List<Parameter> parameters, int expectedRowCount)
        {
            var fastParameters = new FastDynamicParameters(parameters);
            var rowcount = managedConnection.Connection.Execute(sql, fastParameters);
            Interlocked.Increment(ref numberOfRequests);
            if (rowcount != expectedRowCount)
                throw new ConcurrencyException();
        }

        public IEnumerable<TProjection> Query<TProjection>(
            DocumentTable table, out QueryStats stats, string select = null, string where = "",
            int skip = 0, int take = 0, string orderby = "", object parameters = null)
        {
            if (select.IsNullOrEmpty() || select == "*")
                select = "";

            var projectToDictionary = typeof (TProjection).IsA<IDictionary<string, object>>();
            if (!projectToDictionary)
                select = MatchSelectedColumnsWithProjectedType<TProjection>(select);

            var timer = Stopwatch.StartNew();
            using (var connection = Database.Connect())
            {
                var sql = new SqlBuilder();

                var isWindowed = skip > 0 || take > 0;

                if (isWindowed)
                {
                    sql.Append("select count(*) as TotalResults")
                       .Append("from {0}", Database.FormatTableNameAndEscape(table.Name))
                       .Append(!string.IsNullOrEmpty(@where), "where {0}", @where)
                       .Append(";");

                    sql.Append(@"with temp as (select *")
                       .Append(", row_number() over(ORDER BY {0}) as RowNumber", string.IsNullOrEmpty(@orderby) ? "CURRENT_TIMESTAMP" : @orderby)
                       .Append("from {0}", Database.FormatTableNameAndEscape(table.Name))
                       .Append(!string.IsNullOrEmpty(@where), "where {0}", @where)
                       .Append(")")
                       .Append("select {0} from temp", select.IsNullOrEmpty() ? "*" : select + ", RowNumber")
                       .Append("where RowNumber >= {0}", skip + 1)
                       .Append(take > 0, "and RowNumber <= {0}", skip + take)
                       .Append("order by RowNumber");
                }
                else
                {
                    sql.Append(@"with temp as (select *")
                       .Append(", 0 as RowNumber")
                       .Append("from {0}", Database.FormatTableNameAndEscape(table.Name))
                       .Append(!string.IsNullOrEmpty(@where), "where {0}", @where)
                       .Append(")")
                       .Append("select {0} from temp", select.IsNullOrEmpty() ? "*" : select + ", RowNumber")
                       .Append(!string.IsNullOrEmpty(orderby), "order by {0}", orderby);
                }
                
                IEnumerable<TProjection> result;
                if (projectToDictionary)
                {
                    result = (IEnumerable<TProjection>)
                        InternalQuery<object>(connection, sql, parameters, isWindowed, out stats)
                            .Cast<IDictionary<string, object>>();
                }
                else
                {
                    result = InternalQuery<TProjection>(connection, sql, parameters, isWindowed, out stats);
                }

                stats.QueryDurationInMilliseconds = timer.ElapsedMilliseconds;

                if (isWindowed)
                {
                    var potential = stats.TotalResults - skip;
                    if (potential < 0)
                        potential = 0;

                    stats.RetrievedResults = take > 0 && potential > take ? take : potential;
                }
                else
                {
                    stats.RetrievedResults = stats.TotalResults;
                }

                Interlocked.Increment(ref numberOfRequests);

                Logger.Information("Retrieved {0} of {1} in {2}ms", stats.RetrievedResults, stats.TotalResults, stats.QueryDurationInMilliseconds);

                connection.Complete();
                return result;
            }
        }

        public IEnumerable<TProjection> Query<TProjection>(SelectStatement @select, out QueryStats stats)
        {
            Table table;
            if (!Configuration.Tables.TryGetValue(@select.From.Table, out table))
                throw new ArgumentException($"Table '{@select.From.Table}' was not found in configuration.");

            // semantics parse -> symbols and types
            // typecheck

            // 
            // emit sql

            //make select get all properties of projection object type - and check if they are actually projections
            var projectToDictionary = typeof(TProjection).IsA<IDictionary<string, object>>();
            //if (!projectToDictionary)
            //    select = MatchSelectedColumnsWithProjectedType<TProjection>(select);

            var sqlStatement = new SqlStatementEmitter().Emit(@select);

            var timer = Stopwatch.StartNew();
            using (var connection = Database.Connect())
            {
                var sql = new SqlBuilder();

                var isWindowed = sqlStatement.Skip > 0 || sqlStatement.Take > 0;

                if (isWindowed)
                {
                    sql.Append("select count(*) as TotalResults")
                       .Append("from {0}", Database.FormatTableNameAndEscape(table.Name))
                       .Append(!string.IsNullOrEmpty(sqlStatement.Where), "where {0}", sqlStatement.Where)
                       .Append(";");

                    sql.Append(@"with temp as (select *")
                       .Append(", row_number() over(ORDER BY {0}) as RowNumber", string.IsNullOrEmpty(sqlStatement.OrderBy) ? "CURRENT_TIMESTAMP" : sqlStatement.OrderBy)
                       .Append("from {0}", Database.FormatTableNameAndEscape(table.Name))
                       .Append(!string.IsNullOrEmpty(sqlStatement.Where), "where {0}", sqlStatement.Where)
                       .Append(")")
                       .Append("select {0} from temp", sqlStatement.Select.IsNullOrEmpty() ? "*" : sqlStatement.Select + ", RowNumber")
                       .Append("where RowNumber >= {0}", sqlStatement.Skip + 1)
                       .Append(sqlStatement.Take > 0, "and RowNumber <= {0}", sqlStatement.Skip + sqlStatement.Take)
                       .Append("order by RowNumber");
                }
                else
                {
                    sql.Append(@"with temp as (select *")
                       .Append(", 0 as RowNumber")
                       .Append("from {0}", Database.FormatTableNameAndEscape(table.Name))
                       .Append(!string.IsNullOrEmpty(sqlStatement.Where), "where {0}", sqlStatement.Where)
                       .Append(")")
                       .Append("select {0} from temp", sqlStatement.Select.IsNullOrEmpty() ? "*" : sqlStatement.Select + ", RowNumber")
                       .Append(!string.IsNullOrEmpty(sqlStatement.OrderBy), "order by {0}", sqlStatement.OrderBy);
                }

                IEnumerable<TProjection> result;
                if (projectToDictionary)
                {
                    result = (IEnumerable<TProjection>)
                        InternalQuery<object>(connection, sql, sqlStatement.Parameters, isWindowed, out stats)
                            .Cast<IDictionary<string, object>>();
                }
                else
                {
                    result = InternalQuery<TProjection>(connection, sql, sqlStatement.Parameters, isWindowed, out stats);
                }

                stats.QueryDurationInMilliseconds = timer.ElapsedMilliseconds;

                if (isWindowed)
                {
                    var potential = stats.TotalResults - sqlStatement.Skip;
                    if (potential < 0)
                        potential = 0;

                    stats.RetrievedResults = sqlStatement.Take > 0 && potential > sqlStatement.Take ? sqlStatement.Take : potential;
                }
                else
                {
                    stats.RetrievedResults = stats.TotalResults;
                }

                Interlocked.Increment(ref numberOfRequests);

                Logger.Information("Retrieved {0} of {1} in {2}ms", stats.RetrievedResults, stats.TotalResults, stats.QueryDurationInMilliseconds);

                connection.Complete();
                return result;
            }
        }

        static string MatchSelectedColumnsWithProjectedType<TProjection>(string select)
        {
            if (simpleTypes.Contains(typeof(TProjection)))
                return select;

            var neededColumns = typeof(TProjection).GetProperties().Select(x => x.Name).ToList();
            var selectedColumns = 
                from clause in @select.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                let split = Regex.Split(clause, " AS ", RegexOptions.IgnoreCase).Where(x => x != "").ToArray()
                let column = split[0]
                let alias = split.Length > 1 ? split[1] : null
                where neededColumns.Contains(alias)
                select new { column, alias = alias ?? column };

            var missingColumns = 
                from column in neededColumns
                where !selectedColumns.Select(x => x.alias).Contains(column)
                select new {column, alias = column};

            select = string.Join(", ", selectedColumns.Union(missingColumns).Select(x => x.column + " AS " + x.alias));
            return select;
        }

        IEnumerable<T> InternalQuery<T>(ManagedConnection connection, SqlBuilder sql, object parameters, bool hasTotalsQuery, out QueryStats stats)
        {
            var normalizedParameters = new FastDynamicParameters(
                parameters as IEnumerable<Parameter> ?? ConvertToParameters<T>(parameters));

            if (hasTotalsQuery)
            {
                using (var reader = connection.Connection.QueryMultiple(sql.ToString(), normalizedParameters))
                {
                    stats = reader.Read<QueryStats>(buffered: true).Single();
                    return reader.Read<T, object, T>((first, second) => first, "RowNumber", buffered: true);
                }
            }

            using (var reader = connection.Connection.QueryMultiple(sql.ToString(), normalizedParameters))
            {
                var rows = reader.Read<T, object, T>((first, second) => first, "RowNumber", buffered: true).ToList();
                stats = new QueryStats
                {
                    TotalResults = rows.Count
                };
                return rows;
            }
        }

        static IEnumerable<Parameter> ConvertToParameters<T>(object parameters)
        {
            return from projection in parameters as IDictionary<string, object> ?? ObjectToDictionaryRegistry.Convert(parameters)
                   select new Parameter { Name = "@" + projection.Key, Value = projection.Value };
        }

        public IDictionary<string, object> Get(DocumentTable table, string key)
        {
            var timer = Stopwatch.StartNew();
            using (var connection = Database.Connect())
            {
                var sql = string.Format("select * from {0} where {1} = @Id",
                    Database.FormatTableNameAndEscape(table.Name),
                    table.IdColumn.Name);

                var row = ((IDictionary<string, object>)connection.Connection.Query(sql, new { Id = key }).SingleOrDefault());

                Interlocked.Increment(ref numberOfRequests);

                Logger.Information("Retrieved {0} in {1}ms", key, timer.ElapsedMilliseconds);

                connection.Complete();

                return row;
            }
        }

        public long NumberOfRequests => numberOfRequests;
        public Guid LastWrittenEtag => lastWrittenEtag;

        public void Dispose()
        {
            Database.Dispose();
        }

        SqlDatabaseCommand PrepareInsertCommand(InsertCommand command, Guid etag, int uniqueParameterIdentifier)
        {
            var values = command.ConvertAnonymousToProjections(command.Table, command.Projections);

            values[command.Table.IdColumn] = command.Id;
            values[command.Table.EtagColumn] = etag;
            values[command.Table.CreatedAtColumn] = DateTimeOffset.Now;
            values[command.Table.ModifiedAtColumn] = DateTimeOffset.Now;

            var sql = $@"
                insert into {Database.FormatTableNameAndEscape(command.Table.Name)} 
                ({string.Join(", ", from column in values.Keys select column.Name)}) 
                values ({string.Join(", ", from column in values.Keys select "@" + column.Name + uniqueParameterIdentifier)});";

            var parameters = MapProjectionsToParameters(values, uniqueParameterIdentifier);

            return new SqlDatabaseCommand
            {
                Sql = sql,
                Parameters = parameters.Values.ToList(),
                ExpectedRowCount = 1
            };
        }

        SqlDatabaseCommand PrepareUpdateCommand(UpdateCommand command, Guid etag, int uniqueParameterIdentifier)
        {
            var values = command.ConvertAnonymousToProjections(command.Table, command.Projections);

            values[command.Table.EtagColumn] = etag;
            values[command.Table.ModifiedAtColumn] = DateTimeOffset.Now;

            var sql = new SqlBuilder()
                .Append("update {0} set {1} where {2}=@Id{3}",
                        Database.FormatTableNameAndEscape(command.Table.Name),
                        string.Join(", ", from column in values.Keys select column.Name + "=@" + column.Name + uniqueParameterIdentifier),
                        command.Table.IdColumn.Name,
                        uniqueParameterIdentifier)
                .Append(!command.LastWriteWins, "and {0}=@CurrentEtag{1}",
                        command.Table.EtagColumn.Name,
                        uniqueParameterIdentifier)
                .ToString();

            var parameters = MapProjectionsToParameters(values, uniqueParameterIdentifier);
            AddTo(parameters, "@Id" + uniqueParameterIdentifier, command.Key, SqlTypeMap.Convert(command.Table.IdColumn).DbType, null);

            if (!command.LastWriteWins)
            {
                AddTo(parameters, "@CurrentEtag" + uniqueParameterIdentifier, command.CurrentEtag, SqlTypeMap.Convert(command.Table.EtagColumn).DbType, null);
            }

            return new SqlDatabaseCommand
            {
                Sql = sql,
                Parameters = parameters.Values.ToList(),
                ExpectedRowCount = 1
            };
        }

        SqlDatabaseCommand PrepareDeleteCommand(DeleteCommand command, int uniqueParameterIdentifier)
        {
            var sql = new SqlBuilder()
                .Append("delete from {0} where {1} = @Id{2}",
                    Database.FormatTableNameAndEscape(command.Table.Name),
                    command.Table.IdColumn.Name,
                    uniqueParameterIdentifier)
                .Append(!command.LastWriteWins,
                    "and {0} = @CurrentEtag{1}",
                    command.Table.EtagColumn.Name,
                    uniqueParameterIdentifier)
                .ToString();

            var parameters = new Dictionary<string, Parameter>();
            AddTo(parameters, "@Id" + uniqueParameterIdentifier, command.Key, SqlTypeMap.Convert(command.Table.IdColumn).DbType, null);

            if (!command.LastWriteWins)
            {
                AddTo(parameters, "@CurrentEtag" + uniqueParameterIdentifier, command.CurrentEtag, SqlTypeMap.Convert(command.Table.EtagColumn).DbType, null);
            }

            return new SqlDatabaseCommand
            {
                Sql = sql,
                Parameters = parameters.Values.ToList(),
                ExpectedRowCount = 1
            };
        }

        protected static Dictionary<string, Parameter> MapProjectionsToParameters(IDictionary<Column, object> projections, int i)
        {
            var parameters = new Dictionary<string, Parameter>();
            foreach (var projection in projections)
            {
                var column = projection.Key;
                var sqlColumn = SqlTypeMap.Convert(column);
                AddTo(parameters, "@" + column.Name + i, projection.Value, sqlColumn.DbType, sqlColumn.Length);
            }

            return parameters;
        }

        public static void AddTo(Dictionary<string, Parameter> parameters, string name, object value, DbType? dbType, string size)
        {
            parameters[name] = new Parameter { Name = name, Value = value, DbType = dbType, Size = size };
        }


        static readonly HashSet<Type> simpleTypes = new HashSet<Type>
        {
            typeof (byte),
            typeof (sbyte),
            typeof (short),
            typeof (ushort),
            typeof (int),
            typeof (uint),
            typeof (long),
            typeof (ulong),
            typeof (float),
            typeof (double),
            typeof (decimal),
            typeof (bool),
            typeof (string),
            typeof (char),
            typeof (Guid),
            typeof (DateTime),
            typeof (DateTimeOffset),
            typeof (TimeSpan),
            typeof (byte[]),
            typeof (byte?),
            typeof (sbyte?),
            typeof (short?),
            typeof (ushort?),
            typeof (int?),
            typeof (uint?),
            typeof (long?),
            typeof (ulong?),
            typeof (float?),
            typeof (double?),
            typeof (decimal?),
            typeof (bool?),
            typeof (char?),
            typeof (Guid?),
            typeof (DateTime?),
            typeof (DateTimeOffset?),
            typeof (TimeSpan?),
            typeof (object)
        };

        class SqlDatabaseCommand
        {
            public string Sql { get; set; }
            public List<Parameter> Parameters { get; set; }
            public int ExpectedRowCount { get; set; }
        }
    }
}