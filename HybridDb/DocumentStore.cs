using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using Dapper;

namespace HybridDb
{
    public class DocumentStore : IDocumentStore
    {
        readonly Configuration configuration;
        readonly SqlConnection connectionForTesting;
        readonly string connectionString;
        readonly object locker = new object();
        bool initialized;
        Guid lastWrittenEtag;
        long numberOfRequests;

        DocumentStore()
        {
            configuration = new Configuration();
        }

        DocumentStore(SqlConnection connectionForTesting) : this()
        {
            this.connectionForTesting = connectionForTesting;
        }

        public DocumentStore(string connectionString) : this()
        {
            this.connectionString = connectionString;
        }

        public Configuration Configuration
        {
            get { return configuration; }
        }

        public Table<TDocument> ForDocument<TDocument>()
        {
            return configuration.Register<TDocument>();
        }

        public void Initialize()
        {
            if (initialized) return;
            lock (locker)
            {
                if (initialized) return;

                using (var connectionManager = Connect())
                using (var tx = connectionManager.Connection.BeginTransaction())
                {
                    foreach (var table in configuration.Tables.Values)
                    {
                        var sql = "create table ";
                        sql += "\"" + GetFormattedTableName(table) + "\"";
                        sql += " (" + string.Join(", ", table.Columns.Select(x => x.Name + " " + x.Column.SqlType)) + ")";
                        connectionManager.Connection.Execute(sql, null, tx);
                    }
                    tx.Commit();
                }

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

            var i = 0;
            var etag = Guid.NewGuid();
            var sql = "";
            var parameters = new DynamicParameters();
            var expectedRowCount = 0;
            foreach (var command in commands)
            {
                var preparedCommand = command.Prepare(this, etag, i++);
                sql += string.Format("{0};", preparedCommand.Sql);
                parameters.AddDynamicParams(preparedCommand.Parameters);
                expectedRowCount += preparedCommand.ExpectedRowCount;
            }

            using (var connectionManager = Connect())
            using (var tx = connectionManager.Connection.BeginTransaction())
            {
                var rowcount = connectionManager.Connection.Execute(sql, parameters, tx);

                Interlocked.Increment(ref numberOfRequests);
                lastWrittenEtag = etag;

                if (rowcount != expectedRowCount)
                    throw new ConcurrencyException();

                tx.Commit();
            }

            return etag;
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

        public IEnumerable<TProjection> Query<TProjection>(ITable table, string columns = "*", string where = "", object parameters = null)
        {
            using (var connection = Connect())
            {
                if (!columns.Contains(table.IdColumn.Name))
                    columns = string.Format("{0},{1}", table.IdColumn.Name, columns);

                var sql = string.Format("select {0} from {1} where {2}",
                                        columns,
                                        GetFormattedTableName(table),
                                        where);

                if (typeof (IDictionary<IColumn, object>).IsAssignableFrom(typeof (TProjection)))
                {
                    var rows = connection.Connection.Query(sql, parameters);
                    Interlocked.Increment(ref numberOfRequests);
                    return (IEnumerable<TProjection>) rows.Cast<IDictionary<string, object>>()
                                                          .Select(row => row.ToDictionary(x => table[x.Key], x => x.Value));
                }
                else
                {
                    var rows = connection.Connection.Query<TProjection>(sql, parameters);
                    Interlocked.Increment(ref numberOfRequests);
                    return rows;
                }
            }
        }

        public IEnumerable<IDictionary<IColumn, object>> Query(ITable table, string columns = "*", string where = "", object parameters = null)
        {
            return Query<IDictionary<IColumn, object>>(table, columns, where, parameters);
        }

        public IDictionary<IColumn, object> Get(ITable table, Guid key)
        {
            using (var connection = Connect())
            {
                var sql = string.Format("select * from {0} where {1} = @Id",
                                        GetFormattedTableName(table),
                                        table.IdColumn.Name);

                var row = ((IDictionary<string, object>) connection.Connection.Query(sql, new {Id = key}).SingleOrDefault());

                Interlocked.Increment(ref numberOfRequests);

                return row != null ? row.ToDictionary(x => table[x.Key], x => x.Value) : null;
            }
        }


        public static IDocumentStore ForTesting(SqlConnection connectionForTesting)
        {
            return new DocumentStore(connectionForTesting);
        }

        public ConnectionManager Connect()
        {
            if (connectionForTesting != null)
                return new ConnectionManager(connectionForTesting, () => { });

            var connection = new SqlConnection(connectionString);
            connection.Open();
            return new ConnectionManager(connection, connection.Dispose);
        }

        public string GetFormattedTableName(ITable table)
        {
            return (connectionForTesting != null) ? "#" + table.Name : table.Name;
        }

        void AssertInitialized()
        {
            if (!initialized)
                throw new StoreNotInitializedException();
        }

        public class ConnectionManager : IDisposable
        {
            readonly SqlConnection connection;
            readonly Action dispose;

            public ConnectionManager(SqlConnection connection, Action dispose)
            {
                this.connection = connection;
                this.dispose = dispose;
            }

            public SqlConnection Connection
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