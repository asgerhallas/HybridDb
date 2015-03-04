using System;
using System.Collections.Generic;

namespace HybridDb.Tests
{
    public abstract class HybridDbTests : HybridDbConfigurator, IDisposable
    {
        readonly List<IDisposable> disposables;
        readonly Lazy<DocumentStore> factory;

        protected HybridDbTests()
        {
            disposables = new List<IDisposable>();
            factory = new Lazy<DocumentStore>(() => Using(DocumentStore.ForTestingWithTempTables(configurator: this)));
            UseSerializer(new DefaultJsonSerializer());
        }

        // ReSharper disable InconsistentNaming
        protected DocumentStore store
        {
            get { return factory.Value; }
        }
        // ReSharper restore InconsistentNaming

        protected T Using<T>(T disposable) where T : IDisposable
        {
            disposables.Add(disposable);
            return disposable;
        }

        public void Dispose()
        {
            foreach (var disposable in disposables)
            {
                disposable.Dispose();
            }
        }
    }
}