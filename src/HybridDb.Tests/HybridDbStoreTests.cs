using System;

namespace HybridDb.Tests
{
    public abstract class HybridDbStoreTests : HybridDbTests
    {
        Lazy<DocumentStore> factory;

        protected HybridDbStoreTests()
        {
            Reset();
            UseSerializer(new DefaultJsonSerializer());
        }

        protected void Reset(bool keepConfiguration = false)
        {
            base.Reset();
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