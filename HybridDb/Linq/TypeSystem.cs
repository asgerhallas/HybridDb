using System;
using System.Collections.Generic;

namespace HybridDb.Linq
{
    internal static class TypeSystem
    {
        internal static Type GetElementType(Type sequenceType)
        {
            var ienum = FindIEnumerable(sequenceType);
            return ienum != null ? ienum.GetGenericArguments()[0] : sequenceType;
        }

        static Type FindIEnumerable(Type sequenceType)
        {
            if (sequenceType == null || sequenceType == typeof (string))
                return null;

            if (sequenceType.IsArray)
                return typeof (IEnumerable<>).MakeGenericType(sequenceType.GetElementType());

            if (sequenceType.IsGenericType)
            {
                foreach (var arg in sequenceType.GetGenericArguments())
                {
                    var ienum = typeof (IEnumerable<>).MakeGenericType(arg);
                    if (ienum.IsAssignableFrom(sequenceType))
                    {
                        return ienum;
                    }
                }
            }

            var ifaces = sequenceType.GetInterfaces();
            if (ifaces.Length > 0)
            {
                foreach (var iface in ifaces)
                {
                    var ienum = FindIEnumerable(iface);
                    if (ienum != null) return ienum;
                }
            }

            if (sequenceType.BaseType != null && sequenceType.BaseType != typeof (object))
            {
                return FindIEnumerable(sequenceType.BaseType);
            }

            return null;
        }
    }
}