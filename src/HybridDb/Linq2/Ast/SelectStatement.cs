namespace HybridDb.Linq2.Ast
{
    public class SelectStatement : AstNode
    {
        public SelectStatement(From from) : this(@from, null) {}

        public SelectStatement(From from, Where where)
        {
            From = from;
            Where = @where;
        }

        public From From { get; }
        public Select Select { get; } = new Select();
        public Where Where { get; }
    }
}