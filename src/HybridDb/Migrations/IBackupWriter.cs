namespace HybridDb.Migrations
{
    public interface IBackupWriter
    {
        void Write(string name, byte[] document);
    }
}