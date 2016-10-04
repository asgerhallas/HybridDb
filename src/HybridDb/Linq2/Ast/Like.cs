namespace HybridDb.Linq2.Ast
{
    public class Like : Predicate
    {
        //TODO: to be sql-agnostic pattern as a nested language could have an ast to to indicate what is supported
        public Like(SqlExpression left, string pattern)
        {
            Left = left;
            Pattern = pattern;
        }

        public SqlExpression Left { get; }
        public string Pattern { get; }
    }
}