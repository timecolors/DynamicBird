using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ShoreHue.UI.Theme
{
    /// <summary>
    /// 应用窗口图标：从可执行文件内置图标提取（单文件发布下也有效）。
    /// 显式设置窗口 Icon 可避免 WPF 回退到系统默认图标（该默认图标含畸形 PNG，
    /// 每次显示窗口都会触发 libpng 警告），并统一标题栏/任务栏图标。
    /// </summary>
    public static class AppIconHelper
    {
        public static ImageSource? LoadAppIcon()
        {
            try
            {
                string exe = Environment.ProcessPath ?? "";
                if (string.IsNullOrEmpty(exe)) return null;
                using var icon = System.Drawing.Icon.ExtractAssociatedIcon(exe);
                if (icon == null) return null;
                var source = Imaging.CreateBitmapSourceFromHIcon(
                    icon.Handle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            catch
            {
                return null;
            }
        }
    }
}
