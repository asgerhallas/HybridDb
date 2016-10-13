namespace HybridDb.Linq2.Ast
{
    public class TableName : SqlExpression
    {
        public TableName(string name)
        {
            Name = name;
            SuggestedAlias = name;
        }

        public TableName(string name, string suggestedAlias)
        {
            Name = name;
            SuggestedAlias = suggestedAlias;
        }

        public string Name { get; }
        public string SuggestedAlias { get; }
    }
}