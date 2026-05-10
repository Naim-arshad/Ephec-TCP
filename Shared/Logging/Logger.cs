namespace Shared.Logging
{
    public static class Logger
    {
        public enum LogLevel
        {
            Debug,
            Info,
            Warning,
            Error,
            Critical
        }

        private static readonly object Sync = new();

        public static LogLevel MinimumLevel { get; set; } = GetMinimumLevelFromEnvironment();

        public static void Log(LogLevel level, string message)
        {
            Log(level, null, message);
        }

        public static void Log(LogLevel level, string? source, string message)
        {
            lock (Sync)
            {
                if (level < MinimumLevel)
                    return;

                string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff 'UTC'");
                string sourcePart = string.IsNullOrWhiteSpace(source) ? string.Empty : $" [{source}]";
                string logLine = $"[{timestamp}] [{level}]{sourcePart} {message}";
                TextWriter output = level >= LogLevel.Error ? Console.Error : Console.Out;
                output.WriteLine(logLine);
                output.Flush();
            }
        }

        public static void Debug(string message) => Log(LogLevel.Debug, message);
        public static void Debug(string source, string message) => Log(LogLevel.Debug, source, message);
        public static void Info(string message) => Log(LogLevel.Info, message);
        public static void Info(string source, string message) => Log(LogLevel.Info, source, message);
        public static void Warning(string message) => Log(LogLevel.Warning, message);
        public static void Warning(string source, string message) => Log(LogLevel.Warning, source, message);
        public static void Error(string message) => Log(LogLevel.Error, message);
        public static void Error(string source, string message) => Log(LogLevel.Error, source, message);
        public static void Error(Exception exception, string message) =>
            Log(LogLevel.Error, $"{message} Exception: {exception.GetType().Name} - {exception.Message}");
        public static void Error(Exception exception, string source, string message) =>
            Log(LogLevel.Error, source, $"{message} Exception: {exception.GetType().Name} - {exception.Message}");
        public static void Critical(string message) => Log(LogLevel.Critical, message);
        public static void Critical(string source, string message) => Log(LogLevel.Critical, source, message);

        private static LogLevel GetMinimumLevelFromEnvironment()
        {
            string? configuredLevel = Environment.GetEnvironmentVariable("EPHEC_TCP_LOG_LEVEL");

            return Enum.TryParse(configuredLevel, ignoreCase: true, out LogLevel level)
                ? level
                : LogLevel.Debug;
        }
    }
}
