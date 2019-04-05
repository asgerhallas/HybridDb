using System.Collections.Generic;

namespace HybridDb.Events
{
    public interface IReadOnlyMetadata
    {
        IReadOnlyDictionary<string, string> Values { get; }
        bool ContainsKey(string key);
        string this[string key] { get; }
    }
}