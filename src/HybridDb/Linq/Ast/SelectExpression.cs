using HybridDb.Linq2.Ast;

namespace HybridDb.Linq.Ast
{
    public class SelectExpression : SqlExpression
    {
        public SelectExpression(SqlExpression expression, string alias)
        {
            Expression = expression;
            Alias = alias;
        }

        public SqlExpression Expression { get; private set; }
        public string Alias { get; private set; }
    }
}