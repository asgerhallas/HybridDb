using System;

namespace HybridDb.Tests
{
    public abstract class HybridDbStoreTests : HybridDbDatabaseTests
    {
        readonly Lazy<DocumentStore> factory;

        protected HybridDbStoreTests()
        {
            factory = new Lazy<DocumentStore>(() => Using(DocumentStore.ForTesting(database, configuration)));

            UseSerializer(new DefaultJsonSerializer());
        }

        // ReSharper disable InconsistentNaming
        protected DocumentStore store
        {
            get { return factory.Value; }
        }
        // ReSharper restore InconsistentNaming
    }
}