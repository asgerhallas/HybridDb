using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Dynamic;
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

        public TableConfiguration<TDocument> ForDocument<TDocument>()
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

        public void Insert(ITableConfiguration table, Dictionary<IColumnConfiguration, object> values)
        {
            using (var connection = Connect())
            {
                var sql = string.Format("insert into {0} ({1}) values ({2})",
                                        FormatTableName(table.Name),
                                        string.Join(", ", values.Keys.Select(x => x.Name)),
                                        string.Join(", ", values.Keys.Select(x => "@" + x.Name)));

                var parameters = new DynamicParameters();
                foreach (var value in values)
                {
                    var columnConfiguration = value.Key;
                    parameters.Add("@" + columnConfiguration.Name, value.Value, columnConfiguration.Column.DbType, size: columnConfiguration.Column.Length);
                }

                connection.Connection.Execute(sql, parameters);
            }
        }

        public void Update(ITableConfiguration table, Dictionary<IColumnConfiguration, object> values)
        {
            using (var connection = Connect())
            {
                var sql = string.Format("update {0} set {1},{2}=@NewEtag where {3}=@Id and {2}=@Etag",
                                        FormatTableName(table.Name),
                                        string.Join(", ", values.Keys
                                                                .Except(new IColumnConfiguration[] {table.IdColumn, table.EtagColumn})
                                                                .Select(x => x.Name + "=@" + x.Name)),
                                        table.EtagColumn.Name,
                                        table.IdColumn.Name);

                var parameters = new DynamicParameters();
                parameters.Add("@NewEtag", Guid.NewGuid(), table.EtagColumn.Column.DbType);
                foreach (var value in values)
                {
                    var columnConfiguration = value.Key;
                    parameters.Add("@" + columnConfiguration.Name, value.Value, columnConfiguration.Column.DbType, size: columnConfiguration.Column.Length);
                }

                var rowsUpdated = connection.Connection.Execute(sql, parameters);
                if (rowsUpdated == null)

            }
        }

        public Dictionary<IColumnConfiguration, object> Get(ITableConfiguration table, Guid id, Guid? etag)
        {
            using (var connection = Connect())
            {
                var sql = string.Format("select * from {0} where {1} = @Id",
                                        FormatTableName(table.Name),
                                        table.IdColumn.Name);

                var row = (IDictionary<string, object>)connection.Connection.Query(sql, new {Id = id}).SingleOrDefault();
                if (row == null)
                    return null;

                return row.ToDictionary(x => table[x.Key], x => x.Value);

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