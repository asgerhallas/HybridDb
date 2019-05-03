using System.Linq;
using System.Linq.Expressions;
using HybridDb.Linq;
using Xunit;

namespace HybridDb.Tests
{
    public class QueryProviderTests : HybridDbTests
    {
        [Fact(Skip="Not yet implemented")]
        public void CanExecuteUntypedExpression()
        {
            Document<Entity>().With(x => x.Property);

            var queryProvider = new QueryProvider(
                (DocumentSession) store.OpenSession(),
                store.Configuration.GetOrCreateDesignFor(typeof(Entity)));

            queryProvider.ExecuteEnumerable<object>(
                Expression.Call(
                    Expression.Constant(new object()),
                    typeof(Queryable).GetMethod("Where"),
                    Expression.Lambda(
                        Expression.MakeBinary(
                            ExpressionType.Equal,
                            Expression.Constant(1),
                            Expression.Constant(1)))));
        }
    }
}