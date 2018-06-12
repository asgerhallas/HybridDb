using System.Collections.Generic;
using System.Linq;
using HybridDb.Config;
using HybridDb.Migrations.Commands;

namespace HybridDb.Migrations
{
    public class SchemaDiffer : ISchemaDiffer
    {
        public IReadOnlyList<SchemaMigrationCommand> CalculateSchemaChanges(IReadOnlyDictionary<string, List<string>> schema, Configuration configuration)
        {
            var commands = new List<SchemaMigrationCommand>();

            foreach (var table in configuration.Tables.Values)
            {
                if (!schema.TryGetValue(table.Name, out var existingTable))
                {
                    commands.Add(new CreateTable(table));
                    continue;
                }

                foreach (var column in table.Columns)
                {
                    var existingColumn = existingTable.SingleOrDefault(x => Equals(x, column.Name));
                    if (existingColumn == null)
                    {
                        commands.Add(new AddColumn(table.Name, column));
                    }
                }

                foreach (var column in existingTable)
                {
                    if (table.Columns.Any(x => Equals(x.Name, column)))
                        continue;

                    commands.Add(new RemoveColumn(table, column));
                }
            }

            foreach (var table in schema.Keys)
            {
                if (configuration.Tables.ContainsKey(table))
                    continue;

                commands.Add(new RemoveTable(table));
            }

            return commands;
        }
    }
}