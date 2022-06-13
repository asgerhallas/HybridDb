using HybridDb.Migrations.Schema;

namespace HybridDb
{
    delegate object DmlCommandExecutor(DocumentTransaction tx, DmlCommand command);
    delegate void DdlCommandExecutor(DocumentStore store, DdlCommand command);
}