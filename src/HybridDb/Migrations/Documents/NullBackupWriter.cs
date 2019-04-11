namespace HybridDb.Migrations.Documents
{
    public class NullBackupWriter : IBackupWriter
    {
        public void Write(string name, byte[] document)
        {
        }
    }
}