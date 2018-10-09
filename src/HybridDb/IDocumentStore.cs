using System;
using System.Data;
using HybridDb.Config;

namespace HybridDb
{
    public interface IDocumentStore : IDisposable
    {
        Configuration Configuration { get; }
        StoreStats Stats { get; }

        bool IsInitialized { get; }
        bool Testing { get; }
        TableMode TableMode { get; }

        void Initialize();
        IDocumentSession OpenSession(IDocumentTransaction tx = null);
        IDocumentTransaction BeginTransaction(IsolationLevel level = IsolationLevel.ReadCommitted);
    }
}