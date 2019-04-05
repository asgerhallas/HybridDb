using System;

namespace HybridDb.Migrations.Commands
{
    public class SqlMigrationCommand : SchemaMigrationCommand
    {
        public SqlMigrationCommand(string description, Action<SqlBuilder, IDatabase> builder) : this(description, null, builder) { }

        public SqlMigrationCommand(string description, string requiresReprojectionOf, Action<SqlBuilder, IDatabase> builder)
        {
            Builder = builder;

            Description = description;
            RequiresReprojectionOf = requiresReprojectionOf;

            Unsafe = false;
        }

        public Action<SqlBuilder, IDatabase> Builder { get; }
        public string Description { get; }

        public override string ToString() => Description;
    }

    public class SqlMigrationCommandExecutor : DdlCommandExecutor<DocumentStore, SqlMigrationCommand>
    {
        public override void Execute(DocumentStore store, SqlMigrationCommand command)
        {
            var sql = new SqlBuilder();
            command.Builder(sql, store.Database);
            store.Database.RawExecute(sql.ToString());
        }
    }

}