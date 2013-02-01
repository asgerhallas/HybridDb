using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace HybridDb
{
    public interface IMigrator : IDisposable
    {
        void Commit();

        IMigrator AddTable<TEntity>();
        IMigrator RemoveTable(string tableName);
        IMigrator RenameTable(string oldTableName, string newTableName);

        IMigrator UpdateProjectionFor<TEntity, TMember>(Expression<Func<TEntity, TMember>> member);

        IMigrator AddProjection<TEntity, TMember>(Expression<Func<TEntity, TMember>> member);
        IMigrator RemoveProjection<TEntity>(string columnName);
        IMigrator RenameProjection<TEntity>(string oldColumnName, string newColumnName);

        IMigrator Do<T>(string tableName, Action<T, IDictionary<string, object>> action);
    }
}