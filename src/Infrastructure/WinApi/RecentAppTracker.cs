using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Threading;
using Microsoft.Win32;
using DynamicBird.Infrastructure.Utils;

namespace DynamicBird.Infrastructure.WinApi
{
    /// <summary>
    /// 最近打开的应用追踪：
    ///  - 周期性地把“当前前台窗口”所属进程记录为最近使用（系统级，无需钩子）；
    ///  - 面板内主动启动应用时也会立即记录（RecordLaunch）。
    /// 数据保存在 data/recent_apps.json，供“最近使用-程序”页展示。
    /// </summary>
    public static class RecentAppTracker
    {
        public sealed class RecentApp
        {
            public string Path { get; set; } = "";
            public string Name { get; set; } = "";
            public DateTime LastUsed { get; set; } = DateTime.Now;
        }

        private static readonly string StorePath = AppPaths.RecentAppsPath;

        private static readonly object _lock = new();
        private static readonly Dictionary<string, RecentApp> _apps = new(StringComparer.OrdinalIgnoreCase);
        private static DispatcherTimer? _timer;
        private static string? _lastForegroundExe;
        private static int _currentPid = Environment.ProcessId;

        public static void Start()
        {
            if (_timer != null) return;

            Load();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            _timer.Tick += (_, _) => ScanForeground();
            _timer.Start();
        }

        public static void Stop()
        {
            _timer?.Stop();
            _timer = null;
        }

        public static void RecordLaunch(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            string exe = path;
            try
            {
                if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    string? resolved = ShortcutLinkResolver.Resolve(path);
                    if (!string.IsNullOrEmpty(resolved)) exe = resolved;
                }
            }
            catch { }

            lock (_lock)
            {
                string name = FriendlyName(exe);
                _apps[exe] = new RecentApp { Path = exe, Name = name, LastUsed = DateTime.Now };
                Trim();
                Save();
            }
        }

        public static IReadOnlyList<RecentApp> GetRecentApps(int max = 30)
        {
            lock (_lock)
            {
                return _apps.Values
                    .OrderByDescending(a => a.LastUsed)
                    .Take(max)
                    .ToList();
            }
        }

        // ================= 前台窗口扫描 =================

        private static void ScanForeground()
        {
            try
            {
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return;
                GetWindowThreadProcessId(hwnd, out uint pid);
                if (pid == 0 || pid == (uint)_currentPid) return;

                using var proc = Process.GetProcessById((int)pid);
                string? exe = proc.MainModule?.FileName;
                if (string.IsNullOrEmpty(exe)) return;
                if (IsNoise(exe)) return;

                // 桌面/任务栏/系统托盘噪音
                string name = Path.GetFileNameWithoutExtension(exe);
                if (name.Equals("explorer", StringComparison.OrdinalIgnoreCase) &&
                    proc.MainWindowTitle.Length == 0)
                {
                    return;
                }

                if (string.Equals(exe, _lastForegroundExe, StringComparison.OrdinalIgnoreCase)) return;
                _lastForegroundExe = exe;

                lock (_lock)
                {
                    _apps[exe] = new RecentApp { Path = exe, Name = FriendlyName(exe), LastUsed = DateTime.Now };
                    Trim();
                    Save();
                }
            }
            catch { }
        }

        private static string FriendlyName(string exe)
        {
            string baseName = Path.GetFileNameWithoutExtension(exe);
            return baseName.ToLowerInvariant() switch
            {
                "qq" or "qqnt" => "QQ",
                "wechat" or "weixin" => "微信",
                "chrome" => "Chrome",
                "msedge" => "Edge",
                "devenv" => "Visual Studio",
                "explorer" => "文件资源管理器",
                _ => baseName
            };
        }

