namespace HybridDb.Linq.Old.Ast
{
    public class SqlQueryExpression : SqlExpression
    {
        readonly SqlExpression orderBy;
        readonly SqlExpression select;
        readonly SqlExpression where;

        public SqlQueryExpression(SqlExpression select, SqlExpression where, SqlExpression orderBy)
        {
            this.where = where;
            this.select = select;
            this.orderBy = orderBy;
        }

        public SqlExpression Select
        {
            get { return @select; }
        }

        public SqlExpression Where
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