using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LingDongBird.Core
{
    public static class WindowListProvider
    {
        public class WindowItem
        {
            public IntPtr Handle { get; set; }
            public string Title { get; set; } = "";
            public ImageSource? Icon { get; set; }
        }

        public static List<WindowItem> GetOpenWindows()
        {
            var dict = new Dictionary<IntPtr, WindowItem>();
            var callback = new EnumWindowsProc((hwnd, lParam) =>
            {
                try
                {
                    if (!IsWindowVisible(hwnd)) return true;
                    if (GetWindowTextLength(hwnd) == 0) return true;

                    string title = GetWindowText(hwnd);
                    if (string.IsNullOrWhiteSpace(title)) return true;

                    string className = GetClassName(hwnd);
                    if (className == "Progman" || className == "Shell_TrayWnd") return true;

                    IntPtr root = GetAncestor(hwnd, GA_ROOT);

                    if (dict.ContainsKey(root)) return true;

                    var icon = GetWindowIcon(root);

                    dict[root] = new WindowItem
                    {
                        Handle = root,
                        Title = title,
                        Icon = icon
                    };
                }
                catch { }
                return true;
            });

            EnumWindows(callback, IntPtr.Zero);
            return new List<WindowItem>(dict.Values);
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
        private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr GetClassLong(IntPtr hWnd, int nIndex);

        private const uint GA_ROOT = 2;
        private const int WM_GETICON = 0x007F;
        private const int ICON_SMALL = 0;
        private const int ICON_BIG = 1;
        private const int GCL_HICON = -14;
        private const int GCL_HICONSM = -34;

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
    }
}