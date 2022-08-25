using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using Dapper;
using HybridDb.Config;

namespace HybridDb
{
    public class JsonTypeHandler<T> : SqlMapper.TypeHandler<T>
    {
        readonly Configuration configuration;

        public JsonTypeHandler(Configuration configuration)
        {
            this.configuration = configuration;
        }

        public override void SetValue(IDbDataParameter parameter, T value)
        {
            //logic for serializing is in DocumentDesigner class
        }

        public override T Parse(object value)
        {
            return (T)configuration.Serializer.Deserialize(value.ToString(), typeof(T));
        }
    }
}
