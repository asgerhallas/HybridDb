namespace HybridDb.Linq2.Ast
{
    public class Logical : Predicate
    {
        public Logical(LogicalOperator @operator, Predicate left, Predicate right)
        {
            Operator = @operator;
            Left = left;
            Right = right;
        }

        public LogicalOperator Operator { get; }
        public Predicate Left { get; }
        public Predicate Right { get; }
    }
}