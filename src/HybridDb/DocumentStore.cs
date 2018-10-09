using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Transactions;
using Dapper;
using HybridDb.Commands;
using HybridDb.Config;
using HybridDb.Migrations;
using Serilog;
using ShinySwitch;
using IsolationLevel = System.Data.IsolationLevel;

namespace HybridDb
{
    public class DocumentStore : IDocumentStore
    {
        internal DocumentStore(Configuration configuration, TableMode mode, string connectionString, bool testing)
        {
            Configuration = configuration;
            Logger = configuration.Logger;
            Testing = testing;
            TableMode = mode;

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
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        internal DocumentStore(DocumentStore store, Configuration configuration, bool testing)
        {
            Configuration = configuration;
            Database = store.Database;
            Logger = configuration.Logger;

            Testing = testing;
        }

        public static IDocumentStore Create(string connectionString, Action<Configuration> configure = null)
        {
            configure = configure ?? (x => { });
            var configuration = new Configuration();
            configure(configuration);
            return new DocumentStore(configuration, TableMode.UseRealTables, connectionString, false);
        }

        public static IDocumentStore ForTesting(TableMode mode, Action<Configuration> configure = null) => ForTesting(mode, null, configure);

        public static IDocumentStore ForTesting(TableMode mode, string connectionString, Action<Configuration> configure = null)
        {
            configure = configure ?? (x => { });
            var configuration = new Configuration();
            configure(configuration);
            return new DocumentStore(configuration, mode, connectionString ?? "data source=.;Integrated Security=True", true);
        }

        public void Dispose() => Database.Dispose();

        public IDatabase Database { get; }
        public ILogger Logger { get; private set; }
        public Configuration Configuration { get; }
        public bool IsInitialized { get; private set; }
        public bool Testing { get; }
        public TableMode TableMode { get; }

        public StoreStats Stats { get; } = new StoreStats();

        public void Initialize()
        {
            if (IsInitialized)
                return;

            Configuration.Initialize();

            Logger = Configuration.Logger;

            new SchemaMigrationRunner(this, new SchemaDiffer()).Run();
            var documentMigration = new DocumentMigrationRunner().Run(this);
            if (Testing) documentMigration.Wait();

            IsInitialized = true;
        }

        public IDocumentSession OpenSession(IDocumentTransaction tx = null)
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("You must call Initialize() on the store before opening a session.");
            }

            return new DocumentSession(this, tx);
        }

