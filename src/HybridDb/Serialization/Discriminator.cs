using System;
using System.Collections.Generic;

namespace HybridDb.Serialization
{
    public class Discriminator
    {
        Dictionary<Type, string> tags = new Dictionary<Type, string>(); 

        public Discriminator(Type basetype, string name)
        {
            if (!IsDiscriminatable(basetype))
            {
                throw new ArgumentException(string.Format("Can not discriminate {0}.", basetype));
            }

            if (basetype.IsAbstract || basetype.IsInterface)
            {
                throw new ArgumentException(string.Format("Type {0} must be instantiable.", basetype));
            }

            if (name == null)
            {
                throw new ArgumentException(string.Format("Discriminator for type {0} should not be null.", basetype));
            }

            Basetype = basetype;
            Name = name;
        }

        public Type Basetype { get; private set; }
        public string Name { get; private set; }

        public void As<T>(string name)
        {
            tags.Add(typeof(T), name);
        }

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