using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Transactions;
using HybridDb.Config;
using Serilog;

namespace HybridDb
{
    public class SqlServerUsingTempTables : SqlServer
    {
        int numberOfManagedConnections;
        SqlConnection ambientConnectionForTesting;

        public SqlServerUsingTempTables(DocumentStore store, string connectionString) : base(store, connectionString) {}

        public override string FormatTableName(string tablename)
        {
            return "#" + tablename;
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

                numberOfManagedConnections++;

                return new ManagedConnection(ambientConnectionForTesting, complete, dispose);
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

            var tempTables = RawQuery<string>("select * from tempdb.sys.objects where object_id('tempdb.dbo.' + name, 'U') is not null and name like '#%' and name not like '##%'");
            foreach (var tableName in tempTables)
            {
                var formattedTableName = tableName.Remove(tableName.Length - 12, 12).TrimEnd('_');

                var columns = RawQuery<QueryColumn>(
                    String.Format("select * from tempdb.sys.columns where Object_ID = Object_ID(N'tempdb..{0}')", formattedTableName));

                formattedTableName = formattedTableName.TrimStart('#');
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

            if (ambientConnectionForTesting != null)
                ambientConnectionForTesting.Dispose();
        }
    }
}