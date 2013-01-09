using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace HybridDb.Linq
{
    internal class QueryTranslator : ExpressionVisitor
    {
        readonly List<string> select = new List<string>();
        StringBuilder where;
        StringBuilder orderby;
        int take;
        int skip;

        internal Translation Translate(Expression expression)
        {
            where = new StringBuilder();
            orderby = new StringBuilder();
            Visit(expression);
            
            return new Translation
            {
                Select = string.Join(", ", select),
                Where = where.ToString(),
                Take = take,
                Skip = skip,
                OrderBy = orderby.ToString()
            };
        }

        static Expression StripQuotes(Expression e)
        {
            while (e.NodeType == ExpressionType.Quote)
                e = ((UnaryExpression) e).Operand;

            return e;
        }

        protected override Expression VisitMethodCall(MethodCallExpression expression)
        {
            if (expression.Method.DeclaringType == typeof (Queryable))
            {
                return VisitQueryableMethodCall(expression);
            }

            throw new NotSupportedException(string.Format("Translation of methods declared on '{0}' is not supported", expression.Method.DeclaringType));
        }

        protected Expression VisitQueryableMethodCall(MethodCallExpression expression)
        {
            Visit(expression.Arguments[0]);

            switch (expression.Method.Name)
            {
                case "Select":
                    VisitSelect(((UnaryExpression) expression.Arguments[1]).Operand);
                    break;
                case "Where":
                    Visit(((LambdaExpression) StripQuotes(expression.Arguments[1])).Body);
                    break;
                case "OfType":
                    break;
                case "Take":
                    take = (int) ((ConstantExpression)expression.Arguments[1]).Value;
                    break;
                case "Skip":
                    skip = (int)((ConstantExpression)expression.Arguments[1]).Value;
                    break;
                case "OrderBy":
                    orderby.Append(GetColumnNameFromMemberExpression(((LambdaExpression) StripQuotes(expression.Arguments[1])).Body));
                    break;
                case "ThenBy":
                    orderby.Append(", ");
                    orderby.Append(GetColumnNameFromMemberExpression(((LambdaExpression) StripQuotes(expression.Arguments[1])).Body));
                    break;
                case "OrderByDescending":
                    orderby.Append(GetColumnNameFromMemberExpression(((LambdaExpression) StripQuotes(expression.Arguments[1])).Body));
                    orderby.Append(" DESC");
                    break;
                case "ThenByDescending":
                    orderby.Append(", ");
                    orderby.Append(GetColumnNameFromMemberExpression(((LambdaExpression) StripQuotes(expression.Arguments[1])).Body));
                    orderby.Append(" DESC");
                    break;
                default:
                    throw new NotSupportedException(string.Format("The method '{0}' is not supported", expression.Method.Name));
            }

            return expression;
        }

        void VisitSelect(Expression expression)
        {
            var lambdaExpression = expression as LambdaExpression;
            expression = lambdaExpression != null ? lambdaExpression.Body : expression;

            switch (expression.NodeType)
            {
                case ExpressionType.New:
                    var @new = (NewExpression) expression;
                    for (int i = 0; i < @new.Arguments.Count; i++)
                    {
                        var property = @new.Arguments[i] as MemberExpression;
                        if (property == null)
                            continue;
                        
                        select.Add(property.Member.Name + " AS " + @new.Members[i].Name);
                    }
                    break;
                case ExpressionType.MemberAccess:
                    Visit(expression);
                    break;
                default:
                    throw new NotSupportedException(string.Format("Node {0} is not supported in a Select", expression.NodeType));
            }
        }

        protected override Expression VisitUnary(UnaryExpression u)
        {
            switch (u.NodeType)
            {
                case ExpressionType.Not:
                    @where.Append(" NOT ");
                    Visit(u.Operand);
                    break;
                default:
                    throw new NotSupportedException(string.Format("The unary operator '{0}' is not supported", u.NodeType));
            }
            return u;
        }

        protected override Expression VisitBinary(BinaryExpression b)
        {
            var left = b.Left;
            var right = b.Right;

            @where.Append("(");
            switch (b.NodeType)
            {
                case ExpressionType.And:
                case ExpressionType.Or:
                case ExpressionType.LessThan:
                case ExpressionType.LessThanOrEqual:
                case ExpressionType.GreaterThan:
                case ExpressionType.GreaterThanOrEqual:
                    Visit(left);
                    @where.Append(GetOperator(b));
                    Visit(right);
                    break;
                case ExpressionType.Equal:
                    if (right.NodeType == ExpressionType.Constant)
                    {
                        var ce = (ConstantExpression) right;
                        if (ce.Value == null)
                        {
                            Visit(left);
                            @where.Append(" IS NULL");
                            break;
                        }
                    }
                    else if (left.NodeType == ExpressionType.Constant)
                    {
                        var ce = (ConstantExpression) left;
                        if (ce.Value == null)
                        {
                            Visit(right);
                            @where.Append(" IS NULL");
                            break;
                        }
                    }

                    Visit(left);
                    @where.Append(GetOperator(b));
                    Visit(right);
                    break;
                case ExpressionType.NotEqual:
                    if (right.NodeType == ExpressionType.Constant)
                    {
                        var ce = (ConstantExpression) right;
                        if (ce.Value == null)
                        {
                            Visit(left);
                            @where.Append(" IS NOT NULL");
                            break;
                        }
                    }
                    else if (left.NodeType == ExpressionType.Constant)
                    {
                        var ce = (ConstantExpression) left;
                        if (ce.Value == null)
                        {
                            Visit(right);
                            @where.Append(" IS NOT NULL");
                            break;
                        }
                    }

                    Visit(left);
                    @where.Append(GetOperator(b));
                    Visit(right);
                    break;
                default:
                    throw new NotSupportedException(string.Format("The binary operator '{0}' is not supported", b.NodeType));
            }
            @where.Append(")");
            return b;
        }

        protected virtual string GetOperator(BinaryExpression b)
        {
            switch (b.NodeType)
            {
                case ExpressionType.And:
                case ExpressionType.AndAlso:
                    return (IsBoolean(b.Left.Type)) ? "AND" : "&";
                case ExpressionType.Or:
                case ExpressionType.OrElse:
                    return (IsBoolean(b.Left.Type) ? "OR" : "|");
                case ExpressionType.Equal:
                    return " = ";
                case ExpressionType.NotEqual:
                    return " <> ";
                case ExpressionType.LessThan:
                    return " < ";
                case ExpressionType.LessThanOrEqual:
                    return " <= ";
                case ExpressionType.GreaterThan:
                    return " > ";
                case ExpressionType.GreaterThanOrEqual:
                    return " >= ";
                case ExpressionType.Add:
                case ExpressionType.AddChecked:
                    return " + ";
                case ExpressionType.Subtract:
                case ExpressionType.SubtractChecked:
                    return " - ";
                case ExpressionType.Multiply:
                case ExpressionType.MultiplyChecked:
                    return " * ";
                case ExpressionType.Divide:
                    return " / ";
                case ExpressionType.Modulo:
                    return " % ";
                case ExpressionType.ExclusiveOr:
                    return " ^ ";
                case ExpressionType.LeftShift:
                    return " << ";
                case ExpressionType.RightShift:
                    return " >> ";
                default:
                    return "";
            }
        }

        protected override Expression VisitConstant(ConstantExpression c)
        {
            var q = c.Value as IQueryable;
            if (q != null)
                return c;

            if (c.Value == null)
            {
                @where.Append("NULL");
            }
            else
            {
                switch (Type.GetTypeCode(c.Value.GetType()))
                {
                    case TypeCode.Boolean:
                        @where.Append(((bool) c.Value) ? 1 : 0);
                        break;
                    case TypeCode.String:
                        @where.Append("'");
                        @where.Append(c.Value);
                        @where.Append("'");
                        break;
                    case TypeCode.Object:
                        throw new NotSupportedException(string.Format("The constant for '{0}' is not supported", c.Value));
                    default:
                        @where.Append(c.Value);
                        break;
                }
            }
            return c;
        }

        protected override Expression VisitMember(MemberExpression expression)
        {
            var columnName = GetColumnNameFromMemberExpression(expression);
            if (columnName != null)
            {
                where.Append(columnName);
                return expression;
            }

            var constantValue = GetConstantValueFromMemberExpression(expression);
            where.Append(constantValue);
            return expression;
        }

        private string GetColumnNameFromMemberExpression(Expression expression)
        {
            if (expression.NodeType == ExpressionType.MemberAccess)
            {
                var memberExpression = ((MemberExpression)expression);
                var name = GetColumnNameFromMemberExpression(memberExpression.Expression);
                if (name == null)
                    return null;

                return name + memberExpression.Member.Name;
            }

            if (expression.NodeType == ExpressionType.Parameter)
            {
                return "";
            }

            return null;
        }

        private object GetConstantValueFromMemberExpression(Expression expression)
        {
            if (expression.NodeType == ExpressionType.MemberAccess)
            {
                var memberExpression = ((MemberExpression) expression);
                var constant = GetConstantValueFromMemberExpression(memberExpression.Expression);
                if (constant == null)
                    return null;

                return memberExpression.Member.GetValue(constant);
            }

            if (expression.NodeType == ExpressionType.Constant)
            {
                return ((ConstantExpression)expression).Value;
            }

            return null;
        }

        protected virtual bool IsBoolean(Type type)
        {
            return type == typeof (bool) || type == typeof (bool?);
        }
    }
}