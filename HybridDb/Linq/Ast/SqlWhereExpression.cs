using System.Linq.Expressions;

namespace HybridDb.Linq.Ast
{
    internal class SqlWhereExpression : SqlExpression
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

    internal class SqlSelectExpression : SqlExpression
    {
        readonly SqlExpression projection;

        public SqlSelectExpression(SqlExpression projection)
        {
            this.projection = projection;
        }

        public SqlExpression Projection
        {
            get { return projection; }
        }

        public override SqlNodeType NodeType
        {
            get { return SqlNodeType.Where; }
        }
    }
}