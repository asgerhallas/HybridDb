using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using HybridDb.Schema;

namespace HybridDb
{
    public interface IMigration
    {
        void InitializeDatabase();

        IMigrationContext AddTable<TEntity>();
        IMigrationContext RemoveTable(string tableName);
        IMigrationContext RenameTable(string oldTableName, string newTableName);

        IMigrationContext UpdateProjectionFor<TEntity, TMember>(Expression<Func<TEntity, TMember>> member);
        
        IMigrationContext AddProjection<TEntity, TMember>(Expression<Func<TEntity, TMember>> member);
        IMigrationContext RemoveProjection<TEntity>(string columnName);
        IMigrationContext RenameProjection<TEntity>(string oldColumnName, string newColumnName);

        IMigrationContext Do<T>(string tableName, Action<IDictionary<string, object>> action);
    }
}