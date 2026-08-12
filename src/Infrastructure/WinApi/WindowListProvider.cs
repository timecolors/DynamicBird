using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DynamicBird.Infrastructure.WinApi
{
    public static class WindowListProvider
    {
        // ========== Win32 常量 ==========
        private const int GWL_EXSTYLE = -20;
        private const uint WS_EX_TOOLWINDOW = 0x00000080;
        private const uint WS_EX_APPWINDOW = 0x00040000;

        // ========== 窗口项类 ==========
        public class WindowItem
        {
            public IntPtr Handle { get; set; }
            public string Title { get; set; } = "";
            public ImageSource? Icon { get; set; }
            public string ClassName { get; set; } = "";
            public bool IsVisible { get; set; }
            public bool IsToolWindow { get; set; }
            public bool HasTaskbarButton { get; set; }
        }

        // ========== 过滤模式 ==========
        public enum WindowFilterMode
        {
            All,           // 所有可见顶层窗口
            UserAppsOnly   // 仅任务栏显示的应用
        }

        // ========== 公开方法 ==========
        public static List<WindowItem> GetOpenWindows(WindowFilterMode filterMode = WindowFilterMode.UserAppsOnly)
        {
            var windows = new List<WindowItem>();
            var callback = new EnumWindowsProc((hwnd, lParam) =>
            {
                try
                {
                    // 基本过滤：可见 + 有标题
                    if (!IsWindowVisible(hwnd)) return true;
                    if (GetWindowTextLength(hwnd) == 0) return true;

                    string title = GetWindowText(hwnd);
                    if (string.IsNullOrWhiteSpace(title)) return true;

                    string className = GetClassName(hwnd);

                    // 排除系统桌面窗口
                    if (className == "Progman" ||
                        className == "Shell_TrayWnd" ||
                        className == "WorkerW")
                        return true;

                    // ★★★ 核心：判断是否显示在任务栏 ★★★
                    bool showsInTaskbar = ShouldShowInTaskbar(hwnd);

                    var item = new WindowItem
                    {
                        Handle = hwnd,
                        Title = title,
                        ClassName = className,
                        IsVisible = true,
                        IsToolWindow = (GetWindowLong(hwnd, GWL_EXSTYLE) & WS_EX_TOOLWINDOW) != 0,
                        HasTaskbarButton = showsInTaskbar
                    };

                    // 获取图标
                    item.Icon = GetWindowIcon(hwnd);

                    windows.Add(item);
                }
                catch { }
                return true;
            });

            EnumWindows(callback, IntPtr.Zero);

            // 应用过滤
            if (filterMode == WindowFilterMode.UserAppsOnly)
            {
                windows = windows.Where(w => w.HasTaskbarButton).ToList();
            }

            return windows;
        }

        // ============================================================
        //  ★★★ 核心方法：判断窗口是否应该出现在任务栏 ★★★
        //  基于 Windows 官方规则：
        //  1. WS_EX_APPWINDOW → 强制出现
        //  2. WS_EX_TOOLWINDOW → 强制隐藏
        //  3. 有所有者窗口 → 隐藏（由所有者管理）
        //  4. 无所有者窗口 → 出现
        // ============================================================
        private static bool ShouldShowInTaskbar(IntPtr hwnd)
        {
            // 获取扩展样式
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

            // 规则 1：WS_EX_APPWINDOW → 强制出现在任务栏
            if ((exStyle & WS_EX_APPWINDOW) != 0)
                return true;

            // 规则 2：WS_EX_TOOLWINDOW → 强制隐藏
            if ((exStyle & WS_EX_TOOLWINDOW) != 0)
                return false;

            // 规则 3 & 4：检查是否有所有者窗口
            IntPtr owner = GetWindow(hwnd, GW_OWNER);

            // 有所有者 → 隐藏（由所有者管理）
            if (owner != IntPtr.Zero)
                return false;

            // 无所有者 → 出现在任务栏
            return true;
        }

        // ============================================================
        //  Win32 API 声明
        // ============================================================

        private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hwnd, ref RECT rect);

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        private const uint GW_OWNER = 4;
        private const uint GA_ROOT = 2;

        private const int WM_GETICON = 0x007F;
        private const int ICON_SMALL = 0;
        private const int ICON_BIG = 1;
        private const int GCL_HICON = -14;
        private const int GCL_HICONSM = -34;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        // ============================================================
        //  辅助方法
        // ============================================================

        private static string GetWindowText(IntPtr hWnd)
        {
            const int nChars = 256;
            var buff = new System.Text.StringBuilder(nChars);
            return GetWindowText(hWnd, buff, nChars) > 0 ? buff.ToString() : "";
        }

        private static string GetClassName(IntPtr hWnd)
        {
            const int nChars = 256;
            var buff = new System.Text.StringBuilder(nChars);
            return GetClassName(hWnd, buff, nChars) > 0 ? buff.ToString() : "";
        }

        private static ImageSource? GetWindowIcon(IntPtr hwnd)
        {
            try
            {
                IntPtr iconHandle = SendMessage(hwnd, WM_GETICON, (IntPtr)ICON_SMALL, IntPtr.Zero);
                if (iconHandle == IntPtr.Zero)
                    iconHandle = SendMessage(hwnd, WM_GETICON, (IntPtr)ICON_BIG, IntPtr.Zero);
                if (iconHandle == IntPtr.Zero)
                    iconHandle = GetClassLong(hwnd, GCL_HICONSM);
                if (iconHandle == IntPtr.Zero)
                    iconHandle = GetClassLong(hwnd, GCL_HICON);

                if (iconHandle != IntPtr.Zero)
                {
                    using (var icon = System.Drawing.Icon.FromHandle(iconHandle))
                    {
                        return Imaging.CreateBitmapSourceFromHIcon(
                            icon.Handle,
                            new Int32Rect(0, 0, icon.Width, icon.Height),
                            BitmapSizeOptions.FromEmptyOptions());
                    }
                }
            }
            catch { }
            return null;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetClassLong(IntPtr hWnd, int nIndex);
    }
}