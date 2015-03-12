using System.Collections.Generic;
using System.Linq;
using HybridDb.Config;
using HybridDb.Migrations.Commands;

namespace HybridDb.Migrations
{
    public class SchemaDiffer : ISchemaDiffer
    {
        public IReadOnlyList<SchemaMigrationCommand> CalculateSchemaChanges(IReadOnlyList<Table> schema, Configuration configuration)
        {
            var commands = new List<SchemaMigrationCommand>();

            foreach (var table in configuration.Tables.Values)
            {
                var existingTable = schema.SingleOrDefault(x => x.Name == table.Name);

                if (existingTable == null)
                {
                    commands.Add(new CreateTable(table));
                    continue;
                }

                foreach (var column in table.Columns)
                {
                    var existingColumn = existingTable.Columns.SingleOrDefault(x => Equals(x.Name, column.Name));
                    if (existingColumn == null)
                    {
                        commands.Add(new AddColumn(table.Name, column));
                    }
                }

                foreach (var column in existingTable.Columns)
                {
                    if (table.Columns.Any(x => Equals(x.Name, column.Name)))
                        continue;

                    commands.Add(new RemoveColumn(table, column));
                }
            }

            foreach (var table in schema)
            {
                if (configuration.Tables.ContainsKey(table.Name))
                    continue;

                commands.Add(new RemoveTable(table.Name));
            }

            return commands;
        }
    }
}