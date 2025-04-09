using System.Linq;
using HybridDb.Migrations.BuiltIn;
using HybridDb.Queue;
using HybridDb.SqlBuilder;
using ShouldBeLike;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.Tests.Migrations.BuiltIn
{
    public class UpdateCorrelationId_Tests : HybridDbTests
    {
        public UpdateCorrelationId_Tests(ITestOutputHelper output) : base(output) { }

        [Theory]
        [InlineData("UpdateCorrelationId_Tests_Before.sql")]
        public void Test(string beforeFilename)
        {
            UseRealTables();

            Setup(SiblingFile(beforeFilename));

            ResetConfiguration();

            configuration.UseMessageQueue();

            UseMigrations(new UpdateCorrelationId(1));

            TouchStore();

            store.Database.RawQuery<string>(Sql.From("select CorrelationId from messages"))
                .ToList()
                .ShouldBeLike("id1", "id3" , "N/A");
        }
    }
}