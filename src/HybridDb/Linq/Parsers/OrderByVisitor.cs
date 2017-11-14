using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using HybridDb.Linq2.Ast;

namespace HybridDb.Linq.Parsers
{
    internal class OrderByVisitor : LambdaParser
    {
        public OrderByVisitor(Func<Type, string> getTableNameForType, Func<string, Type> getColumnTypeByName, Stack<AstNode> ast) 
            : base(getTableNameForType, getColumnTypeByName, ast) {}

        public static ColumnName Translate(Func<Type, string> getTableNameForType, Func<string, Type> getColumnTypeByName, Expression expression)
        {
            var ast = new Stack<AstNode>();
            new OrderByVisitor(getTableNameForType, getColumnTypeByName, ast).Visit(expression);
            return (ColumnName) ast.Pop();
        }
    }
}