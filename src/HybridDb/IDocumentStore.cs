using System;
using System.Data;
using HybridDb.Config;
using HybridDb.Migrations;
using HybridDb.Migrations.Schema;

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

        void Execute(SchemaMigrationCommand command);
        IDocumentSession OpenSession(DocumentTransaction tx = null);
        DocumentTransaction BeginTransaction(IsolationLevel level = IsolationLevel.ReadCommitted);
    }
}