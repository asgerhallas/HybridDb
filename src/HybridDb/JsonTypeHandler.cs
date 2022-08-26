using System;
using System.Data;
using Dapper;

namespace HybridDb
{
    public class JsonTypeHandler<T> : SqlMapper.TypeHandler<T>
    {
        readonly ISerializer serializer;

        public JsonTypeHandler(ISerializer serializer) => this.serializer = serializer;

        public override void SetValue(IDbDataParameter parameter, T value) => throw new NotSupportedException("Logic for serializing is in DocumentDesigner class");

        public override T Parse(object value) => (T)serializer.Deserialize(value.ToString(), typeof(T));
    }
}
