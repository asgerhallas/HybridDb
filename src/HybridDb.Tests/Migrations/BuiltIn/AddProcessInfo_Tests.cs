using System.Linq;
using HybridDb.Migrations.BuiltIn;
using HybridDb.Queue;
using ShouldBeLike;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.Tests.Migrations.BuiltIn
{
    public class AddProcessInfo_Tests : HybridDbTests
    {
        public AddProcessInfo_Tests(ITestOutputHelper output) : base(output) { }

        [Theory]
        [InlineData("AddProcessInfo_Tests_Before_ProcessInfo.sql")]
        public void SchemaChanges_1(string beforeFilename)
        {
            UseRealTables();

            Setup(SiblingFile(beforeFilename));

            ResetConfiguration();

            configuration.UseMessageQueue();

            UseMigrations(new AddProcessInfo(1));

            TouchStore();

            store.Database.RawQuery<string>("select ProcessInfo from messages")
                .ToList()
                .ShouldBeLike("Process/1/1", "Process/1/2");
        }
    }
}