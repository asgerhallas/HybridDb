using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;

namespace HybridDb
{
    /// <summary>
    /// Loads the values of an object's properties into a <see cref="IDictionary{String,Object}"/>
    /// </summary>
    public class ObjectToDictionaryRegistry
    {
        private static readonly Dictionary<Type, Func<object, IDictionary<string, object>>> cache = new Dictionary<Type, Func<object, IDictionary<string, object>>>();
        private static readonly ReaderWriterLockSlim rwLock = new ReaderWriterLockSlim();

        /// <summary>
        /// Loads the values of an object's properties into a <see cref="IDictionary{String,Object}"/>.
        /// </summary>
        /// <param name="dataObject">The data object.</param>
        /// <returns>If <paramref name="dataObject"/> implements <see cref="IDictionary{String,Object}"/>, 
        /// the object is cast to <see cref="IDictionary{String,Object}"/> and returned.
        /// Otherwise the object returned is a <see cref="System.Collections.Hashtable"/> with all public non-static properties and their respective values
        /// as key-value pairs.
        /// </returns>
        public static IDictionary<string, object> Convert(object dataObject)
        {
            if (dataObject == null)
            {
                return new Dictionary<string, object>();
            }

            return dataObject as IDictionary<string, object> ??
                   GetObjectToDictionaryConverter(dataObject)(dataObject);
        }

        /// <summary>
        /// Handles caching.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns></returns>
        private static Func<object, IDictionary<string, object>> GetObjectToDictionaryConverter(object item)
        {
            rwLock.EnterUpgradeableReadLock();
            try
            {
                Func<object, IDictionary<string, object>> ft;
                if (!cache.TryGetValue(item.GetType(), out ft))
                {
                    rwLock.EnterWriteLock();
                    // double check
                    try
                    {
                        if (!cache.TryGetValue(item.GetType(), out ft))
                        {
                            ft = CreateObjectToDictionaryConverter(item.GetType());
                            cache[item.GetType()] = ft;
                        }
                    }
                    finally
                    {
                        rwLock.ExitWriteLock();
                    }
                }
                return ft;
            }
            finally
            {
                rwLock.ExitUpgradeableReadLock();
            }
        }

        public static Func<object, IDictionary<string, object>> CreateObjectToDictionaryConverter(Type itemType)
        {
            var dictType = typeof(Dictionary<string, object>);

            // setup dynamic method
            // Important: make itemType owner of the method to allow access to internal types
            var dm = new DynamicMethod(string.Empty, typeof(IDictionary<string, object>), new[] { typeof(object) }, itemType);
            var il = dm.GetILGenerator();

            // Dictionary.Add(object key, object value)
            var addMethod = dictType.GetMethod("Add");

            // create the Dictionary and store it in a local variable
            il.DeclareLocal(dictType);
            il.Emit(OpCodes.Newobj, dictType.GetConstructor(Type.EmptyTypes));
            il.Emit(OpCodes.Stloc_0);

            var attributes = BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy;
            foreach (var property in itemType.GetProperties(attributes).Where(info => info.CanRead))
            {
                // load Dictionary (prepare for call later)
                il.Emit(OpCodes.Ldloc_0);
                // load key, i.e. name of the property
                il.Emit(OpCodes.Ldstr, property.Name);

                // load value of property to stack
                il.Emit(OpCodes.Ldarg_0);
                il.EmitCall(OpCodes.Callvirt, property.GetGetMethod(), null);
                // perform boxing if necessary
                if (property.PropertyType.IsValueType)
                {
                    il.Emit(OpCodes.Box, property.PropertyType);
                }

                // stack at this point
                // 1. string or null (value)
                // 2. string (key)
                // 3. dictionary

                // ready to call dict.Add(key, value)
                il.EmitCall(OpCodes.Callvirt, addMethod, null);
            }
            // finally load Dictionary and return
            il.Emit(OpCodes.Ldloc_0);
            il.Emit(OpCodes.Ret);

            return (Func<object, IDictionary<string, object>>)dm.CreateDelegate(typeof(Func<object, IDictionary<string, object>>));
        }
    }
}