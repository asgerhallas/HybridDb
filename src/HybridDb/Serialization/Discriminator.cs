using System;

namespace HybridDb.Serialization
{
    public class Discriminator
    {
        public Discriminator(Type basetype, string name)
        {
            if (!IsDiscriminatable(basetype))
            {
                throw new ArgumentException($"Can not discriminate {basetype}.");
            }

            if (basetype.IsAbstract || basetype.IsInterface)
            {
                throw new ArgumentException($"Type {basetype} must be instantiable.");
            }

            if (name == null)
            {
                throw new ArgumentException($"Discriminator for type {basetype} should not be null.");
            }

            Basetype = basetype;
            Name = name;
        }

        public Type Basetype { get; private set; }
        public string Name { get; private set; }

        public static bool IsDiscriminatable(Type type)
        {
            return type.IsClass && type != typeof(string);
        }
    }

    public class Discriminator<T> : Discriminator
    {
        public Discriminator(string name) : base(typeof(T), name) {}
    }
}