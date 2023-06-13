using System;

namespace HybridDb.Migrations.Schema.Commands
{
    public class SqlCommand : DdlCommand
    {
        public SqlCommand(string description, Action<SqlBuilder, IDocumentStore> builder, int? commandTimeout = null) : this(description, null, builder, commandTimeout) { }

        public SqlCommand(string description, string requiresReprojectionOf, Action<SqlBuilder, IDocumentStore> builder, int? commandTimeout = null)
        {
            Safe = true;

            Builder2 = builder;
            CommandTimeout = commandTimeout;

            Description = description;
            RequiresReprojectionOf = requiresReprojectionOf;
        }

        public Action<SqlBuilder, IDocumentStore> Builder2 { get; }
        public int? CommandTimeout { get; }
        public string Description { get; }

        public override string ToString() => Description;

        public override void Execute(DocumentStore store)
        {
            var sql = new SqlBuilder();
            Builder2(sql, store);
            store.Database.RawExecute(sql.ToString(), sql.Parameters, commandTimeout: CommandTimeout);
        }
    }
}