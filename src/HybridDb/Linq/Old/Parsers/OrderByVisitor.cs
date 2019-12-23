using System.Collections.Generic;
using System.Linq.Expressions;
using HybridDb.Linq.Old.Ast;

namespace HybridDb.Linq.Old.Parsers
{
    internal class OrderByVisitor : LambdaParser
    {
        public OrderByVisitor(Stack<SqlExpression> ast) : base(ast) {}

        public static SqlColumnExpression Translate(Expression expression)
        {
            var ast = new Stack<SqlExpression>();
            new OrderByVisitor(ast).Visit(expression);
            return (SqlColumnExpression) ast.Pop();
        }
    }
}