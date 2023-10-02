using System;
using System.Data;
using Microsoft.Data.SqlClient;
using HybridDb.Config;
using ShouldBeLike;
using Shouldly;
using Xunit;

namespace HybridDb.Tests
{
    public class HybridDbParametersTests
    {
        [Fact]
        public void AddWithColumn_NameRequired()
        {
            var parameters = new HybridDbParameters();

            Should.Throw<ArgumentNullException>(() => parameters.Add(null, null, null));
        }

        [Fact]
        public void AddWithColumn_ColumnRequired()
        {
            var parameters = new HybridDbParameters();

            Should.Throw<ArgumentNullException>(() => parameters.Add("name", null, null));
        }

        [Fact]
        public void AddWithColumn_Added()
        {
            var parameters = new HybridDbParameters();

            parameters.Add("name", "dummy", new Column("", SqlDbType.NVarChar, typeof(string)));

            parameters.Parameters.ShouldBeLike(new SqlParameter
            {
                DbType = DbType.AnsiStringFixedLength,
                ParameterName = "name",
                SqlDbType = SqlDbType.NVarChar,
                SqlValue = "dummy",
                Value = "dummy"
            });
        }

        [Fact]
        public void AddWithDbType_NameRequired()
        {
            var parameters = new HybridDbParameters();

            Should.Throw<ArgumentNullException>(() => parameters.Add(null, null, SqlDbType.NVarChar));
        }

        [Fact]
        public void AddWithDbType_Added()
        {
            var parameters = new HybridDbParameters();

            parameters.Add("name", "dummy", SqlDbType.NVarChar);

            parameters.Parameters.ShouldBeLike(new SqlParameter
            {
                DbType = DbType.AnsiStringFixedLength,
                ParameterName = "name",
                SqlDbType = SqlDbType.NVarChar,
                SqlValue = "dummy",
                Value = "dummy"
            });
        }
    }
}