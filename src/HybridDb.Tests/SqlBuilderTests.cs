using System;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.Tests
{
    public class SqlBuilderTests
    {
        readonly ITestOutputHelper output;

        public SqlBuilderTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void Uniqueness()
        {
            var sqlBuilder = new SqlBuilder();

            for (int i = 0; i < 100000; i++)
            {
                try
                {
                    sqlBuilder.Append(new SqlBuilder());
                }
                catch (Exception e)
                {
                    output.WriteLine(i.ToString());
                    throw;
                }
            }
        }
    }
}