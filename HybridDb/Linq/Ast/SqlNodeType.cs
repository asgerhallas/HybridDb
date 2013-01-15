namespace HybridDb.Linq.Ast
{
    public enum SqlNodeType
    {
        Query,
        Select,
        Where,
        And,
        Or,
        Equal,
        Constant,
        Column
    }
}