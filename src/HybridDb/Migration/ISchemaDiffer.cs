using System.Collections.Generic;
using HybridDb.Config;

namespace HybridDb.Migration
{
    public interface ISchemaDiffer
    {
        IReadOnlyList<SchemaMigrationCommand> CalculateSchemaChanges(IReadOnlyList<Table> schema, Configuration configuration);
    }
}