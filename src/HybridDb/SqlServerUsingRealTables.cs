using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using Dapper;
using Microsoft.Extensions.Logging;

namespace HybridDb
{
    public class SqlServerUsingRealTables : SqlServer
    {
        int numberOfManagedConnections;

        public SqlServerUsingRealTables(DocumentStore store, string connectionString) : base(store, connectionString)
        {
        }

        public override string FormatTableName(string tablename) => store.Configuration.TableNamePrefix + tablename;

        public override ManagedConnection Connect(bool schema = false, TimeSpan? connectionTimeout = null)
        {
            var timeout = connectionTimeout ?? TimeSpan.FromSeconds(15);

            Action dispose = () => { Interlocked.Decrement(ref numberOfManagedConnections); };

            try
            {
                var connection = new SqlConnection(connectionString + $";Connection Timeout={timeout.TotalSeconds}");

                dispose += connection.Dispose;

                Interlocked.Increment(ref numberOfManagedConnections);

                connection.InfoMessage += (obj, args) => OnMessage(args);
                connection.Open();

                return new ManagedConnection(
                    connection,
                    () => connection.Dispose(),
                    dispose);
            }
            catch (Exception)
            {
                dispose();
                throw;
            }
        }

        public override Dictionary<string, List<string>> QuerySchema()
        {
            var schema = new Dictionary<string, List<string>>();

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
WHERE t.table_type='BASE TABLE' and t.table_name LIKE '{store.Configuration.TableNamePrefix}%'
OPTION (FORCE ORDER);",

                    Tuple.Create, splitOn: "column_name");

                foreach (var columnByTable in columns.GroupBy(x => x.Item1))
                {
                    var tableName = columnByTable.Key.table_name.Remove(0, store.Configuration.TableNamePrefix.Length);

                    schema.Add(tableName, columnByTable.Select(column => column.Item2.column_name).ToList());
                }

                return schema;
            }
        }
        
        public override void Dispose()
        {
            if (numberOfManagedConnections > 0)
            {
                store.Logger.LogWarning("A ManagedConnection was not properly disposed. You may be leaking sql connections or transactions.");
            }
        }
    }
}