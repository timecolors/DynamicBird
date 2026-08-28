using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;
using System.Windows.Threading;

namespace DynamicBird.Infrastructure.WinApi
{
    public class ToastNotificationItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string AppName { get; set; } = "";
        public string Message { get; set; } = "";
        public DateTime Time { get; set; } = DateTime.Now;
        public IntPtr PopupHwnd { get; set; }
        public int ProcessId { get; set; }
        /// <summary>Windows 通知中心的 AppId（AUMID / UWP 包名），用于点击后启动应用。</summary>
        public string? AppId { get; set; }
        public string TimeText => Time.ToString("HH:mm");
    }

    /// <summary>
    /// 通知监听：被动捕获新出现的消息弹窗（微信/QQ 等自绘弹窗）与系统 Toast。
    /// QQ 等自绘气泡的 Win32 标题常为空，因此不能按“窗口标题为空”过滤，
    /// 需用 UI Automation 读取窗口或子元素文本；同一窗口内容变化时视为新通知。
    /// </summary>
    public static class ToastMonitor
    {
        public static ObservableCollection<ToastNotificationItem> Notifications { get; } = new();

        public static event Action? Changed;

        private static DispatcherTimer? _timer;
        private static DispatcherTimer? _centerTimer;
        private static readonly Dictionary<string, UpdateService.UpdateInfo> _pendingUpdates = new();
        private static readonly Dictionary<IntPtr, ToastNotificationItem> _active = new();
        private static readonly Dictionary<IntPtr, string> _lastSignature = new();
        private static readonly Dictionary<IntPtr, int> _uiaCooldown = new();
        private static readonly HashSet<string> _ignoredClasses = new(StringComparer.OrdinalIgnoreCase)
        {
            "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd",
            "DV2ControlHost", "NotifyIconOverflowWindow", "Tooltips_class32",
            "MSCTFIME UI", "IME"
            // 注意：Windows.UI.Core.CoreWindow（原生 Toast 宿主）不能忽略，见 IsToastLike
        };

        private const int MaxItems = 30;
        private const int UiaCooldownScans = 4;

        public static void Start()
        {
            if (_timer != null) return;
            // ★ 屏幕弹窗嗅探从 400ms 降到 800ms：系统通知已由通知中心读取（5s 轮询）兜底，
            //   嗅探仅作为自绘气泡（QQ 等）的补充，降低 UIA 遍历的 CPU 占用
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
            _timer.Tick += (_, _) => Scan();
            _timer.Start();

            // 通知中心数据库轮询（成本低，5 秒一次足够）
            _centerTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _centerTimer.Tick += (_, _) => PollCenter();
            _centerTimer.Start();
            PollCenter(); // 启动后立即拉一次历史
        }

        public static void Stop()
        {
            _timer?.Stop();
            _timer = null;
            _centerTimer?.Stop();
            _centerTimer = null;
            _active.Clear();
            _lastSignature.Clear();
            _uiaCooldown.Clear();
            Notifications.Clear();
        }

        /// <summary>
        /// 面板隐藏时暂停扫描（省 CPU），显示时恢复并立即拉取一次通知中心的新通知。
        /// </summary>
        public static void SetPanelVisible(bool visible)
        {
            if (_timer == null) return; // 服务未启动

            if (visible)
            {
                if (!_timer.IsEnabled) _timer!.Start();
                if (!_centerTimer!.IsEnabled)
                {
                    _centerTimer.Start();
                    PollCenter(); // 立即拉取暂停期间的新通知
                }
            }
            else
            {
                _timer.Stop();
                _centerTimer!.Stop();
            }
        }

        public static void ClearAll()
        {
            _active.Clear();
            _lastSignature.Clear();
            _uiaCooldown.Clear();
            Notifications.Clear();
            Changed?.Invoke();
        }

        public static void OpenApp(ToastNotificationItem item)
        {
            try
            {
                // 更新通知：点击即下载安装
                if (item.AppId == "dynamicbird-update")
                {
                    RemoveItem(item);
                    if (_pendingUpdates.TryGetValue(item.Id, out var info))
                    {
                        _pendingUpdates.Remove(item.Id);
                        _ = InstallUpdateAsync(info);
                    }
                    return;
                }

                // 通知中心项没有弹窗句柄，按 AppId 启动应用
                if (item.PopupHwnd == IntPtr.Zero && !string.IsNullOrEmpty(item.AppId))
                {
                    LaunchByAppId(item.AppId);
                    RemoveItem(item);
                    return;
                }

                using var proc = Process.GetProcessById(item.ProcessId);
                if (proc.MainWindowHandle != IntPtr.Zero)
                {
                    WindowAction.SwitchTo(proc.MainWindowHandle);
                }
                else
                {
                    string? exe = proc.MainModule?.FileName;
                    if (!string.IsNullOrEmpty(exe))
                    {
                        Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
                    }
                }
            }
            catch { }

            RemoveItem(item);
        }

        /// <summary>在通知坞插入"发现新版本"通知，点击后下载安装。</summary>
        public static void NotifyUpdateAvailable(UpdateService.UpdateInfo info)
        {
            string key = Guid.NewGuid().ToString("N");
            _pendingUpdates[key] = info;

            var item = new ToastNotificationItem
            {
                Id = key,
                AppName = DynamicBird.UI.Localization.LocalizationManager.Instance["Toast_AppName"],
                Message = string.Format(DynamicBird.UI.Localization.LocalizationManager.Instance["Toast_NewVersion"], info.Version),
                Time = DateTime.Now,
                AppId = "dynamicbird-update"
            };
            Notifications.Insert(0, item);
            while (Notifications.Count > MaxItems) Notifications.RemoveAt(Notifications.Count - 1);
            Changed?.Invoke();
        }

        private static void NotifyUpdateStatus(string message)
        {
            var item = new ToastNotificationItem
            {
                AppName = DynamicBird.UI.Localization.LocalizationManager.Instance["Toast_AppName"],
                Message = message,
                Time = DateTime.Now,
                AppId = "dynamicbird-update-status"
            };
            Notifications.Insert(0, item);
            while (Notifications.Count > MaxItems) Notifications.RemoveAt(Notifications.Count - 1);
            Changed?.Invoke();
        }

        private static async System.Threading.Tasks.Task InstallUpdateAsync(UpdateService.UpdateInfo info)
        {
            NotifyUpdateStatus(string.Format(DynamicBird.UI.Localization.LocalizationManager.Instance["Toast_Downloading"], info.Version));
            string? pkg = await UpdateService.DownloadUpdateAsync(info);
            if (pkg == null)
            {
                NotifyUpdateStatus(DynamicBird.UI.Localization.LocalizationManager.Instance["Toast_DownloadFailed"]);
                return;
            }

            NotifyUpdateStatus(DynamicBird.UI.Localization.LocalizationManager.Instance["Toast_Extracting"]);
            string? exe = await UpdateService.ExtractExeAsync(pkg);
            if (exe == null)
            {
                NotifyUpdateStatus(DynamicBird.UI.Localization.LocalizationManager.Instance["Toast_ExtractFailed"]);
                return;
            }

            if (UpdateService.ApplyUpdate(exe))
            {
                NotifyUpdateStatus(DynamicBird.UI.Localization.LocalizationManager.Instance["Toast_Ready"]);
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(
                    new Action(() => System.Windows.Application.Current?.Shutdown()));
            }
            else
            {
                NotifyUpdateStatus(DynamicBird.UI.Localization.LocalizationManager.Instance["Toast_InstallFailed"]);
            }
        }

        // ================= 通知中心（wpndatabase.db） =================

        private static void PollCenter()
        {
            try
            {
                var items = NotificationCenterReader.Scan();
                if (items.Count == 0) return;

                foreach (var item in items)
                {
                    Notifications.Insert(0, item);
                    while (Notifications.Count > MaxItems) Notifications.RemoveAt(Notifications.Count - 1);
                }
                Changed?.Invoke();
            }
            catch { }
        }

        private static readonly Dictionary<string, string> _appIdShortcutCache = new();

        private static void LaunchByAppId(string appId)
        {
            try
            {
                // 1) 桌面应用：找开始菜单里带相同 AUMID 的快捷方式
                string? lnk = FindShortcutForAppId(appId);
                if (!string.IsNullOrEmpty(lnk))
                {
                    Process.Start(new ProcessStartInfo(lnk) { UseShellExecute = true });
                    return;
                }

                // 2) UWP / 商店应用
                Process.Start(new ProcessStartInfo(
                    "explorer.exe",
                    $"shell:AppsFolder\\{appId}") { UseShellExecute = true });
            }
            catch { }
        }

        private static string? FindShortcutForAppId(string appId)
        {
            if (_appIdShortcutCache.TryGetValue(appId, out var cached))
                return string.IsNullOrEmpty(cached) ? null : cached;

            string[] roots =
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms)
            };

            foreach (var root in roots)
            {
                if (!Directory.Exists(root)) continue;
                foreach (var lnk in Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories))
                {
                    if (string.Equals(SystemToast.ReadAumid(lnk), appId, StringComparison.OrdinalIgnoreCase))
                    {
                        _appIdShortcutCache[appId] = lnk;
                        return lnk;
                    }
                }
            }

            _appIdShortcutCache[appId] = "";
            return null;
        }

        public static void RemoveItem(ToastNotificationItem item)
        {
            if (item.PopupHwnd != IntPtr.Zero)
            {
                _active.Remove(item.PopupHwnd);
                _lastSignature.Remove(item.PopupHwnd);
                _uiaCooldown.Remove(item.PopupHwnd);
            }
            Notifications.Remove(item);
            Changed?.Invoke();
        }

        // ================= 扫描 =================

        private static void Scan()
        {
            try
            {
                bool changed = false;
                var seen = new HashSet<IntPtr>();
                int currentPid = Environment.ProcessId;

                EnumWindows((hwnd, _) =>
                {
                    try
                    {
                        if (!IsWindowVisible(hwnd)) return true;

                        string className = GetClassName(hwnd);
                        if (_ignoredClasses.Contains(className)) return true;

                        if (!GetWindowRect(hwnd, out RECT r)) return true;
                        int w = r.Right - r.Left;
                        int h = r.Bottom - r.Top;
                        if (w < 80 || h < 30 || w > 620 || h > 520) return true;

                        // ★ 排除完全在屏幕外的隐藏窗口（如 -32000 坐标的辅助窗口），
                        //   但保留副屏上的通知（按虚拟屏幕范围判断）
                        int vx = GetSystemMetrics(76), vy = GetSystemMetrics(77);
                        int vw = GetSystemMetrics(78), vh = GetSystemMetrics(79);
                        bool onVirtualScreen =
                            r.Right > vx && r.Bottom > vy && r.Left < vx + vw && r.Top < vy + vh;
                        if (!onVirtualScreen) return true;

                        if (!IsToastLike(hwnd, className, r)) return true;

                        GetWindowThreadProcessId(hwnd, out uint pid);
                        if (pid == 0 || pid == (uint)currentPid) return true;

                        string text = GetNotificationText(hwnd, className);
                        if (string.IsNullOrWhiteSpace(text))
                        {
                            // UIA 冷却期间文本为空：若该窗口已在列表中则保留，避免被误清理
                            if (_active.ContainsKey(hwnd)) seen.Add(hwnd);
                            return true;
                        }

                        DynamicBird.Core.Infrastructure.Logging.LogManager.Debug(
                            $"[ToastMonitor] 捕获候选: class={className} pid={pid} rect=({r.Left},{r.Top},{r.Right},{r.Bottom}) text='{text[..Math.Min(40, text.Length)]}'");

                        string signature = pid + "|" + text;
                        if (_lastSignature.TryGetValue(hwnd, out var prev) && prev == signature)
                        {
                            seen.Add(hwnd);
                            return true;
                        }

                        if (_active.TryGetValue(hwnd, out var existing))
                        {
                            // 同一窗口内容变化（如 QQ 连续消息复用气泡）→ 更新为新通知
                            existing.Message = text.Length > 240 ? text[..240] + "…" : text;
                            existing.Time = DateTime.Now;
                        }
                        else
                        {
                            string appName = GetFriendlyProcessName((int)pid);
                            var item = new ToastNotificationItem
                            {
                                AppName = string.IsNullOrEmpty(appName) ? DynamicBird.UI.Localization.LocalizationManager.Instance["Toast_UnknownApp"] : appName,
                                Message = text.Length > 240 ? text[..240] + "…" : text,
                                PopupHwnd = hwnd,
                                ProcessId = (int)pid,
                                Time = DateTime.Now
                            };
                            _active[hwnd] = item;
                            Notifications.Insert(0, item);
                            while (Notifications.Count > MaxItems) Notifications.RemoveAt(Notifications.Count - 1);
                        }

                        _lastSignature[hwnd] = signature;
                        seen.Add(hwnd);
                        changed = true;
                    }
                    catch { }
                    return true;
                }, IntPtr.Zero);

                // 移除已消失的弹窗
                var gone = _active.Keys.Where(h => !seen.Contains(h)).ToList();
                foreach (var h in gone)
                {
                    if (_active.Remove(h, out var item))
                    {
                        Notifications.Remove(item);
                    }
                    _lastSignature.Remove(h);
                    _uiaCooldown.Remove(h);
                }

                if (changed || gone.Count > 0) Changed?.Invoke();
            }
            catch { }
        }

        private static bool IsToastLike(IntPtr hwnd, string className, RECT r)
        {
            int w = r.Right - r.Left;
            int h = r.Bottom - r.Top;

            // UWP 原生 Toast（通知中心的 XAML 窗口）
            if (className.Equals("Windows.UI.Core.CoreWindow", StringComparison.OrdinalIgnoreCase))
                return true;

            // 已知通知窗口类名
            if (className.Contains("Toast", StringComparison.OrdinalIgnoreCase) ||
                className.Contains("Notification", StringComparison.OrdinalIgnoreCase) ||
                className.Contains("Notify", StringComparison.OrdinalIgnoreCase))
                return true;

            // QQ / TIM 自绘气泡
            if (className is "WTWindow" or "TXGuiFoundation" or "QQUIWindow")
                return true;

            int screenW = GetSystemMetrics(0);
            int screenH = GetSystemMetrics(1);
            int margin = 400;

            bool nearCorner =
                (r.Left <= margin && r.Top <= margin) ||
                (r.Right >= screenW - margin && r.Top <= margin) ||
                (r.Left <= margin && r.Bottom >= screenH - margin) ||
                (r.Right >= screenW - margin && r.Bottom >= screenH - margin);

            // 小尺寸 + 靠近边/角 → 大概率是通知。
            // ★ QQ 等 Chromium 系应用（Chrome_WidgetWin_*）的通知气泡是无所有者的顶层小窗口，
            //   因此不能要求“有所有者”，否则会漏掉。
            bool nearRightOrBottom = r.Right >= screenW - margin || r.Bottom >= screenH - margin;
            return nearCorner || nearRightOrBottom;
        }

        private static string GetNotificationText(IntPtr hwnd, string className)
        {
            string winTitle = GetWindowText(hwnd);
            if (!string.IsNullOrWhiteSpace(winTitle)) return winTitle;

            // 空标题窗口：UIA 遍历代价较高，加冷却避免每轮全量遍历
            if (_uiaCooldown.TryGetValue(hwnd, out int cd))
            {
                if (cd > 0)
                {
                    _uiaCooldown[hwnd] = cd - 1;
                    return "";
                }
            }
            _uiaCooldown[hwnd] = UiaCooldownScans;

            try
            {
                var root = AutomationElement.FromHandle(hwnd);
                if (root == null) return "";

                string name = root.Current.Name;
                if (!string.IsNullOrWhiteSpace(name)) return name.Trim();

                var texts = root.FindAll(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text));
                if (texts == null) return "";

                var parts = new List<string>();
                foreach (AutomationElement el in texts)
                {
                    string t = el.Current.Name;
                    if (!string.IsNullOrWhiteSpace(t)) parts.Add(t.Trim());
                    if (parts.Count >= 4) break;
                }

                if (parts.Count > 0) return string.Join("  ", parts);

                // 纯图片通知（如 QQ 图片消息）：没有文字时给出占位，避免空白条目
                var images = root.FindAll(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Image));
                if (images != null && images.Count > 0) return "[图片]";

                return "";
            }
            catch { return ""; }
        }

        private static readonly Dictionary<int, string> _appNameCache = new();

        private static string GetFriendlyProcessName(int pid)
        {
            if (_appNameCache.TryGetValue(pid, out var cached)) return cached;

            try
            {
                using var proc = Process.GetProcessById(pid);
                string name = proc.ProcessName;
                string friendly = name.ToLowerInvariant() switch
                {
                    "wechat" or "weixin" => DynamicBird.UI.Localization.LocalizationManager.Instance["Recent_Wechat"],
                    "qq" or "qqnt" => "QQ",
                    "tim" => "TIM",
                    "dingtalk" => DynamicBird.UI.Localization.LocalizationManager.Instance["Recent_Dingtalk"],
                    "feishu" or "lark" => DynamicBird.UI.Localization.LocalizationManager.Instance["Recent_Feishu"],
                    "wechatwork" or "wework" => DynamicBird.UI.Localization.LocalizationManager.Instance["Recent_WxWork"],
                    "wps" => "WPS",
                    "chrome" => "Chrome",
                    "msedge" => "Edge",
                    "firefox" => "Firefox",
                    "devenv" => "Visual Studio",
                    "explorer" => DynamicBird.UI.Localization.LocalizationManager.Instance["Recent_Explorer"],
                    _ => name
                };
                _appNameCache[pid] = friendly;
                return friendly;
            }
            catch
            {
                return "";
            }
        }

        // ================= Win32 =================

        private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder className, int maxCount);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        private const uint GW_OWNER = 4;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        private static string GetWindowText(IntPtr hwnd)
        {
            int len = GetWindowTextLength(hwnd);
            if (len <= 0) return "";
            var sb = new StringBuilder(len + 1);
            return GetWindowText(hwnd, sb, sb.Capacity) > 0 ? sb.ToString() : "";
        }

        private static string GetClassName(IntPtr hwnd)
        {
            var sb = new StringBuilder(256);
            return GetClassName(hwnd, sb, sb.Capacity) > 0 ? sb.ToString() : "";
        }
    }
}
