using System;
using System.Reflection;
using System.Windows;
using DynamicBird.Core.Infrastructure.Logging;
using DynamicBird.Core.Infrastructure.Service;
using DynamicBird.Infrastructure.Utils;
using Microsoft.Win32;

namespace DynamicBird.Core.Services
{
    public class TrayIconManager : IService, IDisposable
    {
        private System.Windows.Forms.NotifyIcon? _notifyIcon;
        private readonly Window _owner;
        private readonly Action _onOpenSettings;
        private readonly Action _onToggleWindow;
        private readonly Action _onExit;
        private bool _disposed = false;

        public string Name => "TrayIconManager";
        public bool IsInitialized { get; private set; } = false;

        public TrayIconManager(Window owner, Action onOpenSettings, Action onToggleWindow, Action onExit)
        {
            _owner = owner;
            _onOpenSettings = onOpenSettings;
            _onToggleWindow = onToggleWindow;
            _onExit = onExit;
        }

        public void Initialize()
        {
            if (IsInitialized) return;
            Create();
            IsInitialized = true;
            LogManager.Debug("TrayIconManager 初始化完成");
        }

        public void Shutdown()
        {
            if (!IsInitialized) return;
            Dispose();
            IsInitialized = false;
            LogManager.Debug("TrayIconManager 已关闭");
        }

        public void Create()
        {
            _notifyIcon = new System.Windows.Forms.NotifyIcon();
            _notifyIcon.Text = "🐦 灵动鸟";

            try
            {
                var entryLocation = Environment.ProcessPath;
                if (System.IO.File.Exists("Resources/icon.ico"))
                    _notifyIcon.Icon = new System.Drawing.Icon("Resources/icon.ico");
                else if (!string.IsNullOrEmpty(entryLocation))
                    _notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(entryLocation);
                else
                    _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
            }
            catch { _notifyIcon.Icon = System.Drawing.SystemIcons.Application; }

            _notifyIcon.Visible = true;

            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("设置", null, (s, e) => _onOpenSettings());
            menu.Items.Add("-");
            menu.Items.Add("显示 / 隐藏", null, (s, e) => _onToggleWindow());
            menu.Items.Add("-");

            var autoStartItem = new System.Windows.Forms.ToolStripMenuItem("开机自启")
            {
                CheckOnClick = true,
                Checked = IsAutoStartEnabled()
            };
            autoStartItem.Click += (s, e) => ToggleAutoStart(autoStartItem.Checked);
            menu.Items.Add(autoStartItem);
            menu.Items.Add("-");
            menu.Items.Add("退出", null, (s, e) => _onExit());

            _notifyIcon.ContextMenuStrip = menu;
            _notifyIcon.DoubleClick += (s, e) => _onToggleWindow();
        }

        public void Dispose()
        {
            if (_disposed) return;
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        public static bool IsAutoStartEnabled()
        {
            if (AppPaths.IsPackaged) return IsStartupTaskEnabled();
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", false);
                return key?.GetValue("DynamicBird") != null;
            }
            catch { return false; }
        }

        public static void ToggleAutoStart(bool enable)
        {
            try
            {
                if (AppPaths.IsPackaged)
                {
                    ToggleStartupTask(enable);
                    return;
                }
                using var key = Registry.CurrentUser.CreateSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run");
                if (enable)
                    key?.SetValue("DynamicBird", "\"" + (Environment.ProcessPath ?? "") + "\"");
                else
                    key?.DeleteValue("DynamicBird", false);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"设置开机自启失败: {ex.Message}", "灵动鸟", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>商店版开机自启：使用 MSIX 启动任务（清单中需声明 desktop:StartupTask）。</summary>
        private const string StartupTaskId = "DynamicBirdStartupTask";

        private static bool IsStartupTaskEnabled()
        {
            try
            {
                var task = Windows.ApplicationModel.StartupTask.GetAsync(StartupTaskId)
                    .AsTask().GetAwaiter().GetResult();
                return task.State == Windows.ApplicationModel.StartupTaskState.Enabled ||
                       task.State == Windows.ApplicationModel.StartupTaskState.EnabledByPolicy;
            }
            catch { return false; }
        }

        private static void ToggleStartupTask(bool enable)
        {
            try
            {
                var task = Windows.ApplicationModel.StartupTask.GetAsync(StartupTaskId)
                    .AsTask().GetAwaiter().GetResult();
                if (enable)
                    task.RequestEnableAsync().AsTask().GetAwaiter().GetResult();
                else
                    task.Disable();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"设置开机自启失败（商店版）: {ex.Message}", "灵动鸟", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
