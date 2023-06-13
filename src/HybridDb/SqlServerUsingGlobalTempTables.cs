using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Transactions;
using Dapper;
using HybridDb.Config;
using Indentional;
using Microsoft.Extensions.Logging;

namespace HybridDb
{
    public class SqlServerUsingGlobalTempTables : SqlServer
    {
        SqlConnection schemaBuilderConnection;

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

            if (numberOfManagedConnections > 0)
            {
                store.Logger.LogWarning("A ManagedConnection was not properly disposed. You may be leaking sql connections or transactions.");

            }
        }

        public override string FormatTableName(string tableName)
        {
            if (string.IsNullOrEmpty(prefix)) Initialize();
            return $"{prefix}{tableName}";
        }

        public override ManagedConnection Connect(bool schema = false, TimeSpan? connectionTimeout = null)
        {
            var timeout = connectionTimeout ?? TimeSpan.FromSeconds(15);

            if (schema)
            {
                if (Transaction.Current != null)
                {
                    schemaBuilderConnection.EnlistTransaction(Transaction.Current);
                }

                return new ManagedConnection(schemaBuilderConnection, () => { }, () => { });
            }

            Action complete = () => { };
            Action dispose = () => { Interlocked.Decrement(ref numberOfManagedConnections); };

            try
            {
                Interlocked.Increment(ref numberOfManagedConnections);

                var connection = new SqlConnection(connectionString + $";Connection Timeout={timeout.TotalSeconds}");

                complete = connection.Dispose + complete;
                dispose = connection.Dispose + dispose;

                connection.InfoMessage += (obj, args) => OnMessage(args);
                connection.Open();

                if (Transaction.Current != null)
                {
                    connection.EnlistTransaction(Transaction.Current);
                }

                return new ManagedConnection(connection, complete, dispose);
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