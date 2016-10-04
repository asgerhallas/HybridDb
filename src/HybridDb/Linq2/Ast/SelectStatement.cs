namespace HybridDb.Linq2.Ast
{
    public class SelectStatement : SqlExpression
    {
        public SelectStatement(From from) : this(new Select(), @from, null) {}

        public SelectStatement(Select select, From from, Where where)
        {
            Select = @select;
            From = from;
            Where = @where;
        }

        public From From { get; }
        public Select Select { get; }
        public Where Where { get; }
    }
}