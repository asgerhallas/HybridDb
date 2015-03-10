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
            return type != null && Nullable.GetUnderlyingType(type) != null;
        }

        public static bool CanBeNull(this Type type)
        {
            return !type.IsValueType || type.IsNullable();
        }

        public static Type GetTypeOrDefault(this object self)
        {
            if (self == null)
                return null;

            return self.GetType();
        }

        static readonly Dictionary<Type, List<Type>> dict = new Dictionary<Type, List<Type>>
        {
            { typeof(decimal), new List<Type> { typeof(sbyte), typeof(byte), typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(char) } },
            { typeof(double), new List<Type> { typeof(sbyte), typeof(byte), typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(char), typeof(float) } },
            { typeof(float), new List<Type> { typeof(sbyte), typeof(byte), typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(char), typeof(float) } },
            { typeof(ulong), new List<Type> { typeof(byte), typeof(ushort), typeof(uint), typeof(char) } },
            { typeof(long), new List<Type> { typeof(sbyte), typeof(byte), typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(char) } },
            { typeof(uint), new List<Type> { typeof(byte), typeof(ushort), typeof(char) } },
            { typeof(int), new List<Type> { typeof(sbyte), typeof(byte), typeof(short), typeof(ushort), typeof(char) } },
            { typeof(ushort), new List<Type> { typeof(byte), typeof(char) } },
            { typeof(short), new List<Type> { typeof(byte) } }
        };

        public static bool IsCastableTo(this Type from, Type to)
        {
            if (to.IsAssignableFrom(from))
            {
                return true;
            }

            if (dict.ContainsKey(to) && dict[to].Contains(from))
            {
                return true;
            }

            var castable = from
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Any(
                    m => m.ReturnType == to &&
                         (m.Name == "op_Implicit" ||
                          m.Name == "op_Explicit")
                );

            return castable;
        }
    }
}