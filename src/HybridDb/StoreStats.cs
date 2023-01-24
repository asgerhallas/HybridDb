using System;
using System.Collections.Concurrent;
using System.Data.SqlClient;

namespace HybridDb
{
    public class StoreStats
    {
        public long NumberOfRequests { get; set; } = 0;
        public long NumberOfCommands { get; set; } = 0;
        public long NumberOfGets { get; set; } = 0;
        public long NumberOfQueries { get; set; } = 0;

        public Guid LastWrittenEtag { get; set; }

        public int NumberOfNumberUndisposedConnections => connections.Count;
        public int NumberOfUndisposedTransactions => transactions.Count;

        public static event EventHandler<OnConnectionCreatedEventArgs> OnConnectionCreated;
        public static event EventHandler<OnConnectionDisposedEventArgs> OnConnectionDisposed;
        public static event EventHandler<OnTransactionCreatedEventArgs> OnTransactionCreated;
        public static event EventHandler<OnTransactionDisposedEventArgs> OnTransactionDisposed;

        readonly ConcurrentDictionary<SqlConnection, object> connections = new();
        readonly ConcurrentDictionary<SqlTransaction, object> transactions = new();

        public void CheckLeaks()
        {
            if (NumberOfNumberUndisposedConnections != 0 || NumberOfUndisposedTransactions != 0)
            {
                throw new Exception("Possible connection/transaction leaking detected.");
            }
        }

        internal void ConnectionCreated(SqlConnection connection)
        {
            connections.TryAdd(connection, null);
            OnConnectionCreated?.Invoke(connection, new OnConnectionCreatedEventArgs());
        }

        internal void ConnectionDisposed(SqlConnection connection)
        {
            connections.TryRemove(connection, out _);
            OnConnectionDisposed?.Invoke(connection, new OnConnectionDisposedEventArgs());
        }

        internal void TransactionCreated(SqlTransaction transaction)
        {
            transactions.TryAdd(transaction, null);
            OnTransactionCreated?.Invoke(transaction, new OnTransactionCreatedEventArgs());
        }

        internal void TransactionDisposed(SqlTransaction transaction)
        {
            transactions.TryRemove(transaction, out _);
            OnTransactionDisposed?.Invoke(transaction, new OnTransactionDisposedEventArgs());
        }

        public class OnConnectionCreatedEventArgs : EventArgs { }

        public class OnConnectionDisposedEventArgs : EventArgs { }

        public class OnTransactionCreatedEventArgs : EventArgs { }

        public class OnTransactionDisposedEventArgs : EventArgs { }
    }
}