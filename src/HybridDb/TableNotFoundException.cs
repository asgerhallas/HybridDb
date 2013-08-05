using System;
using System.Runtime.Serialization;
using HybridDb.Schema;

namespace HybridDb
{
    public abstract class HybridDbException : Exception
    {
        protected HybridDbException(string message) : base(message) {}
        protected HybridDbException(string message, Exception innerException) : base(message, innerException) {}
        protected HybridDbException(SerializationInfo info, StreamingContext context) : base(info, context) {}
    }

    public class TableNotFoundException : HybridDbException
    {
        public TableNotFoundException(Type type)
            : base(string.Format("No table was registered for type {0}. " +
                                 "Please run store.ForDocument<{0}>() to register it before use.", type.Name)) {}
    }

    public class ColumnAlreadRegisteredException : HybridDbException
    {
        public ColumnAlreadRegisteredException(Table table, Column column)
            : base(string.Format("The table {0} already has a column named {1} and is not of the same type.", table.Name, column.Name)) {}
    }
}