using System;
using Xunit;

namespace HybridDb.Tests.Bugs
{
    public class IdentityMapThinksIdsAreGlobal : IDisposable
    {
        readonly LambdaHybridDbConfigurator configurator = new LambdaHybridDbConfigurator(config =>
        {
            config.Document<Doc1>().Key(x => x.Id);
            config.Document<Doc2>().Key(x => x.Id);
        });

        readonly IDocumentStore documentStore;

        const string ThisIsAKnownId = "this is a known ID";

        public IdentityMapThinksIdsAreGlobal()
        {
            documentStore = DocumentStore.ForTesting(TableMode.UseTempTables, configurator);
        }

        public void Dispose()
        {
            documentStore.Dispose();
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