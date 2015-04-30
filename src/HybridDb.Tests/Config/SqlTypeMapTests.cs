using System;
using System.Data;
using HybridDb.Config;
using Shouldly;
using Xunit.Extensions;

namespace HybridDb.Tests.Config
{
    public class SqlTypeMapTests
    {
        [Theory]
        [InlineData(typeof(string), null, Int32.MaxValue)]
        [InlineData(typeof(SomeEnum), null, 255)]
        [InlineData(typeof(byte[]), null, Int32.MaxValue)]
        [InlineData(typeof(double), null, null)]
        [InlineData(typeof(decimal), null, null)]
        [InlineData(typeof(bool), null, null)]
        [InlineData(typeof(byte), null, null)]
        [InlineData(typeof(long), null, null)]
        [InlineData(typeof(int), null, null)]
        [InlineData(typeof(short), null, null)]
        [InlineData(typeof(Guid), null, null)]
        [InlineData(typeof(DateTime), null, null)]
        [InlineData(typeof(DateTimeOffset), null, null)]
        [InlineData(typeof(Single), null, null)]
        [InlineData(typeof(TimeSpan), null, null)]
        public void ConvertGivesCorrectDefaultLength(Type columnType, int? length, int? expectedLenght)
        {
            var sqlColumn = SqlTypeMap.Convert(new Column("SomeColumn", columnType, length));
            sqlColumn.Length.ShouldBe(expectedLenght);
        }

        [Theory]
        [InlineData(typeof (string))]
        [InlineData(typeof (SomeEnum))]
        [InlineData(typeof (byte[]))]
        [InlineData(typeof (double))]
        [InlineData(typeof (decimal))]
        [InlineData(typeof (long))]
        [InlineData(typeof (int))]
        [InlineData(typeof(bool))]
        [InlineData(typeof(byte))]
        [InlineData(typeof(short))]
        [InlineData(typeof (Guid))]
        [InlineData(typeof (DateTime))]
        [InlineData(typeof (DateTimeOffset))]
        [InlineData(typeof (Single))]
        [InlineData(typeof (TimeSpan))]
        public void ConvertGivesCorrectLength(Type columnType)
        {
            var sqlColumn = SqlTypeMap.Convert(new Column("SomeColumn", columnType, length: 42));
            sqlColumn.Length.ShouldBe(42);
        }

        [Theory]
        [InlineData(typeof(string), DbType.String)]
        [InlineData(typeof(SomeEnum), DbType.String)]
        [InlineData(typeof(byte[]), DbType.Binary)]
        [InlineData(typeof(double), DbType.Double)]
        [InlineData(typeof(decimal), DbType.Decimal)]
        [InlineData(typeof(bool), DbType.Boolean)]
        [InlineData(typeof(byte), DbType.Byte)]
        [InlineData(typeof(long), DbType.Int64)]
        [InlineData(typeof(int), DbType.Int32)]
        [InlineData(typeof(short), DbType.Int16)]
        [InlineData(typeof(Guid), DbType.Guid)]
        [InlineData(typeof(DateTime), DbType.DateTime2)]
        [InlineData(typeof(DateTimeOffset), DbType.DateTimeOffset)]
        [InlineData(typeof(Single), DbType.Single)]
        [InlineData(typeof(TimeSpan), DbType.Time)]
        public void ConvertGivesCorrectType(Type columnType, DbType expectedType)
        {
            var sqlColumn = SqlTypeMap.Convert(new Column("SomeColumn", columnType, 42));
            sqlColumn.DbType.ShouldBe(expectedType);
        }

        [Theory]
        [InlineData(typeof(object))]
        [InlineData(typeof(SomeClass))]
        public void ThrowsIfUnknownType(Type columnType)
        {
            Should.Throw<ArgumentException>(() => SqlTypeMap.Convert(new Column("SomeColumn", columnType)));
        }

        class SomeClass {}
        enum SomeEnum { }
    }
}