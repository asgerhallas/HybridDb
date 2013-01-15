namespace HybridDb.Linq.Ast
{
    internal class SqlQueryExpression : SqlExpression
    {
        readonly SqlExpression orderBy;
        readonly SqlExpression select;
        readonly SqlWhereExpression where;

        public SqlQueryExpression(SqlExpression select, SqlWhereExpression where, SqlExpression orderBy)
        {
            this.where = where;
            this.select = select;
            this.orderBy = orderBy;
        }

        public SqlExpression Select
        {
            get { return @select; }
        }

        public SqlWhereExpression Where
        {
            get { return @where; }
        }

        public SqlExpression OrderBy
        {
            get { return orderBy; }
        }

        public override SqlNodeType NodeType
        {
            get { return SqlNodeType.Query; }
        }
    }
}