using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HybridDb.Migration;
using HybridDb.Migration.Commands;

namespace HybridDb
{
    public class DocumentStoreMigrator
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

        public IReadOnlyList<SchemaMigrationCommand> FindSchemaChanges(DocumentStore store)
        {
            var commands = new List<SchemaMigrationCommand>();

            foreach (var design in store.Configuration.DocumentDesigns)
            {
                if (commands.OfType<CreateTable>().Any(x => x.Table == design.Table) ||
                    store.TableExists(design.Table.Name))
                {
                    continue;
                }

                commands.Add(new CreateTable(design.Table));
            }

            var tables = store.RawQuery<string>("select table_name from information_schema.tables").ToList();
            foreach (var tablename in tables)
            {
                if (store.Configuration.Tables.ContainsKey(tablename))
                    continue;

                commands.Add(new RemoveTable(tablename));
            }

            return commands;
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