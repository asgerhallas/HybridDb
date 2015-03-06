using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HybridDb.Config;
using HybridDb.Migration.Commands;

namespace HybridDb.Migration
{
    public class Migrator
    {
        public Task Migrate(DocumentStore store)
        {
            var commands = new SchemaDiffer().CalculateSchemaChanges(store.Schema, store.Configuration);
            foreach (var command in commands)
            {
                command.Execute(store);
            }

            return Task.FromResult(1);
        }
    }

    public class SchemaDiffer
    {
        public IReadOnlyList<SchemaMigrationCommand> CalculateSchemaChanges(ISchema db, Configuration configuration)
        {
            var commands = new List<SchemaMigrationCommand>();

            var existingSchema = db.GetSchema();

            foreach (var table in configuration.DocumentDesigns.Select(x => x.Table).Distinct())
            {
                var existingTable = existingSchema.Values.SingleOrDefault(x => x.Name == table.Name);

                if (existingTable == null)
                {
                    commands.Add(new CreateTable(table));
                    continue;
                }

                foreach (var column in table.Columns)
                {
                    var existingColumn = existingTable.Columns.SingleOrDefault(x => x.Equals(column));
                    if (existingColumn == null)
                    {
                        commands.Add(new AddColumn(table.Name, column));
                    }
                }

                foreach (var column in existingTable.Columns)
                {
                    if (table.Columns.Any(x => x.Equals(column)))
                        continue;

                    commands.Add(new RemoveColumn(table, column));
                }
            }

            foreach (var table in existingSchema.Values)
            {
                if (configuration.Tables.ContainsKey(table.Name))
                    continue;

                commands.Add(new RemoveTable(table.Name));
            }

            return commands;
        }
    }
}