namespace HybridDb.Linq2.Ast
{
    public class Projection
    {
        public Projection(ColumnIdentifier column)
        {
            Column = column;
        }

        public Projection(ColumnIdentifier column, string @as)
        {
            Column = column;
            As = @as;
        }

        public ColumnIdentifier Column { get; }
        public string As { get; }
    }
}