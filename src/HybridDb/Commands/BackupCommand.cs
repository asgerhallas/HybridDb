using HybridDb.Config;

namespace HybridDb.Commands
{
    public class BackupCommand : DatabaseCommand
    {
        public DocumentDesign Design { get; }
        public string Key { get; }
        public int Version { get; }
        public byte[] OldDocument { get; }

        public BackupCommand(DocumentDesign design, string key, int version, byte[] oldDocument)
        {
            Design = design;
            Key = key;
            Version = version;
            OldDocument = oldDocument;
        }
    }
}