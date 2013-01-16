using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace HybridDb.Linq.Ast
{
    public class QueryTranslator
    {
        public Translation Translate(Expression expression)
        {
            var selectOperations = new Stack<Operation>();
            var whereOperations = new Stack<Operation>();
            var queryVisitor = new QueryVisitor(selectOperations);

            queryVisitor.Visit(expression);

            var selectSql = new StringBuilder();
            if (selectOperations.Count > 0)
            {
                var select = selectOperations.ParseToSqlExpression();
                new SqlExpressionTranslator(selectSql).Visit(select);
            }

            var whereSql = new StringBuilder();
            //if (whereOperations.Count > 0)
            //{
            //    var where = whereOperations.ParseToSqlExpression();
            //    new SqlExpressionTranslator(whereSql).Visit(@where);
            //}
            new SqlExpressionTranslator(selectSql).Visit(queryVisitor.Where);

            return new Translation
            {
                Select = selectSql.ToString() ?? "",
                Where = selectSql.ToString() ?? "",
                Skip = queryVisitor.Skip,
                Take = queryVisitor.Take
            };
        }
    }

    internal class QueryVisitor : ExpressionVisitor
    {
        readonly Stack<Operation> selectOperations;
        readonly Stack<Operation> whereOperations;

        int skip;
        int take;
        SqlExpression orderBy;
        SqlExpression select;
        SqlExpression where;

        public SqlExpression Where
        {
            get { return @where; }
        }

        public QueryVisitor(Stack<Operation> selectOperations)
        {
            this.selectOperations = selectOperations;
            this.whereOperations = whereOperations;
        }

        public int Skip
        {
            get { return skip; }
        }

        public int Take
        {
            get { return take; }
        }

        protected override Expression VisitMethodCall(MethodCallExpression expression)
        {
            switch (expression.Method.Name)
            {
                case "Select":
                    new SelectVisitor(selectOperations).Visit(expression.Arguments[1]);
                    break;
                case "Where":
                    where = WhereVisitor2.Translate(expression.Arguments[1]);
                    break;
                case "Skip":
                    skip = (int) ((ConstantExpression) expression.Arguments[1]).Value;
                    break;
                case "Take":
                    take = (int) ((ConstantExpression) expression.Arguments[1]).Value;
                    break;
                case "OfType":
                    // Change of type is done else where
                    break;
                default:
                    throw new NotSupportedException(string.Format("The method {0} is not supported", expression.Method.Name));
            }

            Visit(expression.Arguments[0]);
            return expression;
        }
    }
}