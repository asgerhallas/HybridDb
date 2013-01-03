using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
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

        public bool IsInTestMode
        {
            get { return forTesting; }
        }

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

        public void Initialize()
        {
            if (initialized) return;
            lock (locker)
            {
                if (initialized) return;

                var timer = Stopwatch.StartNew();
                using (var connectionManager = Connect())
                using (var tx = connectionManager.Connection.BeginTransaction())
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

                        connectionManager.Connection.Execute(sql, null, tx);
                    }
                    tx.Commit();
                }

                Logger.Info("HybridDb store is initialized in {0}ms", timer.ElapsedMilliseconds);
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
            using (var tx = connectionManager.Connection.BeginTransaction())
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

                Logger.Info("Executed {0} inserts, {1} updates and {2} deletes in {3}ms",
                            numberOfInsertCommands,
                            numberOfUpdateCommands,
                            numberOfDeleteCommands,
                            timer.ElapsedMilliseconds);

                lastWrittenEtag = etag;
                return etag;
            }
        }

        public Guid Insert(ITable table, Guid key, byte[] document, object projections)
        {
            return Execute(new InsertCommand(table, key, document, projections));
        }

        public Guid Update(ITable table, Guid key, Guid etag, byte[] document, object projections)
        {
            return Execute(new UpdateCommand(table, key, etag, document, projections));
        }

        public void Delete(ITable table, Guid key, Guid etag)
        {
            Execute(new DeleteCommand(table, key, etag));
        }

        public long NumberOfRequests
        {
            get { return numberOfRequests; }
        }

        public Guid LastWrittenEtag
        {
            get { return lastWrittenEtag; }
        }

        public IEnumerable<TProjection> Query<TProjection>(ITable table, out QueryStats stats, string columns = "*", string @where = "", int skip = 0, int take = 0,
                                                           string @orderby = "", object parameters = null)
        {
            var timer = Stopwatch.StartNew();
            using (var connection = Connect())
            {
                if (!columns.Contains(table.IdColumn.Name) && columns != "*")
                    columns = string.Format("{0},{1}", table.IdColumn.Name, columns);

                var isWindowed = skip > 0 || take > 0;

                var rowNumberOrderBy = string.IsNullOrEmpty(orderby) ? "CURRENT_TIMESTAMP" : orderby;
                var reverseRowNumberOrderBy =
                    string.Join(", ", rowNumberOrderBy.Split(',').Select(x =>
                    {
                        if (x.IndexOf("asc", StringComparison.InvariantCultureIgnoreCase) >= 0)
                            return x.Replace("asc", "desc", StringComparison.InvariantCultureIgnoreCase);

                        if (x.IndexOf("desc", StringComparison.InvariantCultureIgnoreCase) >= 0)
                            return x.Replace("desc", "asc", StringComparison.InvariantCultureIgnoreCase);

                        return x + " desc";
                    }));

                var sql = new SqlBuilder();
                sql.Append(@"with temp as (select {0}", columns)
                   .Append(isWindowed, ", row_number() over(order by {0}) as RowNumberAsc, row_number() over(order by {1}) as RowNumberDesc",
                           rowNumberOrderBy,
                           reverseRowNumberOrderBy)
                   .Append("from {0}", Escape(GetFormattedTableName(table)))
                   .Append(!string.IsNullOrEmpty(@where), "where {0}", @where)
                   .Append(")")
                   .Append(isWindowed, "select *, (RowNumberAsc + RowNumberDesc - 1) as TotalRows from temp where RowNumberAsc >= {0}", skip + 1)
                   .Append(take > 0, "and RowNumberAsc <= {0}", skip + take)
                   .Append(isWindowed, "order by RowNumberAsc")
                   .Append(!isWindowed, "select *, (select count(*) from temp) as TotalRows from temp")
                   .Append(!isWindowed && !string.IsNullOrEmpty(orderby), "order by {0}", orderby);

                Console.WriteLine();
                Console.WriteLine(sql.ToString());

                var result = typeof (IDictionary<IColumn, object>).IsAssignableFrom(typeof (TProjection))
                                 ? (IEnumerable<TProjection>) (QueryInternal<object>(connection, sql, parameters, out stats)
                                                                  .Cast<IDictionary<string, object>>()
                                                                  .Select(row => row.Select(column => new {Key = table[column.Key], column.Value})
                                                                                    .Where(column => column.Key != null)
                                                                                    .ToDictionary(column => column.Key, column => column.Value)))
                                 : QueryInternal<TProjection>(connection, sql, parameters, out stats);

                Interlocked.Increment(ref numberOfRequests);
                Logger.Info("Retrieved {0} in {1}ms", "", timer.ElapsedMilliseconds);

                return result;
            }
        }

        public IEnumerable<IDictionary<IColumn, object>> Query(ITable table, out QueryStats stats, string columns = "*", string @where = "", int skip = 0, int take = 0,
                                                               string orderby = "", object parameters = null)
        {
            return Query<IDictionary<IColumn, object>>(table, out stats, columns: columns, where: @where, skip: skip, take: take, orderby: @orderby, parameters: parameters);
        }

        public IDictionary<IColumn, object> Get(ITable table, Guid key)
        {
            var timer = Stopwatch.StartNew();
            using (var connection = Connect())
            {
                var sql = string.Format("select * from {0} where {1} = @Id",
                                        Escape(GetFormattedTableName(table)),
                                        table.IdColumn.Name);

                var row = ((IDictionary<string, object>) connection.Connection.Query(sql, new {Id = key}).SingleOrDefault());

                Interlocked.Increment(ref numberOfRequests);

                Logger.Info("Retrieved {0} in {1}ms", key, timer.ElapsedMilliseconds);

                return row != null ? row.ToDictionary(x => table[x.Key], x => x.Value) : null;
            }
        }

        IEnumerable<T> QueryInternal<T>(ManagedConnection connection, SqlBuilder sql, object parameters, out QueryStats metadata)
        {
            var rows = connection.Connection.Query<T, QueryStats, Tuple<T, QueryStats>>(sql.ToString(), Tuple.Create, parameters, splitOn: "TotalRows");

            var firstRow = rows.FirstOrDefault();
            metadata = firstRow != null ? new QueryStats {TotalRows = firstRow.Item2.TotalRows} : new QueryStats();

            Interlocked.Increment(ref numberOfRequests);

            return rows.Select(x => x.Item1);
        }

        public Table<TDocument> ForDocument<TDocument>(string name)
        {
            return configuration.Register<TDocument>(name);
        }

        public static DocumentStore ForTesting(string connectionString)
        {
            return new DocumentStore(connectionString, true);
        }

        void InternalExecute(ManagedConnection managedConnection, IDbTransaction tx, string sql, List<Parameter> parameters, int expectedRowCount)
        {
            Console.WriteLine("Internal execute");
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