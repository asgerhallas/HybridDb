using System;

namespace HybridDb
{
    public interface ISerializer
    {
        byte[] Serialize(object obj);
        object Deserialize(byte[] data, Type type);
    }
}