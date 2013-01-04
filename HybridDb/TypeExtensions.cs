using System;
using System.Collections.Generic;
using System.Linq;

namespace HybridDb
{
    public static class TypeExtensions
    {
        public static bool IsA<T>(this Type type)
        {
            return typeof(T).IsAssignableFrom(type);
        }

        public static bool IsA(this Type type, Type typeToBe)
        {
            if (!typeToBe.IsGenericTypeDefinition)
                return typeToBe.IsAssignableFrom(type);

            var toCheckTypes = new List<Type> { type };
            if (typeToBe.IsInterface)
                toCheckTypes.AddRange(type.GetInterfaces());

            var basedOn = type;
            while (basedOn.BaseType != null)
            {
                toCheckTypes.Add(basedOn.BaseType);
                basedOn = basedOn.BaseType;
            }

            return toCheckTypes.Any(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeToBe);
        }

        public static bool IsA(this object instance, Type typeToBe)
        {
            if (instance == null)
                return false;

            return instance.GetType().IsA(typeToBe);
        }

        public static bool IsA<T>(this object instance)
        {
            return IsA(instance, typeof(T));
        }
    }
}