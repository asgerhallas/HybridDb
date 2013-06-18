using System;
using System.Collections.Generic;
using System.Data.SqlClient;

namespace HybridDb
{
    public class ManagedConnection : IDisposable
    {
        readonly SqlConnection connection;
        readonly Action complete;
        readonly Action dispose;

        public ManagedConnection(SqlConnection connection, Action complete, Action dispose)
        {
            this.connection = connection;
            this.complete = complete;
            this.dispose = dispose;
        }

        public SqlConnection Connection
        {
            get { return connection; }
        }

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