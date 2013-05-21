using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using Dapper;
using HybridDb.Schema;

namespace HybridDb
{
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
            return AddTable(table.Name, table.Columns.Select(col => string.Format("{0} {1}", col.Name, GetColumnSqlType(col))).ToArray());
        }

        public IMigrator RemoveTableAndAssociatedTables(Table table)
        {
            return RemoveTable(table.Name);
        }

        public IMigrator RenameTableAndAssociatedTables(Table oldTable, string newTablename)
        {
            return RenameTable(oldTable.Name, newTablename);
        }

        public IMigrator AddColumn(string tablename, Column column)
        {
            return AddColumn(tablename, column.Name, GetColumnSqlType(column));
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
            return Do(new DocumentConfiguration(table, typeof(T)), serializer, (entity, projections) => action((T)entity, projections));
        }

        public IMigrator Do(DocumentConfiguration relation, ISerializer serializer, Action<object, IDictionary<string, object>> action)
        {
            var tablename = store.FormatTableNameAndEscape(relation.Table.Name);

            string selectSql = string.Format("select * from {0}", tablename);
            foreach (var dictionary in connectionManager.Connection.Query(selectSql).Cast<IDictionary<string, object>>())
            {
                var document = (byte[])dictionary[relation.Table.DocumentColumn.Name];

                var entity = serializer.Deserialize(document, relation.Type);
                action(entity, dictionary);
                dictionary[relation.Table.DocumentColumn.Name] = serializer.Serialize(entity);

                var sql = new SqlBuilder()
                    .Append("update {0} set {1} where {2}=@Id",
                            tablename,
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

        public IMigrator AddTable(string tablename, params string[] columns)
        {
            var escaptedColumns =
                from column in columns
                let split = column.Split(' ')
                let name = split.First()
                let type = string.Join(" ", split.Skip(1))
                select store.Escape(name) + " " + type;

            return Execute(
                string.Format("if not ({0}) begin create table {1} ({2}); end",
                              GetTableExistsSql(tablename),
                              store.FormatTableNameAndEscape(tablename),
                              string.Join(", ", escaptedColumns)));
        }

        public IMigrator RemoveTable(string tablename)
        {
            return Execute(
                string.Format("drop table {0};", store.FormatTableNameAndEscape(tablename)));
        }

        public IMigrator RenameTable(string oldTablename, string newTablename)
        {
            if (store.IsInTestMode)
                throw new NotSupportedException("It is not possible to rename temp tables, so RenameTable is not supported when store is in test mode.");

            return Execute(
                string.Format("sp_rename {0}, {1};", store.FormatTableNameAndEscape(oldTablename), store.FormatTableNameAndEscape(newTablename)));
        }

        public IMigrator AddColumn(string tablename, string columnname, string columntype)
        {
            return Execute(
                string.Format("alter table {0} add {1} {2};", store.FormatTableNameAndEscape(tablename), store.Escape(columnname), columntype));
        }

        public IMigrator RemoveColumn(string tablename, string columnname)
        {
            return Execute(
                string.Format("alter table {0} drop column {1};", store.FormatTableNameAndEscape(tablename), store.Escape(columnname)));
        }

        public IMigrator RenameColumn(string tablename, string oldColumnname, string newColumnname)
        {
            if (store.IsInTestMode)
                throw new NotSupportedException("It is not possible to rename columns on temp tables, therefore RenameColumn is not supported when store is in test mode.");

            return Execute(
                string.Format("sp_rename '{0}.{1}', '{2}', 'COLUMN'", store.FormatTableNameAndEscape(tablename), oldColumnname, newColumnname));
        }


        public IMigrator Commit()
        {
            connectionManager.Complete();
            return this;
        }

        public IMigrator Execute(string sql, object param = null)
        {
            connectionManager.Connection.Execute(sql, param);
            return this;
        }

        public void Dispose()
        {
            connectionManager.Dispose();
        }

        string GetColumnSqlType(Column column)
        {
            if (column.SqlColumn.Type == null)
                throw new ArgumentException(string.Format("Column {0} must have a type", column.Name));

            var sqlDbTypeString = new SqlParameter { DbType = (DbType) column.SqlColumn.Type }.SqlDbType.ToString();
            var lengthString = (column.SqlColumn.Length != null) ? "(" + (column.SqlColumn.Length == Int32.MaxValue ? "MAX" : column.SqlColumn.Length.ToString()) + ")" : "";
            var asPrimaryKeyString = column.SqlColumn.IsPrimaryKey ? " NOT NULL PRIMARY KEY" : "";
            return sqlDbTypeString + lengthString + asPrimaryKeyString;
        }

        string GetTableExistsSql(string tablename)
        {
            return string.Format(store.IsInTestMode
                                     ? "OBJECT_ID('tempdb..{0}') is not null"
                                     : "exists (select * from information_schema.tables where table_catalog = db_name() and table_name = '{0}')",
                                 store.FormatTableName(tablename));
        }
    }
}