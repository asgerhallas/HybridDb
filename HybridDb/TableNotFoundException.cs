using System;

namespace HybridDb
{
    public class TableNotFoundException : Exception
    {
        public TableNotFoundException(Type type) 
            : base(string.Format("No table was registered for type {0}. " +
                                 "Please run store.ForDocument<{0}>() to register it before use.", type.Name))
        {
        }
    }
}