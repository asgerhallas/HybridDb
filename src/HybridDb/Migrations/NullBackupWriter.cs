namespace HybridDb.Migrations
{
    public class NullBackupWriter : IBackupWriter
    {
        public void Write(string name, byte[] document)
        {
        }
    }
}