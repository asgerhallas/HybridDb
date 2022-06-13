using HybridDb.Migrations.Schema;

namespace HybridDb
{
    public delegate void DdlCommandExecutor(DocumentStore store, DdlCommand command);
}