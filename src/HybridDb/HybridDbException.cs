using System;
using System.Runtime.Serialization;

namespace HybridDb
{
    public class HybridDbException : Exception
    {
        public HybridDbException(string message) : base(message) {}
        public HybridDbException(string message, Exception innerException) : base(message, innerException) {}
        public HybridDbException(SerializationInfo info, StreamingContext context) : base(info, context) {}
    }
}