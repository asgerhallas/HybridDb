using System;
using HybridDb.Config;
using HybridDb.Serialization;

namespace HybridDb.Tests
{
    public abstract class HybridDbStoreTests : HybridDbTests
    {
        protected void Reset()
        {
            configuration = new Configuration();

            UseSerializer(new DefaultSerializer());

            documentStore = Using(new DocumentStore(configuration, documentStore.Database, true));
        }

        protected void InitializeStore()
        {
            var x = store;
        }

        // ReSharper disable InconsistentNaming
        protected IDocumentStore store
        {
            get
            {
                if (!documentStore.IsInitalized)
                    documentStore.Initialize();

                return documentStore;
            }
        }
        // ReSharper restore InconsistentNaming

        protected string NewId()
        {
            return Guid.NewGuid().ToString();
        }
    }
}