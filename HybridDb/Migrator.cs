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

        public IMigrator MigrateTo(Table table, bool safe = true)
        {
            var timer = Stopwatch.StartNew();
            using (var connectionManager = store.Connect())
            {
                if (!store.IsInTestMode)
                {
                    var existingTables = connectionManager.Connection.Query("select * from information_schema.tables where table_catalog = db_name()", null);
                    if (existingTables.Any())
                        throw new InvalidOperationException("You cannot initialize a database that is not empty.");
                }

                var sql = new SqlBuilder();
                foreach (var table in store.Configuration.Tables.Values)
                {
                    // for extra security (and to mitigate a very theoretical race condition) recheck existance of tables before creation
                    var tableExists =
                        string.Format(store.IsInTestMode
                                          ? "OBJECT_ID('tempdb..{0}') is not null"
                                          : "exists (select * from information_schema.tables where table_catalog = db_name() and table_name = '{0}')",
                                      table.GetFormattedName(store.TableMode));

                    sql.Append("if not ({0}) begin create table {1} ({2}); end",
                               tableExists,
                               store.Escape(table.GetFormattedName(store.TableMode)),
                               string.Join(", ", table.Columns.Select(x => store.Escape(x.Name) + " " + x.SqlColumn.SqlType)));

                }
                connectionManager.Connection.Execute(sql.ToString(), null);
                connectionManager.Complete();
            }

            Logger.Info("HybridDb store is initialized in {0}ms", timer.ElapsedMilliseconds);
        }

        public IMigrator AddTable(Table table)
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
                throw new NotSupportedException("It is not possible to rename temp tables, therefore RenameTable is not supported when store is in test mode.");

            var oldTableName = oldTable.GetFormattedName(store.TableMode);
            var newTableName = newTable.GetFormattedName(store.TableMode);

            var sql = string.Format("sp_rename {0}, {1};", oldTableName, newTableName);

            connectionManager.Connection.Execute(sql, null);
            return this;
        }

        public IMigrator AddColumn(Table table, Column column)
        {
            var tableName = table.GetFormattedName(store.TableMode);
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

        public IMigrator RenameProjection(Table table, Column oldColumn, Column newColumn)
        {
            if (store.IsInTestMode)
                throw new NotSupportedException("It is not possible to rename columns on temp tables, therefore RenameProjection is not supported when store is in test mode.");

            var tableName = table.GetFormattedName(store.TableMode);
            var sql = string.Format("sp_rename '{0}.{1}', '{2}', 'COLUMN'", tableName, oldColumn.Name, newColumn.Name);

            connectionManager.Connection.Execute(sql, null);
            return this;
        }

        public IMigrator UpdateProjectionColumnsFromDocument(Table table, ISerializer serializer, Type deserializeToType)
        {
            Do<object>(table,
                       store.Configuration.Serializer,
                       (entity, projections) =>
                       {
                           foreach (var column in table.Columns.OfType<ProjectionColumn>())
                           {
                               projections[column.Name] = column.GetValue(entity);
                           }
                       });

            return this;
        }

        public IMigrator Do(Table table, ISerializer serializer, Type deserializeToType, Action<object, IDictionary<string, object>> action)
        {
            var tableName = table.GetFormattedName(store.TableMode);

            string selectSql = string.Format("SELECT * FROM {0}", tableName);
            foreach (var dictionary in connectionManager.Connection.Query(selectSql).Cast<IDictionary<string, object>>())
            {
                var documentColumn = new DocumentColumn();
                var document = (byte[])dictionary[documentColumn.Name];

                var entity = serializer.Deserialize(document, deserializeToType);
                action(entity, dictionary);
                dictionary[documentColumn.Name] = serializer.Serialize(entity);

                store.Update()

                var sql = new SqlBuilder()
                    .Append("update {0} set {1} where {2}=@Id",
                            store.Escape(tableName),
                            string.Join(", ", from column in dictionary.Keys select column + "=@" + column),
                            table.IdColumn.Name)
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

        public IMigrator Do<T>(Table table, ISerializer serializer, Action<T, IDictionary<string, object>> action) 
        {
            return Do(table, serializer, typeof (T), (entity, projections) => action((T) entity, projections));
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