using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using Dapper;
using HybridDb.Logging;
using HybridDb.Schema;

namespace HybridDb
{
    public class Migration : IMigration
    {
        readonly DocumentStore store;

        public Migration(DocumentStore store)
        {
            this.store = store;
        }

        public ILogger Logger
        {
            get { return store.Configuration.Logger; }
        }

        public void InitializeDatabase()
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
                                      store.GetFormattedTableName(table));

                    sql.Append("if not ({0}) begin create table {1} ({2}); end",
                               tableExists,
                               store.Escape(store.GetFormattedTableName(table)),
                               string.Join(", ", table.Columns.Select(x => store.Escape(x.Name) + " " + x.SqlColumn.SqlType)));

                }
                connectionManager.Connection.Execute(sql.ToString(), null);
                connectionManager.Complete();
            }

            Logger.Info("HybridDb store is initialized in {0}ms", timer.ElapsedMilliseconds);
        }

        public IMigrator CreateMigrator()
        {
            return new Migrator(store);
        }

        public void AddTable<TEntity>()
        {
            using (var tx = new Migrator(store))
            {
                tx.AddTable<TEntity>();
                tx.Commit();
            }
        }

        public void RemoveTable(string tableName)
        {
            using (var tx = new Migrator(store))
            {
                tx.RemoveTable(tableName);
                tx.Commit();
            }
        }

        public void RenameTable(string oldTableName, string newTableName)
        {
            using (var tx = new Migrator(store))
            {
                tx.RenameTable(oldTableName, newTableName);
                tx.Commit();
            }
        }

        public void AddProjection<TEntity, TMember>(Expression<Func<TEntity, TMember>> member)
        {
            using (var tx = new Migrator(store))
            {
                tx.AddProjection(member);
                tx.Commit();
            }
        }

        public void RemoveProjection<TEntity>(string columnName)
        {
            using (var tx = new Migrator(store))
            {
                tx.RemoveProjection<TEntity>(columnName);
                tx.Commit();
            }
        }

        public void UpdateProjectionFor<TEntity, TMember>(Expression<Func<TEntity, TMember>> member)
        {
            using (var tx = new Migrator(store))
            {
                tx.UpdateProjectionFor(member);
                tx.Commit();
            }
        }

        public void RenameProjection<TEntity>(string oldColumnName, string newColumnName)
        {
            using (var tx = new Migrator(store))
            {
                tx.RenameProjection<TEntity>(oldColumnName, newColumnName);
                tx.Commit();
            }
        }

        public void Do<T>(string tableName, Action<T, IDictionary<string, object>> action)
        {
            using (var tx = new Migrator(store))
            {
                tx.Do(tableName, action);
                tx.Commit();
            }
        }
    }
}