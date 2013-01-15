namespace HybridDb.Linq.Ast
{
    internal class SqlColumnExpression : SqlExpression
    {
        readonly string columnName;

        public SqlColumnExpression(string columnName)
        {
            this.columnName = columnName;
        }

        public override SqlNodeType NodeType
        {
            get { return SqlNodeType.Column; }
        }
    }
}