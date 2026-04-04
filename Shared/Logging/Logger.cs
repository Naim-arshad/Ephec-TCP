using System;
using System.Collections.Generic;
using System.Text;

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

        public void Log(LogLevel level, string message)
        {
            string timestamp = DateTime.UtcNow.ToString("o");
            Console.WriteLine($"[{timestamp}] [{level}] {message}");
        }

        public void Debug(string message) => Log(LogLevel.Debug, message);
        public void Info(string message) => Log(LogLevel.Info, message);
        public void Warning(string message) => Log(LogLevel.Warning, message);
        public void Error(string message) => Log(LogLevel.Error, message);

    }
}
