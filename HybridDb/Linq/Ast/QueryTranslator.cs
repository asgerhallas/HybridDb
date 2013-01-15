using System.Linq.Expressions;

namespace HybridDb.Linq.Ast
{
    internal class QueryTranslator
    {
        public Translation Translate(Expression expression)
        {
            expression = new StripQuotesVisitor().Visit(expression);
            var sqlSelectExpression = new ClauseExtractionVisitor().Translate(expression);
            return new Translation();
        }
    }
}