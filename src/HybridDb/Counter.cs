using System.Threading;

namespace HybridDb
{
    public static class Global
    {
        static int connections;
        static int transactions;

        public static int Connections => connections;
        public static int Transactions => transactions;

        public static void ConnectionCreated()
        {
            Interlocked.Increment(ref connections);
        }

        public static void ConnectionDisposed()
        {
            Interlocked.Decrement(ref connections);
        }

        public static void TransactionCreated()
        {
            Interlocked.Increment(ref transactions);
        }

        public static void TransactionDisposed()
        {
            Interlocked.Decrement(ref transactions);
        }
    }
}