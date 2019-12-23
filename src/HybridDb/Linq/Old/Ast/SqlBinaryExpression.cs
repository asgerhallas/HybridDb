namespace HybridDb.Linq.Old.Ast
{
    public class SqlBinaryExpression : SqlExpression
    {
        readonly SqlNodeType nodeType;
        readonly SqlExpression left;
        readonly SqlExpression right;

        public SqlBinaryExpression(SqlNodeType nodeType, SqlExpression left, SqlExpression right)
        {
            this.nodeType = nodeType;
            this.left = left;
            this.right = right;
        }

        public SqlExpression Left
        {
            get { return left; }
        }

        public SqlExpression Right
        {
            get { return right; }
        }

        public override SqlNodeType NodeType
        {
            get { return nodeType; }
        }
    }
}