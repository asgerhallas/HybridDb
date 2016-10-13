namespace HybridDb.Linq2.Ast
{
    public class Where : SqlClause
    {
        public Where(Predicate condition)
        {
            Condition = condition;
        }

        public Predicate Condition { get; }
    }
}