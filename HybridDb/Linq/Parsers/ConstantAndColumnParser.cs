﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using HybridDb.Linq.Ast;

namespace HybridDb.Linq.Parsers
{
    /// <summary>
    /// AST
    /// ConstantPropagation
    /// UnaryBoolToBinary
    /// Constant == Constant = remove eller not
    /// Col == Constant(Null) = Col.IsNull() -> Visit omvendt, hvis de står omvendt
    /// 
    /// Opløft til AST for hver clause type? Måske samme visitor dog? Eller nedarvning?
    ///     Her udføres kolonner og metoder på kolonner
    /// Reducer flere Where's flere selects, orderbys m.v.
    /// Husk top1, som kommer fra Where men = take 1
    /// 
    /// Udskriv til streng eventuelt med visitors på AST elementerne
    /// 
    /// </summary>
    /// 
    /// 

    public class ConstantAndColumnParser : ExpressionVisitor
    {
        protected readonly Stack<SqlExpression> ast;

        public ConstantAndColumnParser(Stack<SqlExpression> ast)
        {
            this.ast = ast;
        }

        protected override Expression VisitLambda<T>(Expression<T> expression)
        {
            return Visit(expression.Body);
        }

        protected override Expression VisitUnary(UnaryExpression expression)
        {
            if (expression.NodeType == ExpressionType.Quote)
                Visit(expression.Operand);

            return expression;
        }

        protected override Expression VisitConstant(ConstantExpression expression)
        {
            ast.Push(new SqlConstantExpression(expression.Value));
            return expression;
        }

        protected override Expression VisitParameter(ParameterExpression expression)
        {
            ast.Push(new SqlColumnExpression(expression.Type, ""));
            return expression;
        }

        protected override Expression VisitMethodCall(MethodCallExpression expression)
        {
            if (expression.Object == null)
            {
                Visit(expression.Arguments.Skip(1).ToReadOnlyCollection());
                Visit(expression.Arguments.Take(1).ToReadOnlyCollection());
            }
            else
            {
                Visit(expression.Arguments);
                Visit(expression.Object);
            }
            
            switch (ast.Peek().NodeType)
            {
                case SqlNodeType.Constant:
                    VisitConstantMethodCall(expression);
                    break;
                case SqlNodeType.Column:
                    VisitColumnMethodCall(expression);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return expression;
        }

        protected virtual void VisitConstantMethodCall(MethodCallExpression expression)
        {
            var target = ((SqlConstantExpression)ast.Pop()).Value;
            var arguments = ast.Pop(expression.Arguments.Count)
                               .Cast<SqlConstantExpression>()
                               .Select(x => x.Value);

            ast.Push(new SqlConstantExpression(expression.Method.Invoke(target, arguments.ToArray())));
        }

        protected virtual void VisitColumnMethodCall(MethodCallExpression expression)
        {
            throw new NotSupportedException();
        }

        protected override Expression VisitNewArray(NewArrayExpression expression)
        {
            var items = new object[expression.Expressions.Count];
            for (int i = 0; i < expression.Expressions.Count; i++)
            {
                Visit(expression.Expressions[i]);
                items[i] = ((SqlConstantExpression)ast.Pop()).Value;
            }

            ast.Push(new SqlConstantExpression(items));

            return expression;
        }

        protected override Expression VisitMember(MemberExpression expression)
        {
            if (expression.Expression == null)
            {
                ast.Push(new SqlConstantExpression(expression.Member.GetValue(null)));
                return expression;
            }

            Visit(expression.Expression);

            switch (ast.Peek().NodeType)
            {
                case SqlNodeType.Constant:
                    var constant = (SqlConstantExpression) ast.Pop();
                    if (constant.Value == null)
                        throw new NullReferenceException();

                    ast.Push(new SqlConstantExpression(expression.Member.GetValue(constant.Value)));
                    break;
                case SqlNodeType.Column:
                    ast.Push(new SqlColumnExpression(expression.Member.GetMemberType(),
                                                     ((SqlColumnExpression) ast.Pop()).ColumnName + expression.Member.Name));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return expression;
        }
    }
}