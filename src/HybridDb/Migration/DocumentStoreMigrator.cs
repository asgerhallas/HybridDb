using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HybridDb.Config;
using HybridDb.Migration.Commands;

namespace HybridDb.Migration
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

        public IReadOnlyList<SchemaMigrationCommand> FindSchemaChanges(ISchema schema, Configuration configuration)
        {
            var commands = new List<SchemaMigrationCommand>();

            foreach (var design in configuration.DocumentDesigns.Select(x => x.Table).Distinct())
            {
                if (!schema.TableExists(design.Name))
                {
                    commands.Add(new CreateTable(design));
                    continue;
                }

                foreach (var column in design.Columns)
                {
                    var existingColumn = schema.GetColumn(design.Name, column.Name);
                    if (existingColumn == null)
                    {
                        commands.Add(new AddColumn(design.Name, column));
                    }
                }
            }

            var tables = schema.GetTables();
            foreach (var tablename in tables)
            {
                if (configuration.Tables.ContainsKey(tablename))
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