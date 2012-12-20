using System;
using System.Threading;

namespace HybridDb.Logging
{
    public class ConsoleLogger : ILogger
    {
        readonly LoggingColors loggingColors;
        readonly LogLevel minLevel;

        public ConsoleLogger(LogLevel minLevel, LoggingColors loggingColors)
        {
            this.minLevel = minLevel;
            this.loggingColors = loggingColors;
        }

        public void Debug(string message, params object[] objs)
        {
            Log(LogLevel.Debug, message, loggingColors.Debug, objs);
        }

        public void Info(string message, params object[] objs)
        {
            Log(LogLevel.Info, message, loggingColors.Info, objs);
        }

        public void Warn(string message, params object[] objs)
        {
            Log(LogLevel.Warn, message, loggingColors.Warn, objs);
        }

        public void Error(string message, Exception exception, params object[] objs)
        {
            Log(LogLevel.Error, string.Format(message, objs) + Environment.NewLine + exception, loggingColors.Error);
        }

        public void Error(string message, params object[] objs)
        {
            Log(LogLevel.Error, message, loggingColors.Error, objs);
        }

        void Log(LogLevel level, string message, ColorSetting colorSetting, params object[] objs)
        {
            using (colorSetting.Enter())
            {
                Write(level, message, objs);
            }
        }

        void Write(LogLevel level, string message, object[] objs)
        {
            if ((int) level < (int) minLevel) return;

            var levelString = level.ToString().ToUpperInvariant();

            try
            {
                Console.WriteLine("{0} ({1}): {2}",
                                  levelString,
                                  Thread.CurrentThread.Name,
                                  string.Format(message, objs));
            }
            catch
            {
                Warn("Could not render output string: {0}", message);

                Console.WriteLine("{0} ({1}): {2}",
                                  levelString,
                                  Thread.CurrentThread.Name,
                                  message);
            }
        }
    }
}