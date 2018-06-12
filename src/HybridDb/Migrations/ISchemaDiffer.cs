using System.Collections.Generic;
using HybridDb.Config;

namespace HybridDb.Migrations
{
    public interface ISchemaDiffer
    {
        IReadOnlyList<SchemaMigrationCommand> CalculateSchemaChanges(IReadOnlyDictionary<string, List<string>> schema, Configuration configuration);
    }
}