
namespace HybridDb.Linq.Ast
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
    }
}