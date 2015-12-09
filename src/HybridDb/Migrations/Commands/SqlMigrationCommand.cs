using System;

namespace HybridDb.Migrations.Commands
{
    public class SqlMigrationCommand : SchemaMigrationCommand
    {
        readonly Action<SqlMigrationBuilder> builder;

        public SqlMigrationCommand(string description, Action<SqlMigrationBuilder> builder)
        {
            this.builder = builder;
            Description = description;
        }

        public string Description { get; }

        public sealed override void Execute(IDatabase database)
        {
            var sql = new SqlMigrationBuilder(this, database);
            builder(sql);
            database.RawExecute(sql.ToString());
        }

        public override string ToString()
        {
            return Description;
        }

        public class SqlMigrationBuilder : SqlBuilder
        {
            readonly SqlMigrationCommand command;

            public SqlMigrationBuilder(SqlMigrationCommand command, IDatabase database)
            {
                this.command = command;
                Database = database;
            }

            public IDatabase Database { get;  }

            public SqlMigrationBuilder MarkAsUnsafe()
            {
                command.Unsafe = true;
                return this;
            }

            public SqlMigrationBuilder RequiresReprojectionOf(string tablename)
            {
                command.RequiresReprojectionOf = tablename;
                return this;
            }
        }
    }
}