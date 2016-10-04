using System.Collections.Generic;
using System.Linq.Expressions;
using HybridDb.Linq2.Ast;

namespace HybridDb.Linq.Parsers
{
    internal class OrderByVisitor : LambdaParser
    {
        public OrderByVisitor(Stack<AstNode> ast) : base(ast) {}

        public static ColumnIdentifier Translate(Expression expression)
        {
            var ast = new Stack<AstNode>();
            new OrderByVisitor(ast).Visit(expression);
            return (ColumnIdentifier) ast.Pop();
        }
    }
}