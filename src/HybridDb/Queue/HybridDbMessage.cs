using System.Collections.Generic;

namespace HybridDb.Queue
{
    public sealed record HybridDbMessage(
        string Id,
        object Payload,
        string Topic = null,
        int Order = 0,
        string CorrelationId = null,
        Dictionary<string, string> Metadata = null
    )
    {
        public const string EnqueuedAtKey = "enqueued-at";

        /// <summary>
        /// The list of message ids that resulted in this message.
        /// </summary>
        public const string Breadcrumbs = "correlation-ids";

        public string CorrelationId { get; init; } = CorrelationId ?? Id;

        public Dictionary<string, string> Metadata { get; init; } = Metadata ?? new Dictionary<string, string>();
    }
}