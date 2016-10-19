using System;
using System.Runtime.Serialization;
using HybridDb.Config;

namespace HybridDb
{
    [Serializable]
    public class HybridDbException : Exception
    {
        public HybridDbException() { }
        public HybridDbException(string message) : base(message) {}
        public HybridDbException(string message, Exception innerException) : base(message, innerException) {}
        public HybridDbException(SerializationInfo info, StreamingContext context) : base(info, context) {}
    }

    public class ColumnAlreadRegisteredException : HybridDbException
    {
        public ColumnAlreadRegisteredException(Table table, Column column)
            : base($"The table {table.Name} already has a column named {column.Name} and is not of the same type.") {}
    }
}