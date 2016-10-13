using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using HybridDb.Linq2.Ast;

namespace HybridDb.Linq.Parsers
{
    internal class OrderByVisitor : LambdaParser
    {
        public OrderByVisitor(Func<Type, string> getTableNameForType, Stack<AstNode> ast) : base(getTableNameForType, ast) {}

        public static ColumnName Translate(Func<Type, string> getTableNameForType, Expression expression)
        {
            var ast = new Stack<AstNode>();
            new OrderByVisitor(getTableNameForType, ast).Visit(expression);
            return (ColumnName) ast.Pop();
        }
    }
}