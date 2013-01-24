using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Dapper;
using HybridDb.Commands;
using HybridDb.Logging;
using HybridDb.Schema;

namespace HybridDb
{
    public class DocumentStore : IDocumentStore
    {
        readonly Configuration configuration;
        readonly string connectionString;
        readonly bool forTesting;
        readonly object locker = new object();
        SqlConnection connectionForTesting;
        bool initialized;
        Guid lastWrittenEtag;
        long numberOfRequests;

        DocumentStore(string connectionString, bool forTesting)
        {
            this.forTesting = forTesting;
            this.connectionString = connectionString;

            configuration = new Configuration();
        }

        public DocumentStore(string connectionString) : this(connectionString, false) {}

        ILogger Logger
        {
            get { return Configuration.Logger; }
        }

        internal DbConnection Connection
        {
            get
            {
                if (!IsInTestMode)
                    throw new InvalidOperationException("Only for testing purposes");

                return Connect().Connection;
            }
        }

        public bool IsInTestMode
        {
            get { return forTesting; }
        }

        public void Dispose()
        {
            if (IsInTestMode && connectionForTesting != null)
                connectionForTesting.Dispose();
        }

        public Configuration Configuration
        {
            get { return configuration; }
        }

        public Table<TDocument> ForDocument<TDocument>()
        {
            return ForDocument<TDocument>(null);
        }

        public Table<TDocument> ForDocument<TDocument>(string name)
        {
            return configuration.Register<TDocument>(name);
        }

        public static DocumentStore ForTesting(string connectionString)
        {
            return new DocumentStore(connectionString, true);
        }

        public void Initialize()
        {
            if (initialized) return;
            lock (locker)
            {
                if (initialized) return;

                var timer = Stopwatch.StartNew();
                using (var connectionManager = Connect())
                using (var tx = connectionManager.Connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    foreach (var table in configuration.Tables.Values)
                    {
                        var tableExists =
                            string.Format(IsInTestMode
                                              ? "OBJECT_ID('tempdb..{0}') is not null"
                                              : "exists (select * from information_schema.tables where table_catalog = db_name() and table_name = '{0}')",
                                          GetFormattedTableName(table));

                        var sql =
                            string.Format(@"if not ({0})
                                            begin
                                                create table {1} ({2});
                                            end",
                                          tableExists,
                                          Escape(GetFormattedTableName(table)),
                                          string.Join(", ", table.Columns.Select(x => Escape(x.Name) + " " + x.Column.SqlType)));

                        Console.WriteLine(sql);
                        connectionManager.Connection.Execute(sql, null, tx);
                    }
                    tx.Commit();
                }

                //Logger.Info("HybridDb store is initialized in {0}ms", timer.ElapsedMilliseconds);
                initialized = true;
            }
        }

        public IDocumentSession OpenSession()
        {
            AssertInitialized();

            return new DocumentSession(this);
        }

        public Guid Execute(params DatabaseCommand[] commands)
        {
            if (commands.Length == 0)
                throw new ArgumentException("No commands were passed");

            var timer = Stopwatch.StartNew();
            using (var connectionManager = Connect())
            using (var tx = connectionManager.Connection.BeginTransaction(IsolationLevel.ReadCommitted))
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
                        InternalExecute(connectionManager, tx, sql, parameters, expectedRowCount);

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

                InternalExecute(connectionManager, tx, sql, parameters, expectedRowCount);

                tx.Commit();

                //Logger.Info("--Executed {0} inserts, {1} updates and {2} deletes in {3}ms",
                //            numberOfInsertCommands,
                //            numberOfUpdateCommands,
                //            numberOfDeleteCommands,
                //            timer.ElapsedMilliseconds);

                lastWrittenEtag = etag;
                return etag;
            }
        }

        public Guid Insert(ITable table, Guid key, byte[] document, object projections)
        {
            return Execute(new InsertCommand(table, key, document, projections));
        }

