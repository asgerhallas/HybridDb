using System;

namespace HybridDb.Serialization
{
    public class Discriminator
    {
        public Discriminator(Type type, string name)
        {
            if (!IsDiscriminatable(type))
            {
                throw new ArgumentException(string.Format("Can not discriminate {0}.", type));
            }

            if (type.IsAbstract || type.IsInterface)
            {
                throw new ArgumentException(string.Format("Type {0} must be instantiable.", type));
            }

            if (name == null)
            {
                throw new ArgumentException(string.Format("Discriminator for type {0} should not be null.", type));
            }

            Type = type;
            Name = name;
        }

        public Type Type { get; private set; }
        public string Name { get; private set; }

        public static bool IsDiscriminatable(Type type)
        {
            return type.IsClass && type != typeof(string);
        }
    }
}