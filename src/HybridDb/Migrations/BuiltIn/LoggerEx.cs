using Microsoft.Extensions.Logging;

namespace HybridDb.Migrations.BuiltIn
{
    public static class LoggerEx
    {
        public static void LogMigrationInfo(this ILogger logger, string name, string message) =>
            logger.Log(LogLevel.Information, $"[MIGRATION][INFO] {name}: {message}");

        public static void LogMigrationError(this ILogger logger, string name, string message) =>
            logger.Log(LogLevel.Error, $"[MIGRATION][ERROR] {name}: {message}");
    }
}