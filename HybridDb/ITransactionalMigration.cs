using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace HybridDb
{
    public interface ITransactionalMigration : IDisposable
    {
        void Commit();

        ITransactionalMigration AddTable<TEntity>();
        ITransactionalMigration RemoveTable(string tableName);
        ITransactionalMigration RenameTable(string oldTableName, string newTableName);

        ITransactionalMigration UpdateProjectionFor<TEntity, TMember>(Expression<Func<TEntity, TMember>> member);

        ITransactionalMigration AddProjection<TEntity, TMember>(Expression<Func<TEntity, TMember>> member);
        ITransactionalMigration RemoveProjection<TEntity>(string columnName);
        ITransactionalMigration RenameProjection<TEntity>(string oldColumnName, string newColumnName);

        ITransactionalMigration Do<T>(string tableName, Action<T, IDictionary<string, object>> action);
    }
}