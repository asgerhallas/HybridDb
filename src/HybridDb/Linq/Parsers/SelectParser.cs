using System.Collections.Generic;
using System.Linq.Expressions;
using HybridDb.Linq.Ast;
using HybridDb.Linq2.Ast;

namespace HybridDb.Linq.Parsers
{
    internal class SelectParser : LambdaParser
    {
        public SelectParser(Stack<AstNode> ast) : base(ast) { }

        public static Select Translate(Expression expression)
        {
            var operations = new Stack<AstNode>();
            new SelectParser(operations).Visit(expression);
            return (Select)operations.Pop();
        }
        
        protected override Expression VisitNew(NewExpression expression)
        {
            var projections = new SelectExpression[expression.Arguments.Count];
            for (int i = 0; i < expression.Arguments.Count; i++)
            {
                Visit(expression.Arguments[i]);
                projections[i] = new SelectExpression((ColumnIdentifier) ast.Pop(), expression.Members[i].Name);
            }

            ast.Push(new Select(projections));
            return expression;
        }

        protected override Expression VisitMemberInit(MemberInitExpression expression)
        {
            var projections = new List<SelectExpression>();
            foreach (var memberBinding in expression.Bindings)
            {
                var property = memberBinding as MemberAssignment;
                if (property == null)
                    continue;

                Visit(property.Expression);
                projections.Add(new SelectExpression((ColumnIdentifier)ast.Pop(), property.Member.Name));
            }

            ast.Push(new Select(projections.ToArray()));

            return expression;
        }
    }
}