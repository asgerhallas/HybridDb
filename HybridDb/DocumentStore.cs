using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Dapper;

namespace HybridDb
{
    public class DocumentStore : IDocumentStore
    {
        readonly Configuration configuration;
        readonly SqlConnection connectionForTesting;
        readonly string connectionString;

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
                foreach (var entity in configuration.Tables.Values)
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
            return new DocumentSession(this);
        }

        public Guid Insert(ITable table, Guid key, byte[] document, object projections)
        {
            var values = projections as IDictionary<string, object> ?? ObjectToDictionaryRegistry.Convert(projections);

            var etag = Guid.NewGuid();
            values.Add(table.EtagColumn.Name, etag);
            values.Add(table.IdColumn.Name, key);
            values.Add(table.DocumentColumn.Name, document);

            using (var connection = Connect())
            {
                var sql = string.Format("insert into {0} ({1}) values ({2})",
                                        FormatTableName(table.Name),
                                        string.Join(", ", values.Keys),
                                        string.Join(", ", values.Keys.Select(name => "@" + name)));

                var parameters = new DynamicParameters();
                foreach (var value in values)
                {
                    var columnConfiguration = table[value.Key];
                    parameters.Add("@" + columnConfiguration.Name,
                                   value.Value,
                                   columnConfiguration.Column.DbType,
                                   size: columnConfiguration.Column.Length);
                }

                connection.Connection.Execute(sql, parameters);
            }

            return etag;
        }

        public Guid Update(ITable table, Guid key, Guid etag, byte[] document, object projections)
        {
            var projectionsAsDictionary = (projections as IDictionary<string, object> ?? ObjectToDictionaryRegistry.Convert(projections))
                .ToDictionary(x => table[x.Key], x => x.Value);

            var newEtag = Guid.NewGuid();
            projectionsAsDictionary.Add(table.EtagColumn, newEtag);
            projectionsAsDictionary.Add(table.DocumentColumn, document);

            using (var connection = Connect())
            {
                var sql = string.Format("update {0} set {1} where {2}=@Id and {3}=@CurrentEtag",
                                        FormatTableName(table.Name),
                                        string.Join(", ", projectionsAsDictionary.Keys.Select(name => name + "=@" + name)),
                                        table.IdColumn.Name,
                                        table.EtagColumn.Name);

                var parameters = MapProjectionsToParameters(projectionsAsDictionary);

                var rowsUpdated = connection.Connection.Execute(sql, parameters);
                if (rowsUpdated == 0)
                    throw new ConcurrencyException();
            }

            return newEtag;
        }

        Dictionary<IColumn, object> ConvertAnonymousToProjections(ITable table, object projections)
        {
            projections as IDictionary<IColumn, object>

            var projectionsAsDictionary = (projections as IDictionary<string, object> ?? ObjectToDictionaryRegistry.Convert(projections))
                .ToDictionary(x => table[x.Key], x => x.Value);
        }

        DynamicParameters MapProjectionsToParameters(Dictionary<IColumn, object> projections)
        {
            var parameters = new DynamicParameters();
            //parameters.Add("@Id", key, table.IdColumn.Column.DbType);
            //parameters.Add("@CurrentEtag", etag, table.EtagColumn.Column.DbType);
            foreach (var projection in projections)
            {
                var column = projection.Key;
                parameters.Add("@" + column.Name,
                               projection.Value,
                               column.Column.DbType,
                               size: column.Column.Length);
            }

            return parameters;
        }

        public Document Get(ITable table, Guid key)
        {
            using (var connection = Connect())
            {
                var sql = string.Format("select * from {0} where {1} = @Id",
                                        FormatTableName(table.Name),
                                        table.IdColumn.Name);

                IDictionary<string, object> values = connection.Connection.Query(sql, new {Id = key}).SingleOrDefault();
                return new Document
                {
                    Etag = (Guid) values[table.EtagColumn.Name],
                    Data = (byte[]) values[table.DocumentColumn.Name],
                    Projections =
                        values.Where(x => !new[] {table.IdColumn.Name, table.EtagColumn.Name, table.DocumentColumn.Name}.Contains(x.Key)).ToDictionary(x => x.Key, x => x.Value)
                };
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