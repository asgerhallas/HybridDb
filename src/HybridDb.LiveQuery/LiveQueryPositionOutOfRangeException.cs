using System;
using HybridDb.Config;

namespace HybridDb.LiveQuery
{
    public class LiveQueryPositionOutOfRangeException : HybridDbException
    {
        public LiveQueryPositionOutOfRangeException(string message) : base(message) { }

        public static LiveQueryPositionOutOfRangeException For(DocumentTable table) =>
            new($"The requested live query position for table '{table.Name}' is older than the retained change log. Re-bootstrap and resume from the latest position.");
    }
}
