namespace HybridDb.Migrations.Commands
{
    public abstract class DdlCommandExecutor<TStore, TCommand>
        where TStore : IDocumentStore
        where TCommand : SchemaMigrationCommand
    {
        public abstract void Execute(TStore store, TCommand command);
    }
}