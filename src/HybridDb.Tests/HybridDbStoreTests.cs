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

        protected override void UseTempTables()
        {
            if (factory != null && factory.IsValueCreated)
                throw new InvalidOperationException("Cannot change table mode when store is already initialized.");

            base.UseTempTables();
        }

        protected override void UseRealTables()
        {
            if (factory != null && factory.IsValueCreated)
                throw new InvalidOperationException("Cannot change table mode when store is already initialized.");

            base.UseRealTables();
        }

        // ReSharper disable InconsistentNaming
        protected DocumentStore store
        {
            get { return factory.Value; }
        }
        // ReSharper restore InconsistentNaming
    }
}