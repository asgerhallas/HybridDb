using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Transactions;
using HybridDb.Config;
using Serilog;

namespace HybridDb
{
    public class SqlServerUsingRealTables : SqlServer
    {
        int numberOfManagedConnections;

        public SqlServerUsingRealTables(DocumentStore store, string connectionString) : base(store, connectionString)
        {
        }

        public override string FormatTableName(string tablename)
        {
            return store.Configuration.TableNamePrefix + tablename;
        }

        public override ManagedConnection Connect()
        {
            Action complete = () => { };
            Action dispose = () => { numberOfManagedConnections--; };

            try
            {
                if (Transaction.Current == null)
                {
                    var tx = new TransactionScope(
                        TransactionScopeOption.RequiresNew,
                        new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted });

                    complete += tx.Complete;
                    dispose += tx.Dispose;
                }

                var connection = new SqlConnection(connectionString);
                connection.InfoMessage += (obj, args) => OnMessage(args);
                connection.Open();

                complete = connection.Dispose + complete;
                dispose = connection.Dispose + dispose;

                numberOfManagedConnections++;

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

            var tables = RawQuery<string>($"select table_name from information_schema.tables where table_type='BASE TABLE' and table_name LIKE '{store.Configuration.TableNamePrefix}%'")
                .ToList();
            
            foreach (var tableName in tables)
            {
                var columns = RawQuery<QueryColumn>($"select * from sys.columns where Object_ID = Object_ID(N'{tableName}')");

                var formattedTableName = tableName.Remove(0, store.Configuration.TableNamePrefix.Length);

                schema.Add(formattedTableName, new Table(formattedTableName, columns.Select(column => Map(tableName, column, isTempTable: false))));
            }

            return schema;
        }
        
        public override void Dispose()
        {
            if (numberOfManagedConnections > 0)
                store.Logger.Warning("A ManagedConnection was not properly disposed. You may be leaking sql connections or transactions.");
        }
    }
}