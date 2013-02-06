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

    public class MissingProjectedColumnException : HybridDbException
    {
        public MissingProjectedColumnException(string columnName, Exception innerException)
            : base(string.Format("Could not find column '{0}'. In order to query and only return a few selected fields from a document, " +
                                 "those values must be projected from the document to a column. This is required to avoid deserializing the complete " +
                                 "document when it is not needed. You can configure a projection like this: store.ForDocument<TypeOfEntity>().Projection(x => x.TheFieldToProject); " +
                                 "Remember to migrate your database and update the projections when reconfigurering the store.", columnName)
            , innerException) {}
    }
}