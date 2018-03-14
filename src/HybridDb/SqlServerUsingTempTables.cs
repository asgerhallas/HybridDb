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
    public class SqlServerUsingTempTables : SqlServer
    {
        int numberOfManagedConnections;
        SqlConnection ambientConnectionForTesting;

        public SqlServerUsingTempTables(DocumentStore store, string connectionString) : base(store, connectionString) {}

        public override string FormatTableName(string tablename) => "#" + tablename;

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

                if (ambientConnectionForTesting == null)
                {
                    ambientConnectionForTesting = new SqlConnection(connectionString);
                    ambientConnectionForTesting.InfoMessage += (obj, args) => OnMessage(args);
                    ambientConnectionForTesting.Open();
                }

                // Connections that are kept open during multiple operations (for testing mostly)
                // will not automatically be enlisted in transactions started later, we fix that here.
                // Calling EnlistTransaction on a connection that is already enlisted is a no-op.
                ambientConnectionForTesting.EnlistTransaction(Transaction.Current);

                return new ManagedConnection(ambientConnectionForTesting, complete, dispose);
            }
            catch (Exception)
            {
                dispose();

                ambientConnectionForTesting.Dispose();

                throw;
            }
        }

        public override Dictionary<string, Table> QuerySchema()
        {
            var schema = new Dictionary<string, Table>();

            using (var managedConnection = Connect())
            {
                var columns = managedConnection.Connection.Query<TableInfo, QueryColumn, Tuple<TableInfo, QueryColumn>>(@"
SELECT 
   table_name = SUBSTRING(t.name, 1, CHARINDEX('___', t.name)-1),
   full_table_name = t.name,
   column_name = c.name,
   type_name = (select name from sys.types where user_type_id = c.user_type_id),
   max_length = c.max_length,
   is_nullable = c.is_nullable,
   default_value = (select column_default from tempdb.information_schema.columns where table_name=t.name and column_name=c.name),
   is_primary_key = (
        select 1 
        from tempdb.information_schema.table_constraints as ct
        join tempdb.information_schema.key_column_usage as k
        on ct.table_name = k.table_name
        and ct.constraint_catalog = k.constraint_catalog
        and ct.constraint_schema = k.constraint_schema 
        and ct.constraint_name = k.constraint_name
        where ct.constraint_type = 'primary key'
        and k.table_name = t.name
        and k.column_name = c.name)
FROM tempdb.sys.tables AS t
INNER JOIN tempdb.sys.columns AS c
ON t.[object_id] = c.[object_id]
WHERE t.name LIKE '#%[_][_][_]%'
AND t.[object_id] = OBJECT_ID('tempdb..' + SUBSTRING(t.name, 1, CHARINDEX('___', t.name)-1))
OPTION (FORCE ORDER);", 
  
    Tuple.Create, splitOn: "column_name");

                foreach (var columnByTable in columns.GroupBy(x => x.Item1))
                {
                    var tableName = columnByTable.Key.table_name.TrimStart('#');
                    schema.Add(tableName, new Table(tableName, columnByTable.Select(column => 
                        Map(columnByTable.Key.full_table_name, column.Item2))));
                }

                return schema;
            }
        }

        public override void Dispose()
        {
            if (numberOfManagedConnections > 0)
            {
                store.Logger.Warning("A ManagedConnection was not properly disposed. You may be leaking sql connections or transactions.");
            }

            ambientConnectionForTesting?.Dispose();
        }
    }
}