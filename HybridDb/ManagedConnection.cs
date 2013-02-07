using System;
using System.Data;

namespace HybridDb
{
    public class ManagedConnection : IDisposable
    {
        readonly IDbConnection connection;
        readonly Action complete;
        readonly Action dispose;

        public ManagedConnection(IDbConnection connection, Action complete, Action dispose)
        {
            this.connection = connection;
            this.complete = complete;
            this.dispose = dispose;
        }

        public IDbConnection Connection
        {
            get { return connection; }
        }

        public ManagedConnection Somehit
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
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