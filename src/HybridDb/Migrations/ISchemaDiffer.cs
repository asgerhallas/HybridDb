using System.Collections.Generic;
using HybridDb.Config;

namespace HybridDb.Migrations
{
    public interface ISchemaDiffer
    {
        IReadOnlyList<SchemaMigrationCommand> CalculateSchemaChanges(IReadOnlyList<Table> schema, Configuration configuration);
    }
}