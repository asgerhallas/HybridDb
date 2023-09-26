using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace HybridDb
{
    public class ManagedConnection : IDisposable
    {
        readonly Action complete;
        readonly Action dispose;

        public ManagedConnection(SqlConnection connection, Action complete, Action dispose)
        {
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
            dispose();
        }
    }
}