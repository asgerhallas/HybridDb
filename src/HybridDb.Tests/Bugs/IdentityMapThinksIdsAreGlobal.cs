using Xunit;

namespace HybridDb.Tests.Bugs
{
    public class IdentityMapThinksIdsAreGlobal : HybridDbTests
    {
        readonly IDocumentStore documentStore;

        const string ThisIsAKnownId = "this is a known ID";

        public IdentityMapThinksIdsAreGlobal()
        {
            documentStore = Using(DocumentStore.ForTesting(TableMode.UseLocalTempTables, connectionString));
            documentStore.Configuration.Document<Doc1>().Key(x => x.Id);
            documentStore.Configuration.Document<Doc2>().Key(x => x.Id);
            documentStore.Initialize();
        }

        [Fact]
        public void DocumentsAreCachedNotOnlyByIdButAlsoByType()
        {
            Save(new Doc1 { Id = ThisIsAKnownId, Label = "this is doc1" });
            Save(new Doc2 { Id = ThisIsAKnownId, Caption = "this is doc2" });

            using (var session = documentStore.OpenSession())
            {
                var doc1 = session.Load<Doc1>(ThisIsAKnownId);
                var doc2 = session.Load<Doc2>(ThisIsAKnownId);

                Assert.Equal("this is doc1", doc1.Label);
                Assert.Equal("this is doc2", doc2.Caption);
            }
        }

        void Save(object doc)
        {
            using (var session1 = documentStore.OpenSession())
            {
                session1.Store(doc);
                session1.SaveChanges();
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