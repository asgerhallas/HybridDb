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
            using (var tx = connectionManager.Connection.BeginTransaction(IsolationLevel.Serializable))
            {
                if (!store.IsInTestMode)
                {
                    var existingTables = connectionManager.Connection.Query("select * from information_schema.tables where table_catalog = db_name()", null, tx);
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
                               string.Join(", ", table.Columns.Select(x => store.Escape(x.Name) + " " + x.Column.SqlType)));

                }
                connectionManager.Connection.Execute(sql.ToString(), null, tx);
                tx.Commit();
            }

            Logger.Info("HybridDb store is initialized in {0}ms", timer.ElapsedMilliseconds);
        }

        public void AddIndexFor<T>(Expression<Func<T, object>> member)
        {
            throw new NotImplementedException();
        }

        public IMigrationContext AddTable<TEntity>()
        {
            var context = new MigrationContext(store);
            context.AddTable<TEntity>();
            return context;
        }

        public IMigrationContext RemoveTable(string tableName)
        {
            var context = new MigrationContext(store);
            context.RemoveTable(tableName);
            return context;
        }

        public IMigrationContext RenameTable(string oldTableName, string newTableName)
        {
            var context = new MigrationContext(store);
            context.RenameTable(oldTableName, newTableName);
            return context;
        }

        public IMigrationContext AddProjection<TEntity, TMember>(Expression<Func<TEntity, TMember>> member)
        {
            var context = new MigrationContext(store);
            context.AddProjection(member);
            return context;
        }

        public IMigrationContext RemoveProjection<TEntity>(string columnName)
        {
            var context = new MigrationContext(store);
            context.RemoveProjection<TEntity>(columnName);
            return context;
        }

        public IMigrationContext UpdateProjectionFor<TEntity, TMember>(Expression<Func<TEntity, TMember>> member)
        {
            var context = new MigrationContext(store);
            context.UpdateProjectionFor(member);
            return context;
        }

        public IMigrationContext RenameProjection<TEntity>(string oldColumnName, string newColumnName)
        {
            var context = new MigrationContext(store);
            context.RenameProjection<TEntity>(oldColumnName, newColumnName);
            return context;
        }

        public IMigrationContext Do<TEntity>(string tableName, Action<IDictionary<string, object>> action)
        {
            var context = new MigrationContext(store);
            context.Do<TEntity>(tableName, action);
            return context;
        }
    }
}