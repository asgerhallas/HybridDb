using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using Dapper;
using HybridDb.Schema;

namespace HybridDb
{

    //public IMigrator AddTable<TEntity>()
    //{
    //    var table = new Table(store.Configuration.GetTableNameByConventionFor<TEntity>());
    //    return AddTable(table);
    //}
    

    public class Migrator : IMigrator
    {
        readonly DocumentStore store;
        readonly ManagedConnection connectionManager;

        public Migrator(DocumentStore store)
        {
            this.store = store;
            connectionManager = store.Connect();
        }

        public IMigrator MigrateTo(DocumentConfiguration documentConfiguration, bool safe = true)
        {
            var timer = Stopwatch.StartNew();

            if (!store.IsInTestMode)
            {
                var existingTables = connectionManager.Connection.Query("select * from information_schema.tables where table_catalog = db_name()", null);
                if (existingTables.Any())
                    throw new InvalidOperationException("You cannot initialize a database that is not empty.");
            }

            AddTableAndColumns(documentConfiguration.Table);

            store.Configuration.Logger.Info("HybridDb store is initialized in {0}ms", timer.ElapsedMilliseconds);
            return this;
        }

        public IMigrator AddTableAndColumns(Table table)
        {
            var tableName = table.GetFormattedName(store.TableMode);

            var sql = string.Format("if not ({0}) begin create table {1} ({2}); end",
                                    GetTableExistsSql(tableName),
                                    store.Escape(tableName),
                                    string.Join(", ", table.Columns.Select(x => store.Escape(x.Name) + " " + x.SqlColumn.SqlType)));
            
            connectionManager.Connection.Execute(sql, null);
            return this;
        }

        public IMigrator RemoveTable(Table table)
        {
            var tableName = table.GetFormattedName(store.TableMode);
            var sql = string.Format("drop table {0};", store.Escape(tableName));
            connectionManager.Connection.Execute(sql, null);
            return this;
        }

        public IMigrator RenameTable(Table oldTable, Table newTable)
        {
            if (store.IsInTestMode)
                throw new NotSupportedException("It is not possible to rename temp tables, so RenameTable is not supported when store is in test mode.");

            var oldTableName = oldTable.GetFormattedName(store.TableMode);
            var newTableName = newTable.GetFormattedName(store.TableMode);

            var sql = string.Format("sp_rename {0}, {1};", oldTableName, newTableName);

            connectionManager.Connection.Execute(sql, null);
            return this;
        }

        public IMigrator AddColumn(Table table, Column column)
        {
            var tableName = table.GetFormattedName(store.TableMode);                                              // INLINE THIS SqlType thing
            var sql = string.Format("ALTER TABLE {0} ADD {1};", tableName, store.Escape(column.Name) + " " + column.SqlColumn.SqlType);
            
            connectionManager.Connection.Execute(sql, null);
            return this;
        }

        public IMigrator RemoveColumn(Table table, Column column)
        {
            var tableName = table.GetFormattedName(store.TableMode);
            var sql = string.Format("ALTER TABLE {0} DROP COLUMN {1};", store.Escape(tableName), store.Escape(column.Name));
            connectionManager.Connection.Execute(sql, null);
            return this;
        }

        public IMigrator RenameColumn(Table table, Column oldColumn, Column newColumn)
        {
            if (store.IsInTestMode)
                throw new NotSupportedException("It is not possible to rename columns on temp tables, therefore RenameColumn is not supported when store is in test mode.");

            var tableName = table.GetFormattedName(store.TableMode);
            var sql = string.Format("sp_rename '{0}.{1}', '{2}', 'COLUMN'", tableName, oldColumn.Name, newColumn.Name);

            connectionManager.Connection.Execute(sql, null);
            return this;
        }

        public IMigrator UpdateProjectionColumnsFromDocument(DocumentConfiguration documentConfiguration, ISerializer serializer)
        {
            Do(documentConfiguration,
               store.Configuration.Serializer,
               (entity, projections) =>
               {
                   foreach (var column in documentConfiguration.Projections.Where(x => x.Key is UserColumn))
                   {
                       projections[column.Key.Name] = column.Value(entity);
                   }
               });

            return this;
        }

        public IMigrator Do<T>(Table table, ISerializer serializer, Action<T, IDictionary<string, object>> action) 
        {
            return Do(new DocumentConfiguration(table, typeof(T)), serializer, (entity, projections) => action((T) entity, projections));
        }

        public IMigrator Do(DocumentConfiguration relation, ISerializer serializer, Action<object, IDictionary<string, object>> action)
        {
            var tableName = relation.Table.GetFormattedName(store.TableMode);

            string selectSql = string.Format("SELECT * FROM {0}", tableName);
            foreach (var dictionary in connectionManager.Connection.Query(selectSql).Cast<IDictionary<string, object>>())
            {
                var document = (byte[])dictionary[relation.Table.DocumentColumn.Name];

                var entity = serializer.Deserialize(document, relation.Type);
                action(entity, dictionary);
                dictionary[relation.Table.DocumentColumn.Name] = serializer.Serialize(entity);

                var sql = new SqlBuilder()
                    .Append("update {0} set {1} where {2}=@Id",
                            store.Escape(tableName),
                            string.Join(", ", from column in dictionary.Keys select column + "=@" + column),
                            relation.Table.IdColumn.Name)
                    .ToString();

                var parameters = dictionary.Select(x => new Parameter
                {
                    Name = x.Key,
                    Value = x.Value
                });

                connectionManager.Connection.Execute(sql, new FastDynamicParameters(parameters));
            }

            return this;
        }

        public IMigrator Commit()
        {
            connectionManager.Complete();
            return this;
        }

        public void Dispose()
        {
            connectionManager.Dispose();
        }

        string GetTableExistsSql(string tableName)
        {
            return string.Format(store.IsInTestMode
                                     ? "OBJECT_ID('tempdb..{0}') is not null"
                                     : "exists (select * from information_schema.tables where table_catalog = db_name() and table_name = '{0}')",
                                 tableName);
        }
    }
}