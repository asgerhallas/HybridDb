using System.Collections.Generic;

namespace HybridDb.Linq2.Ast
{
    public class SelectStatement : SqlExpression
    {
        public SelectStatement(From from) : this(new Select(), from, new Where(new True())) {}
        public SelectStatement(Select select, From from) : this(select, @from, new Where(new True())) {}

        public SelectStatement(Select select, From from, Where where)
        {
            Select = select;
            From = from;
            Where = where;

            Clauses = new List<SqlClause>
            {
                select,
                from,
                where
            };
        }

        public Select Select { get; private set; }
        public From From { get; private set; }
        public Where Where { get; private set; }

        public IReadOnlyList<SqlClause> Clauses { get; }
    }
}