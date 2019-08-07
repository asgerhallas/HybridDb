using System.Collections.Generic;
using Serilog.Core;
using Serilog.Events;

namespace HybridDb.Tests
{
    public class ListSink : ILogEventSink
    {
        public ListSink(List<LogEvent> list) => Captures = list;

        public List<LogEvent> Captures { get; set; }

        public void Emit(LogEvent logEvent) => Captures.Add(logEvent);
    }
}