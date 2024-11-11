using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HybridDb
{
    public static class TypeEx
    {
        public static Type GetEnumeratedType(this Type type)
        {
            return type
                .GetInterfaces()
                .Where(t => t.IsGenericType
                            && t.GetGenericTypeDefinition() == typeof (IEnumerable<>))
                .Select(t => t.GetGenericArguments()[0]).SingleOrDefault();
        }

        public static bool IsNullable(this Type type) => type != null && Nullable.GetUnderlyingType(type) != null;
        public static bool CanBeNull(this Type type) => !type.IsValueType || type.IsNullable();

        static readonly Dictionary<Type, List<Type>> dict = new()
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