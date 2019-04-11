using System.Collections.Generic;
using HybridDb.Config;

namespace HybridDb.Migrations.Schema
{
    public interface ISchemaDiffer
    {
        IReadOnlyList<SchemaMigrationCommand> CalculateSchemaChanges(IReadOnlyDictionary<string, List<string>> schema, Configuration configuration);
    }
}