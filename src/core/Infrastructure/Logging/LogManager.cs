using System;

using DynamicBird.Infrastructure.Utils;

namespace DynamicBird.Core.Infrastructure.Logging
{
    /// <summary>
    /// 日志管理器（全局入口）
    /// </summary>
    public static class LogManager
    {
        private static ILogger? _logger;
        private static readonly object _lock = new object();
        private static bool _initialized = false;

        /// <summary>
        /// 初始化日志系统
        /// </summary>
        public static void Initialize(LogLevel minLevel = LogLevel.Debug, string logDirectory = "")
        {
            lock (_lock)
            {
                if (_initialized) return;

                string dir = string.IsNullOrWhiteSpace(logDirectory) ? AppPaths.LogDirectory : logDirectory;
                var fileLogger = new FileLogger(dir, minLevel);
                var consoleLogger = new ConsoleLogger(minLevel);
                _logger = new CompositeLogger(consoleLogger, fileLogger);

                _initialized = true;

                Info("========================================");
                Info($"日志系统初始化完成");
                Info($"日志级别: {minLevel}");
                Info($"日志目录: {dir}");
                Info($"启动时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                Info("========================================");
            }
        }

        /// <summary>
        /// 获取日志记录器
        /// </summary>
        public static ILogger Logger
        {
            get
            {
                if (!_initialized)
                {
                    throw new InvalidOperationException("日志系统未初始化，请先调用 LogManager.Initialize()");
                }
                return _logger!;
            }
        }

        /// <summary>
        /// 是否已初始化
        /// </summary>
        public static bool IsInitialized => _initialized;

        // ========== 便捷方法 ==========

        public static void Debug(string message) => Logger.Debug(message);
        public static void Info(string message) => Logger.Info(message);
        public static void Warning(string message) => Logger.Warning(message);
        public static void Error(string message, Exception? exception = null) => Logger.Error(message, exception);
        public static void Fatal(string message, Exception? exception = null) => Logger.Fatal(message, exception);
        public static void Log(LogLevel level, string message, Exception? exception = null) => Logger.Log(level, message, exception);

        /// <summary>
        /// 释放日志资源
        /// </summary>
        public static void Shutdown()
        {
            lock (_lock)
            {
                if (_logger is IDisposable disposable)
                {
                    try
                    {
                        Info("日志系统关闭");
                        disposable.Dispose();
                    }
                    catch { }
                }
                _logger = null;
                _initialized = false;
            }
        }
    }
}
