using System;
using HybridDb.SqlBuilder;

namespace HybridDb.Migrations.Schema.Commands
{
    public class SqlCommand : DdlCommand
    {
        public SqlCommand(string description, Action<Sql, IDatabase> builder, int? commandTimeout = null) : this(description, null, builder, commandTimeout) { }

        public SqlCommand(string description, string requiresReprojectionOf, Action<Sql, IDatabase> builder, int? commandTimeout = null)
        {
            Safe = true;

            Builder = builder;
            CommandTimeout = commandTimeout;

            Description = description;
            RequiresReprojectionOf = requiresReprojectionOf;
        }

        public Action<Sql, IDatabase> Builder { get; }
        public int? CommandTimeout { get; }
        public string Description { get; }

        public override string ToString() => Description;

        public override void Execute(DocumentStore store)
        {
            var sql = Sql.Empty;
            Builder(sql, store.Database);
            store.Database.RawExecute(sql, commandTimeout: CommandTimeout);
        }
    }
}