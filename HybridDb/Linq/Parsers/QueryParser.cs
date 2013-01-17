using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;
using HybridDb.Linq.Ast;
using System.Linq;

namespace HybridDb.Linq.Parsers
{
    public class QueryTranslator
    {
        public Translation Translate(Expression expression)
        {
            var queryVisitor = new QueryParser();

            queryVisitor.Visit(expression);

            var selectSql = new StringBuilder();
            if (queryVisitor.Select != null)
                new SqlExpressionTranslator(selectSql).Visit(queryVisitor.Select);

            var whereSql = new StringBuilder();
            if (queryVisitor.Where != null)
                new SqlExpressionTranslator(whereSql).Visit(queryVisitor.Where);

            var orderBySql = new StringBuilder();
            if (queryVisitor.OrderBy != null)
                new SqlExpressionTranslator(orderBySql).Visit(queryVisitor.OrderBy);

            return new Translation
            {
                Select = selectSql.ToString(),
                Where = whereSql.ToString(),
                OrderBy = orderBySql.ToString(),
                Skip = queryVisitor.Skip,
                Take = queryVisitor.Take
            };
        }
    }

    internal class QueryParser : ExpressionVisitor
    {
        public int Skip { get; private set; }
        public int Take { get; private set; }
        public SqlExpression Select { get; private set; }
        public SqlExpression Where { get; private set; }
        public SqlOrderByExpression OrderBy { get; private set; }

        protected override Expression VisitMethodCall(MethodCallExpression expression)
        {
            Visit(expression.Arguments[0]);

            switch (expression.Method.Name)
            {
                case "Select":
                    Select = SelectParser.Translate(expression.Arguments[1]);
                    break;
                case "Where":
                    var whereExpression = WhereParser.Translate(expression.Arguments[1]);
                    Where = Where != null
                        ? new SqlBinaryExpression(SqlNodeType.And, Where, whereExpression)
                        : whereExpression;
                    break;
                case "Skip":
                    Skip = (int) ((ConstantExpression) expression.Arguments[1]).Value;
                    break;
                case "Take":
                    Take = (int) ((ConstantExpression) expression.Arguments[1]).Value;
                    break;
                case "OfType":
                    // Change of type is done else where
                    break;
                case "OrderBy":
                case "ThenBy":
                case "OrderByDescending":
                case "ThenByDescending":
                    var direction = expression.Method.Name.Contains("Descending") 
                        ? SqlOrderingExpression.Directions.Descending 
                        : SqlOrderingExpression.Directions.Ascending;
                    
                    var orderByColumnExpression = OrderByVisitor.Translate(expression.Arguments[1]);
                    var orderingExpression = new SqlOrderingExpression(direction, orderByColumnExpression);
                    OrderBy = OrderBy != null
                                  ? new SqlOrderByExpression(OrderBy.Columns.Concat(orderingExpression))
                                  : new SqlOrderByExpression(orderingExpression.AsEnumerable());
                    break;
                default:
                    throw new NotSupportedException(string.Format("The method {0} is not supported", expression.Method.Name));
            }
            return expression;
        }
    }
}