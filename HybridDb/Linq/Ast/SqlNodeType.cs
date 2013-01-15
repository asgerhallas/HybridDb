namespace HybridDb.Linq.Ast
{
    internal enum SqlNodeType
    {
        BLAH,
        Query,
        Select,
        Where,
        And,
        Or,
        Equal,
        Constant,
        Column,
        Argument,
    }
}