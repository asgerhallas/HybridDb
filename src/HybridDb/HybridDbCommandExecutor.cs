namespace HybridDb
{
    public delegate object HybridDbCommandExecutor(DocumentTransaction tx, HybridDbCommand command);
}