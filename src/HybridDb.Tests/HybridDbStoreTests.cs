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

        protected sealed override void Reset()
        {
            base.Reset();

            UseSerializer(new DefaultSerializer());
            factory = new Lazy<IDocumentStore>(() =>
            {
                Configure();
                return Using(DocumentStore.ForTesting(database, configuration));
            });
        }

        protected void InitializeStore()
        {
            var x = store;
        }

        // ReSharper disable InconsistentNaming
        protected IDocumentStore store => factory.Value;
        // ReSharper restore InconsistentNaming

        protected string NewId()
        {
            return Guid.NewGuid().ToString();
        }
    }
}