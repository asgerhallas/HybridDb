using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace HybridDb.Linq
{
    public class Query<T> : IOrderedQueryable<T>
    {
        readonly Expression expression;
        readonly IHybridQueryProvider provider;

        public Query(IHybridQueryProvider provider)
        {
            if (provider == null)
                throw new ArgumentNullException("provider");
            
            this.provider = provider;
            expression = Expression.Constant(this);
        }

        public Query(IHybridQueryProvider provider, Expression expression)
        {
            if (provider == null)
                throw new ArgumentNullException("provider");
            
            if (expression == null)
                throw new ArgumentNullException("expression");
            
            if (!typeof (IQueryable<T>).IsAssignableFrom(expression.Type))
                throw new ArgumentOutOfRangeException("expression");
            
            this.provider = provider;
            this.expression = expression;
        }

        Expression IQueryable.Expression
        {
            get { return expression; }
        }

        Type IQueryable.ElementType
        {
            get { return typeof (T); }
        }

        IQueryProvider IQueryable.Provider
        {
            get { return provider; }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return provider.ExecuteEnumerable<T>(expression).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public override string ToString()
        {
            return provider.GetQueryText(this);
        }
    }
}