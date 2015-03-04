using System.Threading.Tasks;

namespace HybridDb
{
    public class Migrator
    {
        public Task Migrate(IDocumentStore store)
        {
            store.Migrate(migrator =>
            {
                foreach (var table in store.Configuration.Tables.Values)
                {
                    migrator.MigrateTo(table, true);
                }
            });
            return Task.FromResult(1);
        }
    }

    //public interface IMigration
    //{
    //    //void InitializeDatabase();

    //    IMigrator CreateMigrator();

    //    void AddTable<TEntity>();
    //    void RemoveTable(string tableName);
    //    void RenameTable(string oldTableName, string newTableName);
        
    //    void UpdateProjectionFor<TEntity, TMember>(Expression<Func<TEntity, TMember>> member);
        
    //    void AddProjection<TEntity, TMember>(Expression<Func<TEntity, TMember>> member);
    //    void RemoveProjection<TEntity>(string columnName);
    //    void RenameColumn<TEntity>(string oldColumnName, string newColumnName);
        
    //    void Do<T>(string tableName, Action<T, IDictionary<string, object>> action);

    //    //void Execute(string sql);
    //}
}