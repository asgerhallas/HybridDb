using System;
using System.Collections.Generic;
using HybridDb.Schema;

namespace HybridDb
{
    public interface IMigrator : IDisposable
    {
        IMigrator MigrateTo(DocumentConfiguration documentConfiguration, bool safe = true);

        IMigrator AddTable(Table table);
        IMigrator RemoveTable(Table table);
        IMigrator RenameTable(Table oldTable, Table newTable);

        IMigrator AddColumn(Table table, Column column);
        IMigrator RemoveColumn(Table table, Column column);
        IMigrator RenameColumn(Table table, Column oldColumn, Column newColumn);

        IMigrator UpdateProjectionColumnsFromDocument(DocumentConfiguration documentConfiguration, ISerializer serializer);

        IMigrator Do(DocumentConfiguration relation, ISerializer serializer, Action<object, IDictionary<string, object>> action);
        IMigrator Do<T>(Table table, ISerializer serializer, Action<T, IDictionary<string, object>> action);

        IMigrator Commit();
    }
}