namespace HybridDb.Linq2.Ast
{
    public class From : Clause
    {
        public From(string table)
        {
            Table = table;
        }

        public string Table { get; }
    }
}