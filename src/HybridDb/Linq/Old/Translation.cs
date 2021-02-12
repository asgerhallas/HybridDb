using System;
using System.Collections.Generic;

namespace HybridDb.Linq.Old
{
    public class Translation
    {
        public string Select { get; set; }
        public string Where { get; set; }
        public bool Top1 { get; set; }
        public Window Window { get; set; }
        public string OrderBy { get; set; }
        public IDictionary<string, object> Parameters { get; set; }
        public ExecutionSemantics ExecutionMethod { get; set; }
        public Type ProjectAs { get; set; }

        public enum ExecutionSemantics
        {
            Single,
            SingleOrDefault,
            First,
            FirstOrDefault,
            Enumeration
        }
    }
}