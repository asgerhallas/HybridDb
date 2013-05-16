using System;
using System.Collections.Generic;
using System.Data;
using HybridDb.Schema;

namespace HybridDb
{
    public interface IMigrator : IDisposable
    {
        IMigrator MigrateTo(DocumentConfiguration documentConfiguration, bool safe = true);

        IMigrator AddTable(string tablename);
        IMigrator AddTableAndColumns(Table table);

        IMigrator RemoveTable(string tablename);
        IMigrator RemoveTableAndAssociatedTables(Table table);
        
        IMigrator RenameTable(string oldTablename, string newTablename);
        IMigrator RenameTableAndAssociatedTables(Table oldTable, string newTablename);

        IMigrator AddColumn(string tablename, string columnname, DbType type, int length);
        IMigrator AddColumn(string tablename, Column column);
        IMigrator RemoveColumn(string tablename, string columnname);
        IMigrator RenameColumn(string tablename, string oldColumnname, string newColumnname);

        IMigrator UpdateProjectionColumnsFromDocument(DocumentConfiguration documentConfiguration, ISerializer serializer);

        IMigrator Do(DocumentConfiguration relation, ISerializer serializer, Action<object, IDictionary<string, object>> action);
        IMigrator Do<T>(Table table, ISerializer serializer, Action<T, IDictionary<string, object>> action);

        IMigrator Commit();
    }
}