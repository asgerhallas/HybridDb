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

        public IReadOnlyList<SchemaMigrationCommand> FindSchemaChanges(ISchema db, Configuration configuration)
        {
            var commands = new List<SchemaMigrationCommand>();

            var existingSchema = db.GetSchema();

            foreach (var table in configuration.DocumentDesigns.Select(x => x.Table).Distinct())
            {
                var existingTable = existingSchema.Values.SingleOrDefault(x => x.Name == table.Name);

                if (existingTable == null)
                {
                    commands.Add(new CreateTable(table));
                    continue;
                }

                foreach (var column in table.Columns)
                {
                    var existingColumn = existingTable.Columns.SingleOrDefault(x => x.Name == column.Name);
                    if (existingColumn == null)
                    {
                        commands.Add(new AddColumn(table.Name, column));
                    }
                }
            }

            foreach (var table in existingSchema.Values)
            {
                if (configuration.Tables.ContainsKey(table.Name))
                    continue;

                commands.Add(new RemoveTable(table.Name));
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