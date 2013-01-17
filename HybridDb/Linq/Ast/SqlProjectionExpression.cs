namespace HybridDb.Linq.Ast
{
    public class SqlProjectionExpression : SqlExpression
    {
        readonly string to;

        public string To
        {
            get { return to; }
        }

        public SqlColumnExpression From
        {
            get { return from; }
        }

        readonly SqlColumnExpression from;

        public SqlProjectionExpression(SqlColumnExpression from, string to)
        {
            this.to = to;
            this.from = from;
        }

        public override SqlNodeType NodeType
        {
            get { return SqlNodeType.Project; }
        }
    }
}