        public Guid Update(ITable table, Guid key, Guid etag, byte[] document, object projections, bool lastWriteWins = false)
        {
            return Execute(new UpdateCommand(table, key, etag, document, projections, lastWriteWins));
        }

        public void Delete(ITable table, Guid key, Guid etag, bool lastWriteWins = false)
        {
            Execute(new DeleteCommand(table, key, etag, lastWriteWins));
        }

        public long NumberOfRequests
        {
            get { return numberOfRequests; }
        }

        public Guid LastWrittenEtag
        {
            get { return lastWrittenEtag; }
        }

        public IEnumerable<TProjection> Query<TProjection>(ITable table, out QueryStats stats, string select = null, string where = "",
                                                           int skip = 0, int take = 0, string orderby = "", object parameters = null)
        {
            if (select.IsNullOrEmpty() || select == "*")
                select = "";

            var isTypedProjection = !typeof (TProjection).IsA<IDictionary<IColumn, object>>();
            if (isTypedProjection)
                select = MatchSelectedColumnsWithProjectedType<TProjection>(select);

            var timer = Stopwatch.StartNew();
            using (var connection = Connect())
            using (var tx = connection.Connection.BeginTransaction(IsolationLevel.ReadCommitted))
            {
                var isWindowed = skip > 0 || take > 0;
                var rowNumberOrderBy = string.IsNullOrEmpty(@orderby) ? "CURRENT_TIMESTAMP" : @orderby;

                var sql = new SqlBuilder();
                sql.Append(@"with temp as (select {0}", select.IsNullOrEmpty() ? "*" : select)
                   .Append(isWindowed, ", row_number() over(ORDER BY {0}) as RowNumber", rowNumberOrderBy)
                   .Append("from {0}", Escape(GetFormattedTableName(table)))
                   .Append(!string.IsNullOrEmpty(@where), "where {0}", @where)
                   .Append(")")
                   .Append("select *, (select count(*) from temp) as TotalResults from temp")
                   .Append(isWindowed, "where RowNumber >= {0}", skip + 1)
                   .Append(isWindowed && take > 0, "and RowNumber <= {0}", skip + take)
                   .Append(isWindowed, "order by RowNumber")
                   .Append(!isWindowed && !string.IsNullOrEmpty(orderby), "order by {0}", orderby);

                Console.WriteLine();
                Console.WriteLine(sql.ToString());

                var result = isTypedProjection
                                 ? QueryInternal<TProjection>(connection, sql, parameters, tx, out stats)
                                 : (IEnumerable<TProjection>) (QueryInternal<object>(connection, sql, parameters, tx, out stats)
                                                                  .Cast<IDictionary<string, object>>()
                                                                  .Select(row => row.Select(column => new {Key = table[column.Key], column.Value})
                                                                                    .Where(column => column.Key != null)
                                                                                    .ToDictionary(column => column.Key, column => column.Value)));

                Interlocked.Increment(ref numberOfRequests);
                //Logger.Info("Retrieved {0} in {1}ms", "", timer.ElapsedMilliseconds);

                tx.Commit();
                return result;
            }
        }


        public IEnumerable<IDictionary<IColumn, object>> Query(ITable table, out QueryStats stats, string select = null, string where = "",
                                                               int skip = 0, int take = 0, string orderby = "", object parameters = null)
        {
            return Query<IDictionary<IColumn, object>>(table, out stats, select, @where, skip, take, orderby, parameters);
        }

        public IDictionary<IColumn, object> Get(ITable table, Guid key)
        {
            var timer = Stopwatch.StartNew();
            using (var connection = Connect())
            using (var tx = connection.Connection.BeginTransaction(IsolationLevel.ReadCommitted))
            {
                
                var sql = string.Format("select * from {0} where {1} = @Id",
                                        Escape(GetFormattedTableName(table)),
                                        table.IdColumn.Name);

                var row = ((IDictionary<string, object>) connection.Connection.Query(sql, new {Id = key}, tx).SingleOrDefault());

                Interlocked.Increment(ref numberOfRequests);

                Logger.Info("--Retrieved {0} in {1}ms", key, timer.ElapsedMilliseconds);

                tx.Commit();
                return row != null ? row.ToDictionary(x => table[x.Key], x => x.Value) : null;
            }
        }

