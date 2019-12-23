namespace HybridDb.Linq.Old.Ast
{
    public class SqlOrderingExpression : SqlExpression
    {
        public SqlOrderingExpression(Directions direction, SqlColumnExpression column)
        {
            Direction = direction;
            Column = column;
        }

        public Directions Direction { get; private set; }
        public SqlColumnExpression Column { get; private set; }

        public override SqlNodeType NodeType
        {
            get { return SqlNodeType.Ordering; }
        }

        public enum Directions
        {
            Ascending,
            Descending
        }
    }
}