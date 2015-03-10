using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
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

        public Task Migrate(DocumentStore store)
        {
            using (var tx = new TransactionScope(TransactionScopeOption.Required, new TransactionOptions {IsolationLevel = IsolationLevel.Serializable}))
            {
                // abstract out so version is store independent
                var db = store.Database;
                var schema = db.QuerySchema().Values.ToList(); // demeter go home!
                var currentVersion = schema.Any(x => x.Name == "HybridDb")
                    ? db.RawQuery<int>(string.Format("select top 1 SchemaVersion from {0}",
                        db.FormatTableName("HybridDb"))).Single()
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

                        command.Execute(db);
                    }

                    currentVersion++;
                }

                var commands = differ.CalculateSchemaChanges(schema, store.Configuration);
                foreach (var command in commands)
                {
                    if (command.Unsafe)
                    {
                        logger.Warn("Unsafe migration command '{0}' was skipped.", command);
                        continue;
                    }

                    command.Execute(db);
                }

                db.RawExecute(string.Format(@"
if not exists (select * from {0}) 
    insert into {0} (SchemaVersion) values (@version); 
else
    update {0} set SchemaVersion=@version",
                    db.FormatTableName("HybridDb")),
                    new {version = currentVersion});

                tx.Complete();
            }
            
            return Task.FromResult(1);
        }
    }
}