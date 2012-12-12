using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Dapper;

namespace HybridDb
{
    public class DocumentStore : IDocumentStore
    {
        readonly SqlConnection connectionForTesting;
        readonly string connectionString;
        readonly Schema schema;

        DocumentStore()
        {
            schema = new Schema();
        }

        DocumentStore(SqlConnection connectionForTesting) : this()
        {
            this.connectionForTesting = connectionForTesting;
        }

        public DocumentStore(string connectionString) : this()
        {
            this.connectionString = connectionString;
        }

        public Schema Schema
        {
            get { return schema; }
        }

        public Table<TDocument> ForDocument<TDocument>()
        {
            return schema.Register<TDocument>();
        }

        public void Initialize()
        {
            using (var manager = Connect())
            using (var tx = manager.Connection.BeginTransaction())
            {
                foreach (var entity in schema.Tables.Values)
                {
                    var sql = "create table ";
                    sql += FormatTableName(entity.Name);
                    sql += " (" + string.Join(", ", entity.Columns.Select(x => x.Name + " " + x.Column.SqlType)) + ")";
                    manager.Connection.Execute(sql, null, tx);
                }
                tx.Commit();
            }
        }

        public IDocumentSession OpenSession()
        {
            return new DocumentSession(this, schema.Tables);
        }

        public void Insert(Guid key, object projections, byte[] document)
        {
            var table = Schema.Tables[document.GetType()];
            var projectionsAsDictionary = projections as IDictionary<string, object> ?? ObjectToDictionaryRegistry.Convert(projections);

            using (var connection = Connect())
            {
                var sql = string.Format("insert into {0} ({1}) values ({2})",
                                        FormatTableName(table.Name),
                                        string.Join(", ", projectionsAsDictionary.Keys),
                                        string.Join(", ", projectionsAsDictionary.Keys.Select(name => "@" + name)));

                var parameters = new DynamicParameters();
                foreach (var value in projectionsAsDictionary)
                {
                    var columnConfiguration = table[value.Key];
                    parameters.Add("@" + columnConfiguration.Name, value.Value, columnConfiguration.Column.DbType, size: columnConfiguration.Column.Length);
                }

                connection.Connection.Execute(sql, parameters);
            }
        }

        public void Update(Guid key, Guid etag, object projections, byte[] document)
        {
            var table = Schema.Tables[document.GetType()];
            var valuesAsDictionary = projections as IDictionary<string, object> ?? ObjectToDictionaryRegistry.Convert(projections);

            using (var connection = Connect())
            {
                var sql = string.Format("update {0} set {1},{2}=@NewEtag where {3}=@Id and {2}=@Etag",
                                        FormatTableName(table.Name),
                                        string.Join(", ", valuesAsDictionary.Keys
                                                                .Select(name => name + "=@" + name)),
                                        table.EtagColumn.Name,
                                        table.IdColumn.Name);

                var parameters = new DynamicParameters();
                parameters.Add("@NewEtag", Guid.NewGuid(), table.EtagColumn.Column.DbType);
                foreach (var value in valuesAsDictionary)
                {
                    var columnConfiguration = table[value.Key];
                    parameters.Add("@" + columnConfiguration.Name, value.Value, columnConfiguration.Column.DbType, size: columnConfiguration.Column.Length);
                }

                var rowsUpdated = connection.Connection.Execute(sql, parameters);
                if (rowsUpdated == 0)
                    throw new ConcurrencyException();
            }
        }

        public IDictionary<string, object> Get(ITable table, Guid key)
        {
            using (var connection = Connect())
            {
                var sql = string.Format("select * from {0} where {1} = @Id",
                                        FormatTableName(table.Name),
                                        table.IdColumn.Name);

                return (IDictionary<string, object>) connection.Connection.Query(sql, new {Id = key}).SingleOrDefault();
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

        string FormatTableName(string tableName)
        {
            return (connectionForTesting != null) ? "#" + tableName : tableName;
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
    }
}