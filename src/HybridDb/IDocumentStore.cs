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
        DocumentTransaction BeginTransaction(IsolationLevel level = IsolationLevel.ReadCommitted, int timeout = 15);
        DocumentTransaction BeginTransaction(Guid commitId, IsolationLevel level = IsolationLevel.ReadCommitted, int timeout = 15);

        void Execute(DdlCommand command);
        object Execute(DocumentTransaction tx, DmlCommand command);
        T Execute<T>(DocumentTransaction tx, Command<T> command);
    }
}