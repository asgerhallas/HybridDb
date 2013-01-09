using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace HybridDb.Linq
{
    internal class QueryTranslator : ExpressionVisitor
    {
        StringBuilder sb;
        readonly List<string> select = new List<string>();

        internal Translation Translate(Expression expression)
        {
            sb = new StringBuilder();
            Visit(expression);
            
            return new Translation
            {
                Select = string.Join(", ", select),
                Where = sb.ToString()
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
            switch (expression.Method.Name)
            {
                case "Select":
                    Visit(expression.Arguments[0]);
                    VisitSelect(((UnaryExpression) expression.Arguments[1]).Operand);
                    break;
                case "Where":
                    var lambda = (LambdaExpression) StripQuotes(expression.Arguments[1]);
                    Visit(lambda.Body);
                    break;
                case "OfType":
                    Visit(expression.Arguments[0]);
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
                    sb.Append(" NOT ");
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

            sb.Append("(");
            switch (b.NodeType)
            {
                case ExpressionType.And:
                case ExpressionType.Or:
                case ExpressionType.LessThan:
                case ExpressionType.LessThanOrEqual:
                case ExpressionType.GreaterThan:
                case ExpressionType.GreaterThanOrEqual:
                    Visit(left);
                    sb.Append(GetOperator(b));
                    Visit(right);
                    break;
                case ExpressionType.Equal:
                    if (right.NodeType == ExpressionType.Constant)
                    {
                        var ce = (ConstantExpression) right;
                        if (ce.Value == null)
                        {
                            Visit(left);
                            sb.Append(" IS NULL");
                            break;
                        }
                    }
                    else if (left.NodeType == ExpressionType.Constant)
                    {
                        var ce = (ConstantExpression) left;
                        if (ce.Value == null)
                        {
                            Visit(right);
                            sb.Append(" IS NULL");
                            break;
                        }
                    }

                    Visit(left);
                    sb.Append(GetOperator(b));
                    Visit(right);
                    break;
                case ExpressionType.NotEqual:
                    if (right.NodeType == ExpressionType.Constant)
                    {
                        var ce = (ConstantExpression) right;
                        if (ce.Value == null)
                        {
                            Visit(left);
                            sb.Append(" IS NOT NULL");
                            break;
                        }
                    }
                    else if (left.NodeType == ExpressionType.Constant)
                    {
                        var ce = (ConstantExpression) left;
                        if (ce.Value == null)
                        {
                            Visit(right);
                            sb.Append(" IS NOT NULL");
                            break;
                        }
                    }

                    Visit(left);
                    sb.Append(GetOperator(b));
                    Visit(right);
                    break;
                default:
                    throw new NotSupportedException(string.Format("The binary operator '{0}' is not supported", b.NodeType));
            }
            sb.Append(")");
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
                    return "=";
                case ExpressionType.NotEqual:
                    return "<>";
                case ExpressionType.LessThan:
                    return "<";
                case ExpressionType.LessThanOrEqual:
                    return "<=";
                case ExpressionType.GreaterThan:
                    return ">";
                case ExpressionType.GreaterThanOrEqual:
                    return ">=";
                case ExpressionType.Add:
                case ExpressionType.AddChecked:
                    return "+";
                case ExpressionType.Subtract:
                case ExpressionType.SubtractChecked:
                    return "-";
                case ExpressionType.Multiply:
                case ExpressionType.MultiplyChecked:
                    return "*";
                case ExpressionType.Divide:
                    return "/";
                case ExpressionType.Modulo:
                    return "%";
                case ExpressionType.ExclusiveOr:
                    return "^";
                case ExpressionType.LeftShift:
                    return "<<";
                case ExpressionType.RightShift:
                    return ">>";
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
                sb.Append("NULL");
            }
            else
            {
                switch (Type.GetTypeCode(c.Value.GetType()))
                {
                    case TypeCode.Boolean:
                        sb.Append(((bool) c.Value) ? 1 : 0);
                        break;
                    case TypeCode.String:
                        sb.Append("'");
                        sb.Append(c.Value);
                        sb.Append("'");
                        break;
                    case TypeCode.Object:
                        throw new NotSupportedException(string.Format("The constant for '{0}' is not supported", c.Value));
                    default:
                        sb.Append(c.Value);
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
                sb.Append(columnName);
                return expression;
            }

            var constantValue = GetConstantValueFromMemberExpression(expression);
            sb.Append(constantValue);
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

        internal class Translation
        {
            public string Select { get; set; }
            public string Where { get; set; }
        }
    }
}