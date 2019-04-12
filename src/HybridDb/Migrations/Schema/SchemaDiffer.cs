using System.Collections.Generic;
using System.Linq;
using HybridDb.Config;
using HybridDb.Migrations.Schema.Commands;

namespace HybridDb.Migrations.Schema
{
    public class SchemaDiffer : ISchemaDiffer
    {
        public IReadOnlyList<DdlCommand> CalculateSchemaChanges(IReadOnlyDictionary<string, List<string>> schema, Configuration configuration)
        {
            var commands = new List<DdlCommand>();

            foreach (var table in configuration.Tables.Values)
            {
                if (!schema.TryGetValue(table.Name, out var existingTable))
                {
                    commands.Add(table.GetCreateCommand());
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