using System;
using HybridDb.Linq;
using HybridDb.Linq.Bonsai;
using HybridDb.Linq.Compilers;
using Shouldly;
using Xunit;
using Xunit.Extensions;

namespace HybridDb.Tests.Linq.Compilers
{
    public class SqlEmitterTests
    {
        readonly Emitter emitSql;

        public SqlEmitterTests() => emitSql = CompilerBuilder.Compose(SqlEmitter.Emit);

        [Theory]
        [InlineData(ComparisonOperator.Equal, "Text = 'Asger'")]
        [InlineData(ComparisonOperator.NotEqual, "Text != 'Asger'")]
        [InlineData(ComparisonOperator.GreaterThan, "Text > 'Asger'")]
        [InlineData(ComparisonOperator.GreaterThanOrEqualTo, "Text >= 'Asger'")]
        [InlineData(ComparisonOperator.LessThan, "Text < 'Asger'")]
        [InlineData(ComparisonOperator.LessThanOrEqualTo, "Text <= 'Asger'")]
        public void ComparisonEqual(ComparisonOperator @operator, string expectedSql) => emitSql(
            new Comparison(@operator, new Column("Text", false, typeof(string)), new Constant("Asger", typeof(string)))
        ).ShouldBe(expectedSql);

        [Theory]
        [InlineData((short)5, typeof(short), "5")]
        [InlineData((ushort)5, typeof(ushort), "5")]
        [InlineData(1, typeof(int), "1")]
        [InlineData((uint)1, typeof(uint), "1")]
        [InlineData(2L, typeof(long), "2")]
        [InlineData((ulong)2L, typeof(ulong), "2")]
        [InlineData(1.2f, typeof(float), "1.2")]
        [InlineData(1.1, typeof(double), "1.1")]
        [InlineData("hello", typeof(string), "'hello'")]
        [InlineData(MyEnum.Y, typeof(MyEnum), "'Y'")]
        [InlineData(true, typeof(bool), "1")]
        [InlineData(false, typeof(bool), "0")]
        public void Constants(object constant, Type type, string expectedSql) => emitSql(new Constant(constant, type)).ShouldBe(expectedSql);

        [Theory]
        [InlineData(BinaryLogicOperator.OrElse, "Text = 'a' OR Text = 'b'")]
        [InlineData(BinaryLogicOperator.AndAlso, "Text = 'a' AND Text = 'b'")]
        public void BinaryLogic(BinaryLogicOperator @operator, string expectedSql) => emitSql(
            new BinaryLogic(@operator,
                new Comparison(ComparisonOperator.Equal, new Column("Text", false, typeof(string)), new Constant("a", typeof(string))),
                new Comparison(ComparisonOperator.Equal, new Column("Text", false, typeof(string)), new Constant("b", typeof(string))))
        ).ShouldBe(expectedSql);

        [Theory]
        [InlineData("Key", "Id")]
        public void Metadata(string columnName, string expectedSql) => emitSql(new Column(columnName, true, typeof(string))).ShouldBe(expectedSql);

        [Fact]
        public void Not() => emitSql(
            new UnaryLogic(UnaryLogicOperator.Not,
                new Comparison(ComparisonOperator.Equal,
                    new Column("Text", false, typeof(string)),
                    new Constant("Goodbye", typeof(string))))
        ).ShouldBe("NOT (Text = 'Goodbye')");

        enum MyEnum
        {
            X, Y
        }
    }
}