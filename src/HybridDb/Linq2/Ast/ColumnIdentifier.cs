namespace HybridDb.Linq2.Ast
{
    public class ColumnIdentifier : Expression
    {
        public ColumnIdentifier(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }
}