using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Transactions;
using Dapper;
using Microsoft.Extensions.Logging;

namespace HybridDb
{
    public class SqlServerUsingGlobalTempTables : SqlServer
    {
        SqlConnection schemaBuilderConnection;
        //ConcurrentDictionary<Guid, ManagedConnection> connections = new ConcurrentDictionary<Guid, ManagedConnection>();

        string prefix;
        int numberOfManagedConnections;

        public SqlServerUsingGlobalTempTables(DocumentStore store, string connectionString) : base(store, connectionString)
        {
        }

        public override void Initialize()
        {
            // already initialized?
            if (prefix != null) return;

            if (string.IsNullOrEmpty(store.Configuration.TableNamePrefix))
            {
                store.Configuration.UseTableNamePrefix($"{Guid.NewGuid()}_{DateTimeOffset.Now:s}");
            }

            prefix = $"##{store.Configuration.TableNamePrefix}_";

            schemaBuilderConnection = new SqlConnection(connectionString);
            schemaBuilderConnection.Open();
        }

        public override void Dispose()
        {
            schemaBuilderConnection?.Dispose();

            //foreach (var connection in connections)
            //{
            //    connections.TryRemove(connection.Key, out _);
            //    connection.Value.Dispose();
            //}

            if (numberOfManagedConnections > 0)
            {
                store.Logger.LogWarning("A ManagedConnection was not properly disposed. You may be leaking sql connections or transactions.");
            }
        }

        public override string FormatTableName(string tablename)
        {
            if (string.IsNullOrEmpty(prefix)) Initialize();

            return $"{prefix}{tablename}";
        }

        public override ManagedConnection Connect(bool schema = false)
        {
            var newGuid = Guid.NewGuid();

            if (schema)
            {
                if (Transaction.Current != null)
                {
                    schemaBuilderConnection.EnlistTransaction(Transaction.Current);
                }

                return new ManagedConnection(schemaBuilderConnection, () => { }, () => { });
            }

            Action complete = () => { };
            Action dispose = () =>
            {
                Interlocked.Decrement(ref numberOfManagedConnections);
                //connections.TryRemove(newGuid, out _);
            };

            try
            {
                Interlocked.Increment(ref numberOfManagedConnections);

                var connection = new SqlConnection(connectionString);

                complete = connection.Dispose + complete;
                dispose = connection.Dispose + dispose;

                var managedConnection = new ManagedConnection(connection, complete, dispose);

                //connections.TryAdd(newGuid, managedConnection);

                connection.InfoMessage += (obj, args) => OnMessage(args);
                connection.Open();

                if (Transaction.Current != null)
                {
                    connection.EnlistTransaction(Transaction.Current);
                }
                
                return managedConnection;
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

            var columns = schemaBuilderConnection.Query<string, string, (string tablename, string columnname)>($@"
                    select table_name, column_name
                    from tempdb.INFORMATION_SCHEMA.COLUMNS
                    where table_name like '{prefix}%'",
                ValueTuple.Create, splitOn: "column_name");

            foreach (var columnByTable in columns.GroupBy(x => x.tablename))
            {
                var tableName = columnByTable.Key.Remove(0, prefix.Length);

                schema.Add(tableName, columnByTable.Select(column => column.columnname).ToList());
            }

            return schema;
        }

    }
}