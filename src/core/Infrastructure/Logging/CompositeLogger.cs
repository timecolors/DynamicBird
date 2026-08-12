using System;
using System.Collections.Generic;

namespace DynamicBird.Core.Infrastructure.Logging
{
    /// <summary>
    /// 组合日志记录器，将日志同时输出到多个目标
    /// </summary>
    public class CompositeLogger : ILogger
    {
        private readonly List<ILogger> _loggers = new();

        public CompositeLogger(params ILogger[] loggers)
        {
            _loggers.AddRange(loggers);
        }

        public void AddLogger(ILogger logger)
        {
            _loggers.Add(logger);
        }

        public void RemoveLogger(ILogger logger)
        {
            _loggers.Remove(logger);
        }

        public void Log(LogLevel level, string message, Exception? exception = null)
        {
            foreach (var logger in _loggers)
            {
                try
                {
                    logger.Log(level, message, exception);
                }
                catch
                {
                    // 单个日志器失败不影响其他日志器
                }
            }
        }

        public bool IsEnabled(LogLevel level)
        {
            foreach (var logger in _loggers)
            {
                if (logger.IsEnabled(level))
                    return true;
            }
            return false;
        }

        public void Debug(string message) => Log(LogLevel.Debug, message);
        public void Info(string message) => Log(LogLevel.Info, message);
        public void Warning(string message) => Log(LogLevel.Warning, message);
        public void Error(string message, Exception? exception = null) => Log(LogLevel.Error, message, exception);
        public void Fatal(string message, Exception? exception = null) => Log(LogLevel.Fatal, message, exception);
    }
}