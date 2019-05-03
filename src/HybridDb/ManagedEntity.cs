using System;
using System.Collections.Generic;
using HybridDb.Config;

namespace HybridDb
{
    public class ManagedEntity
    {
        public string Key { get; set; }
        public object Entity { get; set; }
        public string Document { get; set; }
        public Dictionary<string, List<string>> Metadata { get; set; }
        public string MetadataDocument { get; set; }
        public Guid Etag { get; set; }
        public int Version { get; set; }
        public EntityState State { get; set; }
        public Table Table { get; set; }
    }
}