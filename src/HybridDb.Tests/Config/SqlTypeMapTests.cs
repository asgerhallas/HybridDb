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
        [InlineData(typeof(string), "1024")]
        [InlineData(typeof(SomeEnum), "255")]
        [InlineData(typeof(byte[]), "MAX")]
        [InlineData(typeof(double), null)]
        [InlineData(typeof(decimal), "28, 14")]
        [InlineData(typeof(bool), null)]
        [InlineData(typeof(byte), null)]
        [InlineData(typeof(long), null)]
        [InlineData(typeof(int), null)]
        [InlineData(typeof(short), null)]
        [InlineData(typeof(Guid), null)]
        [InlineData(typeof(DateTime), null)]
        [InlineData(typeof(DateTimeOffset), null)]
        [InlineData(typeof(Single), null)]
        public void ConvertGivesCorrectDefaultLength(Type columnType, string expectedLenght)
        {
            var sqlColumn = SqlTypeMap.Convert(new Column("SomeColumn", columnType, null));
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
        public void ConvertGivesCorrectLength(Type columnType)
        {
            var sqlColumn = SqlTypeMap.Convert(new Column("SomeColumn", columnType, length: 42));
            sqlColumn.Length.ShouldBe("42");
        }

        //[Theory]
        //[InlineData(typeof(string), DbType.String)]
        //[InlineData(typeof(SomeEnum), DbType.String)]
        //[InlineData(typeof(byte[]), DbType.Binary)]
        //[InlineData(typeof(double), DbType.Double)]
        //[InlineData(typeof(decimal), DbType.Decimal)]
        //[InlineData(typeof(bool), DbType.Boolean)]
        //[InlineData(typeof(byte), DbType.Byte)]
        //[InlineData(typeof(long), DbType.Int64)]
        //[InlineData(typeof(int), DbType.Int32)]
        //[InlineData(typeof(short), DbType.Int16)]
        //[InlineData(typeof(Guid), DbType.Guid)]
        //[InlineData(typeof(DateTime), DbType.DateTime2)]
        //[InlineData(typeof(DateTimeOffset), DbType.DateTimeOffset)]
        //[InlineData(typeof(Single), DbType.Single)]
        //[InlineData(typeof(TimeSpan), DbType.Time)]u
        //public void ConvertGivesCorrectType(Type columnType, DbType expectedType)
        //{
        //    var sqlColumn = SqlTypeMap.Convert(new Column("SomeColumn", columnType, 42));
        //    sqlColumn.DbType.ShouldBe(expectedType);
        //}

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