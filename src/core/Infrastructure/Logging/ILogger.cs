using System;

namespace DynamicBird.Core.Infrastructure.Logging
{
    /// <summary>
    /// 日志记录器接口
    /// </summary>
    public interface ILogger
    {
        /// <summary>
        /// 记录日志
        /// </summary>
        void Log(LogLevel level, string message, Exception? exception = null);

        /// <summary>
        /// 是否启用指定级别
        /// </summary>
        bool IsEnabled(LogLevel level);

        /// <summary>
        /// 记录调试信息
        /// </summary>
        void Debug(string message);

        /// <summary>
        /// 记录信息
        /// </summary>
        void Info(string message);

        /// <summary>
        /// 记录警告
        /// </summary>
        void Warning(string message);

        /// <summary>
        /// 记录错误
        /// </summary>
        void Error(string message, Exception? exception = null);

        /// <summary>
        /// 记录致命错误
        /// </summary>
        void Fatal(string message, Exception? exception = null);
    }
}