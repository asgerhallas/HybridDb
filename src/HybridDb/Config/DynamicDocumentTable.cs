using System.Collections.Generic;

namespace HybridDb.Config
{
    public class DynamicDocumentTable : DocumentTable
    {
        public DynamicDocumentTable(string name) : base(name) { }

        public override Column this[KeyValuePair<string, object> namedValue] => 
            this[namedValue.Key] ?? new Column(namedValue.Key, namedValue.Value.GetTypeOrDefault());
    }
}