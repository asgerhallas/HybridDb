using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using HybridDb.Config;
using HybridDb.Logging;

namespace HybridDb.Migration
{
    public class MigrationRunner
    {
        readonly ILogger logger;
        readonly IMigrationProvider provider;
        readonly ISchemaDiffer differ;

        public MigrationRunner(ILogger logger, IMigrationProvider provider, ISchemaDiffer differ)
        {
            this.logger = logger;
            this.provider = provider;
            this.differ = differ;
        }

        public Task Migrate(Database database, Configuration configuration)
        {
            var metadata = new Table("HybridDb", new Column("SchemaVersion", typeof(int)));
            configuration.Tables.TryAdd(metadata.Name, metadata);

            using (var tx = new TransactionScope(TransactionScopeOption.Required, new TransactionOptions {IsolationLevel = IsolationLevel.Serializable}))
            {
                // abstract out so version is store independent
                var schema = database.QuerySchema().Values.ToList(); // demeter go home!

                var currentVersion = schema.Any(x => x.Name == "HybridDb")
                    ? database.RawQuery<int>(string.Format("select top 1 SchemaVersion from {0}",
                        database.FormatTableName("HybridDb"))).SingleOrDefault()
                    : 0;

                var enumerable = provider.GetMigrations().ToList();
                var migrations = enumerable.Where(x => x.Version > currentVersion).ToList();

                foreach (var migration in migrations)
                {
                    var migrationCommands = migration.Migrate();
                    foreach (var command in migrationCommands.OfType<SchemaMigrationCommand>())
                    {
                        if (command.Unsafe)
                        {
                            logger.Warn("Unsafe migration command '{0}' was skipped.", command);
                            continue;
                        }

                        command.Execute(database);
                    }

                    currentVersion++;
                }

                var commands = differ.CalculateSchemaChanges(schema, configuration);
                foreach (var command in commands)
                {
                    if (command.Unsafe)
                    {
                        logger.Warn("Unsafe migration command '{0}' was skipped.", command);
                        continue;
                    }

                    command.Execute(database);
                }

                database.RawExecute(string.Format(@"
if not exists (select * from {0}) 
    insert into {0} (SchemaVersion) values (@version); 
else
    update {0} set SchemaVersion=@version",
                database.FormatTableName("HybridDb")),
                new { version = currentVersion });

                tx.Complete();
            }
            
            return Task.FromResult(1);
        }
    }
}