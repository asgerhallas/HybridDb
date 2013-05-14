using System;
using System.Collections.Generic;
using HybridDb.Schema;

namespace HybridDb
{
    public interface IMigrator : IDisposable
    {
        IMigrator MigrateTo(Table table, bool safe = true);

        IMigrator AddTable(Table table);
        IMigrator RemoveTable(Table table);
        IMigrator RenameTable(Table oldTable, Table newTable);

        IMigrator AddColumn(Table table, Column column);
        IMigrator RemoveColumn(Table table, Column column);
        IMigrator RenameProjection(Table table, Column oldColumn, Column newColumn);

        IMigrator UpdateProjectionColumnsFromDocument(Table table, ISerializer serializer, Type deserializeToType);

        IMigrator Do(Table table, ISerializer serializer, Type deserializeTo, Action<object, IDictionary<string, object>> action);
        IMigrator Do<T>(Table table, ISerializer serializer, Action<T, IDictionary<string, object>> action);

        IMigrator Commit();
    }
}