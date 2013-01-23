using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using HybridDb.Linq.Ast;

namespace HybridDb.Linq.Parsers
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