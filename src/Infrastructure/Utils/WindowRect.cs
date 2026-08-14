using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DynamicBird.Infrastructure.Utils
{
    /// <summary>
    /// 窗口矩形工具：以单次 SetWindowPos 原子地同时应用位置和尺寸。
    /// WPF 的 Left/Top/Width/Height 每个属性赋值都会立即触发一次 SetWindowPos，
    /// 在“贴边侧坐标依赖尺寸”的边缘（底部/右侧）会渲染出先内缩再贴回的中间态。
    /// 一次原子调用可彻底消除该问题。
    /// </summary>
    public static class WindowRect
    {
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

        /// <summary>
        /// 原子应用窗口矩形（DIP 坐标），WPF 依赖属性会由 WM_MOVE/WM_SIZE 自动同步。
        /// </summary>
        public static void ApplyAtomic(Window window, double left, double top, double width, double height)
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero)
            {
                // 句柄尚未创建（如启动早期）：回退到属性赋值
                window.Left = left;
                window.Top = top;
                window.Width = width;
                window.Height = height;
                return;
            }

            double scale = DpiHelper.GetDpiScale(window);
            if (scale <= 0 || double.IsNaN(scale) || double.IsInfinity(scale))
            {
                scale = 1.0;
            }

            int x = (int)Math.Round(left * scale);
            int y = (int)Math.Round(top * scale);
            int cx = (int)Math.Round(width * scale);
            int cy = (int)Math.Round(height * scale);

            SetWindowPos(hwnd, IntPtr.Zero, x, y, cx, cy, SWP_NOZORDER | SWP_NOACTIVATE);

            // WPF 依赖属性由 Win32 消息同步；若因最小/最大尺寸钳制等与目标值有偏差，
            // 回填属性值以保持状态一致（数值相同则不会产生可见的二次移动）。
            if (!NearlyEqual(window.Left, left) || !NearlyEqual(window.Top, top) ||
                !NearlyEqual(window.Width, width) || !NearlyEqual(window.Height, height))
            {
                window.Left = left;
                window.Top = top;
                window.Width = width;
                window.Height = height;
            }
        }

        private static bool NearlyEqual(double a, double b) => Math.Abs(a - b) < 0.5;
    }
}
