using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using DynamicBird.Core.Infrastructure.Logging;
using DynamicBird.Infrastructure.Utils;

namespace DynamicBird
{
    public partial class App : Application
    {
        private static Mutex? _singleInstanceMutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // ★ 旧版本数据迁移（安装目录/Data -> %LOCALAPPDATA%\DynamicBird），必须在日志初始化前执行
            AppPaths.MigrateLegacyData();

            // ★ Jump List 命令：带动作参数启动时优先转发给已运行实例
            //   （无实例 → 返回 true，动作由 MainWindow 初始化后执行）
            bool startupActions = false;
            try
            {
                startupActions = DynamicBird.Infrastructure.WinApi.JumpListCommand.ForwardOrExecute(e.Args);
            }
            catch { }

            // ★ 单实例保护：已有实例运行时直接退出，避免托盘出现多个进程/图标
            _singleInstanceMutex = new Mutex(true, "DynamicBird_SingleInstance", out bool createdNew);
            if (!createdNew)
            {
                // ★ 已转发动作时静默退出（不打扰）；无动作仍提示
                if (!startupActions && HasJumpListAction(e.Args))
                {
                    // 动作已通过命名事件转发给已有实例，本进程静默退出
                    Current.Shutdown();
                    return;
                }
                MessageBox.Show("灵动鸟已在运行", "灵动鸟", MessageBoxButton.OK, MessageBoxImage.Information);
                Current.Shutdown();
                return;
            }

            // ★ 本实例为唯一实例：注册 Jump List 动作监听（任务栏点击转发进来）
            try
            {
                DynamicBird.Infrastructure.WinApi.JumpListCommand.Listen(ExecuteJumpListAction);
            }
            catch { }

            // ★ 配置任务栏 Jump List（关联开始菜单快捷方式 AUMID）
            try
            {
                DynamicBird.Infrastructure.WinApi.JumpListManager.Configure();
            }
            catch { }

            // 初始化日志系统（最先执行）
            LogManager.Initialize(LogLevel.Debug);

            // ★ 本地化：按配置语言初始化（zh-CN / en-US，空=跟随系统）
            try
            {
                var lang = DynamicBird.Core.Services.SettingsFileManager.Load().Language;
                DynamicBird.UI.Localization.LocalizationManager.Instance.SetCulture(lang);
            }
            catch { }

            // ★ 清理更新残留（.new.exe/.ps1）；上次更新失败则提示"仍为旧版本"
            try
            {
                if (DynamicBird.Infrastructure.WinApi.UpdateService.CleanupStaleFiles())
                {
                    MessageBox.Show("上次更新未能完成，当前仍为旧版本。请稍后重试更新。",
                        "灵动鸟", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch { }

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

        /// <summary>命令行参数是否含 Jump List 动作（决定单实例冲突时是否静默退出）。</summary>
        private static bool HasJumpListAction(string[] args)
        {
            return DynamicBird.Infrastructure.WinApi.JumpListManager.ParseActions(args).Count > 0;
        }

        /// <summary>执行 Jump List 动作（UI 线程，由命令监听/启动 pending 触发；MainWindow 启动动作也调用）。</summary>
        internal static void ExecuteJumpListAction(IReadOnlyList<string> actions)
        {
            try
            {
                if (Current?.MainWindow is not DynamicBird.UI.Main.MainWindow main) return;
                foreach (var action in actions)
                {
                    switch (action)
                    {
                        case DynamicBird.Infrastructure.WinApi.JumpListManager.ArgOpenSettings:
                            main.InvokeJumpListOpenSettings();
                            break;
                        case DynamicBird.Infrastructure.WinApi.JumpListManager.ArgToggleDnd:
                            main.InvokeJumpListToggleDnd();
                            break;
                        case DynamicBird.Infrastructure.WinApi.JumpListManager.ArgTogglePanel:
                            main.InvokeJumpListTogglePanel();
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                LogManager.Error("执行 Jump List 动作失败", ex);
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
