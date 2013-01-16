using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace HybridDb.Linq.Ast
{
    /// <summary>
    /// ConstantPropagation
    /// UnaryBoolToBinary
    /// Constant == Constant = remove eller not
    /// Col == Constant(Null) = Col.IsNull() -> Visit omvendt, hvis de står omvendt
    /// 
    /// Nogle af disse (over) kan måske komme i AST i stedet?
    /// 
    /// Opløft til AST for hver clause type? Måske samme visitor dog? Eller nedarvning?
    ///     Her udføres kolonner og metoder på kolonner
    /// Reducer flere Where's flere selects, orderbys m.v.
    /// Husk top1, som kommer fra Where men = take 1
    /// 
    /// Udskriv til streng eventuelt med visitors på AST elementerne
    /// 
    /// </summary>

    internal class WhereVisitor2 : ExpressionVisitor
    {
        readonly Stack<SqlExpression> operations;

        public WhereVisitor2(Stack<SqlExpression> operations)
        {
            this.operations = operations;
        }

        public static SqlExpression Translate(Expression expression)
        {
            var operations = new Stack<SqlExpression>();
            expression = new UnaryBoolToBinaryExpressionVisitor().Visit(expression);
            new WhereVisitor2(operations).Visit(expression);
            return operations.Pop();
        }

        protected override Expression VisitBinary(BinaryExpression expression)
        {
            Visit(expression.Left);
            var left = operations.Pop();

            Visit(expression.Right);
            var right = operations.Pop();

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
                    throw new NotSupportedException(string.Format("The binary operator '{0}' is not supported", expression.NodeType));
            }

            operations.Push(new SqlBinaryExpression(nodeType, left, right));

            return expression;
        }

        protected override Expression VisitLambda<T>(Expression<T> expression)
        {
            return Visit(expression.Body);
        }

        protected override Expression VisitUnary(UnaryExpression expression)
        {
            switch (expression.NodeType)
            {
                case ExpressionType.Not:
                    Visit(expression.Operand);
                    operations.Push(new SqlNotExpression(operations.Pop()));
                    break;
                case ExpressionType.Quote:
                case ExpressionType.Convert:
                case ExpressionType.ConvertChecked:
                    Visit(expression.Operand);
                    break;
                default:
                    throw new NotSupportedException(string.Format("The unary operator '{0}' is not supported", expression.NodeType));
            }

            return expression;
        }

        protected override Expression VisitConstant(ConstantExpression expression)
        {
            operations.Push(new SqlConstantExpression(expression.Value));
            return expression;
        }

        protected override Expression VisitParameter(ParameterExpression expression)
        {
            operations.Push(new SqlColumnExpression(""));
            return expression;
        }

        protected override Expression VisitMethodCall(MethodCallExpression expression)
        {
            Visit(expression.Arguments);
            Visit(expression.Object);

            switch (operations.Peek().NodeType)
            {
                case SqlNodeType.Constant:
                    var target = (SqlConstantExpression)operations.Pop();
                    var arguments = operations.Pop(expression.Arguments.Count)
                                              .Cast<SqlConstantExpression>()
                                              .Select(x => x.Value);

                    operations.Push(new SqlConstantExpression(expression.Method.Invoke(target, arguments.ToArray())));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return expression;
        }

        protected override Expression VisitMember(MemberExpression expression)
        {
            if (expression.Expression == null)
            {
                operations.Push(new SqlConstantExpression(expression.Member.GetValue(null)));
                return expression;
            }

            Visit(expression.Expression);

            switch (operations.Peek().NodeType)
            {
                case SqlNodeType.Constant:
                    operations.Push(new SqlConstantExpression(expression.Member.GetValue(((SqlConstantExpression)operations.Pop()).Value)));
                    break;
                case SqlNodeType.Column:
                    operations.Push(new SqlColumnExpression(((SqlColumnExpression)operations.Pop()).ColumnName + expression.Member.Name));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return expression;
        }
    }
}