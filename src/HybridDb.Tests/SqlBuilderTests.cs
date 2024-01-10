using Microsoft.Data.SqlClient;
using Shouldly;
using Xunit;

namespace HybridDb.Tests
{
    public class SqlBuilderTests
    {
        [Fact]
        public void CtorDoesNotFailWithNullValues()
        {
            var sql = new SqlBuilder(null, null);

            sql.ToString().ShouldBe("");
            sql.Parameters.Count.ShouldBe(0);
        }

        [Fact]
        public void CtorDoesNotFailWithNullValuesParameters()
        {
            var sql = new SqlBuilder("Dummy", null);

            sql.ToString().ShouldBe("Dummy");
            sql.Parameters.Count.ShouldBe(0);
        }

        [Fact]
        public void CtorDoesNotFailWithNullValuesSql()
        {
            var sql = new SqlBuilder(null, new SqlParameter("@Dummy", "Dummy"));

            sql.ToString().ShouldBe("");
            sql.Parameters.Count.ShouldBe(1);
        }

        [Fact]
        public void AppendDoesNotFailWithNullValues()
        {
            var sql = new SqlBuilder();

            Should.NotThrow(() => sql.Append(null, null));

            sql.ToString().ShouldBe("");
            sql.Parameters.Count.ShouldBe(0);
        }

        [Fact]
        public void AppendDoesNotFailWithNullValuesParameters()
        {
            var sql = new SqlBuilder();

            Should.NotThrow(() => sql.Append("Dummy", null));

            sql.ToString().ShouldBe("Dummy");
            sql.Parameters.Count.ShouldBe(0);
        }

        [Fact]
        public void AppendDoesNotFailWithNullValuesSql()
        {
            var sql = new SqlBuilder();

            Should.NotThrow(() => sql.Append(null, new SqlParameter("@Dummy", "Dummy")));

            sql.ToString().ShouldBe("");
            sql.Parameters.Count.ShouldBe(1);
        }
    }
}