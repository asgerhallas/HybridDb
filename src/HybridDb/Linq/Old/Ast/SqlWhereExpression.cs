
namespace HybridDb.Linq.Old.Ast
{
    public class SqlWhereExpression : SqlExpression
    {
        readonly SqlBinaryExpression predicate;

        public SqlWhereExpression(SqlBinaryExpression predicate)
        {
            this.predicate = predicate;
        }

        public SqlBinaryExpression Predicate
        {
            get { return predicate; }
        }

        public override SqlNodeType NodeType
        {
            get { return SqlNodeType.Where; }
        }
    }
}