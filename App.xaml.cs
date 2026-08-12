using System;
using System.Windows;
using System.Windows.Threading;
using DynamicBird.Core.Infrastructure.Logging;

namespace DynamicBird
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 初始化日志系统（最先执行）
            LogManager.Initialize(LogLevel.Debug);

            // 全局异常捕获
            this.DispatcherUnhandledException += (s, args) =>
            {
                LogManager.Error("Dispatcher未处理异常", args.Exception);
                MessageBox.Show(
                    $"发生未处理异常:\n{args.Exception.Message}\n\n{args.Exception.StackTrace}",
                    "灵动鸟错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                args.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                LogManager.Fatal("AppDomain未处理异常", ex);
                MessageBox.Show(
                    $"发生未处理异常:\n{ex?.Message}\n\n{ex?.StackTrace}",
                    "灵动鸟错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            };

            try
            {
                LogManager.Info("应用程序启动");
            }
            catch (Exception ex)
            {
                LogManager.Fatal("应用程序启动失败", ex);
                MessageBox.Show(
                    $"应用程序启动失败:\n{ex.Message}\n\n详细信息已写入日志文件",
                    "灵动鸟启动失败",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Current.Shutdown();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            LogManager.Info("应用程序退出");
            LogManager.Shutdown();

            // ★★★ 强制结束当前进程（确保所有线程终止） ★★★
            // 这解决 CompositionTarget.Rendering 事件未完全释放导致的进程残留
            try
            {
                Environment.Exit(0);
            }
            catch { }

            base.OnExit(e);
        }
    }
}