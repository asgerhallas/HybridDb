namespace HybridDb.Migrations.Documents
{
    public interface IBackupWriter
    {
        void Write(string name, byte[] document);
    }
}