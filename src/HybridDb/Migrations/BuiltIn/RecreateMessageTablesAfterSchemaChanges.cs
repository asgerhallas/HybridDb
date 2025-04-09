using System.Collections.Generic;
using System.Linq;
using HybridDb.Config;
using HybridDb.Migrations.Schema;
using HybridDb.Migrations.Schema.Commands;
using HybridDb.Queue;
using HybridDb.SqlBuilder;

namespace HybridDb.Migrations.BuiltIn
{
    public class RecreateMessageTablesAfterSchemaChanges : Migration
    {
        public RecreateMessageTablesAfterSchemaChanges(int version) : base(version) { }

        public override IEnumerable<DdlCommand> BeforeAutoMigrations(Configuration configuration)
        {
            yield return new RecreateMessageTablesAfterSchemaChangesCommand();
        }

        public override string ToString() => "Drop old message tables and recreate message tables";

        class RecreateMessageTablesAfterSchemaChangesCommand : DdlCommand
        {
            public override void Execute(DocumentStore store)
            {
                foreach (var table in store.Configuration.Tables.Values.OfType<QueueTable>())
                {
                    var tableNameEscaped = store.Database.FormatTableNameAndEscape(table.Name);
                    var tableNameOld = $"{table.Name}_old";
                    var oldTableNameEscaped = store.Database.FormatTableNameAndEscape(tableNameOld);

                    // Rename the old table and the primary key
                    store.Execute(new RenameTable(table.Name, $"{table.Name}_old"));
                    store.Database.RawExecute(Sql.From($"sp_rename [PK_{table.Name}], [PK_{table.Name}_old]"));

                    // Create the new table as it should be, with correct ordering of columns
                    store.Execute(table.GetCreateCommand());

                    var columns = GetColumns(store.Database, table.Name);
                    var columnsOld = GetColumns(store.Database, tableNameOld);
                    var columnNamesOldEscaped = string.Join(", ", columnsOld.Select(x => store.Database.Escape(x.Name)));

                    // Adding a new identity field when the old table already has one or more identity fields
                    // will result in all identity fields getting new values.
                    var identityInsertOn = columns.Count(x => x.IsIdentity == 1) == columnsOld.Count(x => x.IsIdentity == 1);

                    // Move the data from the old to the new table
                    store.Database.RawExecute(Sql.From(
                        $"""
                             {(identityInsertOn ? $"set identity_insert {tableNameEscaped} on;" : "")}
                             insert into {tableNameEscaped} ({columnNamesOldEscaped})
                             select {columnNamesOldEscaped} from {oldTableNameEscaped};
                             {(identityInsertOn ? $"set identity_insert {tableNameEscaped} off;" : "")}
                         """));

                    store.Database.RawExecute(Sql.From($"drop table {oldTableNameEscaped};"));
                }
            }

            static List<(string Name, int IsIdentity)> GetColumns(IDatabase database, string tableName) =>
                database.RawQuery<(string Name, int IsIdentity)>(Sql.From(
                    $"""
                        select column_name, columnproperty(object_id('messages'), COLUMN_NAME, 'IsIdentity')
                        from information_schema.columns
                        where table_name = {tableName}
                    """)).ToList();

            public override string ToString() => "Drop old message tables and recreate message tables";
        }
    }
}