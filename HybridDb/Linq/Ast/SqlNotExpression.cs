namespace HybridDb.Linq.Ast
{
    public class SqlNotExpression : SqlExpression
    {
        readonly SqlExpression operand;

        public SqlNotExpression(SqlExpression operand)
        {
            this.operand = operand;
        }

        public SqlExpression Operand
        {
            get { return operand; }
        }

        public override SqlNodeType NodeType
        {
            get { return SqlNodeType.Not; }
        }
    }
}