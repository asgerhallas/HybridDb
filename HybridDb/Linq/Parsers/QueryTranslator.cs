using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace HybridDb.Linq.Parsers
{
    public class QueryTranslator
    {
        public Translation Translate(Expression expression)
        {
            var queryVisitor = new QueryParser();
            var parameters = new Dictionary<string, object>();

            queryVisitor.Visit(expression);

            var selectSql = new StringBuilder();
            if (queryVisitor.Select != null)
                new SqlExpressionTranslator(selectSql, parameters).Visit(queryVisitor.Select);

            var whereSql = new StringBuilder();
            if (queryVisitor.Where != null)
                new SqlExpressionTranslator(whereSql, parameters).Visit(queryVisitor.Where);

            var orderBySql = new StringBuilder();
            if (queryVisitor.OrderBy != null)
                new SqlExpressionTranslator(orderBySql, parameters).Visit(queryVisitor.OrderBy);

            return new Translation
            {
                Select = selectSql.ToString(),
                Where = whereSql.ToString(),
                OrderBy = orderBySql.ToString(),
                Skip = queryVisitor.Skip,
                Take = queryVisitor.Take,
                Parameters = parameters,
                ExecutionMethod = queryVisitor.Execution
            };
        }
    }
}