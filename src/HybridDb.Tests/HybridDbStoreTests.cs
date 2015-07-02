using System;
using HybridDb.Serialization;

namespace HybridDb.Tests
{
    public abstract class HybridDbStoreTests : HybridDbTests
    {
        Lazy<IDocumentStore> factory;

        protected HybridDbStoreTests()
        {
            Reset();
        }

        protected override sealed void Reset()
        {
            base.Reset();

            UseSerializer(new DefaultSerializer());
            factory = new Lazy<IDocumentStore>(() => Using(DocumentStore.ForTesting(database, configuration)));
        }

        protected void InitializeStore()
        {
            var x = store;
        }

        // ReSharper disable InconsistentNaming
        protected IDocumentStore store
        {
            get { return factory.Value; }
        }
        // ReSharper restore InconsistentNaming

        protected string NewId()
        {
            return Guid.NewGuid().ToString();
        }
    }
}