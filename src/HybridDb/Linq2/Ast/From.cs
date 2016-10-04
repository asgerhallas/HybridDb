namespace HybridDb.Linq2.Ast
{
    public class From : SqlClause
    {
        public From(string table)
        {
            Table = table;
        }

        public string Table { get; }
    }
}