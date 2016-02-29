using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Transactions;
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
                connection.ChangeDatabase("tempdb");

                complete = connection.Dispose + complete;
                dispose = connection.Dispose + dispose;

                connection.EnlistTransaction(Transaction.Current);

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

            var prefix = $"HybridDb_{store.Configuration.TableNamePrefix}_";

            var tables = RawQuery<string>($"select * from tempdb.sys.objects where object_id('tempdb.dbo.' + name, 'U') is not null AND name LIKE '{prefix}%'");

            foreach (var tableName in tables)
            {
                var columns = RawQuery<QueryColumn>(
                    $"select * from tempdb.sys.columns where Object_ID = Object_ID(N'tempdb..{tableName}')");

                var formattedTableName = tableName.Remove(0, prefix.Length);


                schema.Add(
                    formattedTableName,
                    new Table(formattedTableName, columns.Select(column => Map(tableName, column, isTempTable: true))));
            }

            return schema;
        }

        public override void Dispose()
        {
            if (numberOfManagedConnections > 0)
                this.store.Logger.Warning("A ManagedConnection was not properly disposed. You may be leaking sql connections or transactions.");
        }
    }
}