        static string MatchSelectedColumnsWithProjectedType<TProjection>(string select)
        {
            var neededColumns = typeof(TProjection).GetProperties().Select(x => x.Name).ToList();
            var selectedColumns = from clause in @select.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries)
                                  let split = Regex.Split(clause, " AS ", RegexOptions.IgnoreCase).Where(x => x != "").ToArray()
                                  let column = split[0]
                                  let alias = split.Length > 1 ? split[1] : null
                                  where neededColumns.Contains(alias)
                                  select new { column, alias = alias ?? column };

            var missingColumns = from column in neededColumns
                                 where !selectedColumns.Select(x => x.alias).Contains(column)
                                 select new { column, alias = column };

            select = string.Join(", ", selectedColumns.Union(missingColumns).Select(x => x.column + " AS " + x.alias));
            return select;
        }

        IEnumerable<T> QueryInternal<T>(ManagedConnection connection, SqlBuilder sql, object parameters, DbTransaction tx, out QueryStats metadata)
        {
            var normalizedParameters = parameters as IEnumerable<Parameter> ??
                                       (from projection in (parameters as IDictionary<string, object> ?? ObjectToDictionaryRegistry.Convert(parameters))
                                        select new Parameter {Name = "@" + projection.Key, Value = projection.Value}).ToList();

            var rows = connection.Connection.Query<T, QueryStats, Tuple<T, QueryStats>>(sql.ToString(),
                                                                                        Tuple.Create,
                                                                                        new FastDynamicParameters(normalizedParameters),
                                                                                        tx,
                                                                                        splitOn: "TotalResults");

            var firstRow = rows.FirstOrDefault();
            metadata = firstRow != null ? new QueryStats {TotalResults = firstRow.Item2.TotalResults} : new QueryStats();

            Interlocked.Increment(ref numberOfRequests);

            return rows.Select(x => x.Item1);
        }

        void InternalExecute(ManagedConnection managedConnection, IDbTransaction tx, string sql, List<Parameter> parameters, int expectedRowCount)
        {
            Console.WriteLine(parameters.Aggregate(sql, (current, parameter) =>
                                                   current.Replace(parameter.Name, parameter.ToSql())));

            var fastParameters = new FastDynamicParameters(parameters);
            var rowcount = managedConnection.Connection.Execute(sql, fastParameters, tx);
            Interlocked.Increment(ref numberOfRequests);
            if (rowcount != expectedRowCount)
                throw new ConcurrencyException();
        }

        ManagedConnection Connect()
        {
            if (IsInTestMode)
            {
                if (connectionForTesting == null)
                {
                    connectionForTesting = new SqlConnection(connectionString);
                    connectionForTesting.Open();
                }

                return new ManagedConnection(connectionForTesting, () => { });
            }

            var connection = new SqlConnection(connectionString);
            connection.Open();
            return new ManagedConnection(connection, connection.Dispose);
        }

        public string GetFormattedTableName(ITable table)
        {
            return IsInTestMode ? "#" + table.Name : table.Name;
        }

        public string Escape(string identifier)
        {
            return string.Format("[{0}]", identifier);
        }

        void AssertInitialized()
        {
            if (!initialized)
                throw new StoreNotInitializedException();
        }

        public class ManagedConnection : IDisposable
        {
            readonly DbConnection connection;
            readonly Action dispose;

            public ManagedConnection(DbConnection connection, Action dispose)
            {
                this.connection = connection;
                this.dispose = dispose;
            }

            public DbConnection Connection
            {
                get { return connection; }
            }

            public void Dispose()
            {
                dispose();
            }
        }

        public class MissingProjectionValueException : Exception {}

        public class StoreNotInitializedException : Exception {}
    }
}