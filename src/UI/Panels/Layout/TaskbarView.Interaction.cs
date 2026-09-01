using ShoreHue.Infrastructure.WinApi;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ShoreHue.UI.Panels
{
    public partial class TaskbarView
    {
        // ============ 滚轮滚动 ============

        private void ShortcutScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer scroller)
            {
                double offset = scroller.HorizontalOffset - (e.Delta / 3.0);
                offset = Math.Max(0, Math.Min(scroller.ExtentWidth - scroller.ViewportWidth, offset));
                scroller.ScrollToHorizontalOffset(offset);
                e.Handled = true;
            }
        }

        private void WindowScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer scroller)
            {
                double offset = scroller.HorizontalOffset - (e.Delta / 3.0);
                offset = Math.Max(0, Math.Min(scroller.ExtentWidth - scroller.ViewportWidth, offset));
                scroller.ScrollToHorizontalOffset(offset);
                e.Handled = true;
            }
        }

        // ============ 窗口任务交互 ============

        // ★ 用 PreviewMouseUp 直接触发关闭：窗口标签列表会每秒刷新，
        //   按钮可能在下一次刷新中被重建，导致 WPF 的 Click 事件在 MouseUp 阶段丢失。
        private void CloseBtn_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            OnCloseButtonClick(sender, e);
            e.Handled = true;
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is TaskbarItem item && item.Handle.HasValue)
            {
                try
                {
                    IntPtr hwnd = item.Handle.Value;
                    ShoreHue.Core.Infrastructure.Logging.LogManager.Debug(
                        $"[TaskbarClose] 点击关闭: hwnd={hwnd} title='{item.DisplayName}' isWindow={WindowAction.IsWindowAlive(hwnd)}");
                    WindowAction.Close(hwnd);
                    ShoreHue.Core.Infrastructure.Logging.LogManager.Debug(
                        $"[TaskbarClose] WM_CLOSE 已发送: hwnd={hwnd}");

                    // 立即从列表中移除
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var toRemove = item;
                        if (toRemove != null)
                        {
                            _windows.Remove(toRemove);
                        }

                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            RefreshWindows();
                        }), DispatcherPriority.Background);
                    }), DispatcherPriority.Normal);

                    e.Handled = true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"关闭窗口失败: {ex.Message}");
                    RefreshWindows();
                }
            }
        }

    }
}
