using System.Collections.Generic;
using System.Linq;
using System.Transactions;
using HybridDb.Config;
using HybridDb.Logging;
using HybridDb.Migrations.Commands;

namespace HybridDb.Migrations
{
    public class SchemaMigrationRunner
    {
        readonly ILogger logger;
        readonly IReadOnlyList<Migration> migrations;
        readonly ISchemaDiffer differ;

        public SchemaMigrationRunner(ILogger logger, ISchemaDiffer differ, params Migration[] migrations) : this(logger, differ, migrations.ToList()) { }

        public SchemaMigrationRunner(ILogger logger, ISchemaDiffer differ, IReadOnlyList<Migration> migrations)
        {
            this.logger = logger;
            this.migrations = migrations;
            this.differ = differ;
        }

        public void Run(IDocumentStore store, Configuration configuration)
        {
            var requiresReprojection = new List<string>();

            // todo: abstract out so version is store independent
            var database = ((DocumentStore)store).Database;

            int currentSchemaVersion = 0;
            using (var tx = new TransactionScope(TransactionScopeOption.RequiresNew, new TransactionOptions { IsolationLevel = IsolationLevel.Serializable }))
            {
                var metadata = new Table("HybridDb", new Column("SchemaVersion", typeof(int)));
                configuration.Tables.TryAdd(metadata.Name, metadata);

                new CreateTable(metadata).Execute(database);

                currentSchemaVersion = database.RawQuery<int>(
                    string.Format("select top 1 SchemaVersion from {0} with (tablockx, holdlock)", 
                    database.FormatTableName("HybridDb"))).SingleOrDefault();

                tx.Complete();
            }

            using (var tx = new TransactionScope(TransactionScopeOption.RequiresNew, new TransactionOptions { IsolationLevel = IsolationLevel.Serializable }))
            {
                var schema = database.QuerySchema().Values.ToList(); // demeter go home!
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

                    database.RawExecute(string.Format("update {0} set AwaitsReprojection=@AwaitsReprojection",
                        database.FormatTableNameAndEscape(tablename)), new { AwaitsReprojection = true });
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
        }
    }
}