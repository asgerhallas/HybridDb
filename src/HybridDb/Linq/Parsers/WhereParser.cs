using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using HybridDb.Linq.Ast;
using HybridDb.Linq2.Ast;
using ShinySwitch;
using static HybridDb.Linq2.Ast.NullNotNull;

namespace HybridDb.Linq.Parsers
{
    internal class WhereParser : LambdaParser
    {
        public WhereParser(Stack<AstNode> ast) : base(ast) { }

        public AstNode Result => ast.Peek();

        public static Where Translate(Expression expression)
        {
            var ast = new Stack<AstNode>();
            new WhereParser(ast).Visit(expression);

            if (ast.Count == 0)
                return null;

            var sqlExpression = (Predicate)ast.Pop();
            //TODO:
            //sqlExpression = new ImplicitBooleanPredicatePropagator().Visit(sqlExpression);
            //sqlExpression = new NullCheckPropagator().Visit(sqlExpression);

            return new Where(sqlExpression);
        }

        protected override Expression VisitBinary(BinaryExpression expression)
        {
            Visit(expression.Left);
            Visit(expression.Right);
            
            var right = (SqlExpression)ast.Pop();
            var left = (SqlExpression)ast.Pop();

            ast.Push(Switch<SqlExpression>.On(expression.NodeType)
                .Match(ExpressionType.AndAlso, x => new Logical(LogicalOperator.And, (Predicate) left, (Predicate) right))
                .Match(ExpressionType.OrElse, x => new Logical(LogicalOperator.Or, (Predicate) left, (Predicate) right))
                .Match(ExpressionType.And, x => new Bitwise(BitwiseOperator.And, left, right))
                .Match(ExpressionType.Or, x => new Bitwise(BitwiseOperator.Or, left, right))
                .Match(ExpressionType.LessThan, x => new Comparison(ComparisonOperator.LessThan, left, right))
                .Match(ExpressionType.LessThanOrEqual, x => new Comparison(ComparisonOperator.LessThenOrEqualTo, left, right))
                .Match(ExpressionType.GreaterThan, x => new Comparison(ComparisonOperator.GreaterThan, left, right))
                .Match(ExpressionType.GreaterThanOrEqual, x => new Comparison(ComparisonOperator.GreaterThanOrEqualTo, left, right))
                .Match(ExpressionType.Equal, x => Switch<SqlExpression>.On(left)
                    .Match<Constant>(c => c.Value == null, _ => Switch<SqlExpression>.On(right)
                        .Match<Constant>(c => c.Value == null, __ => new True())
                        .Else(() => new Is(Null, right)))
                    .Else(() => Switch<SqlExpression>.On(right)
                        .Match<Constant>(c => c.Value == null, _ => new Is(Null, left))
                        .Else(() => new Comparison(ComparisonOperator.Equal, left, right))))
                .Match(ExpressionType.NotEqual, x => Switch<SqlExpression>.On(left)
                    .Match<Constant>(c => c.Value == null, _ => Switch<SqlExpression>.On(right)
                        .Match<Constant>(c => c.Value == null, __ => new False())
                        .Else(() => new Is(NotNull, right)))
                    .Else(() => Switch<SqlExpression>.On(right)
                        .Match<Constant>(c => c.Value == null, _ => new Is(NotNull, left))
                        .Else(() => new Comparison(ComparisonOperator.NotEqual, left, right))))
                .OrThrow(new NotSupportedException($"The binary operator '{expression.NodeType}' is not supported")));

            return expression;
        }

        protected override Expression VisitUnary(UnaryExpression expression)
        {
            switch (expression.NodeType)
            {
                case ExpressionType.Not:
                    Visit(expression.Operand);
                    ast.Push(new Not((SqlExpression)ast.Pop()));
                    break;
                case ExpressionType.Quote:
                case ExpressionType.Convert:
                case ExpressionType.ConvertChecked:
                    Visit(expression.Operand);
                    break;
                default:
                    throw new NotSupportedException($"The unary operator '{expression.NodeType}' is not supported");
            }

            return expression;
        }

        protected override void VisitColumnMethodCall(MethodCallExpression expression)
        {
            switch (expression.Method.Name)
            {
                case "StartsWith":
                    ast.Push(new Like((SqlExpression) ast.Pop(), $"%{((Constant) ast.Pop()).Value}"));
                    break;
                case "Contains":
                    ast.Push(new Like((SqlExpression)ast.Pop(), $"%{((Constant) ast.Pop()).Value}%"));
                    break;
                case "In":
                    var column = ast.Pop();
                    var sqlExpression = ast.Pop();
                    var set = (Constant)sqlExpression;
                    if (((IEnumerable) set.Value).Cast<object>().Any())
                    {
                        var sqlExpressions = ((IEnumerable<object>)set.Value).Select(x => new Constant(x.GetType(), x)).ToArray();
                        
                        // ReSharper disable once CoVariantArrayConversion
                        ast.Push(new In((SqlExpression) column, sqlExpressions));
                    }
                    else
                    {
                        ast.Push(new Constant(typeof(bool), false));
                    }
                    break;
                default:
                    base.VisitColumnMethodCall(expression);
                    break;
            }
        }
    }
}