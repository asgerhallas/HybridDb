using System.Linq;
using HybridDb.Migrations.BuiltIn;
using HybridDb.Tests;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace HybridDb.LiveQueryTests.Migrations
{
    public class RemoveLegacyDocumentTableChangeTracking_Tests(ITestOutputHelper output) : HybridDbTests(output)
    {
        [Fact]
        public void RemovesLegacyColumnsAndDeletedRows()
        {
            UseRealTables();

            Setup(SiblingFile("RemoveLegacyDocumentTableChangeTracking_Before.sql"));

            ResetConfiguration();

            Document<Entity>().With(x => x.Property);
            UseMigrations(new RemoveLegacyDocumentTableChangeTracking(1));

            TouchStore();

            var schema = store.Database.QuerySchema();
            var rows = store.Database.RawQuery<string>("select Id from [Entities]").ToList();

            schema["Entities"].ShouldNotContain("Timestamp");
            schema["Entities"].ShouldNotContain("LastOperation");
            rows.ShouldBe(["active-id"]);
        }
    }
}
