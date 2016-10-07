using HybridDb.Linq.Ast;

namespace HybridDb.Linq2.Ast
{
    public class True : Comparison
    {
        public True() : base(
            ComparisonOperator.Equal, 
            new Constant(typeof(int), 1), 
            new Constant(typeof(int), 1)) {}
    }
}