using System;
using System.Collections.Generic;

namespace HybridDb.Linq.Bonsai
{
    public abstract class BonsaiExpression
    {
        protected BonsaiExpression(Type type) => Type = type;

        public Type Type { get; }

        public T AssertNotNull<T>(T value) where T : class => value ?? throw new ArgumentNullException();
    }

    public class QueryExpression : BonsaiExpression
    {
        public QueryExpression(Type type) : base(type)
        {
        }

        public ExecutionSemantics ExecutionMethod { get; set; }
        public BonsaiExpression Select { get; set; }
        public BonsaiExpression Where { get; set; }
        public Window Window { get; set; }
        public BonsaiExpression OrderBy { get; set; }

        public IDictionary<string, object> Parameters { get; set; }

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