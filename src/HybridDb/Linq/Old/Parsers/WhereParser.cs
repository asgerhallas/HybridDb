using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using HybridDb.Linq.Old.Ast;

namespace HybridDb.Linq.Old.Parsers
{
    internal class WhereParser : LambdaParser
    {
        public WhereParser(Stack<SqlExpression> ast) : base(ast) { }

        public SqlExpression Result => ast.Peek();

        public static SqlExpression Translate(Expression expression)
        {
            var ast = new Stack<SqlExpression>();
            new WhereParser(ast).Visit(expression);

            if (ast.Count == 0)
                return null;

            var sqlExpression = ast.Pop();
            sqlExpression = new ImplicitBooleanPredicatePropagator().Visit(sqlExpression);
            sqlExpression = new NullCheckPropagator().Visit(sqlExpression);

            return sqlExpression;
        }

        protected override Expression VisitBinary(BinaryExpression expression)
        {
            var left = VisitPop(expression.Left);
            var right = VisitPop(expression.Right);
            
            SqlNodeType nodeType;
            switch (expression.NodeType)
            {
                case ExpressionType.And:
                    nodeType = SqlNodeType.BitwiseAnd;
                    break;
                case ExpressionType.AndAlso:
                    nodeType = SqlNodeType.And;
                    break;
                case ExpressionType.Or:
                    nodeType = SqlNodeType.BitwiseOr;
                    break;
                case ExpressionType.OrElse:
                    nodeType = SqlNodeType.Or;
                    break;
                case ExpressionType.LessThan:
                    nodeType = SqlNodeType.LessThan;
                    break;
                case ExpressionType.LessThanOrEqual:
                    nodeType = SqlNodeType.LessThanOrEqual;
                    break;
                case ExpressionType.GreaterThan:
                    nodeType = SqlNodeType.GreaterThan;
                    break;
                case ExpressionType.GreaterThanOrEqual:
                    nodeType = SqlNodeType.GreaterThanOrEqual;
                    break;
                case ExpressionType.Equal:
                    nodeType = SqlNodeType.Equal;
                    break;
                case ExpressionType.NotEqual:
                    nodeType = SqlNodeType.NotEqual;
                    break;
                default:
                    throw new NotSupportedException($"The binary operator '{expression.NodeType}' is not supported");
            }

            ast.Push(new SqlBinaryExpression(nodeType, left, right));

            return expression;
        }

        protected override Expression VisitUnary(UnaryExpression expression)
        {
            switch (expression.NodeType)
            {
                case ExpressionType.Not:
                    ast.Push(new SqlNotExpression(VisitPop(expression.Operand)));
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
                    ast.Push(new SqlBinaryExpression(SqlNodeType.LikeStartsWith, ast.Pop(), ast.Pop()));
                    break;
                case "Contains":
                    ast.Push(new SqlBinaryExpression(SqlNodeType.LikeContains, ast.Pop(), ast.Pop()));
                    break;
                case "In":
                    var column = ast.Pop();
                    var sqlExpression = ast.Pop();
                    var set = (SqlConstantExpression)sqlExpression;
                    if (((IEnumerable) set.Value).Cast<object>().Any())
                    {
                        ast.Push(new SqlBinaryExpression(SqlNodeType.In, column, set));
                    }
                    else
                    {
                        ast.Push(new SqlConstantExpression(typeof(bool), false));
                    }
                    break;
                default:
                    base.VisitColumnMethodCall(expression);
                    break;
            }
        }
    }
}