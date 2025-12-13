using System;
using System.Data.SqlClient;
using System.Linq;
using HybridDb.Config;
using HybridDb.Serialization;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.Tests.Documentation
{
    public class Doc03_ConfigurationTests : DocumentationTestBase
    {
        public Doc03_ConfigurationTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void ProductionConfiguration()
        {
            #region ProductionConfiguration
            var newStore = DocumentStore.Create(config =>
            {
                config.UseConnectionString(
                    "Server=(LocalDb)\\MSSQLLocalDB;Database=MyAppDb;Integrated Security=True;Encrypt=False;");
            });
            #endregion

            newStore.Dispose();
        }

        [Fact]
        public void ConfigurationObject()
        {
            #region ConfigurationObject
            var configuration = new Configuration();
            configuration.UseConnectionString("Server=(LocalDb)\\MSSQLLocalDB;Database=MyDb;Integrated Security=True;Encrypt=False;");

            var newStore = DocumentStore.Create(configuration);
            #endregion

            newStore.Dispose();
        }

        [Fact]
        public void DeferredInitialization()
        {
            #region DeferredInitialization
            var newStore = DocumentStore.Create(config =>
            {
                config.UseConnectionString("Server=(LocalDb)\\MSSQLLocalDB;Database=MyDb;Integrated Security=True;Encrypt=False;");
            }, initialize: false);

            // Configure from multiple places
            newStore.Configuration.Document<Product>().With(x => x.Name);

            // Manually initialize when ready
            newStore.Initialize();
            #endregion

            newStore.Dispose();
        }

        [Fact]
        public void GlobalTempTablesMode()
        {
            #region GlobalTempTablesMode
            var newStore = DocumentStore.ForTesting(
                TableMode.GlobalTempTables,
                config =>
                {
                    config.UseConnectionString(
                        "Server=(LocalDb)\\MSSQLLocalDB;Integrated Security=True;Encrypt=False;");
                });
            #endregion

            newStore.Dispose();
        }

        [Fact]
        public void RealTablesMode()
        {
            #region RealTablesMode
            var newStore = DocumentStore.ForTesting(
                TableMode.RealTables,
                config =>
                {
                    config.UseConnectionString(
                        "Server=(LocalDb)\\MSSQLLocalDB;Database=HybridDb;Integrated Security=True;Encrypt=False;");
                    
                    // Add a unique prefix to avoid conflicts
                    // If not set a default randomized prefix is used
                    config.UseTableNamePrefix($"Test_{Guid.NewGuid():N}_");
                });

            // Remember to clean up
            newStore.Dispose();
            #endregion
        }

        [Fact]
        public void LoggerConfiguration()
        {
            #region LoggerConfiguration
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Debug);
            });

            var newStore = DocumentStore.ForTesting(TableMode.GlobalTempTables, config =>
            {
                config.UseLogger(loggerFactory.CreateLogger("HybridDb"));
            });
            #endregion

            newStore.Dispose();
        }

        [Fact]
        public void DefaultSerializerConfiguration()
        {
            #region DefaultSerializerConfiguration
            var newStore = DocumentStore.ForTesting(TableMode.GlobalTempTables, config =>
            {
                // Create and configure a custom serializer
                var serializer = new DefaultSerializer();
                serializer.EnableAutomaticBackReferences();
                
                config.UseSerializer(serializer);
            });
            #endregion

            newStore.Dispose();
        }

        [Fact]
        public void TableNamePrefix()
        {
            #region TableNamePrefix
            var newStore = DocumentStore.ForTesting(TableMode.GlobalTempTables, config =>
            {
                config.UseTableNamePrefix("MyApp_");
                // Results in tables like: MyApp_Products, MyApp_Orders, etc.
            });
            #endregion

            newStore.Dispose();
        }

        [Fact]
        public void CustomKeyResolver()
        {
            #region CustomKeyResolver
            var newStore = DocumentStore.ForTesting(TableMode.GlobalTempTables, config =>
            {
                config.UseKeyResolver(entity =>
                {
                    // Custom logic to get the key from an entity
                    return entity.GetType()
                        .GetProperty("Id")
                        ?.GetValue(entity)
                        ?.ToString();
                });
            });
            #endregion

            newStore.Dispose();
        }

        [Fact]
        public void SoftDeleteConfiguration()
        {
            #region SoftDeleteConfiguration
            var newStore = DocumentStore.ForTesting(TableMode.GlobalTempTables, config =>
            {
                config.UseSoftDelete();
                // Deleted documents will have a metadata flag instead of being removed
            });
            #endregion

            newStore.Dispose();
        }

        [Fact]
        public void BackgroundMigrationsConfiguration()
        {
            #region BackgroundMigrationsConfiguration
            var newStore = DocumentStore.ForTesting(TableMode.GlobalTempTables, config =>
            {
                // Disable background migrations (migrations only run on document load)
                config.DisableBackgroundMigrations();

                // Set migration batch size
                config.UseMigrationBatchSize(1000);
            });
            #endregion

            newStore.Dispose();
        }

        [Fact]
        public void EventStoreConfiguration()
        {
            #region EventStoreConfiguration
            var newStore = DocumentStore.ForTesting(TableMode.GlobalTempTables, config =>
            {
                config.UseEventStore();
            });
            #endregion

            newStore.Dispose();
        }

        [Fact]
        public void DevelopmentEnvironment()
        {
            #region DevelopmentEnvironment
            var newStore = DocumentStore.Create(config =>
            {
                config.UseConnectionString(
                    "Server=(LocalDb)\\MSSQLLocalDB;Database=MyApp_Dev;Integrated Security=True;Encrypt=False;");
                
                var loggerFactory = LoggerFactory.Create(b => b.SetMinimumLevel(LogLevel.Debug));
                config.UseLogger(loggerFactory.CreateLogger("HybridDb"));
            }, initialize: false);
            #endregion

            newStore.Dispose();
        }

        [Fact]
        public void ConnectionPooling()
        {
            #region ConnectionPooling
            var newStore = DocumentStore.Create(config =>
            {
                config.UseConnectionString(
                    "Server=(LocalDb)\\MSSQLLocalDB;Database=MyDb;Integrated Security=True;" +
                    "Min Pool Size=5;Max Pool Size=100;" +
                    "Connection Lifetime=300;Encrypt=False;");
            }, initialize: false);
            #endregion

            newStore.Dispose();
        }
    }
}
