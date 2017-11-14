using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using HybridDb.Linq.Ast;
using HybridDb.Linq2.Ast;
using ShinySwitch;

namespace HybridDb.Linq.Parsers
{
    internal class SelectParser : LambdaParser
    {
        readonly Select previousSelect;

        public SelectParser(Func<Type, string> getTableNameForType, Func<string, Type> getColumnTypeByName, Select previousSelect, Stack<AstNode> ast) 
            : base(getTableNameForType, getColumnTypeByName, ast)
        {
            this.previousSelect = previousSelect;
        }

        public static Select Translate(Func<Type, string> getTableNameForType, Func<string, Type> getColumnTypeByName, Select previousSelect, Expression expression)
        {
            var operations = new Stack<AstNode>();
            new SelectParser(getTableNameForType, getColumnTypeByName, previousSelect, operations).Visit(expression);
            return (Select)operations.Pop();
        }

        protected override Expression VisitParameter(ParameterExpression expression)
        {
            if (previousSelect != null)
            {
                ast.Push(new TableName("anon"));
                return expression;
            }

            return base.VisitParameter(expression);
        }

        protected override Expression VisitMember(MemberExpression expression)
        {
            var ex = base.VisitMember(expression);

            if (previousSelect != null)
            {
                Switch.On(ast.Peek())
                    .Match<TypedColumnName>(x =>
                    {
                        var column = (TypedColumnName)ast.Pop();
                        var origin = previousSelect.SelectList.Single(s => s.Alias == column.Identifier);
                        ast.Push(new TypedColumnName(column.Type, origin.Column.TableName, origin.Column.Identifier));
                    })
                    .Else(x => { });

            }

            return ex;
        }

        protected override Expression VisitNew(NewExpression expression)
        {
            var projections = new SelectColumn[expression.Arguments.Count];
            for (int i = 0; i < expression.Arguments.Count; i++)
            {
                Visit(expression.Arguments[i]);
                projections[i] = new SelectColumn((ColumnName) ast.Pop(), expression.Members[i].Name);
            }

            ast.Push(new Select(projections));
            return expression;
        }

        protected override Expression VisitMemberInit(MemberInitExpression expression)
        {
            var projections = new List<SelectColumn>();
            foreach (var memberBinding in expression.Bindings)
            {
                var property = memberBinding as MemberAssignment;
                if (property == null)
                    continue;

                Visit(property.Expression);
                projections.Add(new SelectColumn((ColumnName)ast.Pop(), property.Member.Name));
            }

            ast.Push(new Select(projections.ToArray()));

            return expression;
        }
    }
}