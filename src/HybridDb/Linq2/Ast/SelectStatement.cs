using System.Collections.Generic;
using System.Linq;
using HybridDb.Linq;
using ShinySwitch;

namespace HybridDb.Linq2.Ast
{
    public class SelectStatement : SqlExpression
    {
        public SelectStatement(From from) : this(ListOf(new Select(), @from)) {}
        public SelectStatement(Select select, From from) : this(ListOf(select, @from)) {}
        public SelectStatement(Select select, From from, Where where) : this(ListOf(select, @from, where)) {}

        SelectStatement(IEnumerable<SqlClause> clauses)
        {
            Clauses = clauses
                .Where(clause => clause != null)
                .Do(clause => Switch.On(clause)
                    .Match<From>(@from => From = @from))
                .ToList();
        }

        public From From { get; private set; }

        public IReadOnlyList<SqlClause> Clauses { get; }

        static IEnumerable<SqlClause> ListOf(params SqlClause[] items)
        {
            return items.ToList();
        }
    }
}