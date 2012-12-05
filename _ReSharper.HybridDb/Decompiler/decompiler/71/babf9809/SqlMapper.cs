// Type: Dapper.SqlMapper
// Assembly: Dapper, Version=1.12.0.0, Culture=neutral, PublicKeyToken=null
// Assembly location: c:\workspace\HybridDb\packages\Dapper.1.12.1\lib\net40\Dapper.dll

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Dapper
{
  /// <summary>
  /// Dapper, a light weight object mapper for ADO.NET
  /// 
  /// </summary>
  public static class SqlMapper
  {
    private static readonly ConcurrentDictionary<SqlMapper.Identity, SqlMapper.CacheInfo> _queryCache = new ConcurrentDictionary<SqlMapper.Identity, SqlMapper.CacheInfo>();
    private static readonly MethodInfo enumParse = typeof (Enum).GetMethod("Parse", new Type[3]
    {
      typeof (Type),
      typeof (string),
      typeof (bool)
    });
    private static readonly MethodInfo getItem = Enumerable.First<MethodInfo>(Enumerable.Select<PropertyInfo, MethodInfo>(Enumerable.Where<PropertyInfo>((IEnumerable<PropertyInfo>) typeof (IDataRecord).GetProperties(BindingFlags.Instance | BindingFlags.Public), (Func<PropertyInfo, bool>) (p =>
    {
      if (Enumerable.Any<ParameterInfo>((IEnumerable<ParameterInfo>) p.GetIndexParameters()))
        return p.GetIndexParameters()[0].ParameterType == typeof (int);
      else
        return false;
    })), (Func<PropertyInfo, MethodInfo>) (p => p.GetGetMethod())));
    private static readonly Hashtable _typeMaps = new Hashtable();
    private static readonly Dictionary<Type, DbType> typeMap = new Dictionary<Type, DbType>();
    private const int COLLECT_PER_ITEMS = 1000;
    private const int COLLECT_HIT_COUNT_MIN = 0;
    internal const string LinqBinary = "System.Data.Linq.Binary";
    private static SqlMapper.Link<Type, Action<IDbCommand, bool>> bindByNameCache;
    private static EventHandler QueryCachePurged;
    private static int collect;

    /// <summary>
    /// Called if the query cache is purged via PurgeQueryCache
    /// 
    /// </summary>
    public static event EventHandler QueryCachePurged
    {
      add
      {
        EventHandler eventHandler1 = SqlMapper.QueryCachePurged;
        EventHandler comparand;
        do
        {
          comparand = eventHandler1;
          EventHandler eventHandler2 = comparand + value;
          eventHandler1 = Interlocked.CompareExchange<EventHandler>(ref SqlMapper.QueryCachePurged, eventHandler2, comparand);
        }
        while (eventHandler1 != comparand);
      }
      remove
      {
        EventHandler eventHandler1 = SqlMapper.QueryCachePurged;
        EventHandler comparand;
        do
        {
          comparand = eventHandler1;
          EventHandler eventHandler2 = comparand - value;
          eventHandler1 = Interlocked.CompareExchange<EventHandler>(ref SqlMapper.QueryCachePurged, eventHandler2, comparand);
        }
        while (eventHandler1 != comparand);
      }
    }

    static SqlMapper()
    {
      SqlMapper.typeMap[typeof (byte)] = DbType.Byte;
      SqlMapper.typeMap[typeof (sbyte)] = DbType.SByte;
      SqlMapper.typeMap[typeof (short)] = DbType.Int16;
      SqlMapper.typeMap[typeof (ushort)] = DbType.UInt16;
      SqlMapper.typeMap[typeof (int)] = DbType.Int32;
      SqlMapper.typeMap[typeof (uint)] = DbType.UInt32;
      SqlMapper.typeMap[typeof (long)] = DbType.Int64;
      SqlMapper.typeMap[typeof (ulong)] = DbType.UInt64;
      SqlMapper.typeMap[typeof (float)] = DbType.Single;
      SqlMapper.typeMap[typeof (double)] = DbType.Double;
      SqlMapper.typeMap[typeof (Decimal)] = DbType.Decimal;
      SqlMapper.typeMap[typeof (bool)] = DbType.Boolean;
      SqlMapper.typeMap[typeof (string)] = DbType.String;
      SqlMapper.typeMap[typeof (char)] = DbType.StringFixedLength;
      SqlMapper.typeMap[typeof (Guid)] = DbType.Guid;
      SqlMapper.typeMap[typeof (DateTime)] = DbType.DateTime;
      SqlMapper.typeMap[typeof (DateTimeOffset)] = DbType.DateTimeOffset;
      SqlMapper.typeMap[typeof (TimeSpan)] = DbType.Time;
      SqlMapper.typeMap[typeof (byte[])] = DbType.Binary;
      SqlMapper.typeMap[typeof (byte?)] = DbType.Byte;
      SqlMapper.typeMap[typeof (sbyte?)] = DbType.SByte;
      SqlMapper.typeMap[typeof (short?)] = DbType.Int16;
      SqlMapper.typeMap[typeof (ushort?)] = DbType.UInt16;
      SqlMapper.typeMap[typeof (int?)] = DbType.Int32;
      SqlMapper.typeMap[typeof (uint?)] = DbType.UInt32;
      SqlMapper.typeMap[typeof (long?)] = DbType.Int64;
      SqlMapper.typeMap[typeof (ulong?)] = DbType.UInt64;
      SqlMapper.typeMap[typeof (float?)] = DbType.Single;
      SqlMapper.typeMap[typeof (double?)] = DbType.Double;
      SqlMapper.typeMap[typeof (Decimal?)] = DbType.Decimal;
      SqlMapper.typeMap[typeof (bool?)] = DbType.Boolean;
      SqlMapper.typeMap[typeof (char?)] = DbType.StringFixedLength;
      SqlMapper.typeMap[typeof (Guid?)] = DbType.Guid;
      SqlMapper.typeMap[typeof (DateTime?)] = DbType.DateTime;
      SqlMapper.typeMap[typeof (DateTimeOffset?)] = DbType.DateTimeOffset;
      SqlMapper.typeMap[typeof (TimeSpan?)] = DbType.Time;
      SqlMapper.typeMap[typeof (object)] = DbType.Object;
    }

    private static Action<IDbCommand, bool> GetBindByName(Type commandType)
    {
      if (commandType == (Type) null)
        return (Action<IDbCommand, bool>) null;
      Action<IDbCommand, bool> action1;
      if (SqlMapper.Link<Type, Action<IDbCommand, bool>>.TryGet(SqlMapper.bindByNameCache, commandType, out action1))
        return action1;
      PropertyInfo property = commandType.GetProperty("BindByName", BindingFlags.Instance | BindingFlags.Public);
      Action<IDbCommand, bool> action2 = (Action<IDbCommand, bool>) null;
      ParameterInfo[] indexParameters;
      MethodInfo setMethod;
      if (property != (PropertyInfo) null && property.CanWrite && property.PropertyType == typeof (bool) && (((indexParameters = property.GetIndexParameters()) == null || indexParameters.Length == 0) && (setMethod = property.GetSetMethod()) != (MethodInfo) null))
      {
        DynamicMethod dynamicMethod = new DynamicMethod(commandType.Name + "_BindByName", (Type) null, new Type[2]
        {
          typeof (IDbCommand),
          typeof (bool)
        });
        ILGenerator ilGenerator = dynamicMethod.GetILGenerator();
        ilGenerator.Emit(OpCodes.Ldarg_0);
        ilGenerator.Emit(OpCodes.Castclass, commandType);
        ilGenerator.Emit(OpCodes.Ldarg_1);
        ilGenerator.EmitCall(OpCodes.Callvirt, setMethod, (Type[]) null);
        ilGenerator.Emit(OpCodes.Ret);
        action2 = (Action<IDbCommand, bool>) dynamicMethod.CreateDelegate(typeof (Action<IDbCommand, bool>));
      }
      SqlMapper.Link<Type, Action<IDbCommand, bool>>.TryAdd(ref SqlMapper.bindByNameCache, commandType, ref action2);
      return action2;
    }

    private static int GetColumnHash(IDataReader reader)
    {
      int fieldCount = reader.FieldCount;
      int num = fieldCount;
      for (int i = 0; i < fieldCount; ++i)
      {
        object obj = (object) reader.GetName(i);
        num = num * 31 + (obj == null ? 0 : obj.GetHashCode());
      }
      return num;
    }

    private static void OnQueryCachePurged()
    {
      EventHandler eventHandler = SqlMapper.QueryCachePurged;
      if (eventHandler == null)
        return;
      eventHandler((object) null, EventArgs.Empty);
    }

    private static void SetQueryCache(SqlMapper.Identity key, SqlMapper.CacheInfo value)
    {
      if (Interlocked.Increment(ref SqlMapper.collect) == 1000)
        SqlMapper.CollectCacheGarbage();
      SqlMapper._queryCache[key] = value;
    }

    private static void CollectCacheGarbage()
    {
      try
      {
        foreach (KeyValuePair<SqlMapper.Identity, SqlMapper.CacheInfo> keyValuePair in SqlMapper._queryCache)
        {
          if (keyValuePair.Value.GetHitCount() <= 0)
          {
            SqlMapper.CacheInfo cacheInfo;
            SqlMapper._queryCache.TryRemove(keyValuePair.Key, out cacheInfo);
          }
        }
      }
      finally
      {
        Interlocked.Exchange(ref SqlMapper.collect, 0);
      }
    }

    private static bool TryGetQueryCache(SqlMapper.Identity key, out SqlMapper.CacheInfo value)
    {
      if (SqlMapper._queryCache.TryGetValue(key, out value))
      {
        value.RecordHit();
        return true;
      }
      else
      {
        value = (SqlMapper.CacheInfo) null;
        return false;
      }
    }

    /// <summary>
    /// Purge the query cache
    /// 
    /// </summary>
    public static void PurgeQueryCache()
    {
      SqlMapper._queryCache.Clear();
      SqlMapper.OnQueryCachePurged();
    }

    private static void PurgeQueryCacheByType(Type type)
    {
      foreach (KeyValuePair<SqlMapper.Identity, SqlMapper.CacheInfo> keyValuePair in SqlMapper._queryCache)
      {
        if (keyValuePair.Key.type == type)
        {
          SqlMapper.CacheInfo cacheInfo;
          SqlMapper._queryCache.TryRemove(keyValuePair.Key, out cacheInfo);
        }
      }
    }

    /// <summary>
    /// Return a count of all the cached queries by dapper
    /// 
    /// </summary>
    /// 
    /// <returns/>
    public static int GetCachedSQLCount()
    {
      return SqlMapper._queryCache.Count;
    }

    /// <summary>
    /// Return a list of all the queries cached by dapper
    /// 
    /// </summary>
    /// <param name="ignoreHitCountAbove"/>
    /// <returns/>
    public static IEnumerable<Tuple<string, string, int>> GetCachedSQL(int ignoreHitCountAbove = 2147483647)
    {
      IEnumerable<Tuple<string, string, int>> source = Enumerable.Select<KeyValuePair<SqlMapper.Identity, SqlMapper.CacheInfo>, Tuple<string, string, int>>((IEnumerable<KeyValuePair<SqlMapper.Identity, SqlMapper.CacheInfo>>) SqlMapper._queryCache, (Func<KeyValuePair<SqlMapper.Identity, SqlMapper.CacheInfo>, Tuple<string, string, int>>) (pair => Tuple.Create<string, string, int>(pair.Key.connectionString, pair.Key.sql, pair.Value.GetHitCount())));
      if (ignoreHitCountAbove < int.MaxValue)
        source = Enumerable.Where<Tuple<string, string, int>>(source, (Func<Tuple<string, string, int>, bool>) (tuple => tuple.Item3 <= ignoreHitCountAbove));
      return source;
    }

    /// <summary>
    /// Deep diagnostics only: find any hash collisions in the cache
    /// 
    /// </summary>
    /// 
    /// <returns/>
    public static IEnumerable<Tuple<int, int>> GetHashCollissions()
    {
      Dictionary<int, int> dictionary = new Dictionary<int, int>();
      foreach (SqlMapper.Identity identity in (IEnumerable<SqlMapper.Identity>) SqlMapper._queryCache.Keys)
      {
        int num;
        if (!dictionary.TryGetValue(identity.hashCode, out num))
          dictionary.Add(identity.hashCode, 1);
        else
          dictionary[identity.hashCode] = num + 1;
      }
      return Enumerable.Select<KeyValuePair<int, int>, Tuple<int, int>>(Enumerable.Where<KeyValuePair<int, int>>((IEnumerable<KeyValuePair<int, int>>) dictionary, (Func<KeyValuePair<int, int>, bool>) (pair => pair.Value > 1)), (Func<KeyValuePair<int, int>, Tuple<int, int>>) (pair => Tuple.Create<int, int>(pair.Key, pair.Value)));
    }

    private static DbType LookupDbType(Type type, string name)
    {
      Type underlyingType = Nullable.GetUnderlyingType(type);
      if (underlyingType != (Type) null)
        type = underlyingType;
      if (type.IsEnum)
        type = Enum.GetUnderlyingType(type);
      DbType dbType;
      if (SqlMapper.typeMap.TryGetValue(type, out dbType))
        return dbType;
      if (type.FullName == "System.Data.Linq.Binary")
        return DbType.Binary;
      if (typeof (IEnumerable).IsAssignableFrom(type))
        return DbType.Xml;
      else
        throw new NotSupportedException(string.Format("The member {0} of type {1} cannot be used as a parameter value", (object) name, (object) type));
    }

    /// <summary>
    /// Execute parameterized SQL
    /// 
    /// </summary>
    /// 
    /// <returns>
    /// Number of rows affected
    /// </returns>
    public static int Execute(this IDbConnection cnn, string sql, object param = null, IDbTransaction transaction = null, int? commandTimeout = null, CommandType? commandType = null)
    {
      IEnumerable enumerable = param as IEnumerable;
      SqlMapper.CacheInfo cacheInfo = (SqlMapper.CacheInfo) null;
      if (enumerable != null && !(enumerable is string))
      {
        bool flag = true;
        int num = 0;
        using (IDbCommand dbCommand = SqlMapper.SetupCommand(cnn, transaction, sql, (Action<IDbCommand, object>) null, (object) null, commandTimeout, commandType))
        {
          string str = (string) null;
          foreach (object obj in enumerable)
          {
            if (flag)
            {
              str = dbCommand.CommandText;
              flag = false;
              cacheInfo = SqlMapper.GetCacheInfo(new SqlMapper.Identity(sql, new CommandType?(dbCommand.CommandType), cnn, (Type) null, obj.GetType(), (Type[]) null));
            }
            else
            {
              dbCommand.CommandText = str;
              dbCommand.Parameters.Clear();
            }
            cacheInfo.ParamReader(dbCommand, obj);
            num += dbCommand.ExecuteNonQuery();
          }
        }
        return num;
      }
      else
      {
        if (param != null)
          cacheInfo = SqlMapper.GetCacheInfo(new SqlMapper.Identity(sql, commandType, cnn, (Type) null, param == null ? (Type) null : param.GetType(), (Type[]) null));
        return SqlMapper.ExecuteCommand(cnn, transaction, sql, param == null ? (Action<IDbCommand, object>) null : cacheInfo.ParamReader, param, commandTimeout, commandType);
      }
    }

    /// <summary>
    /// Return a list of dynamic objects, reader is closed after the call
    /// 
    /// </summary>
    public static IEnumerable<object> Query(this IDbConnection cnn, string sql, object param = null, IDbTransaction transaction = null, bool buffered = true, int? commandTimeout = null, CommandType? commandType = null)
    {
      return (IEnumerable<object>) SqlMapper.Query<SqlMapper.FastExpando>(cnn, sql, param, transaction, buffered, commandTimeout, commandType);
    }

    /// <summary>
    /// Executes a query, returning the data typed as per T
    /// 
    /// </summary>
    /// 
    /// <remarks>
    /// the dynamic param may seem a bit odd, but this works around a major usability issue in vs, if it is Object vs completion gets annoying. Eg type new [space] get new object
    /// </remarks>
    /// 
    /// <returns>
    /// A sequence of data of the supplied type; if a basic type (int, string, etc) is queried then the data from the first column in assumed, otherwise an instance is
    ///             created per row, and a direct column-name===member-name mapping is assumed (case insensitive).
    /// 
    /// </returns>
    public static IEnumerable<T> Query<T>(this IDbConnection cnn, string sql, object param = null, IDbTransaction transaction = null, bool buffered = true, int? commandTimeout = null, CommandType? commandType = null)
    {
      IEnumerable<T> source = SqlMapper.QueryInternal<T>(cnn, sql, param, transaction, commandTimeout, commandType);
      if (!buffered)
        return source;
      else
        return (IEnumerable<T>) Enumerable.ToList<T>(source);
    }

    /// <summary>
    /// Execute a command that returns multiple result sets, and access each in turn
    /// 
    /// </summary>
    public static SqlMapper.GridReader QueryMultiple(this IDbConnection cnn, string sql, object param = null, IDbTransaction transaction = null, int? commandTimeout = null, CommandType? commandType = null)
    {
      SqlMapper.Identity identity = new SqlMapper.Identity(sql, commandType, cnn, typeof (SqlMapper.GridReader), param == null ? (Type) null : param.GetType(), (Type[]) null);
      SqlMapper.CacheInfo cacheInfo = SqlMapper.GetCacheInfo(identity);
      IDbCommand command = (IDbCommand) null;
      IDataReader reader = (IDataReader) null;
      try
      {
        command = SqlMapper.SetupCommand(cnn, transaction, sql, cacheInfo.ParamReader, param, commandTimeout, commandType);
        reader = command.ExecuteReader();
        return new SqlMapper.GridReader(command, reader, identity);
      }
      catch
      {
        if (reader != null)
          reader.Dispose();
        if (command != null)
          command.Dispose();
        throw;
      }
    }

    /// <summary>
    /// Return a typed list of objects, reader is closed after the call
    /// 
    /// </summary>
    private static IEnumerable<T> QueryInternal<T>(this IDbConnection cnn, string sql, object param, IDbTransaction transaction, int? commandTimeout, CommandType? commandType)
    {
      SqlMapper.Identity identity = new SqlMapper.Identity(sql, commandType, cnn, typeof (T), param == null ? (Type) null : param.GetType(), (Type[]) null);
      SqlMapper.CacheInfo info = SqlMapper.GetCacheInfo(identity);
      using (IDbCommand dbCommand = SqlMapper.SetupCommand(cnn, transaction, sql, info.ParamReader, param, commandTimeout, commandType))
      {
        using (IDataReader reader = dbCommand.ExecuteReader())
        {
          SqlMapper.DeserializerState tuple = info.Deserializer;
          int hash = SqlMapper.GetColumnHash(reader);
          if (tuple.Func == null || tuple.Hash != hash)
          {
            tuple = info.Deserializer = new SqlMapper.DeserializerState(hash, SqlMapper.GetDeserializer(typeof (T), reader, 0, -1, false));
            SqlMapper.SetQueryCache(identity, info);
          }
          Func<IDataReader, object> func = tuple.Func;
          while (reader.Read())
            yield return (T) func(reader);
        }
      }
    }

    /// <summary>
    /// Maps a query to objects
    /// 
    /// </summary>
    /// <typeparam name="TFirst">The first type in the recordset</typeparam><typeparam name="TSecond">The second type in the recordset</typeparam><typeparam name="TReturn">The return type</typeparam><param name="cnn"/><param name="sql"/><param name="map"/><param name="param"/><param name="transaction"/><param name="buffered"/><param name="splitOn">The Field we should split and read the second object from (default: id)</param><param name="commandTimeout">Number of seconds before command execution timeout</param><param name="commandType">Is it a stored proc or a batch?</param>
    /// <returns/>
    public static IEnumerable<TReturn> Query<TFirst, TSecond, TReturn>(this IDbConnection cnn, string sql, Func<TFirst, TSecond, TReturn> map, object param = null, IDbTransaction transaction = null, bool buffered = true, string splitOn = "Id", int? commandTimeout = null, CommandType? commandType = null)
    {
      return SqlMapper.MultiMap<TFirst, TSecond, SqlMapper.DontMap, SqlMapper.DontMap, SqlMapper.DontMap, TReturn>(cnn, sql, (object) map, param, transaction, buffered, splitOn, commandTimeout, commandType);
    }

    /// <summary>
    /// Maps a query to objects
    /// 
    /// </summary>
    /// <typeparam name="TFirst"/><typeparam name="TSecond"/><typeparam name="TThird"/><typeparam name="TReturn"/><param name="cnn"/><param name="sql"/><param name="map"/><param name="param"/><param name="transaction"/><param name="buffered"/><param name="splitOn">The Field we should split and read the second object from (default: id)</param><param name="commandTimeout">Number of seconds before command execution timeout</param><param name="commandType"/>
    /// <returns/>
    public static IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TReturn>(this IDbConnection cnn, string sql, Func<TFirst, TSecond, TThird, TReturn> map, object param = null, IDbTransaction transaction = null, bool buffered = true, string splitOn = "Id", int? commandTimeout = null, CommandType? commandType = null)
    {
      return SqlMapper.MultiMap<TFirst, TSecond, TThird, SqlMapper.DontMap, SqlMapper.DontMap, TReturn>(cnn, sql, (object) map, param, transaction, buffered, splitOn, commandTimeout, commandType);
    }

    /// <summary>
    /// Perform a multi mapping query with 4 input parameters
    /// 
    /// </summary>
    /// <typeparam name="TFirst"/><typeparam name="TSecond"/><typeparam name="TThird"/><typeparam name="TFourth"/><typeparam name="TReturn"/><param name="cnn"/><param name="sql"/><param name="map"/><param name="param"/><param name="transaction"/><param name="buffered"/><param name="splitOn"/><param name="commandTimeout"/><param name="commandType"/>
    /// <returns/>
    public static IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TFourth, TReturn>(this IDbConnection cnn, string sql, Func<TFirst, TSecond, TThird, TFourth, TReturn> map, object param = null, IDbTransaction transaction = null, bool buffered = true, string splitOn = "Id", int? commandTimeout = null, CommandType? commandType = null)
    {
      return SqlMapper.MultiMap<TFirst, TSecond, TThird, TFourth, SqlMapper.DontMap, TReturn>(cnn, sql, (object) map, param, transaction, buffered, splitOn, commandTimeout, commandType);
    }

    /// <summary>
    /// Perform a multi mapping query with 5 input parameters
    /// 
    /// </summary>
    /// <typeparam name="TFirst"/><typeparam name="TSecond"/><typeparam name="TThird"/><typeparam name="TFourth"/><typeparam name="TFifth"/><typeparam name="TReturn"/><param name="cnn"/><param name="sql"/><param name="map"/><param name="param"/><param name="transaction"/><param name="buffered"/><param name="splitOn"/><param name="commandTimeout"/><param name="commandType"/>
    /// <returns/>
    public static IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>(this IDbConnection cnn, string sql, Func<TFirst, TSecond, TThird, TFourth, TFifth, TReturn> map, object param = null, IDbTransaction transaction = null, bool buffered = true, string splitOn = "Id", int? commandTimeout = null, CommandType? commandType = null)
    {
      return SqlMapper.MultiMap<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>(cnn, sql, (object) map, param, transaction, buffered, splitOn, commandTimeout, commandType);
    }

    private static IEnumerable<TReturn> MultiMap<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>(this IDbConnection cnn, string sql, object map, object param, IDbTransaction transaction, bool buffered, string splitOn, int? commandTimeout, CommandType? commandType)
    {
      IEnumerable<TReturn> source = SqlMapper.MultiMapImpl<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>(cnn, sql, map, param, transaction, splitOn, commandTimeout, commandType, (IDataReader) null, (SqlMapper.Identity) null);
      if (!buffered)
        return source;
      else
        return (IEnumerable<TReturn>) Enumerable.ToList<TReturn>(source);
    }

    private static IEnumerable<TReturn> MultiMapImpl<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>(this IDbConnection cnn, string sql, object map, object param, IDbTransaction transaction, string splitOn, int? commandTimeout, CommandType? commandType, IDataReader reader, SqlMapper.Identity identity)
    {
      // ISSUE: variable of a compiler-generated type
      SqlMapper.\u003CMultiMapImpl\u003Ed__19<TFirst, TSecond, TThird, TFourth, TFifth, TReturn> multiMapImplD19 = this;
      SqlMapper.Identity identity1 = identity;
      if (identity1 == null)
        identity1 = new SqlMapper.Identity(sql, commandType, cnn, typeof (TFirst), param == null ? (Type) null : param.GetType(), new Type[5]
        {
          typeof (TFirst),
          typeof (TSecond),
          typeof (TThird),
          typeof (TFourth),
          typeof (TFifth)
        });
      // ISSUE: reference to a compiler-generated field
      multiMapImplD19.identity = identity1;
      SqlMapper.CacheInfo cinfo = SqlMapper.GetCacheInfo(identity);
      IDbCommand ownedCommand = (IDbCommand) null;
      IDataReader ownedReader = (IDataReader) null;
      try
      {
        if (reader == null)
        {
          ownedCommand = SqlMapper.SetupCommand(cnn, transaction, sql, cinfo.ParamReader, param, commandTimeout, commandType);
          ownedReader = ownedCommand.ExecuteReader();
          reader = ownedReader;
        }
        SqlMapper.DeserializerState deserializer = new SqlMapper.DeserializerState();
        Func<IDataReader, object>[] otherDeserializers = (Func<IDataReader, object>[]) null;
        int hash = SqlMapper.GetColumnHash(reader);
        if ((deserializer = cinfo.Deserializer).Func == null || (otherDeserializers = cinfo.OtherDeserializers) == null || hash != deserializer.Hash)
        {
          Func<IDataReader, object>[] funcArray = SqlMapper.GenerateDeserializers(new Type[5]
          {
            typeof (TFirst),
            typeof (TSecond),
            typeof (TThird),
            typeof (TFourth),
            typeof (TFifth)
          }, splitOn, reader);
          deserializer = cinfo.Deserializer = new SqlMapper.DeserializerState(hash, funcArray[0]);
          otherDeserializers = cinfo.OtherDeserializers = Enumerable.ToArray<Func<IDataReader, object>>(Enumerable.Skip<Func<IDataReader, object>>((IEnumerable<Func<IDataReader, object>>) funcArray, 1));
          SqlMapper.SetQueryCache(identity, cinfo);
        }
        Func<IDataReader, TReturn> mapIt = SqlMapper.GenerateMapper<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>(deserializer.Func, otherDeserializers, map);
        if (mapIt != null)
        {
          while (reader.Read())
            yield return mapIt(reader);
        }
      }
      finally
      {
        try
        {
          if (ownedReader != null)
            ownedReader.Dispose();
        }
        finally
        {
          if (ownedCommand != null)
            ownedCommand.Dispose();
        }
      }
    }

    private static Func<IDataReader, TReturn> GenerateMapper<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>(Func<IDataReader, object> deserializer, Func<IDataReader, object>[] otherDeserializers, object map)
    {
      switch (otherDeserializers.Length)
      {
        case 1:
          return (Func<IDataReader, TReturn>) (r => ((Func<TFirst, TSecond, TReturn>) map)((TFirst) deserializer(r), (TSecond) otherDeserializers[0](r)));
        case 2:
          return (Func<IDataReader, TReturn>) (r => ((Func<TFirst, TSecond, TThird, TReturn>) map)((TFirst) deserializer(r), (TSecond) otherDeserializers[0](r), (TThird) otherDeserializers[1](r)));
        case 3:
          return (Func<IDataReader, TReturn>) (r => ((Func<TFirst, TSecond, TThird, TFourth, TReturn>) map)((TFirst) deserializer(r), (TSecond) otherDeserializers[0](r), (TThird) otherDeserializers[1](r), (TFourth) otherDeserializers[2](r)));
        case 4:
          return (Func<IDataReader, TReturn>) (r => ((Func<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>) map)((TFirst) deserializer(r), (TSecond) otherDeserializers[0](r), (TThird) otherDeserializers[1](r), (TFourth) otherDeserializers[2](r), (TFifth) otherDeserializers[3](r)));
        default:
          throw new NotSupportedException();
      }
    }

    private static Func<IDataReader, object>[] GenerateDeserializers(Type[] types, string splitOn, IDataReader reader)
    {
      int current = 0;
      string[] splits = Enumerable.ToArray<string>((IEnumerable<string>) splitOn.Split(new char[1]
      {
        ','
      }));
      int splitIndex = 0;
      Func<Type, int> func = (Func<Type, int>) (type =>
      {
        string local_0 = splits[splitIndex];
        if (splits.Length > splitIndex + 1)
          ++splitIndex;
        bool local_1 = false;
        int local_2 = current + 1;
        if (type != typeof (object))
        {
          List<PropertyInfo> local_3 = DefaultTypeMap.GetSettableProps(type);
          List<FieldInfo> local_4 = DefaultTypeMap.GetSettableFields(type);
          foreach (string item_1 in Enumerable.Concat<string>(Enumerable.Select<PropertyInfo, string>((IEnumerable<PropertyInfo>) local_3, (Func<PropertyInfo, string>) (p => p.Name)), Enumerable.Select<FieldInfo, string>((IEnumerable<FieldInfo>) local_4, (Func<FieldInfo, string>) (f => f.Name))))
          {
            if (string.Equals(item_1, local_0, StringComparison.OrdinalIgnoreCase))
            {
              local_1 = true;
              local_2 = current;
              break;
            }
          }
        }
        int local_6;
        for (local_6 = local_2; local_6 < reader.FieldCount && !(splitOn == "*"); ++local_6)
        {
          if (string.Equals(reader.GetName(local_6), local_0, StringComparison.OrdinalIgnoreCase))
          {
            if (local_1)
              local_1 = false;
            else
              break;
          }
        }
        current = local_6;
        return local_6;
      });
      List<Func<IDataReader, object>> list = new List<Func<IDataReader, object>>();
      int startBound = 0;
      bool flag = true;
      foreach (Type type in types)
      {
        if (type != typeof (SqlMapper.DontMap))
        {
          int num = func(type);
          list.Add(SqlMapper.GetDeserializer(type, reader, startBound, num - startBound, !flag));
          flag = false;
          startBound = num;
        }
      }
      return list.ToArray();
    }

    private static SqlMapper.CacheInfo GetCacheInfo(SqlMapper.Identity identity)
    {
      SqlMapper.CacheInfo cacheInfo;
      if (!SqlMapper.TryGetQueryCache(identity, out cacheInfo))
      {
        cacheInfo = new SqlMapper.CacheInfo();
        if (identity.parametersType != (Type) null)
          cacheInfo.ParamReader = !typeof (SqlMapper.IDynamicParameters).IsAssignableFrom(identity.parametersType) ? (!typeof (IEnumerable<KeyValuePair<string, object>>).IsAssignableFrom(identity.parametersType) || !typeof (IDynamicMetaObjectProvider).IsAssignableFrom(identity.parametersType) ? SqlMapper.CreateParamInfoGenerator(identity) : (Action<IDbCommand, object>) ((cmd, obj) => new DynamicParameters(obj).AddParameters(cmd, identity))) : (Action<IDbCommand, object>) ((cmd, obj) => (obj as SqlMapper.IDynamicParameters).AddParameters(cmd, identity));
        SqlMapper.SetQueryCache(identity, cacheInfo);
      }
      return cacheInfo;
    }

    private static Func<IDataReader, object> GetDeserializer(Type type, IDataReader reader, int startBound, int length, bool returnNullIfFirstMissing)
    {
      if (type == typeof (object) || type == typeof (SqlMapper.FastExpando))
        return SqlMapper.GetDynamicDeserializer((IDataRecord) reader, startBound, length, returnNullIfFirstMissing);
      Type type1 = (Type) null;
      if (!SqlMapper.typeMap.ContainsKey(type) && !type.IsEnum && !(type.FullName == "System.Data.Linq.Binary") && (!type.IsValueType || !((type1 = Nullable.GetUnderlyingType(type)) != (Type) null) || !type1.IsEnum))
        return SqlMapper.GetTypeDeserializer(type, reader, startBound, length, returnNullIfFirstMissing);
      else
        return SqlMapper.GetStructDeserializer(type, type1 ?? type, startBound);
    }

    private static Func<IDataReader, object> GetDynamicDeserializer(IDataRecord reader, int startBound, int length, bool returnNullIfFirstMissing)
    {
      int fieldCount = reader.FieldCount;
      if (length == -1)
        length = fieldCount - startBound;
      if (fieldCount <= startBound)
        throw new ArgumentException("When using the multi-mapping APIs ensure you set the splitOn param if you have keys other than Id", "splitOn");
      else
        return (Func<IDataReader, object>) (r =>
        {
          IDictionary<string, object> local_0 = (IDictionary<string, object>) new Dictionary<string, object>(length);
          for (int local_1 = startBound; local_1 < startBound + length; ++local_1)
          {
            object local_2 = r.GetValue(local_1);
            object local_2_1 = local_2 == DBNull.Value ? (object) null : local_2;
            local_0[r.GetName(local_1)] = local_2_1;
            if (returnNullIfFirstMissing && local_1 == startBound && local_2_1 == null)
              return (object) null;
          }
          return (object) SqlMapper.FastExpando.Attach(local_0);
        });
    }

    /// <summary>
    /// Internal use only
    /// 
    /// </summary>
    /// <param name="value"/>
    /// <returns/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("This method is for internal usage only", false)]
    [Browsable(false)]
    public static char ReadChar(object value)
    {
      if (value == null || value is DBNull)
        throw new ArgumentNullException("value");
      string str = value as string;
      if (str == null || str.Length != 1)
        throw new ArgumentException("A single-character was expected", "value");
      else
        return str[0];
    }

    /// <summary>
    /// Internal use only
    /// 
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("This method is for internal usage only", false)]
    public static char? ReadNullableChar(object value)
    {
      if (value == null || value is DBNull)
        return new char?();
      string str = value as string;
      if (str == null || str.Length != 1)
        throw new ArgumentException("A single-character was expected", "value");
      else
        return new char?(str[0]);
    }

    /// <summary>
    /// Internal use only
    /// 
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Browsable(false)]
    [Obsolete("This method is for internal usage only", true)]
    public static void PackListParameters(IDbCommand command, string namePrefix, object value)
    {
      IEnumerable enumerable = value as IEnumerable;
      int count = 0;
      if (enumerable == null)
        return;
      if (FeatureSupport.Get(command.Connection).Arrays)
      {
        IDbDataParameter parameter = command.CreateParameter();
        parameter.Value = (object) enumerable;
        parameter.ParameterName = namePrefix;
        command.Parameters.Add((object) parameter);
      }
      else
      {
        bool flag1 = value is IEnumerable<string>;
        bool flag2 = value is IEnumerable<DbString>;
        foreach (object obj in enumerable)
        {
          ++count;
          IDbDataParameter parameter = command.CreateParameter();
          parameter.ParameterName = namePrefix + (object) count;
          parameter.Value = obj ?? (object) DBNull.Value;
          if (flag1)
          {
            parameter.Size = 4000;
            if (obj != null && ((string) obj).Length > 4000)
              parameter.Size = -1;
          }
          if (flag2 && obj is DbString)
            (obj as DbString).AddParameter(command, parameter.ParameterName);
          else
            command.Parameters.Add((object) parameter);
        }
        if (count == 0)
          command.CommandText = Regex.Replace(command.CommandText, "[?@:]" + Regex.Escape(namePrefix), "(SELECT NULL WHERE 1 = 0)");
        else
          command.CommandText = Regex.Replace(command.CommandText, "[?@:]" + Regex.Escape(namePrefix), (MatchEvaluator) (match =>
          {
            string local_0 = match.Value;
            StringBuilder local_1 = new StringBuilder("(").Append(local_0).Append(1);
            for (int local_2 = 2; local_2 <= count; ++local_2)
              local_1.Append(',').Append(local_0).Append(local_2);
            return ((object) local_1.Append(')')).ToString();
          }));
      }
    }

    private static IEnumerable<PropertyInfo> FilterParameters(IEnumerable<PropertyInfo> parameters, string sql)
    {
      return Enumerable.Where<PropertyInfo>(parameters, (Func<PropertyInfo, bool>) (p => Regex.IsMatch(sql, "[@:]" + p.Name + "([^a-zA-Z0-9_]+|$)", RegexOptions.IgnoreCase | RegexOptions.Multiline)));
    }

    /// <summary>
    /// Internal use only
    /// 
    /// </summary>
    public static Action<IDbCommand, object> CreateParamInfoGenerator(SqlMapper.Identity identity)
    {
      Type type = identity.parametersType;
      bool flag1 = identity.commandType.GetValueOrDefault(CommandType.Text) == CommandType.Text;
      DynamicMethod dynamicMethod = new DynamicMethod(string.Format("ParamInfo{0}", (object) Guid.NewGuid()), (Type) null, new Type[2]
      {
        typeof (IDbCommand),
        typeof (object)
      }, type, 1 != 0);
      ILGenerator ilGenerator = dynamicMethod.GetILGenerator();
      ilGenerator.DeclareLocal(type);
      bool flag2 = false;
      ilGenerator.Emit(OpCodes.Ldarg_1);
      ilGenerator.Emit(OpCodes.Unbox_Any, type);
      ilGenerator.Emit(OpCodes.Stloc_0);
      ilGenerator.Emit(OpCodes.Ldarg_0);
      ilGenerator.EmitCall(OpCodes.Callvirt, typeof (IDbCommand).GetProperty("Parameters").GetGetMethod(), (Type[]) null);
      IEnumerable<PropertyInfo> parameters = (IEnumerable<PropertyInfo>) Enumerable.OrderBy<PropertyInfo, string>(Enumerable.Where<PropertyInfo>((IEnumerable<PropertyInfo>) type.GetProperties(), (Func<PropertyInfo, bool>) (p => p.GetIndexParameters().Length == 0)), (Func<PropertyInfo, string>) (p => p.Name));
      if (flag1)
        parameters = SqlMapper.FilterParameters(parameters, identity.sql);
      foreach (PropertyInfo propertyInfo in parameters)
      {
        if (!flag1 || identity.sql.IndexOf("@" + propertyInfo.Name, StringComparison.InvariantCultureIgnoreCase) >= 0 || identity.sql.IndexOf(":" + propertyInfo.Name, StringComparison.InvariantCultureIgnoreCase) >= 0)
        {
          if (propertyInfo.PropertyType == typeof (DbString))
          {
            ilGenerator.Emit(OpCodes.Ldloc_0);
            ilGenerator.Emit(OpCodes.Callvirt, propertyInfo.GetGetMethod());
            ilGenerator.Emit(OpCodes.Ldarg_0);
            ilGenerator.Emit(OpCodes.Ldstr, propertyInfo.Name);
            ilGenerator.EmitCall(OpCodes.Callvirt, typeof (DbString).GetMethod("AddParameter"), (Type[]) null);
          }
          else
          {
            DbType dbType = SqlMapper.LookupDbType(propertyInfo.PropertyType, propertyInfo.Name);
            if (dbType == DbType.Xml)
            {
              ilGenerator.Emit(OpCodes.Ldarg_0);
              ilGenerator.Emit(OpCodes.Ldstr, propertyInfo.Name);
              ilGenerator.Emit(OpCodes.Ldloc_0);
              ilGenerator.Emit(OpCodes.Callvirt, propertyInfo.GetGetMethod());
              if (propertyInfo.PropertyType.IsValueType)
                ilGenerator.Emit(OpCodes.Box, propertyInfo.PropertyType);
              ilGenerator.EmitCall(OpCodes.Call, typeof (SqlMapper).GetMethod("PackListParameters"), (Type[]) null);
            }
            else
            {
              ilGenerator.Emit(OpCodes.Dup);
              ilGenerator.Emit(OpCodes.Ldarg_0);
              ilGenerator.EmitCall(OpCodes.Callvirt, typeof (IDbCommand).GetMethod("CreateParameter"), (Type[]) null);
              ilGenerator.Emit(OpCodes.Dup);
              ilGenerator.Emit(OpCodes.Ldstr, propertyInfo.Name);
              ilGenerator.EmitCall(OpCodes.Callvirt, typeof (IDataParameter).GetProperty("ParameterName").GetSetMethod(), (Type[]) null);
              if (dbType != DbType.Time)
              {
                ilGenerator.Emit(OpCodes.Dup);
                SqlMapper.EmitInt32(ilGenerator, (int) dbType);
                ilGenerator.EmitCall(OpCodes.Callvirt, typeof (IDataParameter).GetProperty("DbType").GetSetMethod(), (Type[]) null);
              }
              ilGenerator.Emit(OpCodes.Dup);
              SqlMapper.EmitInt32(ilGenerator, 1);
              ilGenerator.EmitCall(OpCodes.Callvirt, typeof (IDataParameter).GetProperty("Direction").GetSetMethod(), (Type[]) null);
              ilGenerator.Emit(OpCodes.Dup);
              ilGenerator.Emit(OpCodes.Ldloc_0);
              ilGenerator.Emit(OpCodes.Callvirt, propertyInfo.GetGetMethod());
              bool flag3 = true;
              if (propertyInfo.PropertyType.IsValueType)
              {
                ilGenerator.Emit(OpCodes.Box, propertyInfo.PropertyType);
                if (Nullable.GetUnderlyingType(propertyInfo.PropertyType) == (Type) null)
                  flag3 = false;
              }
              if (flag3)
              {
                if (dbType == DbType.String && !flag2)
                {
                  ilGenerator.DeclareLocal(typeof (int));
                  flag2 = true;
                }
                ilGenerator.Emit(OpCodes.Dup);
                Label label1 = ilGenerator.DefineLabel();
                Label? nullable = dbType == DbType.String ? new Label?(ilGenerator.DefineLabel()) : new Label?();
                ilGenerator.Emit(OpCodes.Brtrue_S, label1);
                ilGenerator.Emit(OpCodes.Pop);
                ilGenerator.Emit(OpCodes.Ldsfld, typeof (DBNull).GetField("Value"));
                if (dbType == DbType.String)
                {
                  SqlMapper.EmitInt32(ilGenerator, 0);
                  ilGenerator.Emit(OpCodes.Stloc_1);
                }
                if (nullable.HasValue)
                  ilGenerator.Emit(OpCodes.Br_S, nullable.Value);
                ilGenerator.MarkLabel(label1);
                if (propertyInfo.PropertyType == typeof (string))
                {
                  ilGenerator.Emit(OpCodes.Dup);
                  ilGenerator.EmitCall(OpCodes.Callvirt, typeof (string).GetProperty("Length").GetGetMethod(), (Type[]) null);
                  SqlMapper.EmitInt32(ilGenerator, 4000);
                  ilGenerator.Emit(OpCodes.Cgt);
                  Label label2 = ilGenerator.DefineLabel();
                  Label label3 = ilGenerator.DefineLabel();
                  ilGenerator.Emit(OpCodes.Brtrue_S, label2);
                  SqlMapper.EmitInt32(ilGenerator, 4000);
                  ilGenerator.Emit(OpCodes.Br_S, label3);
                  ilGenerator.MarkLabel(label2);
                  SqlMapper.EmitInt32(ilGenerator, -1);
                  ilGenerator.MarkLabel(label3);
                  ilGenerator.Emit(OpCodes.Stloc_1);
                }
                if (propertyInfo.PropertyType.FullName == "System.Data.Linq.Binary")
                  ilGenerator.EmitCall(OpCodes.Callvirt, propertyInfo.PropertyType.GetMethod("ToArray", BindingFlags.Instance | BindingFlags.Public), (Type[]) null);
                if (nullable.HasValue)
                  ilGenerator.MarkLabel(nullable.Value);
              }
              ilGenerator.EmitCall(OpCodes.Callvirt, typeof (IDataParameter).GetProperty("Value").GetSetMethod(), (Type[]) null);
              if (propertyInfo.PropertyType == typeof (string))
              {
                Label label = ilGenerator.DefineLabel();
                ilGenerator.Emit(OpCodes.Ldloc_1);
                ilGenerator.Emit(OpCodes.Brfalse_S, label);
                ilGenerator.Emit(OpCodes.Dup);
                ilGenerator.Emit(OpCodes.Ldloc_1);
                ilGenerator.EmitCall(OpCodes.Callvirt, typeof (IDbDataParameter).GetProperty("Size").GetSetMethod(), (Type[]) null);
                ilGenerator.MarkLabel(label);
              }
              ilGenerator.EmitCall(OpCodes.Callvirt, typeof (IList).GetMethod("Add"), (Type[]) null);
              ilGenerator.Emit(OpCodes.Pop);
            }
          }
        }
      }
      ilGenerator.Emit(OpCodes.Pop);
      ilGenerator.Emit(OpCodes.Ret);
      return (Action<IDbCommand, object>) dynamicMethod.CreateDelegate(typeof (Action<IDbCommand, object>));
    }

    private static IDbCommand SetupCommand(IDbConnection cnn, IDbTransaction transaction, string sql, Action<IDbCommand, object> paramReader, object obj, int? commandTimeout, CommandType? commandType)
    {
      IDbCommand command = cnn.CreateCommand();
      Action<IDbCommand, bool> bindByName = SqlMapper.GetBindByName(command.GetType());
      if (bindByName != null)
        bindByName(command, true);
      if (transaction != null)
        command.Transaction = transaction;
      command.CommandText = sql;
      if (commandTimeout.HasValue)
        command.CommandTimeout = commandTimeout.Value;
      if (commandType.HasValue)
        command.CommandType = commandType.Value;
      if (paramReader != null)
        paramReader(command, obj);
      return command;
    }

    private static int ExecuteCommand(IDbConnection cnn, IDbTransaction transaction, string sql, Action<IDbCommand, object> paramReader, object obj, int? commandTimeout, CommandType? commandType)
    {
      using (IDbCommand dbCommand = SqlMapper.SetupCommand(cnn, transaction, sql, paramReader, obj, commandTimeout, commandType))
        return dbCommand.ExecuteNonQuery();
    }

    private static Func<IDataReader, object> GetStructDeserializer(Type type, Type effectiveType, int index)
    {
      if (type == typeof (char))
        return (Func<IDataReader, object>) (r => (object) SqlMapper.ReadChar(r.GetValue(index)));
      if (type == typeof (char?))
        return (Func<IDataReader, object>) (r => (object) SqlMapper.ReadNullableChar(r.GetValue(index)));
      if (type.FullName == "System.Data.Linq.Binary")
        return (Func<IDataReader, object>) (r => Activator.CreateInstance(type, new object[1]
        {
          r.GetValue(index)
        }));
      if (effectiveType.IsEnum)
        return (Func<IDataReader, object>) (r =>
        {
          object local_0 = r.GetValue(index);
          if (!(local_0 is DBNull))
            return Enum.ToObject(effectiveType, local_0);
          else
            return (object) null;
        });
      else
        return (Func<IDataReader, object>) (r =>
        {
          object local_0 = r.GetValue(index);
          if (!(local_0 is DBNull))
            return local_0;
          else
            return (object) null;
        });
    }

    /// <summary>
    /// Gets type-map for the given type
    /// 
    /// </summary>
    /// 
    /// <returns>
    /// Type map implementation, DefaultTypeMap instance if no override present
    /// </returns>
    public static SqlMapper.ITypeMap GetTypeMap(Type type)
    {
      if (type == (Type) null)
        throw new ArgumentNullException("type");
      SqlMapper.ITypeMap typeMap = (SqlMapper.ITypeMap) SqlMapper._typeMaps[(object) type];
      if (typeMap == null)
      {
        lock (SqlMapper._typeMaps)
        {
          typeMap = (SqlMapper.ITypeMap) SqlMapper._typeMaps[(object) type];
          if (typeMap == null)
          {
            typeMap = (SqlMapper.ITypeMap) new DefaultTypeMap(type);
            SqlMapper._typeMaps[(object) type] = (object) typeMap;
          }
        }
      }
      return typeMap;
    }

    /// <summary>
    /// Set custom mapping for type deserializers
    /// 
    /// </summary>
    /// <param name="type">Entity type to override</param><param name="map">Mapping rules impementation, null to remove custom map</param>
    public static void SetTypeMap(Type type, SqlMapper.ITypeMap map)
    {
      if (type == (Type) null)
        throw new ArgumentNullException("type");
      if (map == null || map is DefaultTypeMap)
      {
        lock (SqlMapper._typeMaps)
          SqlMapper._typeMaps.Remove((object) type);
      }
      else
      {
        lock (SqlMapper._typeMaps)
          SqlMapper._typeMaps[(object) type] = (object) map;
      }
      SqlMapper.PurgeQueryCacheByType(type);
    }

    /// <summary>
    /// Internal use only
    /// 
    /// </summary>
    /// <param name="type"/><param name="reader"/><param name="startBound"/><param name="length"/><param name="returnNullIfFirstMissing"/>
    /// <returns/>
    public static Func<IDataReader, object> GetTypeDeserializer(Type type, IDataReader reader, int startBound = 0, int length = -1, bool returnNullIfFirstMissing = false)
    {
      DynamicMethod dynamicMethod = new DynamicMethod(string.Format("Deserialize{0}", (object) Guid.NewGuid()), typeof (object), new Type[1]
      {
        typeof (IDataReader)
      }, 1 != 0);
      ILGenerator ilGenerator = dynamicMethod.GetILGenerator();
      ilGenerator.DeclareLocal(typeof (int));
      ilGenerator.DeclareLocal(type);
      ilGenerator.Emit(OpCodes.Ldc_I4_0);
      ilGenerator.Emit(OpCodes.Stloc_0);
      if (length == -1)
        length = reader.FieldCount - startBound;
      if (reader.FieldCount <= startBound)
        throw new ArgumentException("When using the multi-mapping APIs ensure you set the splitOn param if you have keys other than Id", "splitOn");
      string[] names = Enumerable.ToArray<string>(Enumerable.Select<int, string>(Enumerable.Range(startBound, length), (Func<int, string>) (i => reader.GetName(i))));
      SqlMapper.ITypeMap typeMap = SqlMapper.GetTypeMap(type);
      int i1 = startBound;
      ConstructorInfo specializedConstructor = (ConstructorInfo) null;
      if (type.IsValueType)
      {
        ilGenerator.Emit(OpCodes.Ldloca_S, (byte) 1);
        ilGenerator.Emit(OpCodes.Initobj, type);
      }
      else
      {
        Type[] types = new Type[length];
        for (int i2 = startBound; i2 < startBound + length; ++i2)
          types[i2 - startBound] = reader.GetFieldType(i2);
        if (type.IsValueType)
        {
          ilGenerator.Emit(OpCodes.Ldloca_S, (byte) 1);
          ilGenerator.Emit(OpCodes.Initobj, type);
        }
        else
        {
          ConstructorInfo constructor = typeMap.FindConstructor(names, types);
          if (constructor == (ConstructorInfo) null)
            throw new InvalidOperationException(string.Format("A parameterless default constructor or one matching signature {0} is required for {1} materialization", (object) ("(" + string.Join(", ", Enumerable.ToArray<string>(Enumerable.Select<Type, string>((IEnumerable<Type>) types, (Func<Type, int, string>) ((t, i) => t.FullName + " " + names[i])))) + ")"), (object) type.FullName));
          if (constructor.GetParameters().Length == 0)
          {
            ilGenerator.Emit(OpCodes.Newobj, constructor);
            ilGenerator.Emit(OpCodes.Stloc_1);
          }
          else
            specializedConstructor = constructor;
        }
      }
      ilGenerator.BeginExceptionBlock();
      if (type.IsValueType)
        ilGenerator.Emit(OpCodes.Ldloca_S, (byte) 1);
      else if (specializedConstructor == (ConstructorInfo) null)
        ilGenerator.Emit(OpCodes.Ldloc_1);
      List<SqlMapper.IMemberMap> list = Enumerable.ToList<SqlMapper.IMemberMap>(specializedConstructor != (ConstructorInfo) null ? Enumerable.Select<string, SqlMapper.IMemberMap>((IEnumerable<string>) names, (Func<string, SqlMapper.IMemberMap>) (n => typeMap.GetConstructorParameter(specializedConstructor, n))) : Enumerable.Select<string, SqlMapper.IMemberMap>((IEnumerable<string>) names, (Func<string, SqlMapper.IMemberMap>) (n => typeMap.GetMember(n))));
      bool flag1 = true;
      Label label1 = ilGenerator.DefineLabel();
      int index = -1;
      foreach (SqlMapper.IMemberMap memberMap in list)
      {
        if (memberMap != null)
        {
          if (specializedConstructor == (ConstructorInfo) null)
            ilGenerator.Emit(OpCodes.Dup);
          Label label2 = ilGenerator.DefineLabel();
          Label label3 = ilGenerator.DefineLabel();
          ilGenerator.Emit(OpCodes.Ldarg_0);
          SqlMapper.EmitInt32(ilGenerator, i1);
          ilGenerator.Emit(OpCodes.Dup);
          ilGenerator.Emit(OpCodes.Stloc_0);
          ilGenerator.Emit(OpCodes.Callvirt, SqlMapper.getItem);
          Type memberType = memberMap.MemberType;
          if (memberType == typeof (char) || memberType == typeof (char?))
          {
            ilGenerator.EmitCall(OpCodes.Call, typeof (SqlMapper).GetMethod(memberType == typeof (char) ? "ReadChar" : "ReadNullableChar", BindingFlags.Static | BindingFlags.Public), (Type[]) null);
          }
          else
          {
            ilGenerator.Emit(OpCodes.Dup);
            ilGenerator.Emit(OpCodes.Isinst, typeof (DBNull));
            ilGenerator.Emit(OpCodes.Brtrue_S, label2);
            Type underlyingType = Nullable.GetUnderlyingType(memberType);
            Type type1 = !(underlyingType != (Type) null) || !underlyingType.IsEnum ? memberType : underlyingType;
            if (type1.IsEnum)
            {
              if (index == -1)
                index = ilGenerator.DeclareLocal(typeof (string)).LocalIndex;
              Label label4 = ilGenerator.DefineLabel();
              ilGenerator.Emit(OpCodes.Dup);
              ilGenerator.Emit(OpCodes.Isinst, typeof (string));
              ilGenerator.Emit(OpCodes.Dup);
              SqlMapper.StoreLocal(ilGenerator, index);
              ilGenerator.Emit(OpCodes.Brfalse_S, label4);
              ilGenerator.Emit(OpCodes.Pop);
              ilGenerator.Emit(OpCodes.Ldtoken, type1);
              ilGenerator.EmitCall(OpCodes.Call, typeof (Type).GetMethod("GetTypeFromHandle"), (Type[]) null);
              ilGenerator.Emit(OpCodes.Ldloc_2);
              ilGenerator.Emit(OpCodes.Ldc_I4_1);
              ilGenerator.EmitCall(OpCodes.Call, SqlMapper.enumParse, (Type[]) null);
              ilGenerator.MarkLabel(label4);
              ilGenerator.Emit(OpCodes.Unbox_Any, type1);
              if (underlyingType != (Type) null)
                ilGenerator.Emit(OpCodes.Newobj, memberType.GetConstructor(new Type[1]
                {
                  underlyingType
                }));
            }
            else if (memberType.FullName == "System.Data.Linq.Binary")
            {
              ilGenerator.Emit(OpCodes.Unbox_Any, typeof (byte[]));
              ilGenerator.Emit(OpCodes.Newobj, memberType.GetConstructor(new Type[1]
              {
                typeof (byte[])
              }));
            }
            else
            {
              Type fieldType = reader.GetFieldType(i1);
              TypeCode typeCode1 = Type.GetTypeCode(fieldType);
              TypeCode typeCode2 = Type.GetTypeCode(type1);
              if (fieldType == type1 || typeCode1 == typeCode2 || typeCode1 == Type.GetTypeCode(underlyingType))
              {
                ilGenerator.Emit(OpCodes.Unbox_Any, type1);
              }
              else
              {
                bool flag2 = true;
                OpCode opcode = new OpCode();
                if (typeCode1 != TypeCode.Decimal)
                {
                  switch (typeCode2)
                  {
                    case TypeCode.Decimal:
                      break;
                    case TypeCode.Boolean:
                    case TypeCode.Int32:
                      opcode = OpCodes.Conv_Ovf_I4;
                      goto label_50;
                    case TypeCode.SByte:
                      opcode = OpCodes.Conv_Ovf_I1;
                      goto label_50;
                    case TypeCode.Byte:
                      opcode = OpCodes.Conv_Ovf_I1_Un;
                      goto label_50;
                    case TypeCode.Int16:
                      opcode = OpCodes.Conv_Ovf_I2;
                      goto label_50;
                    case TypeCode.UInt16:
                      opcode = OpCodes.Conv_Ovf_I2_Un;
                      goto label_50;
                    case TypeCode.UInt32:
                      opcode = OpCodes.Conv_Ovf_I4_Un;
                      goto label_50;
                    case TypeCode.Int64:
                      opcode = OpCodes.Conv_Ovf_I8;
                      goto label_50;
                    case TypeCode.UInt64:
                      opcode = OpCodes.Conv_Ovf_I8_Un;
                      goto label_50;
                    case TypeCode.Single:
                      opcode = OpCodes.Conv_R4;
                      goto label_50;
                    case TypeCode.Double:
                      opcode = OpCodes.Conv_R8;
                      goto label_50;
                    default:
                      flag2 = false;
                      goto label_50;
                  }
                }
                flag2 = false;
label_50:
                if (flag2)
                {
                  ilGenerator.Emit(OpCodes.Unbox_Any, fieldType);
                  ilGenerator.Emit(opcode);
                }
                else
                {
                  ilGenerator.Emit(OpCodes.Ldtoken, type1);
                  ilGenerator.EmitCall(OpCodes.Call, typeof (Type).GetMethod("GetTypeFromHandle"), (Type[]) null);
                  ilGenerator.EmitCall(OpCodes.Call, typeof (Convert).GetMethod("ChangeType", new Type[2]
                  {
                    typeof (object),
                    typeof (Type)
                  }), (Type[]) null);
                  ilGenerator.Emit(OpCodes.Unbox_Any, type1);
                }
              }
            }
          }
          if (specializedConstructor == (ConstructorInfo) null)
          {
            if (memberMap.Property != (PropertyInfo) null)
            {
              if (type.IsValueType)
                ilGenerator.Emit(OpCodes.Call, DefaultTypeMap.GetPropertySetter(memberMap.Property, type));
              else
                ilGenerator.Emit(OpCodes.Callvirt, DefaultTypeMap.GetPropertySetter(memberMap.Property, type));
            }
            else
              ilGenerator.Emit(OpCodes.Stfld, memberMap.Field);
          }
          ilGenerator.Emit(OpCodes.Br_S, label3);
          ilGenerator.MarkLabel(label2);
          if (specializedConstructor != (ConstructorInfo) null)
          {
            ilGenerator.Emit(OpCodes.Pop);
            if (memberMap.MemberType.IsValueType)
            {
              int localIndex = ilGenerator.DeclareLocal(memberMap.MemberType).LocalIndex;
              SqlMapper.LoadLocalAddress(ilGenerator, localIndex);
              ilGenerator.Emit(OpCodes.Initobj, memberMap.MemberType);
              SqlMapper.LoadLocal(ilGenerator, localIndex);
            }
            else
              ilGenerator.Emit(OpCodes.Ldnull);
          }
          else
          {
            ilGenerator.Emit(OpCodes.Pop);
            ilGenerator.Emit(OpCodes.Pop);
          }
          if (flag1 && returnNullIfFirstMissing)
          {
            ilGenerator.Emit(OpCodes.Pop);
            ilGenerator.Emit(OpCodes.Ldnull);
            ilGenerator.Emit(OpCodes.Stloc_1);
            ilGenerator.Emit(OpCodes.Br, label1);
          }
          ilGenerator.MarkLabel(label3);
        }
        flag1 = false;
        ++i1;
      }
      if (type.IsValueType)
      {
        ilGenerator.Emit(OpCodes.Pop);
      }
      else
      {
        if (specializedConstructor != (ConstructorInfo) null)
          ilGenerator.Emit(OpCodes.Newobj, specializedConstructor);
        ilGenerator.Emit(OpCodes.Stloc_1);
      }
      ilGenerator.MarkLabel(label1);
      ilGenerator.BeginCatchBlock(typeof (Exception));
      ilGenerator.Emit(OpCodes.Ldloc_0);
      ilGenerator.Emit(OpCodes.Ldarg_0);
      ilGenerator.EmitCall(OpCodes.Call, typeof (SqlMapper).GetMethod("ThrowDataException"), (Type[]) null);
      ilGenerator.EndExceptionBlock();
      ilGenerator.Emit(OpCodes.Ldloc_1);
      if (type.IsValueType)
        ilGenerator.Emit(OpCodes.Box, type);
      ilGenerator.Emit(OpCodes.Ret);
      return (Func<IDataReader, object>) dynamicMethod.CreateDelegate(typeof (Func<IDataReader, object>));
    }

    private static void LoadLocal(ILGenerator il, int index)
    {
      if (index < 0 || index >= (int) short.MaxValue)
        throw new ArgumentNullException("index");
      switch (index)
      {
        case 0:
          il.Emit(OpCodes.Ldloc_0);
          break;
        case 1:
          il.Emit(OpCodes.Ldloc_1);
          break;
        case 2:
          il.Emit(OpCodes.Ldloc_2);
          break;
        case 3:
          il.Emit(OpCodes.Ldloc_3);
          break;
        default:
          if (index <= (int) byte.MaxValue)
          {
            il.Emit(OpCodes.Ldloc_S, (byte) index);
            break;
          }
          else
          {
            il.Emit(OpCodes.Ldloc, (short) index);
            break;
          }
      }
    }

    private static void StoreLocal(ILGenerator il, int index)
    {
      if (index < 0 || index >= (int) short.MaxValue)
        throw new ArgumentNullException("index");
      switch (index)
      {
        case 0:
          il.Emit(OpCodes.Stloc_0);
          break;
        case 1:
          il.Emit(OpCodes.Stloc_1);
          break;
        case 2:
          il.Emit(OpCodes.Stloc_2);
          break;
        case 3:
          il.Emit(OpCodes.Stloc_3);
          break;
        default:
          if (index <= (int) byte.MaxValue)
          {
            il.Emit(OpCodes.Stloc_S, (byte) index);
            break;
          }
          else
          {
            il.Emit(OpCodes.Stloc, (short) index);
            break;
          }
      }
    }

    private static void LoadLocalAddress(ILGenerator il, int index)
    {
      if (index < 0 || index >= (int) short.MaxValue)
        throw new ArgumentNullException("index");
      if (index <= (int) byte.MaxValue)
        il.Emit(OpCodes.Ldloca_S, (byte) index);
      else
        il.Emit(OpCodes.Ldloca, (short) index);
    }

    /// <summary>
    /// Throws a data exception, only used internally
    /// 
    /// </summary>
    /// <param name="ex"/><param name="index"/><param name="reader"/>
    public static void ThrowDataException(Exception ex, int index, IDataReader reader)
    {
      Exception exception;
      try
      {
        string str1 = "(n/a)";
        string str2 = "(n/a)";
        if (reader != null && index >= 0 && index < reader.FieldCount)
        {
          str1 = reader.GetName(index);
          object obj = reader.GetValue(index);
          str2 = obj == null || obj is DBNull ? "<null>" : Convert.ToString(obj) + (object) " - " + (string) (object) Type.GetTypeCode(obj.GetType());
        }
        exception = (Exception) new DataException(string.Format("Error parsing column {0} ({1}={2})", (object) index, (object) str1, (object) str2), ex);
      }
      catch
      {
        exception = (Exception) new DataException(ex.Message, ex);
      }
      throw exception;
    }

    private static void EmitInt32(ILGenerator il, int value)
    {
      switch (value)
      {
        case -1:
          il.Emit(OpCodes.Ldc_I4_M1);
          break;
        case 0:
          il.Emit(OpCodes.Ldc_I4_0);
          break;
        case 1:
          il.Emit(OpCodes.Ldc_I4_1);
          break;
        case 2:
          il.Emit(OpCodes.Ldc_I4_2);
          break;
        case 3:
          il.Emit(OpCodes.Ldc_I4_3);
          break;
        case 4:
          il.Emit(OpCodes.Ldc_I4_4);
          break;
        case 5:
          il.Emit(OpCodes.Ldc_I4_5);
          break;
        case 6:
          il.Emit(OpCodes.Ldc_I4_6);
          break;
        case 7:
          il.Emit(OpCodes.Ldc_I4_7);
          break;
        case 8:
          il.Emit(OpCodes.Ldc_I4_8);
          break;
        default:
          if (value >= (int) sbyte.MinValue && value <= (int) sbyte.MaxValue)
          {
            il.Emit(OpCodes.Ldc_I4_S, (sbyte) value);
            break;
          }
          else
          {
            il.Emit(OpCodes.Ldc_I4, value);
            break;
          }
      }
    }

    /// <summary>
    /// Implement this interface to pass an arbitrary db specific set of parameters to Dapper
    /// 
    /// </summary>
    public interface IDynamicParameters
    {
      /// <summary>
      /// Add all the parameters needed to the command just before it executes
      /// 
      /// </summary>
      /// <param name="command">The raw command prior to execution</param><param name="identity">Information about the query</param>
      void AddParameters(IDbCommand command, SqlMapper.Identity identity);
    }

    /// <summary>
    /// Implement this interface to change default mapping of reader columns to type memebers
    /// 
    /// </summary>
    public interface ITypeMap
    {
      /// <summary>
      /// Finds best constructor
      /// 
      /// </summary>
      /// <param name="names">DataReader column names</param><param name="types">DataReader column types</param>
      /// <returns>
      /// Matching constructor or default one
      /// </returns>
      ConstructorInfo FindConstructor(string[] names, Type[] types);

      /// <summary>
      /// Gets mapping for constructor parameter
      /// 
      /// </summary>
      /// <param name="constructor">Constructor to resolve</param><param name="columnName">DataReader column name</param>
      /// <returns>
      /// Mapping implementation
      /// </returns>
      SqlMapper.IMemberMap GetConstructorParameter(ConstructorInfo constructor, string columnName);

      /// <summary>
      /// Gets member mapping for column
      /// 
      /// </summary>
      /// <param name="columnName">DataReader column name</param>
      /// <returns>
      /// Mapping implementation
      /// </returns>
      SqlMapper.IMemberMap GetMember(string columnName);
    }

    /// <summary>
    /// Implements this interface to provide custom member mapping
    /// 
    /// </summary>
    public interface IMemberMap
    {
      /// <summary>
      /// Source DataReader column name
      /// 
      /// </summary>
      string ColumnName { get; }

      /// <summary>
      /// Target member type
      /// 
      /// </summary>
      Type MemberType { get; }

      /// <summary>
      /// Target property
      /// 
      /// </summary>
      PropertyInfo Property { get; }

      /// <summary>
      /// Target field
      /// 
      /// </summary>
      FieldInfo Field { get; }

      /// <summary>
      /// Target constructor parameter
      /// 
      /// </summary>
      ParameterInfo Parameter { get; }
    }

    /// <summary>
    /// This is a micro-cache; suitable when the number of terms is controllable (a few hundred, for example),
    ///             and strictly append-only; you cannot change existing values. All key matches are on **REFERENCE**
    ///             equality. The type is fully thread-safe.
    /// 
    /// </summary>
    private class Link<TKey, TValue> where TKey : class
    {
      public TKey Key { get; private set; }

      public TValue Value { get; private set; }

      public SqlMapper.Link<TKey, TValue> Tail { get; private set; }

      private Link(TKey key, TValue value, SqlMapper.Link<TKey, TValue> tail)
      {
        this.Key = key;
        this.Value = value;
        this.Tail = tail;
      }

      public static bool TryGet(SqlMapper.Link<TKey, TValue> link, TKey key, out TValue value)
      {
        for (; link != null; link = link.Tail)
        {
          if ((object) key == (object) link.Key)
          {
            value = link.Value;
            return true;
          }
        }
        value = default (TValue);
        return false;
      }

      public static bool TryAdd(ref SqlMapper.Link<TKey, TValue> head, TKey key, ref TValue value)
      {
        SqlMapper.Link<TKey, TValue> link1;
        SqlMapper.Link<TKey, TValue> link2;
        do
        {
          link1 = Interlocked.CompareExchange<SqlMapper.Link<TKey, TValue>>(ref head, (SqlMapper.Link<TKey, TValue>) null, (SqlMapper.Link<TKey, TValue>) null);
          TValue obj;
          if (SqlMapper.Link<TKey, TValue>.TryGet(link1, key, out obj))
          {
            value = obj;
            return false;
          }
          else
            link2 = new SqlMapper.Link<TKey, TValue>(key, value, link1);
        }
        while (Interlocked.CompareExchange<SqlMapper.Link<TKey, TValue>>(ref head, link2, link1) != link1);
        return true;
      }
    }

    private class CacheInfo
    {
      private int hitCount;

      public SqlMapper.DeserializerState Deserializer { get; set; }

      public Func<IDataReader, object>[] OtherDeserializers { get; set; }

      public Action<IDbCommand, object> ParamReader { get; set; }

      public int GetHitCount()
      {
        return Interlocked.CompareExchange(ref this.hitCount, 0, 0);
      }

      public void RecordHit()
      {
        Interlocked.Increment(ref this.hitCount);
      }
    }

    private struct DeserializerState
    {
      public readonly int Hash;
      public readonly Func<IDataReader, object> Func;

      public DeserializerState(int hash, Func<IDataReader, object> func)
      {
        this.Hash = hash;
        this.Func = func;
      }
    }

    /// <summary>
    /// Identity of a cached query in Dapper, used for extensability
    /// 
    /// </summary>
    public class Identity : IEquatable<SqlMapper.Identity>
    {
      /// <summary>
      /// The sql
      /// 
      /// </summary>
      public readonly string sql;
      /// <summary>
      /// The command type
      /// 
      /// </summary>
      public readonly CommandType? commandType;
      /// <summary/>
      public readonly int hashCode;
      /// <summary/>
      public readonly int gridIndex;
      /// <summary/>
      public readonly Type type;
      /// <summary/>
      public readonly string connectionString;
      /// <summary/>
      public readonly Type parametersType;

      internal Identity(string sql, CommandType? commandType, IDbConnection connection, Type type, Type parametersType, Type[] otherTypes)
        : this(sql, commandType, connection.ConnectionString, type, parametersType, otherTypes, 0)
      {
      }

      private Identity(string sql, CommandType? commandType, string connectionString, Type type, Type parametersType, Type[] otherTypes, int gridIndex)
      {
        this.sql = sql;
        this.commandType = commandType;
        this.connectionString = connectionString;
        this.type = type;
        this.parametersType = parametersType;
        this.gridIndex = gridIndex;
        this.hashCode = 17;
        this.hashCode = this.hashCode * 23 + commandType.GetHashCode();
        this.hashCode = this.hashCode * 23 + gridIndex.GetHashCode();
        this.hashCode = this.hashCode * 23 + (sql == null ? 0 : sql.GetHashCode());
        this.hashCode = this.hashCode * 23 + (type == (Type) null ? 0 : type.GetHashCode());
        if (otherTypes != null)
        {
          foreach (Type type1 in otherTypes)
            this.hashCode = this.hashCode * 23 + (type1 == (Type) null ? 0 : type1.GetHashCode());
        }
        this.hashCode = this.hashCode * 23 + (connectionString == null ? 0 : connectionString.GetHashCode());
        this.hashCode = this.hashCode * 23 + (parametersType == (Type) null ? 0 : parametersType.GetHashCode());
      }

      internal SqlMapper.Identity ForGrid(Type primaryType, int gridIndex)
      {
        return new SqlMapper.Identity(this.sql, this.commandType, this.connectionString, primaryType, this.parametersType, (Type[]) null, gridIndex);
      }

      internal SqlMapper.Identity ForGrid(Type primaryType, Type[] otherTypes, int gridIndex)
      {
        return new SqlMapper.Identity(this.sql, this.commandType, this.connectionString, primaryType, this.parametersType, otherTypes, gridIndex);
      }

      /// <summary>
      /// Create an identity for use with DynamicParameters, internal use only
      /// 
      /// </summary>
      /// <param name="type"/>
      /// <returns/>
      public SqlMapper.Identity ForDynamicParameters(Type type)
      {
        return new SqlMapper.Identity(this.sql, this.commandType, this.connectionString, this.type, type, (Type[]) null, -1);
      }

      /// <summary/>
      /// <param name="obj"/>
      /// <returns/>
      public override bool Equals(object obj)
      {
        return this.Equals(obj as SqlMapper.Identity);
      }

      /// <summary/>
      /// 
      /// <returns/>
      public override int GetHashCode()
      {
        return this.hashCode;
      }

      /// <summary>
      /// Compare 2 Identity objects
      /// 
      /// </summary>
      /// <param name="other"/>
      /// <returns/>
      public bool Equals(SqlMapper.Identity other)
      {
        if (other != null && this.gridIndex == other.gridIndex && (this.type == other.type && this.sql == other.sql))
        {
          CommandType? nullable1 = this.commandType;
          CommandType? nullable2 = other.commandType;
          if ((nullable1.GetValueOrDefault() != nullable2.GetValueOrDefault() ? 0 : (nullable1.HasValue == nullable2.HasValue ? 1 : 0)) != 0 && this.connectionString == other.connectionString)
            return this.parametersType == other.parametersType;
        }
        return false;
      }
    }

    private class DontMap
    {
    }

    private class FastExpando : DynamicObject, IDictionary<string, object>, ICollection<KeyValuePair<string, object>>, IEnumerable<KeyValuePair<string, object>>, IEnumerable
    {
      private IDictionary<string, object> data;

      ICollection<string> IDictionary<string, object>.Keys
      {
        get
        {
          return this.data.Keys;
        }
      }

      ICollection<object> IDictionary<string, object>.Values
      {
        get
        {
          return this.data.Values;
        }
      }

      int ICollection<KeyValuePair<string, object>>.Count
      {
        get
        {
          return this.data.Count;
        }
      }

      bool ICollection<KeyValuePair<string, object>>.IsReadOnly
      {
        get
        {
          return true;
        }
      }

      public static SqlMapper.FastExpando Attach(IDictionary<string, object> data)
      {
        return new SqlMapper.FastExpando()
        {
          data = data
        };
      }

      public override bool TrySetMember(SetMemberBinder binder, object value)
      {
        this.data[binder.Name] = value;
        return true;
      }

      public override bool TryGetMember(GetMemberBinder binder, out object result)
      {
        return this.data.TryGetValue(binder.Name, out result);
      }

      public override IEnumerable<string> GetDynamicMemberNames()
      {
        return (IEnumerable<string>) this.data.Keys;
      }

      void IDictionary<string, object>.Add(string key, object value)
      {
        throw new NotImplementedException();
      }

      bool IDictionary<string, object>.ContainsKey(string key)
      {
        return this.data.ContainsKey(key);
      }

      bool IDictionary<string, object>.Remove(string key)
      {
        throw new NotImplementedException();
      }

      bool IDictionary<string, object>.TryGetValue(string key, out object value)
      {
        return this.data.TryGetValue(key, out value);
      }

      object IDictionary<string, object>.get_Item(string key)
      {
        return this.data[key];
      }

      void IDictionary<string, object>.set_Item(string key, object value)
      {
        if (!this.data.ContainsKey(key))
          throw new NotImplementedException();
        this.data[key] = value;
      }

      void ICollection<KeyValuePair<string, object>>.Add(KeyValuePair<string, object> item)
      {
        throw new NotImplementedException();
      }

      void ICollection<KeyValuePair<string, object>>.Clear()
      {
        throw new NotImplementedException();
      }

      bool ICollection<KeyValuePair<string, object>>.Contains(KeyValuePair<string, object> item)
      {
        return this.data.Contains(item);
      }

      void ICollection<KeyValuePair<string, object>>.CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
      {
        this.data.CopyTo(array, arrayIndex);
      }

      bool ICollection<KeyValuePair<string, object>>.Remove(KeyValuePair<string, object> item)
      {
        throw new NotImplementedException();
      }

      IEnumerator<KeyValuePair<string, object>> IEnumerable<KeyValuePair<string, object>>.GetEnumerator()
      {
        return this.data.GetEnumerator();
      }

      IEnumerator IEnumerable.GetEnumerator()
      {
        return (IEnumerator) this.data.GetEnumerator();
      }
    }

    /// <summary>
    /// The grid reader provides interfaces for reading multiple result sets from a Dapper query
    /// 
    /// </summary>
    public class GridReader : IDisposable
    {
      private IDataReader reader;
      private IDbCommand command;
      private SqlMapper.Identity identity;
      private int gridIndex;
      private bool consumed;

      internal GridReader(IDbCommand command, IDataReader reader, SqlMapper.Identity identity)
      {
        this.command = command;
        this.reader = reader;
        this.identity = identity;
      }

      /// <summary>
      /// Read the next grid of results, returned as a dynamic object
      /// 
      /// </summary>
      public IEnumerable<object> Read()
      {
        return (IEnumerable<object>) this.Read<SqlMapper.FastExpando>();
      }

      /// <summary>
      /// Read the next grid of results
      /// 
      /// </summary>
      public IEnumerable<T> Read<T>()
      {
        if (this.reader == null)
          throw new ObjectDisposedException(this.GetType().Name);
        if (this.consumed)
          throw new InvalidOperationException("Each grid can only be iterated once");
        SqlMapper.Identity identity = this.identity.ForGrid(typeof (T), this.gridIndex);
        SqlMapper.CacheInfo cacheInfo = SqlMapper.GetCacheInfo(identity);
        SqlMapper.DeserializerState deserializerState = cacheInfo.Deserializer;
        int columnHash = SqlMapper.GetColumnHash(this.reader);
        if (deserializerState.Func == null || deserializerState.Hash != columnHash)
        {
          deserializerState = new SqlMapper.DeserializerState(columnHash, SqlMapper.GetDeserializer(typeof (T), this.reader, 0, -1, false));
          cacheInfo.Deserializer = deserializerState;
        }
        this.consumed = true;
        return this.ReadDeferred<T>(this.gridIndex, deserializerState.Func, identity);
      }

      private IEnumerable<TReturn> MultiReadInternal<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>(object func, string splitOn)
      {
        // ISSUE: reference to a compiler-generated field
        this.\u003Cidentity\u003E5__60 = this.identity.ForGrid(typeof (TReturn), new Type[5]
        {
          typeof (TFirst),
          typeof (TSecond),
          typeof (TThird),
          typeof (TFourth),
          typeof (TFifth)
        }, this.gridIndex);
        try
        {
          SqlMapper.Identity identity;
          foreach (TReturn @return in SqlMapper.MultiMapImpl<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>((IDbConnection) null, (string) null, func, (object) null, (IDbTransaction) null, splitOn, new int?(), new CommandType?(), this.reader, identity))
            yield return @return;
        }
        finally
        {
          this.NextResult();
        }
      }

      /// <summary>
      /// Read multiple objects from a single recordset on the grid
      /// 
      /// </summary>
      /// <typeparam name="TFirst"/><typeparam name="TSecond"/><typeparam name="TReturn"/><param name="func"/><param name="splitOn"/>
      /// <returns/>
      public IEnumerable<TReturn> Read<TFirst, TSecond, TReturn>(Func<TFirst, TSecond, TReturn> func, string splitOn = "id")
      {
        return this.MultiReadInternal<TFirst, TSecond, SqlMapper.DontMap, SqlMapper.DontMap, SqlMapper.DontMap, TReturn>((object) func, splitOn);
      }

      /// <summary>
      /// Read multiple objects from a single recordset on the grid
      /// 
      /// </summary>
      /// <typeparam name="TFirst"/><typeparam name="TSecond"/><typeparam name="TThird"/><typeparam name="TReturn"/><param name="func"/><param name="splitOn"/>
      /// <returns/>
      public IEnumerable<TReturn> Read<TFirst, TSecond, TThird, TReturn>(Func<TFirst, TSecond, TThird, TReturn> func, string splitOn = "id")
      {
        return this.MultiReadInternal<TFirst, TSecond, TThird, SqlMapper.DontMap, SqlMapper.DontMap, TReturn>((object) func, splitOn);
      }

      /// <summary>
      /// Read multiple objects from a single record set on the grid
      /// 
      /// </summary>
      /// <typeparam name="TFirst"/><typeparam name="TSecond"/><typeparam name="TThird"/><typeparam name="TFourth"/><typeparam name="TReturn"/><param name="func"/><param name="splitOn"/>
      /// <returns/>
      public IEnumerable<TReturn> Read<TFirst, TSecond, TThird, TFourth, TReturn>(Func<TFirst, TSecond, TThird, TFourth, TReturn> func, string splitOn = "id")
      {
        return this.MultiReadInternal<TFirst, TSecond, TThird, TFourth, SqlMapper.DontMap, TReturn>((object) func, splitOn);
      }

      /// <summary>
      /// Read multiple objects from a single record set on the grid
      /// 
      /// </summary>
      /// <typeparam name="TFirst"/><typeparam name="TSecond"/><typeparam name="TThird"/><typeparam name="TFourth"/><typeparam name="TFifth"/><typeparam name="TReturn"/><param name="func"/><param name="splitOn"/>
      /// <returns/>
      public IEnumerable<TReturn> Read<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>(Func<TFirst, TSecond, TThird, TFourth, TFifth, TReturn> func, string splitOn = "id")
      {
        return this.MultiReadInternal<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>((object) func, splitOn);
      }

      private IEnumerable<T> ReadDeferred<T>(int index, Func<IDataReader, object> deserializer, SqlMapper.Identity typedIdentity)
      {
        try
        {
          while (index == this.gridIndex && this.reader.Read())
            yield return (T) deserializer(this.reader);
        }
        finally
        {
          if (index == this.gridIndex)
            this.NextResult();
        }
      }

      private void NextResult()
      {
        if (this.reader.NextResult())
        {
          ++this.gridIndex;
          this.consumed = false;
        }
        else
          this.Dispose();
      }

      /// <summary>
      /// Dispose the grid, closing and disposing both the underlying reader and command.
      /// 
      /// </summary>
      public void Dispose()
      {
        if (this.reader != null)
        {
          this.reader.Dispose();
          this.reader = (IDataReader) null;
        }
        if (this.command == null)
          return;
        this.command.Dispose();
        this.command = (IDbCommand) null;
      }
    }
  }
}
