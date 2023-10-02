using System;
using System.Data;
using HybridDb.Config;
using Shouldly;
using Xunit;

namespace HybridDb.Tests.Config
{
    public class SqlTypeMapTests
    {
        [Theory]
        [InlineData(typeof(string), "850")]
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
        [InlineData(typeof(float), null)]
        [InlineData(typeof(DateOnly), null)]
        public void ConvertGivesCorrectDefaultLength(Type columnType, string expectedLength)
        {
            var sqlColumn = SqlTypeMap.Convert(new Column("SomeColumn", columnType, null));
            sqlColumn.Length.ShouldBe(expectedLength);
        }

        [Theory]
        [InlineData(typeof(string))]
        [InlineData(typeof(SomeEnum))]
        [InlineData(typeof(byte[]))]
        [InlineData(typeof(double))]
        [InlineData(typeof(decimal))]
        [InlineData(typeof(long))]
        [InlineData(typeof(int))]
        [InlineData(typeof(bool))]
        [InlineData(typeof(byte))]
        [InlineData(typeof(short))]
        [InlineData(typeof(Guid))]
        [InlineData(typeof(DateTime))]
        [InlineData(typeof(DateTimeOffset))]
        [InlineData(typeof(float))]
        [InlineData(typeof(DateOnly))]
        public void ConvertGivesCorrectLength(Type columnType)
        {
            var sqlColumn = SqlTypeMap.Convert(new Column("SomeColumn", columnType, 42));
            sqlColumn.Length.ShouldBe("42");
        }

        [Theory]
        [InlineData(typeof(string), SqlDbType.NVarChar)]
        [InlineData(typeof(SomeEnum), SqlDbType.NVarChar)]
        [InlineData(typeof(byte[]), SqlDbType.VarBinary)]
        [InlineData(typeof(double), SqlDbType.Float)]
        [InlineData(typeof(decimal), SqlDbType.Decimal)]
        [InlineData(typeof(bool), SqlDbType.Bit)]
        [InlineData(typeof(byte), SqlDbType.TinyInt)]
        [InlineData(typeof(long), SqlDbType.BigInt)]
        [InlineData(typeof(int), SqlDbType.Int)]
        [InlineData(typeof(short), SqlDbType.SmallInt)]
        [InlineData(typeof(Guid), SqlDbType.UniqueIdentifier)]
        [InlineData(typeof(DateTime), SqlDbType.DateTime2)]
        [InlineData(typeof(DateTimeOffset), SqlDbType.DateTimeOffset)]
        [InlineData(typeof(float), SqlDbType.Real)]
        [InlineData(typeof(DateOnly), SqlDbType.Date)]
        public void ConvertGivesCorrectType(Type columnType, SqlDbType expectedType)
        {
            var sqlColumn = SqlTypeMap.Convert(new Column("SomeColumn", columnType, 42));
            sqlColumn.DbType.ShouldBe(expectedType);
        }

        [Theory]
        [InlineData(typeof(object))]
        [InlineData(typeof(SomeClass))]
        public void ThrowsIfUnknownType(Type columnType) =>
            Should.Throw<ArgumentException>(() => SqlTypeMap.Convert(new Column("SomeColumn", columnType)));

        class SomeClass { }
        enum SomeEnum { }
    }
}