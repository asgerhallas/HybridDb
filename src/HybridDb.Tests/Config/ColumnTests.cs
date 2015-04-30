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

        [Theory]
        [InlineData(typeof(bool), true)]
        [InlineData(typeof(int), 1)]
        [InlineData(typeof(string), "some string")]
        [InlineData(typeof(double), 1.2)]
        [InlineData(typeof(int?), 1)]
        [InlineData(typeof(double?), 1.2)]
        [InlineData(typeof(bool?), true)]
        public void CanAddDefaultValue(Type columnType, object defaultValue)
        {
             new Column("SomeColumn", columnType, defaultValue: defaultValue).DefaultValue.ShouldBe(defaultValue);
        }

        [Theory]
        [InlineData(typeof(bool), "1")]
        [InlineData(typeof(bool), "True")]
        [InlineData(typeof(bool), 1)]
        [InlineData(typeof(int), "1")]
        [InlineData(typeof(string), 1)]
        [InlineData(typeof(Guid), "C3CE3F60-9353-4830-8511-F82A5A727EA3")]
        [InlineData(typeof(DateTime), "1999-12-02")]
        public void ThrowsIfDefaultValueIsNotOfColumnType(Type columnType, object defaultValue)
        {
            Should.Throw<ArgumentException>(() => new Column("SomeColumn", columnType, defaultValue: defaultValue));
        }

        enum SomeEnum{}
    }
}