namespace HybridDb.Linq.Ast
{
    public class SqlColumnExpression : SqlExpression
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

        public string ColumnName
        {
            get { return columnName; }
        }
    }
}