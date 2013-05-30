using System;
using HybridDb.Logging;

namespace HybridDb.MigrationRunner
{
    public class WriteThroughLogger : ILogger
    {
        readonly Action<Entry> action;

        public WriteThroughLogger(Action<Entry> action)
        {
            this.action = action;
        }

        public void Debug(string message, params object[] objs)
        {
            Log(LogLevel.Debug, message, objs);
        }

        public void Info(string message, params object[] objs)
        {
            Log(LogLevel.Info, message, objs);
        }

        public void Warn(string message, params object[] objs)
        {
            Log(LogLevel.Warn, message, objs);
        }

        public void Error(string message, Exception exception, params object[] objs)
        {
            Log(LogLevel.Error, string.Format(message, objs) + Environment.NewLine + exception);
        }

        public void Error(string message, params object[] objs)
        {
            Log(LogLevel.Error, message, objs);
        }

        void Log(LogLevel level, string message, params object[] objs)
        {
            action(new Entry
            {
                Level = level, 
                Message = message, 
                Args = objs
            });
        }

        public class Entry 
        {
            public LogLevel Level { get; set; }
            public string Message { get; set; }
            public object[] Args { get; set; }
        }
    }
}