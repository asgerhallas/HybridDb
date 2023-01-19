using System.Threading;

namespace HybridDb
{
    public static class GlobalStats
    {
        public static long NumberOfNumberUndisposedConnections => numberOfUndisposedConnections;
        public static long NumberOfUndisposedTransactions => numberOfUndisposedTransactions;

        static int numberOfUndisposedConnections;
        static int numberOfUndisposedTransactions;

        internal static void ConnectionCreated()
        {
            Interlocked.Increment(ref numberOfUndisposedConnections);
        }

        internal static void ConnectionDisposed()
        {
            Interlocked.Decrement(ref numberOfUndisposedConnections);
        }

        internal static void TransactionCreated()
        {
            Interlocked.Increment(ref numberOfUndisposedTransactions);
        }

        internal static void TransactionDisposed()
        {
            Interlocked.Decrement(ref numberOfUndisposedTransactions);
        }
    }
}