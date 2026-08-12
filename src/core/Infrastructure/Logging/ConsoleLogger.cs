using System;

namespace DynamicBird.Core.Infrastructure.Logging
{
    /// <summary>
    /// 控制台/调试输出日志记录器
    /// </summary>
    public class ConsoleLogger : ILogger
    {
        private readonly LogLevel _minLevel;

        public ConsoleLogger(LogLevel minLevel = LogLevel.Debug)
        {
            _minLevel = minLevel;
        }

        public void Log(LogLevel level, string message, Exception? exception = null)
        {
            if (!IsEnabled(level)) return;

            string prefix = level switch
            {
                LogLevel.Trace => "[TRC]",
                LogLevel.Debug => "[DBG]",
                LogLevel.Info => "[INF]",
                LogLevel.Warning => "[WRN]",
                LogLevel.Error => "[ERR]",
                LogLevel.Fatal => "[FTL]",
                _ => "[   ]"
            };

            string logLine = $"{DateTime.Now:HH:mm:ss.fff} {prefix} {message}";
            System.Diagnostics.Debug.WriteLine(logLine);

            if (exception != null)
            {
                System.Diagnostics.Debug.WriteLine($"  Exception: {exception.Message}");
                System.Diagnostics.Debug.WriteLine($"  StackTrace: {exception.StackTrace}");
            }
        }

        public bool IsEnabled(LogLevel level) => level >= _minLevel;

        public void Debug(string message) => Log(LogLevel.Debug, message);
        public void Info(string message) => Log(LogLevel.Info, message);
        public void Warning(string message) => Log(LogLevel.Warning, message);
        public void Error(string message, Exception? exception = null) => Log(LogLevel.Error, message, exception);
        public void Fatal(string message, Exception? exception = null) => Log(LogLevel.Fatal, message, exception);
    }
}