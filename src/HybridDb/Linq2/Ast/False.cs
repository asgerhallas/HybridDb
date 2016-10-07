using HybridDb.Linq.Ast;

namespace HybridDb.Linq2.Ast
{
    public class False : Comparison
    {
        public False() : base(
            ComparisonOperator.NotEqual, 
            new Constant(typeof(int), 1), 
            new Constant(typeof(int), 1)) { }
    }
}