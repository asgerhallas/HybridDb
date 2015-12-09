using System;

namespace HybridDb.Migrations.Commands
{
    public class SqlMigrationCommand : SchemaMigrationCommand
    {
        readonly Action<SqlBuilder, IDatabase> builder;

        public SqlMigrationCommand(string description, Action<SqlBuilder, IDatabase> builder) : this(description, null, builder) { }

        public SqlMigrationCommand(string description, string requiresReprojectionOf, Action<SqlBuilder, IDatabase> builder)
        {
            this.builder = builder;

            Description = description;
            RequiresReprojectionOf = requiresReprojectionOf;

            Unsafe = false;
        }

        public string Description { get; }

        public sealed override void Execute(IDatabase database)
        {
            var sql = new SqlBuilder();
            builder(sql, database);
            database.RawExecute(sql.ToString());
        }

        public override string ToString()
        {
            return Description;
        }
    }
}