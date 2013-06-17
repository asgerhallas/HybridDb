using System;
using HybridDb.Schema;

namespace HybridDb.Migration
{
    public interface ISchemaMigrator : IDisposable
    {
        ISchemaMigrator MigrateTo(Table table, bool safe = true);

        ISchemaMigrator AddTableAndColumnsAndAssociatedTables(Table table);
        ISchemaMigrator RemoveTableAndAssociatedTables(Table table);
        ISchemaMigrator RenameTableAndAssociatedTables(Table oldTable, string newTablename);
        ISchemaMigrator AddColumn(string tablename, Column column);

        ISchemaMigrator AddTable(string tablename, params string[] columns);
        ISchemaMigrator RemoveTable(string tablename);
        ISchemaMigrator RenameTable(string oldTablename, string newTablename);

        ISchemaMigrator AddColumn(string tablename, string columnname, string columntype, object parameters = null);
        ISchemaMigrator RemoveColumn(string tablename, string columnname);
        ISchemaMigrator RenameColumn(string tablename, string oldColumnname, string newColumnname);
        
        ISchemaMigrator Execute(string sql, object param = null);
        ISchemaMigrator Commit();
    }
}