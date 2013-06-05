using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HybridDb
{
    public static class TypeEx
    {
        public static bool IsA<T>(this Type type)
        {
            return typeof (T).IsAssignableFrom(type);
        }

        public static bool IsA(this Type type, Type typeToBe)
        {
            if (!typeToBe.IsGenericTypeDefinition)
                return typeToBe.IsAssignableFrom(type);

            var toCheckTypes = new List<Type> {type};
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
            return IsA(instance, typeof (T));
        }

        public static Type GetEnumeratedType(this Type type)
        {
            return type
                .GetInterfaces()
                .Where(t => t.IsGenericType
                            && t.GetGenericTypeDefinition() == typeof (IEnumerable<>))
                .Select(t => t.GetGenericArguments()[0]).SingleOrDefault();
        }

        public static bool IsNullable(this Type type)
        {
            return type != null && type.IsGenericType && type.GetGenericTypeDefinition() == typeof (Nullable<>);
        }

        public static Type AsNonNullable(this Type type)
        {
            if (IsNullable(type))
            {
                return type.GetGenericArguments()[0];
            }
            return type;
        }

        public static MethodInfo GetGenericMethod(this Type type, string name, Type[] parameterTypes)
        {
            var methods = type.GetMethods();
            return (from method in methods.Where(m => m.Name == name) 
                    let methodParameterTypes = method.GetParameters().Select(p => p.ParameterType).ToArray() 
                    where methodParameterTypes.SequenceEqual(parameterTypes, new SimpleTypeComparer()) 
                    select method).FirstOrDefault();
        }

        public static Type GetTypeOrDefault(this object self)
        {
            if (self == null)
                return null;

            return self.GetType();
        }

        class SimpleTypeComparer : IEqualityComparer<Type>
        {
            public bool Equals(Type x, Type y)
            {
                return x.Assembly == y.Assembly &&
                       x.Namespace == y.Namespace &&
                       x.Name == y.Name;
            }

            public int GetHashCode(Type obj)
            {
                throw new NotImplementedException();
            }
        }
    }

    public static class LinqEx
    {
        public static Dictionary<TKey, TValue> ToDictionary<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> keyValuePairs)
        {
            if (keyValuePairs == null)
                return null;

            return keyValuePairs.ToDictionary(x => x.Key, x => x.Value);
        }
    }
}