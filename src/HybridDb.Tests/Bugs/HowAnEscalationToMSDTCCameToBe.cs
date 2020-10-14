using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Transactions;
using Dapper;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.Tests.Bugs
{
    /// <summary>
    /// This was a bug in the test setup, but I wrote these tests to keep in mind why it happened, so I don't do it again.
    /// </summary>
    public class HowAnEscalationToMSDTCCameToBe
    {
        readonly ITestOutputHelper output;
        readonly string cnstr;
        readonly string uniqueDbName;

        public HowAnEscalationToMSDTCCameToBe(ITestOutputHelper output)
        {
            this.output = output;

            cnstr = "Data Source=.;Integrated Security=True";
            uniqueDbName = $"HybridDbTests_{Guid.NewGuid().ToString().Replace("-", "_")}";

            using (var connection = new SqlConnection(cnstr + ";Pooling=false"))
            {
                connection.Open();

                connection.Execute(string.Format(@"
                        IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = '{0}')
                        BEGIN
                            CREATE DATABASE {0}
                        END", uniqueDbName));
            }
        }

        [Fact(Skip = "For demonstration only.")]
        public void CannotDropDatabaseInMyTest()
        {
            using (var connection = new SqlConnection(cnstr + ";Initial Catalog=" + uniqueDbName))
            {
                connection.Open();

                // The connection is _not_ closed, but returned to the pool.
            }

            using (var connection = new SqlConnection(cnstr + ";Initial Catalog=Master"))
            {
                connection.Open();

                // This will fail, as there's still an open connection to the database.
                connection.Execute($"DROP DATABASE {uniqueDbName};");
            }
        }

        [Fact(Skip = "For demonstration only.")]
        public void SoIReachForMyHammer()
        {
            using (var connection = new SqlConnection(cnstr + ";Initial Catalog=" + uniqueDbName))
            {
                connection.Open();

                // The connection is not closed, but returned to the pool.
            }

            // Remove the all connections from the pool before dropping the database
            SqlConnection.ClearAllPools();

            using (var connection = new SqlConnection(cnstr + ";Initial Catalog=Master"))
            {
                connection.Open();

                // This works now. Great!
                connection.Execute($"DROP DATABASE {uniqueDbName};");
            }
        }

        [Fact(Skip = "For demonstration only.")]
        public void AndSmashConcurrencyCompletely()
        {
            using var tx = new TransactionScope(TransactionScopeOption.RequiresNew);

            using (var connection = new SqlConnection(cnstr + ";Initial Catalog=" + uniqueDbName))
            {
                connection.Open();
            }

            // Remove the all connections from the pool before dropping the database.
            // Even those in other tests on other threads... 
            // We simulate this happening in another test with a task, though you could just take my word for it.
            Task.Run(SqlConnection.ClearAllPools).Wait();

            using (var connection = new SqlConnection(cnstr + ";Initial Catalog=" + uniqueDbName))
            {
                // This connection is new (not from the pool) and using different connections in the same tx will escalate to DTC.
                connection.Open();
            }

            tx.Complete();

            // In conclusion:
            // I could have just used SqlConnection.ClearPool(connection) to only clear the connection of the current test.
            // That would have worked, but I was angry and ended up clearing no connections, but alter the database like this:
            // ALTER DATABASE {uniqueDbName} SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
            // which drops all connections to the database.
        }
    }
}