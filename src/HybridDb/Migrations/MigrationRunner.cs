using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using HybridDb.Config;
using HybridDb.Logging;

namespace HybridDb.Migrations
{
    public class MigrationRunner
    {
        readonly ILogger logger;
        readonly IReadOnlyList<Migration> migrations;
        readonly ISchemaDiffer differ;

        public MigrationRunner(ILogger logger, ISchemaDiffer differ, params Migration[] migrations) : this(logger, differ, migrations.ToList()) { }

        public MigrationRunner(ILogger logger, ISchemaDiffer differ, IReadOnlyList<Migration> migrations)
        {
            this.logger = logger;
            this.migrations = migrations;
            this.differ = differ;
        }

        public Task Migrate(IDocumentStore store, Configuration configuration)
        {
            var metadata = new Table("HybridDb", new Column("SchemaVersion", typeof(int)));
            configuration.Tables.TryAdd(metadata.Name, metadata);

            var requiresReprojection = new List<string>();

            using (var tx = new TransactionScope(TransactionScopeOption.Required, new TransactionOptions {IsolationLevel = IsolationLevel.Serializable}))
            {
                // todo: abstract out so version is store independent
                var database = ((DocumentStore)store).Database;
                var schema = database.QuerySchema().Values.ToList(); // demeter go home!

                var currentSchemaVersion = schema.Any(x => x.Name == "HybridDb")
                    ? database.RawQuery<int>(string.Format("select top 1 SchemaVersion from {0}",
                        database.FormatTableName("HybridDb"))).SingleOrDefault()
                    : 0;

                foreach (var migration in migrations.Where(x => x.Version > currentSchemaVersion).ToList())
                {
                    var migrationCommands = migration.MigrateSchema();
                    foreach (var command in migrationCommands)
                    {
                        if (command.Unsafe)
                        {
                            logger.Warn("Unsafe migration command '{0}' was skipped.", command);
                            continue;
                        }

                        command.Execute(database);
                    }

                    currentSchemaVersion++;
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

                    if (command.RequiresReprojectionOf != null)
                    {
                        requiresReprojection.Add(command.RequiresReprojectionOf);
                    }
                }

                foreach (var tablename in requiresReprojection)
                {
                    // TODO: Only set RequireReprojection on command if it is documenttable - can it be done?
                    var design = configuration.DocumentDesigns.FirstOrDefault(x => x.Table.Name == tablename);
                    if (design == null) continue;

                    database.RawExecute(string.Format("update {0} set AwaitsReprojection=@AwaitsReprojection", database.FormatTableNameAndEscape(tablename)), new { AwaitsReprojection = true });
                }

                database.RawExecute(string.Format(@"
if not exists (select * from {0}) 
    insert into {0} (SchemaVersion) values (@version); 
else
    update {0} set SchemaVersion=@version",
                database.FormatTableName("HybridDb")),
                new { version = currentSchemaVersion });

                tx.Complete();
            }

            var requiredDocumentVersion = store.CurrentVersion;

            return new Task(() =>
            {
                foreach (var table in configuration.Tables.Values.OfType<DocumentTable>())
                {
                    var baseDesign = configuration.DocumentDesigns.First(x => x.Table.Name == table.Name);

                    QueryStats stats;
                    foreach (var row in store.Query(table, out stats,
                        where: "AwaitsReprojection = @AwaitsReprojection or Version < @version",
                        parameters: new { AwaitsReprojection = true, version = requiredDocumentVersion }))
                    {
                        var discriminator = ((string)row[table.DiscriminatorColumn]).Trim();

                        DocumentDesign concreteDesign;
                        if (!baseDesign.DecendentsAndSelf.TryGetValue(discriminator, out concreteDesign))
                        {
                            throw new InvalidOperationException(string.Format("Discriminator '{0}' was not found in configuration.", discriminator));
                        }

                        var entity = DocumentSession.DeserializeAndMigrate(store, concreteDesign, row);
                        var projections = concreteDesign.Projections.ToDictionary(x => x.Key, x => x.Value.Projector(entity));
                        projections.Add(table.VersionColumn, store.CurrentVersion);

                        store.Update(table, (Guid) row[table.IdColumn], (Guid) row[table.EtagColumn], projections);
                    }
                }

            }, TaskCreationOptions.LongRunning);
        }
    }
}