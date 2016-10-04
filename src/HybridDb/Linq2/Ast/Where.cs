namespace HybridDb.Linq2.Ast
{
    public class Where : SqlClause
    {
        public Where(Predicate predicate)
        {
            Predicate = predicate;
        }

        public Predicate Predicate { get; }
    }
}