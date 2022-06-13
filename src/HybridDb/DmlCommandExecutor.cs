namespace HybridDb
{
    public delegate object DmlCommandExecutor(DocumentTransaction tx, DmlCommand command);
}