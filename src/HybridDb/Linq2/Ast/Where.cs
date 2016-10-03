namespace HybridDb.Linq2.Ast
{
    public class Where : Clause
    {
        public Where(Predicate predicate)
        {
            Predicate = predicate;
        }

        public Predicate Predicate { get; }
    }
}