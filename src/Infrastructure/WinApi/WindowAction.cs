using System;
using System.Runtime.InteropServices;

namespace DynamicBird.Infrastructure.WinApi
{
    public static class WindowAction
    {
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

        private const int SW_SHOW = 5;
        private const int SW_RESTORE = 9;
        private const int SW_MINIMIZE = 6;
        private const int WM_CLOSE = 0x0010;
        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_MINIMIZE = 0xF020;
        private const int SC_RESTORE = 0xF120;
        private const uint GA_ROOT = 2;

        public static void SwitchTo(IntPtr hwnd)
        {
            try
            {
                if (IsIconic(hwnd))
                    ShowWindow(hwnd, SW_RESTORE);
                ShowWindow(hwnd, SW_SHOW);
                SetForegroundWindow(hwnd);
                BringWindowToTop(hwnd);
                SwitchToThisWindow(hwnd, true);
            }
            catch { }
        }

        public static void Close(IntPtr hwnd)
        {
            try { SendMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero); } catch { }
        }

        public static bool IsWindowAlive(IntPtr hwnd) => hwnd != IntPtr.Zero && IsWindow(hwnd);

        public static void Restore(IntPtr hwnd)
        {
            try
            {
                if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);
                SetForegroundWindow(hwnd);
                BringWindowToTop(hwnd);
                SwitchToThisWindow(hwnd, true);
            }
            catch { }
        }

        public static void ToggleMinimize(IntPtr hwnd)
        {
            try
            {
                if (IsIconic(hwnd))
                {
                    SendMessage(hwnd, WM_SYSCOMMAND, (IntPtr)SC_RESTORE, IntPtr.Zero);
                    SetForegroundWindow(hwnd);
                    SwitchToThisWindow(hwnd, true);
                }
                else
                {
                    SendMessage(hwnd, WM_SYSCOMMAND, (IntPtr)SC_MINIMIZE, IntPtr.Zero);
                }
            }
            catch { }
        }

        public static IntPtr GetRootWindow(IntPtr hwnd)
        {
            try
            {
                return GetAncestor(hwnd, GA_ROOT);
            }
            catch
            {
                IntPtr top = hwnd;
                while (true)
                {
                    IntPtr parent = GetParent(top);
                    if (parent == IntPtr.Zero) break;
                    top = parent;
                }
                return top;
            }
        }

        public static IntPtr GetForegroundRootWindow()
        {
            IntPtr foreground = GetForegroundWindow();
            return GetRootWindow(foreground);
        }

        public static bool IsCurrentWindow(IntPtr hwnd)
        {
            IntPtr hwndRoot = GetRootWindow(hwnd);
            IntPtr foregroundRoot = GetForegroundRootWindow();
            return hwndRoot == foregroundRoot;
        }
    }
}
