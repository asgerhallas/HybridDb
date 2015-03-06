using System;
using System.Runtime.Serialization;
using HybridDb.Config;

namespace HybridDb
{
    public class HybridDbException : Exception
    {
        public HybridDbException(string message) : base(message) {}
        public HybridDbException(string message, Exception innerException) : base(message, innerException) {}
        public HybridDbException(SerializationInfo info, StreamingContext context) : base(info, context) {}
    }

    public class ColumnAlreadRegisteredException : HybridDbException
    {
        public ColumnAlreadRegisteredException(Table table, Column column)
            : base(string.Format("The table {0} already has a column named {1} and is not of the same type.", table.Name, column.Name)) {}
    }
}