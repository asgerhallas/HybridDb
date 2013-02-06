using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace HybridDb
{
    public interface IMigration
    {
        void InitializeDatabase();

        IMigrator CreateMigrator();

        void AddTable<TEntity>();
        void RemoveTable(string tableName);
        void RenameTable(string oldTableName, string newTableName);
        
        void UpdateProjectionFor<TEntity, TMember>(Expression<Func<TEntity, TMember>> member);
        
        void AddProjection<TEntity, TMember>(Expression<Func<TEntity, TMember>> member);
        void RemoveProjection<TEntity>(string columnName);
        void RenameProjection<TEntity>(string oldColumnName, string newColumnName);
        
        void Do<T>(string tableName, Action<T, IDictionary<string, object>> action);

        //void Execute(string sql);
    }
}