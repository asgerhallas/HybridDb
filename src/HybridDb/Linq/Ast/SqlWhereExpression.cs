using System.Linq.Expressions;

namespace HybridDb.Linq.Ast
{
    public class SqlWhereExpression : SqlExpression
    {
        readonly SqlBinaryExpression predicate;
        bool top1 = false;

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