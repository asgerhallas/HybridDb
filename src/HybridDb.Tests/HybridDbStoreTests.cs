using System;

namespace HybridDb.Tests
{
    public abstract class HybridDbStoreTests : HybridDbTests
    {
        Lazy<DocumentStore> factory;

        protected HybridDbStoreTests()
        {
            ResetStore();
            UseSerializer(new DefaultJsonSerializer());
        }

        public void ResetStore()
        {
            factory = new Lazy<DocumentStore>(() => Using(DocumentStore.ForTesting(database, configuration)));
        }

        // ReSharper disable InconsistentNaming
        protected IDocumentStore store
        {
            get { return factory.Value; }
        }
        // ReSharper restore InconsistentNaming
    }
}