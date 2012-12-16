using System;

namespace HybridDb
{
    public interface IAdvancedDocumentSessionCommands
    {
        void Clear();
        bool IsLoaded(Guid id);
    }
}