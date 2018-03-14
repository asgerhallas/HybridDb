using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Transactions;
using Dapper;
using HybridDb.Config;
using Serilog;

namespace HybridDb
{
    public class SqlServerUsingTempDb : SqlServer
    {
        int numberOfManagedConnections;

        public SqlServerUsingTempDb(DocumentStore store, string connectionString) : base(store, connectionString)
        {
        }

        public override string FormatTableName(string tablename)
        {
            if (string.IsNullOrEmpty(store.Configuration.TableNamePrefix))
                throw new InvalidOperationException("SqlServerUsingTempDb requires a table name prefix. Please call UseTableNamePrefix() in your configuration.");

            return $"HybridDb_{store.Configuration.TableNamePrefix}_{tablename}";
        }

        public override ManagedConnection Connect()
        {
            Action complete = () => { };
            Action dispose = () => { numberOfManagedConnections--; };

            try
            {
                numberOfManagedConnections++;

                if (Transaction.Current == null)
                {
                    var tx = new TransactionScope(
                        TransactionScopeOption.RequiresNew,
                        new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted });

                    complete += tx.Complete;
                    dispose += tx.Dispose;
                }

                var connection = new SqlConnection(connectionString);

                complete = connection.Dispose + complete;
                dispose = connection.Dispose + dispose;

                connection.InfoMessage += (obj, args) => OnMessage(args);
                connection.Open();
                connection.ChangeDatabase("tempdb");

                connection.EnlistTransaction(Transaction.Current);

                return new ManagedConnection(connection, complete, dispose);
            }
            catch (Exception)
            {
                dispose();
                throw;
            }
        }

        public override Dictionary<string, Table> QuerySchema()
        {
            var schema = new Dictionary<string, Table>();

            var prefix = $"HybridDb_{store.Configuration.TableNamePrefix}_";

            using (var managedConnection = Connect())
            {
                var columns = managedConnection.Connection.Query<TableInfo, QueryColumn, Tuple<TableInfo, QueryColumn>>($@"
SELECT 
   table_name = t.table_name,
   full_table_name = t.table_name,
   column_name = c.name,
   type_name = (select name from sys.types where user_type_id = c.user_type_id),
   max_length = c.max_length,
   is_nullable = c.is_nullable,
   default_value = (select column_default from information_schema.columns where table_name=t.table_name and column_name=c.name),
   is_primary_key = (
        select 1 
        from information_schema.table_constraints as ct
        join information_schema.key_column_usage as k
        on ct.table_name = k.table_name
        and ct.constraint_catalog = k.constraint_catalog
        and ct.constraint_schema = k.constraint_schema 
        and ct.constraint_name = k.constraint_name
        where ct.constraint_type = 'primary key'
        and k.table_name = t.table_name
        and k.column_name = c.name)
FROM information_schema.tables AS t
INNER JOIN sys.columns AS c
ON OBJECT_ID(t.table_name) = c.[object_id]
WHERE t.table_type='BASE TABLE' and t.table_name LIKE '{prefix}%'
OPTION (FORCE ORDER);",

                    Tuple.Create, splitOn: "column_name");

                foreach (var columnByTable in columns.GroupBy(x => x.Item1))
                {
                    var tableName = columnByTable.Key.table_name.Remove(0, prefix.Length);
                    schema.Add(tableName, new Table(tableName, columnByTable.Select(column =>
                        Map(columnByTable.Key.full_table_name, column.Item2))));
                }

                return schema;
            }
        }

        public override void Dispose()
        {
            if (numberOfManagedConnections > 0)
                this.store.Logger.Warning("A ManagedConnection was not properly disposed. You may be leaking sql connections or transactions.");
        }
    }
}