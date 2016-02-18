using System;

namespace HybridDb
{
    public class ManagedEntity
    {
        public string Key { get; set; }
        public object Entity { get; set; }
        public Guid Etag { get; set; }
        public EntityState State { get; set; }
        public int Version { get; set; }
        public byte[] Document { get; set; }
    }
}