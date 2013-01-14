using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace HybridDb.Linq
{

    internal class SqlExpression
    {
    }

    internal class SqlWhereExpression : SqlExpression
    {
        readonly Expression predicate;
        bool top1 = false;

        public SqlWhereExpression(Expression predicate)
        {
            this.predicate = predicate;
        }

        public Expression Predicate
        {
            get { return predicate; }
        }
    }

    internal class PredicateExpression {}

    internal class Visitor
    {
        protected virtual SqlExpression Visit(Expression expression)
        {
            if (expression == null)
                return null;

            switch (expression.NodeType)
            {
                case ExpressionType.Negate:
                case ExpressionType.NegateChecked:
                case ExpressionType.Not:
                case ExpressionType.Convert:
                case ExpressionType.ConvertChecked:
                case ExpressionType.ArrayLength:
                case ExpressionType.Quote:
                case ExpressionType.TypeAs:
                    return VisitUnary((UnaryExpression) expression);
                case ExpressionType.Add:
                case ExpressionType.AddChecked:
                case ExpressionType.Subtract:
                case ExpressionType.SubtractChecked:
                case ExpressionType.Multiply:
                case ExpressionType.MultiplyChecked:
                case ExpressionType.Divide:
                case ExpressionType.Modulo:
                case ExpressionType.And:
                case ExpressionType.AndAlso:
                case ExpressionType.Or:
                case ExpressionType.OrElse:
                case ExpressionType.LessThan:
                case ExpressionType.LessThanOrEqual:
                case ExpressionType.GreaterThan:
                case ExpressionType.GreaterThanOrEqual:
                case ExpressionType.Equal:
                case ExpressionType.NotEqual:
                case ExpressionType.Coalesce:
                case ExpressionType.ArrayIndex:
                case ExpressionType.RightShift:
                case ExpressionType.LeftShift:
                case ExpressionType.ExclusiveOr:
                    return VisitBinary((BinaryExpression) expression);
                //case ExpressionType.TypeIs:
                //    return this.VisitTypeIs((TypeBinaryExpression) expression);
                //case ExpressionType.Conditional:
                //    return this.VisitConditional((ConditionalExpression) expression);
                //case ExpressionType.Constant:
                //    return this.VisitConstant((ConstantExpression) expression);
                //case ExpressionType.Parameter:
                //    return this.VisitParameter((ParameterExpression) expression);
                //case ExpressionType.MemberAccess:
                //    return this.VisitMemberAccess((MemberExpression) expression);
                //case ExpressionType.Call:
                //    return this.VisitMethodCall((MethodCallExpression) expression);
                //case ExpressionType.Lambda:
                //    return this.VisitLambda((LambdaExpression) expression);
                //case ExpressionType.New:
                //    return this.VisitNew((NewExpression) expression);
                //case ExpressionType.NewArrayInit:
                //case ExpressionType.NewArrayBounds:
                //    return this.VisitNewArray((NewArrayExpression) expression);
                //case ExpressionType.Invoke:
                //    return this.VisitInvocation((InvocationExpression) expression);
                //case ExpressionType.MemberInit:
                //    return this.VisitMemberInit((MemberInitExpression) expression);
                //case ExpressionType.ListInit:
                //    return this.VisitListInit((ListInitExpression) expression);
                default:
                    throw new Exception(string.Format("Unhandled expression type: '{0}'", expression.NodeType));
            }
        }

        SqlExpression VisitUnary(UnaryExpression expression)
        {
            return Visit(expression.Operand);
        }

        SqlExpression VisitBinary(BinaryExpression expression)
        {
            //new BinaryExpression(Visit()
            return null;
        }
    }


    internal class ClauseExtractionVisitor : ExpressionVisitor
    {
        SqlWhereExpression where;
        SqlExpression select;
        SqlExpression orderBy;
        
        public ClauseExtractionVisitor()
        {
        }

        public ClauseExtractionVisitor Translate(Expression expression)
        {
            Visit(expression);
            new ConstantPropagationVisitor().Visit(where.Predicate);

            return this;
        }

        protected override Expression VisitMethodCall(MethodCallExpression expression)
        {
            if (expression.Method.DeclaringType == typeof (Queryable))
            {
                switch (expression.Method.Name)
                {
                    case "Where":
                        where = new SqlWhereExpression(expression.Arguments[1]);
                        break;
                    default:
                        throw new NotSupportedException(string.Format("The method {0} is not supported", expression.Method.Name));
                }
            }

            Visit(expression.Arguments[0]);
            return expression;
        }
    }

    internal class ConstantPropagationVisitor : ExpressionVisitor
    {
        public object Constant { get; set; }

        protected override Expression VisitMember(MemberExpression expression)
        {
            if (expression.Expression is ConstantExpression)


            return expression;
        }
    }


    internal class ParameterMemberVisitor : ExpressionVisitor
    {
        SqlExpression sqlExpression;
        string columnName;

        public static SqlExpression Translate(Expression expression)
        {
            var visitor = new ParameterMemberVisitor();
            visitor.Visit(expression);
            return visitor.sqlExpression ?? new SqlExpression();
        }

        protected override Expression VisitMember(MemberExpression expression)
        {
            if (expression.Expression is ParameterExpression)
            {
                sqlExpression = new SqlColumnExpression(columnName);
            }
            else
            {
                columnName += expression.Member.Name;
                Visit(expression.Expression);
            }

            return expression;
        }
    }

    internal class SqlColumnExpression : SqlExpression 
    {
        readonly string columnName;

        public SqlColumnExpression(string columnName)
        {
            this.columnName = columnName;
        }
    }

    internal class UnaryBoolToBinaryEXpressionVisitor : ExpressionVisitor
    {
        protected override Expression VisitBinary(BinaryExpression node)
        {
            switch (node.NodeType)
            {
                case ExpressionType.And:
                case ExpressionType.AndAlso:
                case ExpressionType.Or:
                case ExpressionType.OrElse:
                    return base.VisitBinary(node);
            }

            return node;
        }

        protected override Expression VisitMember(MemberExpression expression)
        {
            var member = expression.GetMemberExpressionInfo();
            if (member != null)
            {
                if (member.ResultingType == typeof (bool))
                {
                    return Expression.MakeBinary(ExpressionType.Equal, expression, Expression.Constant(true));
                }
            }
            return expression;
        }
    }
}