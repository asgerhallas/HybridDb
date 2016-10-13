using System.Collections.Generic;
using HybridDb.Linq.Ast;

namespace HybridDb.Linq2.Ast
{
    public class Like : Predicate
    {
        public Like(SqlExpression left, params Either<Constant, Wildcard>[] pattern)
        {
            Left = left;
            Pattern = pattern;
        }

        public SqlExpression Left { get; }
        public IReadOnlyList<Either<Constant, Wildcard>> Pattern { get; }
    }

    public class Wildcard
    {
        public Wildcard(WildcardOperator @operator)
        {
            Operator = @operator;
        }

        public WildcardOperator Operator { get; }
    }

    public enum WildcardOperator
    {
        OneOrMore,
        ExactlyOne
    }
}