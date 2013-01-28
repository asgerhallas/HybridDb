using System;
using System.Linq;

namespace HybridDb
{
    public interface IMigration
    {
        void UpdateIndexesFor<T>();
        void UpdateIndexesFor(params Type[] types);

        
        void UpdateSchema();
        
        
        void Do<T>(Action<T> action);
        void Do<T>(IQueryable<T> query, Action<T> action);
    }
}