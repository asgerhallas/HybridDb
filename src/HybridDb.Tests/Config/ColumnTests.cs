using System;
using HybridDb.Config;
using Shouldly;
using Xunit;
using Xunit.Extensions;

namespace HybridDb.Tests.Config
{
    public class ColumnTests
    {
        [Fact]
        public void ThrowsIfConfiguringDefaultValueForByteArray()
        {
            Should.Throw<ArgumentException>(() => new Column("SomeColumn", typeof (byte[]), defaultValue: "someparam"));
        }

        [Theory]
        [InlineData(typeof(string))]
        [InlineData(typeof(int?))]
        [InlineData(typeof(double?))]
        [InlineData(typeof(Guid?))]
        public void PrimaryKeyColumnCanNotBeNullable(Type columnType)
        {
            var column = new Column("SomeColumn", columnType, isPrimaryKey: true);
            column.Nullable.ShouldBe(false);
        }

        [Theory]
        [InlineData(typeof(string), true)]
        [InlineData(typeof(int?), true)]
        [InlineData(typeof(decimal?), true)]
        [InlineData(typeof(double?), true)]
        [InlineData(typeof(Guid?), true)]
        [InlineData(typeof(SomeEnum?), true)]
        [InlineData(typeof(int), false)]
        [InlineData(typeof(double), false)]
        [InlineData(typeof(decimal), false)]
        [InlineData(typeof(Guid), false)]
        [InlineData(typeof(SomeEnum), false)]
        public void ColumnGetsNullabilityFromType(Type columnType, bool isNullable)
        {
            var column = new Column("SomeColumn", columnType);
            column.Nullable.ShouldBe(isNullable);
        }

        enum SomeEnum{}
    }
}