using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using HybridDb.Linq.Ast;
using HybridDb.Linq2.Ast;
using ShinySwitch;

namespace HybridDb.Linq.Parsers
{
    public class LambdaParser : ExpressionVisitor
    {
        protected readonly Stack<AstNode> ast;

        readonly Func<Type, string> getTableNameForType;

        public LambdaParser(Func<Type, string> getTableNameForType, Stack<AstNode> ast)
        {
            this.getTableNameForType = getTableNameForType;
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
            var type = expression.Value?.GetType() ?? typeof(object);
            ast.Push(new Constant(type, expression.Value));
            return expression;
        }

        protected override Expression VisitParameter(ParameterExpression expression)
        {
            ast.Push(new TableName(getTableNameForType(expression.Type)));
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

            Switch.On(ast.Peek())
                .Match<Constant>(_ => VisitConstantMethodCall(expression))
                .Match<ColumnName>(_ => VisitColumnMethodCall(expression))
                .OrThrow(new ArgumentOutOfRangeException());

            return expression;
        }

        protected virtual void VisitConstantMethodCall(MethodCallExpression expression)
        {
            if (expression.Object == null)
            {
                var arguments = ast.Pop(expression.Arguments.Count)
                                   .Cast<Constant>()
                                   .Select(x => x.Value);

                ast.Push(new Constant(expression.Method.ReturnType, expression.Method.Invoke(null, arguments.ToArray())));
            }
            else
            {
                var receiver = ((Constant)ast.Pop()).Value;
                var arguments = ast.Pop(expression.Arguments.Count)
                                   .Cast<Constant>()
                                   .Select(x => x.Value);

                ast.Push(new Constant(expression.Method.ReturnType, expression.Method.Invoke(receiver, arguments.ToArray())));
            }
        }

        protected virtual void VisitColumnMethodCall(MethodCallExpression expression)
        {
            switch (expression.Method.Name)
            {
                case "Column":
                {
                    var table = ast.Pop() as TableName; // remove the current column expression
                    if (table == null)
                    {
                        throw new NotSupportedException($"{expression} method must be called on the lambda parameter.");
                    }

                    var constant = (Constant) ast.Pop();
                    var columnType = expression.Method.GetGenericArguments()[0];
                    var columnName = (string) constant.Value;

                    ast.Push(new TypedColumnName(columnType, table.Name, columnName));
                    break;
                }
                case "Index":
                {
                    var table = ast.Pop() as TableName; // remove the current column expression
                    if (table == null)
                    {
                        throw new NotSupportedException($"{expression} method must be called on the lambda parameter.");
                    }

                    var type = expression.Method.GetGenericArguments()[0];

                    //TODO:
                    //ast.Push(new SqlColumnPrefixExpression(type.Name));
                    break;
                }
                default:
                    ast.Pop();
                    var name = ColumnNameBuilder.GetColumnNameByConventionFor(expression);
                    ast.Push(new TypedColumnName(expression.Method.ReturnType, "", name));
                    break;
            }
        }

        protected override Expression VisitNewArray(NewArrayExpression expression)
        {
            var items = new object[0];

            if (expression.NodeType == ExpressionType.NewArrayInit)
            {
                items = new object[expression.Expressions.Count];

                for (var i = 0; i < expression.Expressions.Count; i++)
                {
                    Visit(expression.Expressions[i]);
                    items[i] = ((Constant) ast.Pop()).Value;
                }
            }

            ast.Push(new Constant(typeof(object[]), items));

            return expression;
        }

        protected override Expression VisitMember(MemberExpression expression)
        {
            if (expression.Expression == null)
            {
                ast.Push(new Constant(expression.Member.GetMemberType(), expression.Member.GetValue(null)));
                return expression;
            }

            Visit(expression.Expression);

            Switch.On(ast.Peek())
                .Match<Constant>(x =>
                {
                    var constant = (Constant) ast.Pop();
                    if (constant.Value == null)
                        throw new NullReferenceException();

                    ast.Push(new Constant(expression.Member.GetMemberType(), expression.Member.GetValue(constant.Value)));
                })
                .Match<TableName>(x =>
                {
                    var table = (TableName)ast.Pop();
                    var name = ColumnNameBuilder.GetColumnNameByConventionFor(expression);
                    ast.Push(new TypedColumnName(expression.Member.GetMemberType(), table.Name, name));
                })
                .Match<ColumnName>(x =>
                {
                    var column = (ColumnName)ast.Pop();
                    var name = ColumnNameBuilder.GetColumnNameByConventionFor(expression);
                    ast.Push(new TypedColumnName(expression.Member.GetMemberType(), column.TableName, name));
                })
                .OrThrow(new ArgumentOutOfRangeException());

            //TODO:
            //    case SqlNodeType.ColumnPrefix:
            //        //TODO: clean up this mess. 
            //        var prefix = (SqlColumnPrefixExpression)ast.Pop();
            //ast.Push(new SqlColumnExpression(expression.Member.GetMemberType(), expression.Member.Name));
            //break;
            

            return expression;
        }

        public static Predicate ToPredicate(AstNode node)
        {
            return Switch<Predicate>.On(node)
                .Match<Predicate>(x => x)
                .Match<Constant>(x => x.Type == typeof(bool), constant =>
                   (bool)constant.Value ? (Predicate)new True() : new False())
                .Match<TypedColumnName>(x => x.Type == typeof(bool), column =>
                   new Comparison(ComparisonOperator.Equal, column, new Constant(typeof(bool), true)));
        }

        // TypedColumnName is a column enriched with type of the expression it derives from.
        // This is used to convert unary bool predicates (e.g. .Where(x => x.BoolIsTrue)) to binary predicates.
        internal class TypedColumnName : ColumnName
        {
            public TypedColumnName(Type type, string tableName, string name) : base(tableName, name)
            {
                Type = type;
            }

            public Type Type { get; }
        }
    }
}