        public IDocumentTransaction BeginTransaction(IsolationLevel level = IsolationLevel.ReadCommitted) => new DocumentTransaction(this, level, Stats);
    }

    public class StoreStats
    {
        public long NumberOfRequests { get; set; }
        public long NumberOfInsertCommands { get; set; } = 0;
        public long NumberOfUpdateCommands { get; set; } = 0;
        public long NumberOfDeleteCommands { get; set; } = 0;
        public long NumberOfGets { get; set; } = 0;
        public long NumberOfQueries { get; set; } = 0;

        public Guid LastWrittenEtag { get; set; }
    }

    public class DocumentTransaction : IDocumentTransaction
    {
        readonly DocumentStore store;
        readonly StoreStats storeStats;

        readonly ManagedConnection managedConnection;
        readonly SqlConnection connection;
        readonly SqlTransaction tx;

        public DocumentTransaction(DocumentStore store, IsolationLevel level, StoreStats storeStats)
        {
            this.store = store;
            this.storeStats = storeStats;

            managedConnection = store.Database.Connect();
            connection = managedConnection.Connection;

            if (Transaction.Current == null)
            {
                tx = connection.BeginTransaction(level);
            }

            Etag = Guid.NewGuid();
        }

        public void Dispose()
        {
            tx?.Dispose();
            managedConnection.Dispose();
        }

        public Guid Complete()
        {
            tx?.Commit();
            return Etag;
        }

        public Guid Etag { get; }

        public IDocumentStore Store => store;

        public Guid Execute(DatabaseCommand command)
        {
            storeStats.NumberOfRequests++;

            var preparedCommand = Switch<SqlDatabaseCommand>.On(command)
                .Match<InsertCommand>(insert =>
                {
                    storeStats.NumberOfInsertCommands++;
                    return PrepareInsertCommand(insert);
                })
                .Match<UpdateCommand>(update =>
                {
                    storeStats.NumberOfUpdateCommands++;
                    return PrepareUpdateCommand(update);
                })
                .Match<DeleteCommand>(delete =>
                {
                    storeStats.NumberOfDeleteCommands++;
                    return PrepareDeleteCommand(delete);
                })
                .OrThrow();

            var numberOfNewParameters = preparedCommand.Parameters.Count;

            // NOTE: Sql parameter threshold is actually lower than the stated 2100 (or maybe extra 
            // params are added some where in the stack) so we cut it some slack and say 2000.
            if (numberOfNewParameters >= 2000)
            {
                throw new InvalidOperationException("Cannot execute a single command with more than 2000 parameters.");
            }

            var fastParameters = new FastDynamicParameters(preparedCommand.Parameters);
            var rowcount = connection.Execute(preparedCommand.Sql, fastParameters, tx);

            if (rowcount != preparedCommand.ExpectedRowCount)
            {
                throw new ConcurrencyException(
                    $"Someone beat you to it. Expected {preparedCommand.ExpectedRowCount} changes, but got {rowcount}. " +
                    $"The transaction is rolled back now, so no changes was actually made.");
            }

            return storeStats.LastWrittenEtag = Etag;
        }

        public IDictionary<string, object> Get(DocumentTable table, string key)
        {
            storeStats.NumberOfRequests++;
            storeStats.NumberOfGets++;

            var sql = $"select * from {store.Database.FormatTableNameAndEscape(table.Name)} where {table.IdColumn.Name} = @Id and {table.LastOperationColumn.Name} <> @Op";

            return (IDictionary<string, object>)connection.Query(sql, new { Id = key, Op = Operation.Deleted }, tx).SingleOrDefault();
        }

        public (QueryStats stats, IEnumerable<QueryResult<TProjection>> rows) Query<TProjection>(
            DocumentTable table, string select = null, string where = "", int skip = 0, int take = 0, 
            string orderby = "", bool includeDeleted = false, object parameters = null)
        {
            storeStats.NumberOfRequests++;
            storeStats.NumberOfQueries++;

            if (select.IsNullOrEmpty() || select == "*")
            {
                select = "";

                if (typeof(TProjection) != typeof(object))
                {
                    select = MatchSelectedColumnsWithProjectedType<TProjection>(select);
                }
            }

            if (!includeDeleted)
            {
                where = string.IsNullOrEmpty(where)
                    ? $"{table.LastOperationColumn.Name} <> {Operation.Deleted:D}" // TODO: Use parameters
                    : $"({where}) AND ({table.LastOperationColumn.Name} <> {Operation.Deleted:D})";
            }

            var timer = Stopwatch.StartNew();

            var sql = new SqlBuilder();

            var isWindowed = skip > 0 || take > 0;

            if (isWindowed)
            {
                sql.Append("select count(*) as TotalResults")
                    .Append($"from {store.Database.FormatTableNameAndEscape(table.Name)}")
                    .Append(!string.IsNullOrEmpty(where), $"where {where}")
                    .Append(";");

                sql.Append(@"with temp as (select *")
                    .Append($", {table.DiscriminatorColumn.Name} as __Discriminator")
                    .Append($", {table.LastOperationColumn.Name} as __LastOperation")
                    .Append($", {table.RowVersionColumn.Name} as __RowVersion")
                    .Append($", row_number() over(ORDER BY {(string.IsNullOrEmpty(orderby) ? "CURRENT_TIMESTAMP" : orderby)}) as RowNumber")
                    .Append($"from {store.Database.FormatTableNameAndEscape(table.Name)}")
                    .Append(!string.IsNullOrEmpty(where), $"where {where}")
                    .Append(")")
                    .Append(select.IsNullOrEmpty(), "select * from temp").Or($"select {select}, __Discriminator, __LastOperation, __RowVersion from temp")
                    .Append($"where RowNumber >= {skip + 1}")
                    .Append(take > 0, $"and RowNumber <= {skip + take}")
                    .Append("order by RowNumber")
                    .Append(";");
            }
            else
            {
                sql.Append(select.IsNullOrEmpty(), "select *").Or($"select {select}")
                    .Append($", {table.DiscriminatorColumn.Name} as __Discriminator")
                    .Append($", {table.LastOperationColumn.Name} AS __LastOperation")
                    .Append($", {table.RowVersionColumn.Name} AS __RowVersion")
                    .Append($"from {store.Database.FormatTableNameAndEscape(table.Name)}")
                    .Append(!string.IsNullOrEmpty(where), $"where ({where})")
                    .Append(!string.IsNullOrEmpty(orderby), $"order by {orderby}");
            }

            var result = InternalQuery<TProjection>(sql, parameters, isWindowed, out var stats);

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

            return (stats, result);
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
                select new { column, alias = column };

            select = string.Join(", ", selectedColumns.Union(missingColumns).Select(x => x.column + " AS " + x.alias));
            return select;
        }

        IEnumerable<QueryResult<T>> InternalQuery<T>(SqlBuilder sql, object parameters, bool hasTotalsQuery, out QueryStats stats)
        {
            var normalizedParameters = new FastDynamicParameters(
                parameters as IEnumerable<Parameter> ?? ConvertToParameters<T>(parameters));

            if (hasTotalsQuery)
            {
                using (var reader = connection.QueryMultiple(sql.ToString(), normalizedParameters, tx))
                {
                    stats = reader.Read<QueryStats>(buffered: true).Single();

                    return reader.Read<T, string, Operation, byte[], QueryResult<T>>((obj, a, b, c) =>
                        new QueryResult<T>(obj, a, b, c), splitOn: "__Discriminator,__LastOperation,__RowVersion", buffered: true);
                }
            }

            using (var reader = connection.QueryMultiple(sql.ToString(), normalizedParameters, tx))
            {
                var rows = (List<QueryResult<T>>)reader.Read<T, string, Operation, byte[], QueryResult<T>>((obj, a, b, c) =>
                   new QueryResult<T>(obj, a, b, c), splitOn: "__Discriminator,__LastOperation,__RowVersion", buffered: true);

                stats = new QueryStats
                {
                    TotalResults = rows.Count
                };

                return rows;
            }
        }

        static IEnumerable<Parameter> ConvertToParameters<T>(object parameters) =>
            from projection in parameters as IDictionary<string, object> ?? ObjectToDictionaryRegistry.Convert(parameters)
            select new Parameter { Name = "@" + projection.Key, Value = projection.Value };

        SqlDatabaseCommand PrepareInsertCommand(InsertCommand command)
        {
            var values = command.ConvertAnonymousToProjections(command.Table, command.Projections);

            values[command.Table.IdColumn] = command.Id;
            values[command.Table.EtagColumn] = Etag;
            values[command.Table.CreatedAtColumn] = DateTimeOffset.Now;
            values[command.Table.ModifiedAtColumn] = DateTimeOffset.Now;
            values[command.Table.LastOperationColumn] = Operation.Inserted;

            var sql = $@"
                insert into {store.Database.FormatTableNameAndEscape(command.Table.Name)} 
                ({string.Join(", ", from column in values.Keys select column.Name)}) 
                values ({string.Join(", ", from column in values.Keys select "@" + column.Name)});";

            var parameters = MapProjectionsToParameters(values);

            return new SqlDatabaseCommand
            {
                Sql = sql,
                Parameters = parameters.Values.ToList(),
                ExpectedRowCount = 1
            };
        }

        SqlDatabaseCommand PrepareUpdateCommand(UpdateCommand command)
        {
            var values = command.ConvertAnonymousToProjections(command.Table, command.Projections);

            values[command.Table.EtagColumn] = Etag;
            values[command.Table.ModifiedAtColumn] = DateTimeOffset.Now;
            values[command.Table.LastOperationColumn] = Operation.Updated;

            var sql = new SqlBuilder()
                .Append($"update {store.Database.FormatTableNameAndEscape(command.Table.Name)}")
                .Append($"set {string.Join(", ", from column in values.Keys select column.Name + " = @" + column.Name)}")
                .Append($"where {command.Table.IdColumn.Name}=@Id")
                .Append(!command.LastWriteWins, $"and {command.Table.EtagColumn.Name}=@ExpectedEtag")
                .ToString();

            var parameters = MapProjectionsToParameters(values);
            AddTo(parameters, "@Id", command.Id, SqlTypeMap.Convert(command.Table.IdColumn).DbType, null);

            if (!command.LastWriteWins)
            {
                AddTo(parameters, "@ExpectedEtag", command.ExpectedEtag, SqlTypeMap.Convert(command.Table.EtagColumn).DbType, null);
            }

            return new SqlDatabaseCommand
            {
                Sql = sql,
                Parameters = parameters.Values.ToList(),
                ExpectedRowCount = 1
            };
        }

        SqlDatabaseCommand PrepareDeleteCommand(DeleteCommand command)
        {
            // Note that last write wins can actually still produce a ConcurrencyException if the 
            // row was already deleted, which would result in 0 resulting rows changed

            var sql = new SqlBuilder()
                .Append($"update {store.Database.FormatTableNameAndEscape(command.Table.Name)}")
                .Append($"set {command.Table.IdColumn.Name} = @NewId")
                .Append($", {command.Table.LastOperationColumn.Name} = {(byte)Operation.Deleted}")
                .Append($"where {command.Table.IdColumn.Name} = @Id")
                .Append(!command.LastWriteWins, $"and {command.Table.EtagColumn.Name} = @ExpectedEtag")
                .ToString();

            var parameters = new Dictionary<string, Parameter>();
            AddTo(parameters, "@Id", command.Key, SqlTypeMap.Convert(command.Table.IdColumn).DbType, null);
            AddTo(parameters, "@NewId", $"{command.Key}/{Guid.NewGuid()}", SqlTypeMap.Convert(command.Table.IdColumn).DbType, null);

            if (!command.LastWriteWins)
            {
                AddTo(parameters, "@ExpectedEtag", command.ExpectedEtag, SqlTypeMap.Convert(command.Table.EtagColumn).DbType, null);
            }

            return new SqlDatabaseCommand
            {
                Sql = sql,
                Parameters = parameters.Values.ToList(),
                ExpectedRowCount = 1
            };
        }

        protected static Dictionary<string, Parameter> MapProjectionsToParameters(IDictionary<Column, object> projections)
        {
            var parameters = new Dictionary<string, Parameter>();
            foreach (var projection in projections)
            {
                var column = projection.Key;
                var sqlColumn = SqlTypeMap.Convert(column);
                AddTo(parameters, "@" + column.Name, projection.Value, sqlColumn.DbType, sqlColumn.Length);
            }

            return parameters;
        }

        static void AddTo(Dictionary<string, Parameter> parameters, string name, object value, SqlDbType? dbType, string size)
        {
            parameters[name] = new Parameter { Name = name, Value = value, DbType = dbType };
        }

        class SqlDatabaseCommand
        {
            public string Sql { get; set; }
            public List<Parameter> Parameters { get; set; }
            public int ExpectedRowCount { get; set; }
        }

        static readonly HashSet<Type> simpleTypes = new HashSet<Type>
        {
            typeof(byte),
            typeof(sbyte),
            typeof(short),
            typeof(ushort),
            typeof(int),
            typeof(uint),
            typeof(long),
            typeof(ulong),
            typeof(float),
            typeof(double),
            typeof(decimal),
            typeof(bool),
            typeof(string),
            typeof(char),
            typeof(Guid),
            typeof(DateTime),
            typeof(DateTimeOffset),
            typeof(TimeSpan),
            typeof(byte[]),
            typeof(byte?),
            typeof(sbyte?),
            typeof(short?),
            typeof(ushort?),
            typeof(int?),
            typeof(uint?),
            typeof(long?),
            typeof(ulong?),
            typeof(float?),
            typeof(double?),
            typeof(decimal?),
            typeof(bool?),
            typeof(char?),
            typeof(Guid?),
            typeof(DateTime?),
            typeof(DateTimeOffset?),
            typeof(TimeSpan?),
            typeof(object)
        };

        IDocumentTransaction documentTransactionImplementation;
    }
}