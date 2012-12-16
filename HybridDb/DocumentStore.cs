using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using Dapper;

namespace HybridDb
{
    public abstract class DatabaseCommand
    {
        internal abstract PreparedDatabaseCommand Prepare(DocumentStore store, Guid etag, int uniqueParameterIdentifier);

        protected static IDictionary<IColumn, object> ConvertAnonymousToProjections(ITable table, object projections)
        {
            return projections as IDictionary<IColumn, object> ??
                   (projections as IDictionary<string, object> ?? ObjectToDictionaryRegistry.Convert(projections))
                       .ToDictionary(x => table[x.Key], x => x.Value);
        }

        protected static DynamicParameters MapProjectionsToParameters(IDictionary<IColumn, object> projections, int i)
        {
            var parameters = new DynamicParameters();
            foreach (var projection in projections)
            {
                var column = projection.Key;
                parameters.Add("@" + column.Name + i,
                               projection.Value,
                               column.Column.DbType,
                               size: column.Column.Length);
            }

            return parameters;
        }

        public class PreparedDatabaseCommand
        {
            public string Sql { get; set; }
            public DynamicParameters Parameters { get; set; }
            public int ExpectedRowCount { get; set; }
        }
    }

    public class InsertCommand : DatabaseCommand
    {
        readonly byte[] document;
        readonly Guid key;
        readonly object projections;
        readonly ITable table;

        public InsertCommand(ITable table, Guid key, byte[] document, object projections)
        {
            this.table = table;
            this.key = key;
            this.document = document;
            this.projections = projections;
        }

        internal override PreparedDatabaseCommand Prepare(DocumentStore store, Guid etag, int uniqueParameterIdentifier)
        {
            var values = ConvertAnonymousToProjections(table, projections);

            values.Add(table.EtagColumn, etag);
            values.Add(table.IdColumn, key);
            values.Add(table.DocumentColumn, document);

            var sql = string.Format("insert into {0} ({1}) values ({2})",
                                    store.GetFormattedTableName(table),
                                    string.Join(", ", from column in values.Keys select column.Name),
                                    string.Join(", ", from column in values.Keys select "@" + column.Name + uniqueParameterIdentifier));

            var parameters = MapProjectionsToParameters(values, uniqueParameterIdentifier);

            return new PreparedDatabaseCommand
            {
                Sql = sql,
                Parameters = parameters,
                ExpectedRowCount = 1
            };
        }
    }

    public class UpdateCommand : DatabaseCommand
    {
        readonly Guid currentEtag;
        readonly byte[] document;
        readonly Guid key;
        readonly object projections;
        readonly ITable table;

        public UpdateCommand(ITable table, Guid key, Guid etag, byte[] document, object projections)
        {
            this.table = table;
            this.key = key;
            currentEtag = etag;
            this.document = document;
            this.projections = projections;
        }

        internal override PreparedDatabaseCommand Prepare(DocumentStore store, Guid etag, int uniqueParameterIdentifier)
        {
            var values = ConvertAnonymousToProjections(table, projections);

            values.Add(table.EtagColumn, etag);
            values.Add(table.DocumentColumn, document);

            var sql = string.Format("update {0} set {1} where {2}=@Id{4} and {3}=@CurrentEtag{4}",
                                    store.GetFormattedTableName(table),
                                    string.Join(", ", from column in values.Keys select column.Name + "=@" + column.Name + uniqueParameterIdentifier),
                                    table.IdColumn.Name,
                                    table.EtagColumn.Name,
                                    uniqueParameterIdentifier);

            var parameters = MapProjectionsToParameters(values, uniqueParameterIdentifier);
            parameters.Add("@Id" + uniqueParameterIdentifier, key, table.IdColumn.Column.DbType);
            parameters.Add("@CurrentEtag" + uniqueParameterIdentifier, currentEtag, table.EtagColumn.Column.DbType);

            return new PreparedDatabaseCommand
            {
                Sql = sql,
                Parameters = parameters,
                ExpectedRowCount = 1
            };
        }
    }

    public class DeleteCommand : DatabaseCommand
    {
        readonly Guid currentEtag;
        readonly Guid key;
        readonly ITable table;

        public DeleteCommand(ITable table, Guid key, Guid etag)
        {
            this.table = table;
            this.key = key;
            currentEtag = etag;
        }

        internal override PreparedDatabaseCommand Prepare(DocumentStore store, Guid etag, int uniqueParameterIdentifier)
        {
            var sql = string.Format("delete from {0} where {1} = @Id{3} and {2} = @CurrentEtag{3}",
                                    store.GetFormattedTableName(table),
                                    table.IdColumn.Name,
                                    table.EtagColumn.Name,
                                    uniqueParameterIdentifier);

            var parameters = new DynamicParameters();
            parameters.Add("@Id" + uniqueParameterIdentifier, key, table.IdColumn.Column.DbType);
            parameters.Add("@CurrentEtag" + uniqueParameterIdentifier, currentEtag, table.EtagColumn.Column.DbType);

            return new PreparedDatabaseCommand
            {
                Sql = sql,
                Parameters = parameters,
                ExpectedRowCount = 1
            };
        }
    }

    public class DocumentStore : IDocumentStore
    {
        readonly Configuration configuration;
        readonly SqlConnection connectionForTesting;
        readonly string connectionString;
        bool initialized;
        long numberOfRequests;
        Guid lastWrittenEtag;

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
            using (var manager = Connect())
            using (var tx = manager.Connection.BeginTransaction())
            {
                foreach (var table in configuration.Tables.Values)
                {
                    var sql = "create table ";
                    sql += GetFormattedTableName(table);
                    sql += " (" + string.Join(", ", table.Columns.Select(x => x.Name + " " + x.Column.SqlType)) + ")";
                    manager.Connection.Execute(sql, null, tx);
                }
                tx.Commit();
            }

            initialized = true;
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

            using (var connection = Connect())
            {
                var rowcount = connection.Connection.Execute(sql, parameters);

                Interlocked.Increment(ref numberOfRequests);
                lastWrittenEtag = etag;

                if (rowcount != expectedRowCount)
                    throw new ConcurrencyException();
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