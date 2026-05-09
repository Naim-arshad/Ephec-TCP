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
            if (level < MinimumLevel)
                return;

            string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff 'UTC'");
            string logLine = $"[{timestamp}] [{level}] {message}";

            lock (Sync)
            {
                TextWriter output = level >= LogLevel.Error ? Console.Error : Console.Out;
                output.WriteLine(logLine);
            }
        }

        public static void Debug(string message) => Log(LogLevel.Debug, message);
        public static void Info(string message) => Log(LogLevel.Info, message);
        public static void Warning(string message) => Log(LogLevel.Warning, message);
        public static void Error(string message) => Log(LogLevel.Error, message);
        public static void Error(Exception exception, string message) =>
            Log(LogLevel.Error, $"{message} Exception: {exception.GetType().Name} - {exception.Message}");
        public static void Critical(string message) => Log(LogLevel.Critical, message);

        private static LogLevel GetMinimumLevelFromEnvironment()
        {
            string? configuredLevel = Environment.GetEnvironmentVariable("EPHEC_TCP_LOG_LEVEL");

            return Enum.TryParse(configuredLevel, ignoreCase: true, out LogLevel level)
                ? level
                : LogLevel.Debug;
        }
    }
}
