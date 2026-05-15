using System;

namespace HybridDb.LiveQuery
{
    public class LiveQueryOptions
    {
        public string TableName { get; set; } = "LiveQueryChanges";
        public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(30);
    }
}
