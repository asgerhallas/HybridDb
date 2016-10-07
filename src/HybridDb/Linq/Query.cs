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
                throw new ArgumentNullException(nameof(provider));
            
            this.provider = provider;
            expression = Expression.Constant(this);
        }

        public Query(IHybridQueryProvider provider, Expression expression)
        {
            if (provider == null)
                throw new ArgumentNullException(nameof(provider));
            
            if (expression == null)
                throw new ArgumentNullException(nameof(expression));
            
            if (!typeof (IQueryable<T>).IsAssignableFrom(expression.Type))
                throw new ArgumentOutOfRangeException(nameof(expression));
            
            this.provider = provider;
            this.expression = expression;
        }

        Expression IQueryable.Expression => expression;
        Type IQueryable.ElementType => typeof (T);
        IQueryProvider IQueryable.Provider => provider;

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
            return provider.GetQueryText(((IQueryable)this).Expression).ToString();
        }
    }
}