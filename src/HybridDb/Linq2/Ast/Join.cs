namespace HybridDb.Linq2.Ast
{
    public class Join : SqlClause
    {
        public Join(TableName table, Predicate condition)
        {
            Table = table;
            Condition = condition;
        }

        public TableName Table { get; }
        public Predicate Condition { get; }
    }
}