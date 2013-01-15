using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace HybridDb.Linq.Ast
{
    public class QueryTranslator
    {
        public Translation Translate(Expression expression)
        {
            var whereOperations = new Stack<Operation>();
            new QueryVisitor(whereOperations).Visit(expression);
            var where = whereOperations.ParseToSqlExpression();
            return new Translation()
            {};
        }
    }

    internal class QueryVisitor : ExpressionVisitor
    {
        readonly Stack<Operation> whereOperations;

        SqlExpression orderBy;
        SqlExpression select;
        SqlWhereExpression where;

        public QueryVisitor(Stack<Operation> whereOperations)
        {
            this.whereOperations = whereOperations;
        }

        protected override Expression VisitMethodCall(MethodCallExpression expression)
        {
            switch (expression.Method.Name)
            {
                case "Where":
                    new WhereVisitor(whereOperations).Visit(expression.Arguments[1]);
                    break;
                case "Select":
                    select = new SelectVisitor().Translate(expression.Arguments[1]);
                    break;
                default:
                    throw new NotSupportedException(string.Format("The method {0} is not supported", expression.Method.Name));
            }

            Visit(expression.Arguments[0]);
            return expression;
        }
    }

    internal class SelectVisitor : ExpressionVisitor
    {
        public SqlWhereExpression Translate(Expression expression)
        {
            return null;
        }
    }
}