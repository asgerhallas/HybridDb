using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using Dapper;
using HybridDb.Schema;

namespace HybridDb
{
    public class Migrator : IMigrator
    {
        readonly DocumentStore store;
        readonly DbTransaction tx;
        readonly ManagedConnection connectionManager;

        public Migrator(DocumentStore store)
        {
            this.store = store;
            connectionManager = store.Connect();
            tx = connectionManager.Connection.BeginTransaction(IsolationLevel.Serializable);
        }

        public void InitializeDatabase()
        {
            throw new NotSupportedException("Use store.Migrations.InitializeDatabase()");
        }

        public IMigrator AddTable<TEntity>()
        {
            var table = new Table<TEntity>(null);
            var tableName = store.GetFormattedTableName(table);
            
            var sql = string.Format("if not ({0}) begin create table {1} ({2}); end",
                        GetTableExistsSql(tableName),
                        store.Escape(tableName),
                        string.Join(", ", table.Columns.Select(x => store.Escape(x.Name) + " " + x.Column.SqlType)));

            connectionManager.Connection.Execute(sql, null, tx);
            return this;
        }

        public IMigrator RemoveTable(string tableName)
        {
            var sql = string.Format("drop table {0};", store.GetFormattedTableName(tableName));
            connectionManager.Connection.Execute(sql, null, tx);
            return this;
        }

        string GetTableExistsSql(string tableName)
        {
            return string.Format(store.IsInTestMode
                                     ? "OBJECT_ID('tempdb..{0}') is not null"
                                     : "exists (select * from information_schema.tables where table_catalog = db_name() and table_name = '{0}')",
                                 tableName);
        }

        public IMigrator RenameTable(string oldTableName, string newTableName)
        {
            if (store.IsInTestMode)
                throw new NotSupportedException("It is not possible to rename temp tables, therefore RenameTable is not supported when store is in test mode.");

            var sql = string.Format("sp_rename {0}, {1};",
                                    store.GetFormattedTableName(oldTableName),
                                    store.GetFormattedTableName(newTableName));
            connectionManager.Connection.Execute(sql, null, tx);
            return this;
        }

        public IMigrator AddProjection<TEntity, TMember>(Expression<Func<TEntity, TMember>> member)
        {
            var table = new Table<TEntity>(null);
            var projection = new ProjectionColumn<TEntity, TMember>(member);

            var sql = string.Format("ALTER TABLE {0} ADD {1};",
                                    store.GetFormattedTableName(table),
                                    store.Escape(projection.Name) + " " + projection.Column.SqlType);
            connectionManager.Connection.Execute(sql, null, tx);
            return this;
        }

        public IMigrator RemoveProjection<TEntity>(string columnName)
        {
            var table = new Table<TEntity>(null);

            var sql = string.Format("ALTER TABLE {0} DROP COLUMN {1};",
                                    store.GetFormattedTableName(table),
                                    columnName);
            connectionManager.Connection.Execute(sql, null, tx);
            return this;
        }

        public IMigrator RenameProjection<TEntity>(string oldColumnName, string newColumnName)
        {
            if (store.IsInTestMode)
                throw new NotSupportedException("It is not possible to rename columns on temp tables, therefore RenameProjection is not supported when store is in test mode.");

            var table = new Table<TEntity>(null);

            var sql = string.Format("sp_rename '{0}.{1}', '{2}', 'COLUMN'", store.GetFormattedTableName(table), oldColumnName, newColumnName);
            connectionManager.Connection.Execute(sql, null, tx);
            return this;
        }

        public IMigrator UpdateProjectionFor<TEntity, TMember>(Expression<Func<TEntity, TMember>> member)
        {
            var table = new Table<TEntity>(null);
            var column = new ProjectionColumn<TEntity, TMember>(member);

            Do<TEntity>(table.Name, (entity, projections) =>
            {
                projections[column.Name] = column.GetValue(entity);
            });

            return this;
        }

        public IMigrator Do<T>(string tableName, Action<T, IDictionary<string, object>> action)
        {
            var table = new Table<T>(tableName);
            string selectSql = string.Format("SELECT * FROM {0}", store.GetFormattedTableName(table));
            foreach (var dictionary in connectionManager.Connection.Query(selectSql, transaction: tx).Cast<IDictionary<string, object>>())
            {
                var documentColumn = new DocumentColumn();
                var document = (byte[])dictionary[documentColumn.Name];
                
                var entity = (T)store.Configuration.Serializer.Deserialize(document, table.EntityType);
                action(entity, dictionary);
                dictionary[documentColumn.Name] = store.Configuration.Serializer.Serialize(entity);

                var sql = new SqlBuilder()
                    .Append("update {0} set {1} where {2}=@Id",
                            store.Escape(store.GetFormattedTableName(table)),
                            string.Join(", ", from column in dictionary.Keys select column + "=@" + column),
                            table.IdColumn.Name)
                    .ToString();

                var parameters = dictionary.Select(x => new Parameter
                {
                    Name = x.Key,
                    Value = x.Value
                });
                connectionManager.Connection.Execute(sql, new FastDynamicParameters(parameters), tx);
            }

            return this;
        }

        public void Commit()
        {
            tx.Commit();
        }

        public void Dispose()
        {
            tx.Dispose();
            connectionManager.Dispose();
        }
    }
}