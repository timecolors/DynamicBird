using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using DynamicBird.Core.Infrastructure.Logging;

namespace DynamicBird
{
    public partial class App : Application
    {
        private static Mutex? _singleInstanceMutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // ★ 单实例保护：已有实例运行时直接退出，避免托盘出现多个进程/图标
            _singleInstanceMutex = new Mutex(true, "DynamicBird_SingleInstance", out bool createdNew);
            if (!createdNew)
            {
                MessageBox.Show("灵动鸟已在运行", "灵动鸟", MessageBoxButton.OK, MessageBoxImage.Information);
                Current.Shutdown();
                return;
            }

            // 初始化日志系统（最先执行）
            LogManager.Initialize(LogLevel.Debug);

            // ★ 后台注册 AppUserModelID（创建开始菜单快捷方式），保证系统 Toast 可显示
            try
            {
                System.Threading.Tasks.Task.Run(
                    DynamicBird.Infrastructure.WinApi.SystemToast.EnsureRegistered);
            }
            catch { }

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
