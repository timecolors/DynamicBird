using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace DynamicBird.UI.Main
{
    public partial class MainWindow
    {
        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, ref RECT rect);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(ref POINT point);

        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private struct POINT
        {
            public int X;
            public int Y;
        }

        private double GetTaskbarHeight()
        {
            try
            {
                IntPtr taskbarHandle = FindWindow("Shell_TrayWnd", null);
                if (taskbarHandle != IntPtr.Zero)
                {
                    RECT rect = new RECT();
                    GetWindowRect(taskbarHandle, ref rect);
                    return rect.Bottom - rect.Top;
                }
            }
            catch { }
            return 40;
        }

        /// <summary>
        /// ★★★ 获取任务栏顶部坐标（DIP 单位） ★★★
        /// 返回任务栏上边缘的 Y 坐标，面板底部应贴在此位置
        /// </summary>
        private double GetTaskbarTopInDips()
        {
            try
            {
                IntPtr taskbarHandle = FindWindow("Shell_TrayWnd", null);
                if (taskbarHandle != IntPtr.Zero)
                {
                    RECT rect = new RECT();
                    if (GetWindowRect(taskbarHandle, ref rect))
                    {
                        // 物理像素 → DIP 转换
                        double dpiScale = 1.0;
                        var source = PresentationSource.FromVisual(this);
                        if (source?.CompositionTarget != null)
                        {
                            dpiScale = source.CompositionTarget.TransformToDevice.M11;
                        }
                        if (dpiScale <= 0 || double.IsNaN(dpiScale) || double.IsInfinity(dpiScale))
                        {
                            dpiScale = 1.0;
                        }

                        // 任务栏顶部坐标（DIP）
                        double taskbarTopDips = rect.Top / dpiScale;

                        // ★★★ 验证：任务栏顶部不能大于屏幕高度 ★★★
                        double screenHeightDips = SystemParameters.PrimaryScreenHeight;
                        if (taskbarTopDips > 0 && taskbarTopDips < screenHeightDips)
                        {
                            return taskbarTopDips;
                        }

                        // 如果值异常，回退到 WorkArea.Bottom
                        System.Diagnostics.Debug.WriteLine($"任务栏顶部坐标异常: {taskbarTopDips}, 回退到 WorkArea.Bottom");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取任务栏位置失败: {ex.Message}");
            }

            // ★★★ 备用：使用 WorkArea.Bottom ★★★
            return SystemParameters.WorkArea.Bottom;
        }
    }
}