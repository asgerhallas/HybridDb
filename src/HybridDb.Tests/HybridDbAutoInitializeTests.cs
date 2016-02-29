namespace HybridDb.Tests
{
    public abstract class HybridDbAutoInitializeTests : HybridDbTests
    {
        protected void InitializeStore()
        {
            var x = store;
        }

        // ReSharper disable InconsistentNaming
        protected override DocumentStore store
        {
            get
            {
                var baseStore = base.store;

                if (!baseStore.IsInitialized)
                    baseStore.Initialize();

                return baseStore;
            }
        }
        // ReSharper restore InconsistentNaming
    }
}