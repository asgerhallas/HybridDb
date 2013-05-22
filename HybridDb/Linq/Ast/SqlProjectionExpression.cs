namespace HybridDb.Linq.Ast
{
    public class SqlProjectionExpression : SqlExpression
    {
        public SqlProjectionExpression(SqlColumnExpression from, string to)
        {
            To = to;
            From = @from;
        }

        public string To { get; private set; }
        public SqlColumnExpression From { get; private set; }

        public override SqlNodeType NodeType
        {
            get { return SqlNodeType.Project; }
        }
    }
}