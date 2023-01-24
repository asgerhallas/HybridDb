using System;
using System.Collections.Concurrent;
using System.Data.SqlClient;
using System.Runtime.CompilerServices;
using System.Text;

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

        readonly ConcurrentDictionary<SqlConnection, (string FilePath, int LineNumber, string MemberName)> connections = new();
        readonly ConcurrentDictionary<SqlTransaction, (string FilePath, int LineNumber, string MemberName)> transactions = new();

        public void CheckLeaks()
        {
            if (NumberOfNumberUndisposedConnections == 0 && NumberOfUndisposedTransactions == 0) return;

            var sb = new StringBuilder();

            foreach (var connection in connections)
            {
                sb.AppendLine($"Instance: {nameof(connection)}");
                sb.AppendLine($"File Path: {connection.Value.FilePath}");
                sb.AppendLine($"Line Number: {connection.Value.LineNumber}");
                sb.AppendLine($"Member Name: {connection.Value.MemberName}");
                sb.AppendLine();
            }

            foreach (var transaction in transactions)
            {
                sb.AppendLine($"Instance: {nameof(transaction)}");
                sb.AppendLine($"File Path: {transaction.Value.FilePath}");
                sb.AppendLine($"Line Number: {transaction.Value.LineNumber}");
                sb.AppendLine($"Member Name: {transaction.Value.MemberName}");
                sb.AppendLine();
            }

            throw new Exception($"Possible connection/transaction leaking detected:\n\n{sb}");
        }

        internal void ConnectionCreated(
            SqlConnection connection,
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0,
            [CallerMemberName] string memberName = "")

        {
            connections.TryAdd(connection, (filePath, lineNumber, memberName));
        }

        internal void ConnectionDisposed(SqlConnection connection)
        {
            connections.TryRemove(connection, out _);
        }

        internal void TransactionCreated(
            SqlTransaction transaction,
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0,
            [CallerMemberName] string memberName = "")
        {
            transactions.TryAdd(transaction, (filePath, lineNumber, memberName));
        }

        internal void TransactionDisposed(SqlTransaction transaction)
        {
            transactions.TryRemove(transaction, out _);
        }
    }
}