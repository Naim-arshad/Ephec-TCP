namespace Shared.Logging
{
    public class Logger
    {
        public enum LogLevel
        {
            Debug,
            Info,
            Warning,
            Error,
            Critical
        }

        public static void Log(LogLevel level, string message)
        {
            string timestamp = DateTime.UtcNow.ToString("HH:mm:ss.fff");
            Console.Out.WriteLine($"[{timestamp}] [{level}] {message}");
        }

        public static void Debug(string message) => Log(LogLevel.Debug, message);
        public static void Info(string message) => Log(LogLevel.Info, message);
        public static void Warning(string message) => Log(LogLevel.Warning, message);
        public static void Error(string message) => Log(LogLevel.Error, message);
        public static void Critical(string message) => Log(LogLevel.Critical, message);


    }
}
