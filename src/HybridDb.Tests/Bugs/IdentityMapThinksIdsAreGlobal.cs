using System.Threading.Tasks;
using Xunit;

namespace HybridDb.Tests.Bugs
{
    public class IdentityMapThinksIdsAreGlobal : HybridDbTests
    {
        readonly IDocumentStore documentStore;

        const string ThisIsAKnownId = "this is a known ID";

        public IdentityMapThinksIdsAreGlobal()
        {
            documentStore = Using(DocumentStore.ForTesting(TableMode.UseTempTables, connectionString));
            documentStore.Configuration.Document<Doc1>().Key(x => x.Id);
            documentStore.Configuration.Document<Doc2>().Key(x => x.Id);
            documentStore.Initialize();
        }

        [Fact]
        public async Task DocumentsAreCachedNotOnlyByIdButAlsoByType()
        {
            await Save(new Doc1 { Id = ThisIsAKnownId, Label = "this is doc1" });
            await Save(new Doc2 { Id = ThisIsAKnownId, Caption = "this is doc2" });

            using (var session = documentStore.OpenSession())
            {
                var doc1 = await session.LoadAsync<Doc1>(ThisIsAKnownId);
                var doc2 = await session.LoadAsync<Doc2>(ThisIsAKnownId);

                Assert.Equal("this is doc1", doc1.Label);
                Assert.Equal("this is doc2", doc2.Caption);
            }
        }

        async Task Save(object doc)
        {
            using (var session1 = documentStore.OpenSession())
            {
                session1.Store(doc);
                await session1.SaveChangesAsync();
            }
        }

        class Doc1
        {
            public string Id { get; set; }
            public string Label { get; set; }
        }

        class Doc2
        {
            public string Id { get; set; }
            public string Caption { get; set; }
        }
    }
}