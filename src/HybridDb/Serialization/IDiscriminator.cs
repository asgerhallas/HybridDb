using System;

namespace HybridDb.Serialization
{
    public class Discriminator<T> : Discriminator
    {
        public Discriminator(string name) : base(typeof(T), name)
        {
        }
    }

    public interface IDiscriminator
    {
        bool TryGetFromType(Type type, out string discriminator);
        bool TryGetFromDiscriminator(string discriminator, out Type type);
        bool Discriminates(Type type);
    }
}