using System;
using HybridDb.Linq2.Ast;

namespace HybridDb.Linq.Ast
{
    public class Constant : Expression
    {
        public Constant(Type type, object value)
        {
            Type = type;
            Value = value;
        }

        public Type Type { get; set; }
        public object Value { get; set; }
    }
}