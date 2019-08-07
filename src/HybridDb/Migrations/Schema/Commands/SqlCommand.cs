using System;

namespace HybridDb.Migrations.Schema.Commands
{
    public class SqlCommand : DdlCommand
    {
        public SqlCommand(string description, Action<SqlBuilder, IDatabase> builder) : this(description, null, builder) { }

        public SqlCommand(string description, string requiresReprojectionOf, Action<SqlBuilder, IDatabase> builder)
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
}