        /// <summary>
        /// 过滤明显不适用于“最近打开的应用”的噪音（驱动安装器、系统组件、临时程序）。
        /// </summary>
        private static bool IsNoise(string exe)
        {
            try
            {
                string p = exe.ToLowerInvariant();
                if (p.Contains("\\drivers\\") ||
                    p.Contains("\\sysdiag\\bin\\") ||
                    p.Contains("\\installer\\") ||
                    p.Contains("\\temp\\") ||
                    p.StartsWith(@"c:\windows\") ||
                    p.StartsWith(@"c:\program files\windows"))
                {
                    return true;
                }

                string name = Path.GetFileNameWithoutExtension(exe).ToLowerInvariant();
                return name.StartsWith("setup") ||
                       name.StartsWith("install") ||
                       name.StartsWith("unins") ||
                       name.Contains("cleanup");
            }
            catch { return false; }
        }

        private static void Trim()
        {
            while (_apps.Count > 40)
            {
                var oldest = _apps.Values.OrderBy(a => a.LastUsed).FirstOrDefault();
                if (oldest == null) break;
                _apps.Remove(oldest.Path);
            }
        }

        // ================= 持久化 =================

        private static void Load()
        {
            try
            {
                lock (_lock)
                {
                    if (File.Exists(StorePath))
                    {
                        var list = JsonSerializer.Deserialize<List<RecentApp>>(File.ReadAllText(StorePath));
                        if (list != null)
                        {
                            _apps.Clear();
                            foreach (var item in list)
                            {
                                if (!string.IsNullOrEmpty(item.Path))
                                {
                                    _apps[item.Path] = item;
                                }
                            }
                        }
                    }

                    if (_apps.Count == 0)
                    {
                        SeedFromUserAssist();
                        Save();
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// 首次运行时用 UserAssist（系统记录过的启动项）做初始种子，
        /// 让“最近打开的应用”页一开始就有内容；之后由前台窗口扫描持续更新。
        /// </summary>
        private static void SeedFromUserAssist()
        {
            try
            {
                const string userAssistRoot =
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\UserAssist";
                using var root = Registry.CurrentUser.OpenSubKey(userAssistRoot);
                if (root == null) return;

                var entries = new List<(string Path, DateTime Time, int Order)>();
                int fallbackOrder = 0;

                foreach (var guidName in root.GetSubKeyNames())
                {
                    try
                    {
                        using var countKey = root.OpenSubKey(guidName + @"\Count");
                        if (countKey == null) continue;

                        foreach (var valueName in countKey.GetValueNames())
                        {
                            if (valueName.StartsWith("UEME_", StringComparison.OrdinalIgnoreCase)) continue;
                            if (countKey.GetValue(valueName) is not byte[] data || data.Length < 16) continue;

                            string decoded = Rot13(valueName);
                            if (string.IsNullOrWhiteSpace(decoded)) continue;

                            string exe = ResolveToExe(decoded);
                            if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) continue;
                            if (IsNoise(exe)) continue;
                            if (_apps.ContainsKey(exe)) continue;

                            long fileTime = BitConverter.ToInt64(data, 4);
                            DateTime time = fileTime > 0
                                ? DateTime.FromFileTime(fileTime)
                                : DateTime.Now.AddMinutes(-fallbackOrder);
                            fallbackOrder += 3;

                            entries.Add((exe, time, fallbackOrder));
                        }
                    }
                    catch { }
                }

                foreach (var e in entries.OrderByDescending(e => e.Time).Take(25))
                {
                    if (!_apps.ContainsKey(e.Path))
                    {
                        _apps[e.Path] = new RecentApp
                        {
                            Path = e.Path,
                            Name = FriendlyName(e.Path),
                            LastUsed = e.Time
                        };
                    }
                }
            }
            catch { }
        }

        private static string Rot13(string s)
        {
            var chars = s.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (c >= 'A' && c <= 'Z')
                    chars[i] = (char)('A' + (c - 'A' + 13) % 26);
                else if (c >= 'a' && c <= 'z')
                    chars[i] = (char)('a' + (c - 'a' + 13) % 26);
            }
            return new string(chars);
        }

        private static string ResolveToExe(string decoded)
        {
            try
            {
                if (decoded.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    // 形如 C:\...\app.exe
                    return decoded.Contains(":\\") ? decoded : "";
                }
                if (decoded.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    // 形如 {GUID}\Path\App.lnk 或 C:\...\App.lnk
                    string? resolved = ShortcutLinkResolver.Resolve(decoded);
                    if (!string.IsNullOrEmpty(resolved) &&
                        resolved.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        return resolved;
                    }
                }
            }
            catch { }
            return "";
        }

        private static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
                File.WriteAllText(StorePath, JsonSerializer.Serialize(_apps.Values.ToList()));
            }
            catch { }
        }

        // ================= Win32 =================

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    }
}
