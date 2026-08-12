using DynamicBird.Infrastructure.WinApi;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace DynamicBird.UI.Panels
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

        private void OnWindowMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is TaskbarItem item && item.Handle.HasValue)
            {
                // 如果点击的是关闭按钮，不处理（由关闭按钮自己处理）
                if (e.OriginalSource is Button) return;

                WindowAction.ToggleMinimize(item.Handle.Value);
                e.Handled = true;
            }
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is TaskbarItem item && item.Handle.HasValue)
            {
                try
                {
                    IntPtr hwnd = item.Handle.Value;
                    WindowAction.Close(hwnd);

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

        /// <summary>
        /// 阻止点击关闭按钮时触发 Border 的 PreviewMouseLeftButtonDown
        /// </summary>
        private void OnCloseButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }
    }
}