using System;
using System.Collections.Generic;
using System.Data;
using HybridDb.Schema;

namespace HybridDb
{
    public interface IMigrator : IDisposable
    {
        IMigrator MigrateTo(DocumentConfiguration documentConfiguration, bool safe = true);

        IMigrator AddTableAndColumns(Table table);
        IMigrator RemoveTableAndAssociatedTables(Table table);
        IMigrator RenameTableAndAssociatedTables(Table oldTable, string newTablename);
        IMigrator AddColumn(string tablename, Column column);

        IMigrator UpdateProjectionColumnsFromDocument(DocumentConfiguration documentConfiguration, ISerializer serializer);

        IMigrator Do(DocumentConfiguration relation, ISerializer serializer, Action<object, IDictionary<string, object>> action);
        IMigrator Do<T>(Table table, ISerializer serializer, Action<T, IDictionary<string, object>> action);

        IMigrator AddTable(string tablename, params string[] columns);
        IMigrator RemoveTable(string tablename);
        IMigrator RenameTable(string oldTablename, string newTablename);

        IMigrator AddColumn(string tablename, string columnname, string columntype);
        IMigrator RemoveColumn(string tablename, string columnname);
        IMigrator RenameColumn(string tablename, string oldColumnname, string newColumnname);
        
        IMigrator Execute(string sql, object param = null);
        IMigrator Commit();
    }
}