using System.Collections.Generic;
using Serilog.Core;
using Serilog.Events;

namespace HybridDb.Tests
{
    public class ListSink(List<LogEvent> list) : ILogEventSink
    {
        public List<LogEvent> Captures { get; set; } = list;

        public void Emit(LogEvent logEvent) => Captures.Add(logEvent);
    }
}