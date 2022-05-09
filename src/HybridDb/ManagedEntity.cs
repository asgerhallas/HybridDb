using System;
using System.Collections.Generic;
using HybridDb.Config;

namespace HybridDb
{
    public class ManagedEntity
    {
        public ManagedEntity(EntityKey entityKey, bool readOnly = false)
        {
            EntityKey = entityKey ?? throw new ArgumentNullException(nameof(entityKey));
            ReadOnly = readOnly;
        }
        
        public EntityKey EntityKey { get; }
        public bool ReadOnly { get; }

        public string Key => EntityKey.Id;
        public Table Table => EntityKey.Table;

        public EntityState State { get; set; }

        public Guid? Etag { get; set; }
        public int Version { get; set; }

        public DocumentDesign Design { get; set; }
        public object Entity { get; set; }
        public string Document { get; set; }
        
        public Dictionary<string, List<string>> Metadata { get; set; }
        public string MetadataDocument { get; set; }

    }
}