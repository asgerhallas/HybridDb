namespace HybridDb
{
    public interface IMigrationContext : IMigration
    {
        void Commit();
    }
}