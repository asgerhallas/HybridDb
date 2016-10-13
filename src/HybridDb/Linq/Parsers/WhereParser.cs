using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Cryptography.X509Certificates;
using HybridDb.Linq.Ast;
using HybridDb.Linq2.Ast;
using ShinySwitch;
using static HybridDb.Linq2.Ast.NullNotNull;

namespace HybridDb.Linq.Parsers
{
    //public static class SqlVisitor
    //{
    //    public static TExpression Visit<TExpression>(TypeSwitchExpression<AstNode, TExpression> match, AstNode node)
    //    {
    //        match.Else(x => Switch<TExpression>.On(node)
    //            .Match<Comparison>(c => new Comparison(c.Operator, Visit<TExpression>(match, c.Left), Visit<TExpression>(match, c.Right))))
    //    }
    //}

    internal class WhereParser : LambdaParser
    {
        public WhereParser(Func<Type, string> getTableNameForType, Stack<AstNode> ast) : base(getTableNameForType, ast) { }

        public AstNode Result => ast.Peek();

        public static Where Translate(Func<Type, string> getTableNameForType, Expression expression)
        {
            var ast = new Stack<AstNode>();
            new WhereParser(getTableNameForType, ast).Visit(expression);

            if (ast.Count == 0)
                return null;

            var predicate = ToPredicate(ast.Pop());

            //TODO:
            //sqlExpression = new ImplicitBooleanPredicatePropagator().Visit(sqlExpression);
            //sqlExpression = new NullCheckPropagator().Visit(sqlExpression);

            return new Where(predicate);
        }


        protected override Expression VisitBinary(BinaryExpression expression)
        {
            Visit(expression.Left);
            Visit(expression.Right);
            
            var right = (SqlExpression)ast.Pop();
            var left = (SqlExpression)ast.Pop();

            ast.Push(Switch<SqlExpression>.On(expression.NodeType)
                .Match(ExpressionType.AndAlso, x => new Logic(LogicOperator.And, ToPredicate(left), ToPredicate(right)))
                .Match(ExpressionType.OrElse, x => new Logic(LogicOperator.Or, ToPredicate(left), ToPredicate(right)))
                .Match(ExpressionType.And, x => new Bitwise(BitwiseOperator.And, left, right))
                .Match(ExpressionType.Or, x => new Bitwise(BitwiseOperator.Or, left, right))
                .Match(ExpressionType.LessThan, x => new Comparison(ComparisonOperator.LessThan, left, right))
                .Match(ExpressionType.LessThanOrEqual, x => new Comparison(ComparisonOperator.LessThenOrEqualTo, left, right))
                .Match(ExpressionType.GreaterThan, x => new Comparison(ComparisonOperator.GreaterThan, left, right))
                .Match(ExpressionType.GreaterThanOrEqual, x => new Comparison(ComparisonOperator.GreaterThanOrEqualTo, left, right))
                .Match(ExpressionType.Equal, x => 
                    Switch<SqlExpression>.On(left, right)
                        .Match<Constant, Constant>((l, r) => new True())
                        .MatchLeft<Constant>(l => l.Value == null, (l, r) => new Is(Null, right))
                        .MatchRight<Constant>(r => r.Value == null, (l, r) => new Is(Null, left))
                        .Else(() => new Comparison(ComparisonOperator.Equal, left, right)))
                .Match(ExpressionType.NotEqual, x => 
                    Switch<SqlExpression>.On(left, right)
                        .Match((Constant l, Constant r) => new True())
                        .MatchLeft<Constant>(l => l.Value == null, (l, r) => new Is(NotNull, right))
                        .MatchRight<Constant>(r => r.Value == null, (l, r) => new Is(NotNull, left))
                        .Else(() => new Comparison(ComparisonOperator.NotEqual, left, right)))
                .OrThrow(new NotSupportedException($"The binary operator '{expression.NodeType}' is not supported")));

            return expression;
        }

        protected override Expression VisitUnary(UnaryExpression expression)
        {
            switch (expression.NodeType)
            {
                case ExpressionType.Not:
                    Visit(expression.Operand);
                    ast.Push(new Not(ToPredicate(ast.Pop())));
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
                    ast.Push(new Like((SqlExpression) ast.Pop(), (Constant) ast.Pop(), new Wildcard(WildcardOperator.OneOrMore)));
                    break;
                case "Contains":
                    ast.Push(new Like((SqlExpression)ast.Pop(), new Wildcard(WildcardOperator.OneOrMore), (Constant)ast.Pop(), new Wildcard(WildcardOperator.OneOrMore)));
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