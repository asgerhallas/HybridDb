using System;
using System.Data;
using HybridDb.Config;
using HybridDb.Migrations.Schema;

namespace HybridDb
{
    public interface IDocumentStore : IDisposable
    {
        Configuration Configuration { get; }
        StoreStats Stats { get; }
        TableMode TableMode { get; }
        IDatabase Database { get; }

        void Initialize();

        IDocumentSession OpenSession(DocumentTransaction tx = null);
        DocumentTransaction BeginTransaction(IsolationLevel level = IsolationLevel.ReadCommitted, TimeSpan? connectionTimeout = null);
        DocumentTransaction BeginTransaction(Guid commitId, IsolationLevel level = IsolationLevel.ReadCommitted, TimeSpan? connectionTimeout = null);

        void Execute(DdlCommand command);
        object Execute(DocumentTransaction tx, HybridDbCommand command);
        T Execute<T>(DocumentTransaction tx, HybridDbCommand<T> command);
    }
}