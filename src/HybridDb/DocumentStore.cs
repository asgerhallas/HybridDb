using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Dapper;
using HybridDb.Commands;
using HybridDb.Config;
using HybridDb.Migrations;
using Serilog;

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
            return new DocumentSession(this);
        }

        public Guid Execute(IEnumerable<DatabaseCommand> commands)
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
                    if (command is InsertCommand)
                        numberOfInsertCommands++;

                    if (command is UpdateCommand)
                        numberOfUpdateCommands++;

                    if (command is DeleteCommand)
                        numberOfDeleteCommands++;

                    var preparedCommand = command.Prepare(this, etag, i++);
                    var numberOfNewParameters = preparedCommand.Parameters.Count;

                    if (numberOfNewParameters >= 2100)
                        throw new InvalidOperationException("Cannot execute a query with more than 2100 parameters.");

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

                    sql += string.Format("{0};", preparedCommand.Sql);
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

                sql.Append("select count(*) as TotalResults")
                   .Append("from {0}", Database.FormatTableNameAndEscape(table.Name))
                   .Append(!string.IsNullOrEmpty(@where), "where {0}", @where)
                   .Append(";");

                var isWindowed = skip > 0 || take > 0;

                if (isWindowed)
                {
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
                        InternalQuery<object>(connection, sql, parameters, out stats)
                            .Cast<IDictionary<string, object>>();
                }
                else
                {
                    result = InternalQuery<TProjection>(connection, sql, parameters, out stats);
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

        static string MatchSelectedColumnsWithProjectedType<TProjection>(string select)
        {
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

        IEnumerable<T> InternalQuery<T>(ManagedConnection connection, SqlBuilder sql, object parameters, out QueryStats stats)
        {
            var normalizedParameters = new FastDynamicParameters(
                parameters as IEnumerable<Parameter> ?? ConvertToParameters<T>(parameters));

            using (var reader = connection.Connection.QueryMultiple(sql.ToString(), normalizedParameters))
            {
                stats = reader.Read<QueryStats>(buffered: true).Single();
                return reader.Read<T, object, T>((first, second) => first, "RowNumber", buffered: true);
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


        public long NumberOfRequests
        {
            get { return numberOfRequests; }
        }

        public Guid LastWrittenEtag
        {
            get { return lastWrittenEtag; }
        }

        public void Dispose()
        {
            Database.Dispose();
        }
    }
}