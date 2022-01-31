using HybridDb.Linq.Old;
using ShouldBeLike;
using Xunit;

namespace HybridDb.Tests
{
    public class SqlBuilderTests
    {
        [Fact]
        public void Params_ByInterpolation()
        {
            var sqlBuilder = new SqlBuilder().Append($"select {1}");

            sqlBuilder.Parameters.ShouldBeLike();
        }


        [Fact]
        public void PlayGround()
        {
            var builder = new SqlBuilder<Entity>(x => x
                .Append(x.Col.Property, x.Table)
                ));

            var sqlBuilder = builder
                .Append($"select {nameof(Entity.Property)} from {builder.TableFor<Entity>}");

            sqlBuilder.Parameters.ShouldBeLike();
        }

        public class Entity
        {
            public string Property { get; set; }
        }
    }
}