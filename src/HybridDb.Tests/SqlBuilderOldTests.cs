using Microsoft.Data.SqlClient;
using Shouldly;
using Xunit;

namespace HybridDb.Tests
{
    public class SqlBuilderOldTests
    {
        [Fact]
        public void CtorDoesNotFailWithNullValues()
        {
            var sql = new SqlBuilderOld(null, null);

            sql.ToString().ShouldBe("");
            sql.Parameters.Count.ShouldBe(0);
        }

        [Fact]
        public void CtorDoesNotFailWithNullValuesParameters()
        {
            var sql = new SqlBuilderOld("Dummy", null);

            sql.ToString().ShouldBe("Dummy");
            sql.Parameters.Count.ShouldBe(0);
        }

        [Fact]
        public void CtorDoesNotFailWithNullValuesSql()
        {
            var sql = new SqlBuilderOld(null, new SqlParameter("@Dummy", "Dummy"));

            sql.ToString().ShouldBe("");
            sql.Parameters.Count.ShouldBe(1);
        }

        [Fact]
        public void AppendDoesNotFailWithNullValues()
        {
            var sql = new SqlBuilderOld();

            Should.NotThrow(() => sql.Append(null, null));

            sql.ToString().ShouldBe("");
            sql.Parameters.Count.ShouldBe(0);
        }

        [Fact]
        public void AppendDoesNotFailWithNullValuesParameters()
        {
            var sql = new SqlBuilderOld();

            Should.NotThrow(() => sql.Append("Dummy", null));

            sql.ToString().ShouldBe("Dummy");
            sql.Parameters.Count.ShouldBe(0);
        }

        [Fact]
        public void AppendDoesNotFailWithNullValuesSql()
        {
            var sql = new SqlBuilderOld();

            Should.NotThrow(() => sql.Append(null, new SqlParameter("@Dummy", "Dummy")));

            sql.ToString().ShouldBe("");
            sql.Parameters.Count.ShouldBe(1);
        }
    }
}