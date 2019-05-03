using System;

namespace HybridDb
{
    public interface ISerializer
    {
        string Serialize(object obj);
        object Deserialize(string data, Type type);
    }
}