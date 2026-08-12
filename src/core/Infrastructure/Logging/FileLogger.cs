using System;
using System.IO;
using System.Diagnostics;

namespace DynamicBird.Core.Infrastructure.Logging
{
    public class FileLogger : ILogger, IDisposable
    {
        private readonly string _logDirectory;
        private readonly LogLevel _minLevel;
        private readonly object _lock = new object();
        private StreamWriter? _writer;
        private string? _currentLogFile;
        private DateTime _currentDate;

        public FileLogger(string logDirectory = "Data/Logs", LogLevel minLevel = LogLevel.Debug)
        {
            _logDirectory = logDirectory;
            _minLevel = minLevel;
            EnsureDirectoryExists();
            OpenLogFile();
        }

        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(_logDirectory))
                Directory.CreateDirectory(_logDirectory);
        }

        private void OpenLogFile()
        {
            _currentDate = DateTime.Now.Date;
            string baseFileName = $"log_{DateTime.Now:yyyy-MM-dd}.log";
            string baseFilePath = Path.Combine(_logDirectory, baseFileName);

            string logFilePath = baseFilePath;
            int retryCount = 0;
            bool opened = false;

            while (!opened && retryCount < 3)
            {
                try
                {
                    _writer = new StreamWriter(logFilePath, true);
                    _currentLogFile = logFilePath;
                    opened = true;
                }
                catch (IOException)
                {
                    retryCount++;
                    if (retryCount < 3)
                    {
                        System.Threading.Thread.Sleep(100);
                    }
                    else
                    {
                        int pid = Process.GetCurrentProcess().Id;
                        string fallbackFileName = $"log_{DateTime.Now:yyyy-MM-dd}_{pid}.log";
                        logFilePath = Path.Combine(_logDirectory, fallbackFileName);
                    }
                }
                catch
                {
                    retryCount = 3;
                }
            }

            if (!opened)
            {
                try
                {
                    var fs = new FileStream(logFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                    _writer = new StreamWriter(fs);
                    _currentLogFile = logFilePath;
                }
                catch
                {
                    // ★★★ 使用完全限定名，避免与 FileLogger.Debug 方法冲突 ★★★
                    System.Diagnostics.Debug.WriteLine("无法打开日志文件，文件日志已禁用。");
                    _writer = null;
                }
            }

            if (_writer != null)
            {
                _writer.AutoFlush = true;
            }
        }

        private void CheckAndRotate()
        {
            if (DateTime.Now.Date != _currentDate)
            {
                _writer?.Dispose();
                OpenLogFile();
            }
        }

        public void Log(LogLevel level, string message, Exception? exception = null)
        {
            if (!IsEnabled(level)) return;

            lock (_lock)
            {
                try
                {
                    CheckAndRotate();

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

                    _writer?.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {prefix} {message}");

                    if (exception != null)
                    {
                        _writer?.WriteLine($"  Exception: {exception.Message}");
                        _writer?.WriteLine($"  StackTrace: {exception.StackTrace}");
                        if (exception.InnerException != null)
                        {
                            _writer?.WriteLine($"  InnerException: {exception.InnerException.Message}");
                        }
                    }
                }
                catch
                {
                    // 日志写入失败时静默，避免无限递归
                }
            }
        }

        public bool IsEnabled(LogLevel level) => level >= _minLevel;

        public void Debug(string message) => Log(LogLevel.Debug, message);
        public void Info(string message) => Log(LogLevel.Info, message);
        public void Warning(string message) => Log(LogLevel.Warning, message);
        public void Error(string message, Exception? exception = null) => Log(LogLevel.Error, message, exception);
        public void Fatal(string message, Exception? exception = null) => Log(LogLevel.Fatal, message, exception);

        public void Dispose()
        {
            _writer?.Dispose();
            _writer = null;
            GC.SuppressFinalize(this);
        }
    }
}