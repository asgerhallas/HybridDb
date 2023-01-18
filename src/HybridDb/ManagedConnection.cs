using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SqlClient;

namespace HybridDb
{
    public class ManagedConnection : IDisposable
    {
        public static ConcurrentDictionary<SqlConnection, string> ConnectionStrings { get; } = new();

        readonly Action complete;
        readonly Action dispose;

        public ManagedConnection(SqlConnection connection, Action complete, Action dispose)
        {
            ConnectionStrings.TryAdd(connection, "");

            this.Connection = connection;
            this.complete = complete;
            this.dispose = dispose;
        }

        public SqlConnection Connection { get; }

        public void Complete()
        {
            complete();
        }

        public void Dispose()
        {
            ConnectionStrings.TryUpdate(Connection, "disposed", "");

            dispose();
        }
    }
}