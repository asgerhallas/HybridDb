// Type: Dapper.DynamicParameters
// Assembly: Dapper, Version=1.12.0.0, Culture=neutral, PublicKeyToken=null
// Assembly location: c:\workspace\HybridDb\packages\Dapper.1.12.1\lib\net40\Dapper.dll

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Dapper
{
  /// <summary>
  /// A bag of parameters that can be passed to the Dapper Query and Execute methods
  /// 
  /// </summary>
  public class DynamicParameters : SqlMapper.IDynamicParameters
  {
    private static Dictionary<SqlMapper.Identity, Action<IDbCommand, object>> paramReaderCache = new Dictionary<SqlMapper.Identity, Action<IDbCommand, object>>();
    private Dictionary<string, DynamicParameters.ParamInfo> parameters = new Dictionary<string, DynamicParameters.ParamInfo>();
    private List<object> templates;

    /// <summary>
    /// All the names of the param in the bag, use Get to yank them out
    /// 
    /// </summary>
    public IEnumerable<string> ParameterNames
    {
      get
      {
        return Enumerable.Select<KeyValuePair<string, DynamicParameters.ParamInfo>, string>((IEnumerable<KeyValuePair<string, DynamicParameters.ParamInfo>>) this.parameters, (Func<KeyValuePair<string, DynamicParameters.ParamInfo>, string>) (p => p.Key));
      }
    }

    static DynamicParameters()
    {
    }

    /// <summary>
    /// construct a dynamic parameter bag
    /// 
    /// </summary>
    public DynamicParameters()
    {
    }

    /// <summary>
    /// construct a dynamic parameter bag
    /// 
    /// </summary>
    /// <param name="template">can be an anonymous type or a DynamicParameters bag</param>
    public DynamicParameters(object template)
    {
      this.AddDynamicParams(template);
    }

    /// <summary>
    /// Append a whole object full of params to the dynamic
    ///             EG: AddDynamicParams(new {A = 1, B = 2}) // will add property A and B to the dynamic
    /// 
    /// </summary>
    /// <param name="param"/>
    public void AddDynamicParams(object param)
    {
      object obj1 = param;
      if (obj1 == null)
        return;
      DynamicParameters dynamicParameters = obj1 as DynamicParameters;
      if (dynamicParameters == null)
      {
        IEnumerable<KeyValuePair<string, object>> enumerable = obj1 as IEnumerable<KeyValuePair<string, object>>;
        if (enumerable == null)
        {
          this.templates = this.templates ?? new List<object>();
          this.templates.Add(obj1);
        }
        else
        {
          foreach (KeyValuePair<string, object> keyValuePair in enumerable)
            this.Add(keyValuePair.Key, keyValuePair.Value, new DbType?(), new ParameterDirection?(), new int?());
        }
      }
      else
      {
        if (dynamicParameters.parameters != null)
        {
          foreach (KeyValuePair<string, DynamicParameters.ParamInfo> keyValuePair in dynamicParameters.parameters)
            this.parameters.Add(keyValuePair.Key, keyValuePair.Value);
        }
        if (dynamicParameters.templates == null)
          return;
        this.templates = this.templates ?? new List<object>();
        foreach (object obj2 in dynamicParameters.templates)
          this.templates.Add(obj2);
      }
    }

    /// <summary>
    /// Add a parameter to this dynamic parameter list
    /// 
    /// </summary>
    /// <param name="name"/><param name="value"/><param name="dbType"/><param name="direction"/><param name="size"/>
    public void Add(string name, object value = null, DbType? dbType = null, ParameterDirection? direction = null, int? size = null)
    {
      Dictionary<string, DynamicParameters.ParamInfo> dictionary = this.parameters;
      string index = DynamicParameters.Clean(name);
      DynamicParameters.ParamInfo paramInfo1 = new DynamicParameters.ParamInfo();
      paramInfo1.Name = name;
      paramInfo1.Value = value;
      DynamicParameters.ParamInfo paramInfo2 = paramInfo1;
      ParameterDirection? nullable = direction;
      int num = nullable.HasValue ? (int) nullable.GetValueOrDefault() : 1;
      paramInfo2.ParameterDirection = (ParameterDirection) num;
      paramInfo1.DbType = dbType;
      paramInfo1.Size = size;
      DynamicParameters.ParamInfo paramInfo3 = paramInfo1;
      dictionary[index] = paramInfo3;
    }

    private static string Clean(string name)
    {
      if (!string.IsNullOrEmpty(name))
      {
        switch (name[0])
        {
          case ':':
          case '?':
          case '@':
            return name.Substring(1);
        }
      }
      return name;
    }

    void SqlMapper.IDynamicParameters.AddParameters(IDbCommand command, SqlMapper.Identity identity)
    {
      this.AddParameters(command, identity);
    }

    /// <summary>
    /// Add all the parameters needed to the command just before it executes
    /// 
    /// </summary>
    /// <param name="command">The raw command prior to execution</param><param name="identity">Information about the query</param>
    protected void AddParameters(IDbCommand command, SqlMapper.Identity identity)
    {
      if (this.templates != null)
      {
        foreach (object obj in this.templates)
        {
          SqlMapper.Identity index = identity.ForDynamicParameters(obj.GetType());
          Action<IDbCommand, object> paramInfoGenerator;
          lock (DynamicParameters.paramReaderCache)
          {
            if (!DynamicParameters.paramReaderCache.TryGetValue(index, out paramInfoGenerator))
            {
              paramInfoGenerator = SqlMapper.CreateParamInfoGenerator(index);
              DynamicParameters.paramReaderCache[index] = paramInfoGenerator;
            }
          }
          paramInfoGenerator(command, obj);
        }
      }
      foreach (DynamicParameters.ParamInfo paramInfo in this.parameters.Values)
      {
        string parameterName = DynamicParameters.Clean(paramInfo.Name);
        bool flag = !command.Parameters.Contains(parameterName);
        IDbDataParameter dbDataParameter;
        if (flag)
        {
          dbDataParameter = command.CreateParameter();
          dbDataParameter.ParameterName = parameterName;
        }
        else
          dbDataParameter = (IDbDataParameter) command.Parameters[parameterName];
        object obj = paramInfo.Value;
        dbDataParameter.Value = obj ?? (object) DBNull.Value;
        dbDataParameter.Direction = paramInfo.ParameterDirection;
        string str = obj as string;
        if (str != null && str.Length <= 4000)
          dbDataParameter.Size = 4000;
        if (paramInfo.Size.HasValue)
          dbDataParameter.Size = paramInfo.Size.Value;
        if (paramInfo.DbType.HasValue)
          dbDataParameter.DbType = paramInfo.DbType.Value;
        if (flag)
          command.Parameters.Add((object) dbDataParameter);
        paramInfo.AttachedParam = dbDataParameter;
      }
    }

    /// <summary>
    /// Get the value of a parameter
    /// 
    /// </summary>
    /// <typeparam name="T"/><param name="name"/>
    /// <returns>
    /// The value, note DBNull.Value is not returned, instead the value is returned as null
    /// </returns>
    public T Get<T>(string name)
    {
      object obj = this.parameters[DynamicParameters.Clean(name)].AttachedParam.Value;
      if (obj != DBNull.Value)
        return (T) obj;
      if ((object) default (T) != null)
        throw new ApplicationException("Attempting to cast a DBNull to a non nullable type!");
      else
        return default (T);
    }

    private class ParamInfo
    {
      public string Name { get; set; }

      public object Value { get; set; }

      public ParameterDirection ParameterDirection { get; set; }

      public DbType? DbType { get; set; }

      public int? Size { get; set; }

      public IDbDataParameter AttachedParam { get; set; }
    }
  }
}
