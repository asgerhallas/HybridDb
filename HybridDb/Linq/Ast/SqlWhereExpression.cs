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

    internal class SqlSelectExpression : SqlExpression
    {
        readonly SqlExpression projection;

        public SqlSelectExpression(SqlProjectionExpression projection)
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