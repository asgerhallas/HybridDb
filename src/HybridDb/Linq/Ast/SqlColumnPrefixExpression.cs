namespace HybridDb.Linq.Ast
{
    public class SqlColumnPrefixExpression : SqlExpression
    {
        public SqlColumnPrefixExpression(string prefix)
        {
            Prefix = prefix;
        }

        public string Prefix { get; private set; }

        public override SqlNodeType NodeType
        {
            get { return SqlNodeType.ColumnPrefix; }
        }
    }
}