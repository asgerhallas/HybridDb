using System.Data;
using HybridDb.Config;
using HybridDb.SqlBuilder;
using Microsoft.Data.SqlClient;
using ShouldBeLike;
using Shouldly;
using Xunit;
using Table = HybridDb.Config.Table;

namespace HybridDb.Tests.SqlBuilder
{
    public class SqlTests
    {
        readonly DocumentStore store = DocumentStore.ForTesting(TableMode.GlobalTempTables);

        [Fact]
        public void AppendWithParameter()
        {
            var myParam = "asger";
            var sql = Sql.From($"select {myParam}");

            sql.Build(store, out var parameters)
                .ShouldBe("select @MyParam_1");

            parameters.Parameters.ShouldBeLike(
                new SqlParameter("MyParam_1", SqlDbType.NVarChar)
                {
                    Value = "asger"
                });
        }

        [Fact]
        public void AppendWithParameter_NoVariable()
        {
            var sql = Sql.From($"select {1}");

            sql.Build(store, out var parameters)
                .ShouldBe("select @Param_1");

            parameters.Parameters.ShouldBeLike(
                new SqlParameter("Param_1", SqlDbType.Int)
                {
                    Value = 1
                });
        }

        [Fact]
        public void AppendWithParameter_ComplexExpression()
        {
            var x = "test";
            // ReSharper disable once RedundantToStringCall
            var sql = Sql.From($"select {x.ToString() + 33}");

            sql.Build(store, out var parameters)
                .ShouldBe("select @Param_1");

            parameters.Parameters.ShouldBeLike(
                new SqlParameter("Param_1", SqlDbType.NVarChar)
                {
                    Value = "test33"
                });
        }

        [Fact]
        public void AppendWithParameter_Multiple()
        {
            var myParam = "asger";
            var sql = Sql.From($"select {myParam}, {1}");

            sql.Build(store, out var parameters)
                .ShouldBe("select @MyParam_1, @Param_2");

            parameters.Parameters.ShouldBeLike(
                new SqlParameter("MyParam_1", SqlDbType.NVarChar)
                {
                    Value = "asger"
                },
                new SqlParameter("Param_2", SqlDbType.Int)
                {
                    Value = 1
                });
        }

        [Fact]
        public void AppendOtherBuilderWithParameters()
        {
            var myParam = "asger";
            var sql = Sql.From($"select {myParam}");

            sql.Append(Sql.From($"where x = {myParam}"));

            sql.Build(store, out var parameters)
                .ShouldBe("select @MyParam_1 where x = @MyParam_2");

            parameters.Parameters.ShouldBeLike(
                new SqlParameter("MyParam_1", SqlDbType.NVarChar)
                {
                    Value = "asger"
                },
                new SqlParameter("MyParam_2", SqlDbType.NVarChar)
                {
                    Value = "asger"
                });
        }

        [Fact]
        public void AppendTable() =>
            Sql.From($"select {1} from {new Table("MyTable")}").Build(store, out _)
                .ShouldBe($"select @Param_1 from {store.Database.FormatTableNameAndEscape("MyTable")}");

        [Fact]
        public void AppendTable_ByModifier() =>
            Sql.From($"select {1} from {"MyTable":table}").Build(store, out _)
                .ShouldBe($"select @Param_1 from {store.Database.FormatTableNameAndEscape("MyTable")}");

        [Fact]
        public void AppendColumn() =>
            Sql.From($"select {1} from {new Column("MyColumn", typeof(int))}")
                .Build(store, out _)
                .ShouldBe($"select @Param_1 from {store.Database.Escape("MyColumn")}");

        [Fact]
        public void AppendColumnByNameOf() =>
            Sql.From($"select * from {nameof(AppendColumnByNameOf)}")
                .Build(store, out _)
                .ShouldBe($"select * from {store.Database.Escape("AppendColumnByNameOf")}");

        [Fact]
        public void AppendColumnByNameOf_Dotted() =>
            // ReSharper disable once ArrangeStaticMemberQualifier
            Sql.From($"select * from {nameof(SqlTests.AppendColumnByNameOf_Dotted)}")
                .Build(store, out _)
                .ShouldBe($"select * from {store.Database.Escape("AppendColumnByNameOf_Dotted")}");

        [Fact]
        public void AppendColumn_ByModifier() =>
            Sql.From($"select {1} from {"MyColumn":column}")
                .Build(store, out _)
                .ShouldBe($"select @Param_1 from {store.Database.Escape("MyColumn")}");

        [Fact]
        public void AppendInfix()
        {
            Sql.From("1<>0")
                .Append("and", "1=1")
                .Build(store, out _)
                .ShouldBe($"1<>0 and 1=1");

            Sql.Empty
                .Append("and", "1=1")
                .Build(store, out _)
                .ShouldBe($"1=1");

            Sql.From("1<>0")
                .Append("and", Sql.Empty)
                .Build(store, out _)
                .ShouldBe($"1<>0");
        }

        [Fact]
        public void AppendVerbatimString_ByModifier()
        {
            var alias = "table";

            Sql.From($"select 1 as {alias:verbatim}")
                .Build(store, out _)
                .ShouldBe("select 1 as table");

            Sql.From($"select 1 as {alias:@}")
                .Build(store, out _)
                .ShouldBe("select 1 as table");
        }

        [Fact]
        public void Append_AddSpaces() =>
            Sql.From($"select 1")
                .Append(",")
                .Append("X")
                .Append(".Y")
                .Append($"from {"table":table}.{"column":column}")
                .Append($"where {"text":verbatim} and")
                .Append($"{"othertext":verbatim}")
                .Build(store, out _)
                .ShouldBe($"select 1, X.Y from {store.Database.FormatTableNameAndEscape("table")}.[column] where text and othertext");
    }
}