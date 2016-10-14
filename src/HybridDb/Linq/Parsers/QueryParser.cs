using System;
using System.Linq.Expressions;
using HybridDb.Linq.Ast;
using HybridDb.Linq2.Ast;

namespace HybridDb.Linq.Parsers
{
    public class QueryParser : ExpressionVisitor
    {
        readonly Func<Type, string> getTableNameForType;

        public QueryParser(Func<Type, string> getTableNameForType)
        {
            this.getTableNameForType = getTableNameForType;
        }

        public int Skip { get; private set; }
        public int Take { get; private set; }
        public Select Select { get; private set; }
        public Where Where { get; private set; }
        public OrderBy OrderBy { get; private set; }
        public Execution Execution { get; private set; }
        public Type TableType { get; private set; }

        public Result Parse(Expression expression)
        {
            Visit(expression);

            return new Result(new SelectStatement(
                Select ?? new Select(),
                new From(new TableName(getTableNameForType(TableType))), 
                Where ?? new Where(new True())), Execution);
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Type.IsGenericType && typeof (Query<>) == node.Type.GetGenericTypeDefinition())
            {
                TableType = node.Type.GetGenericArguments()[0];
            }

            return base.VisitConstant(node);
        }

        protected override Expression VisitMethodCall(MethodCallExpression expression)
        {
            Visit(expression.Arguments[0]);

            switch (expression.Method.Name)
            {
                case "Select":
                    Select = SelectParser.Translate(getTableNameForType, Select, expression.Arguments[1]);
                    break;
                case "SingleOrDefault":
                    Execution = Execution.SingleOrDefault;
                    goto Take1;
                case "Single":
                    Execution = Execution.Single;
                    goto Take1;
                case "FirstOrDefault":
                    Execution = Execution.FirstOrDefault;
                    goto Take1;
                case "First":
                    Execution = Execution.First;
                    goto Take1;
                case "Take1":
                    Take1:
                    Take = 1;
                    if (expression.Arguments.Count <= 1) break;
                    goto Where;
                case "Where":
                    Where:
                    var whereExpression = WhereParser.Translate(getTableNameForType, expression.Arguments[1]);
                    if (whereExpression == null)
                        break;

                    Where = Where != null
                        ? new Where(new Logic(LogicOperator.And, Where.Condition, whereExpression.Condition))
                        : whereExpression;

                    break;
                case "Skip":
                    Skip = (int) ((ConstantExpression) expression.Arguments[1]).Value;
                    break;
                case "Take":
                    Take = (int) ((ConstantExpression) expression.Arguments[1]).Value;
                    break;
                case "OfType":
                    // Change of type is done else where
                    break;
                case "OrderBy":
                case "ThenBy":
                case "OrderByDescending":
                case "ThenByDescending":
                    var direction = expression.Method.Name.Contains("Descending")
                                        ? OrderByExpression.Directions.Descending
                                        : OrderByExpression.Directions.Ascending;

                    var orderByColumnExpression = OrderByVisitor.Translate(getTableNameForType, expression.Arguments[1]);
                    var orderingExpression = new OrderByExpression(orderByColumnExpression, direction);
                    OrderBy = OrderBy != null
                                  ? new OrderBy(OrderBy.Columns.Concat(orderingExpression))
                                  : new OrderBy(orderingExpression.AsEnumerable());
                    break;
                default:
                    throw new NotSupportedException($"The method {expression.Method.Name} is not supported");
            }

            return expression;
        }

        public class Result
        {
            public Result(SelectStatement statement, Execution execution)
            {
                Statement = statement;
                Execution = execution;
            }

            public SelectStatement Statement { get; }
            public Execution Execution { get; }
        }
    }
}