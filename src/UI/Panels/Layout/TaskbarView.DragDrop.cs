using DynamicBird.Core.Services;
using DynamicBird.Infrastructure.WinApi;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace DynamicBird.UI.Panels
{
    public partial class TaskbarView
    {
        // ★★★ 在 TaskbarView.xaml.cs 的 OnLoaded 中调用此方法 ★★★
        private void InitializeDragDropEvents()
        {
            MainGrid.PreviewMouseLeftButtonDown += OnMainGridMouseDown;
            MainGrid.PreviewMouseMove += OnMainGridMouseMove;
            MainGrid.PreviewMouseLeftButtonUp += OnMainGridMouseUp;
        }

        // 状态变量
        private TaskbarItem? _draggedShortcut = null;
        private bool _isDragging = false;
        private Point _dragStartPoint;
        private TaskbarItem? _pendingClickItem = null;
        private DateTime _mouseDownTime;

        private void OnMainGridMouseDown(object sender, MouseButtonEventArgs e)
        {
            var hit = VisualTreeHelper.HitTest(MainGrid, e.GetPosition(MainGrid));
            if (hit == null) return;

            var dep = hit.VisualHit;
            while (dep != null)
            {
                if (dep is Border border && border.DataContext is TaskbarItem item && item.Type == TaskbarItemType.Shortcut)
                {
                    _draggedShortcut = item;
                    _dragStartPoint = e.GetPosition(MainGrid);
                    _isDragging = false;
                    _pendingClickItem = item;
                    _mouseDownTime = DateTime.Now;
                    MainGrid.CaptureMouse();
                    e.Handled = true;
                    return;
                }
                dep = VisualTreeHelper.GetParent(dep);
            }
        }

        private void OnMainGridMouseMove(object sender, MouseEventArgs e)
        {
            if (_draggedShortcut == null) return;

            var currentPos = e.GetPosition(MainGrid);
            double dx = currentPos.X - _dragStartPoint.X;
            double dy = currentPos.Y - _dragStartPoint.Y;

            if (!_isDragging && (Math.Abs(dx) > 5 || Math.Abs(dy) > 5))
            {
                _isDragging = true;
                _pendingClickItem = null;

                // ★★★ 启动系统拖拽流程 ★★★
                // 这样灵动鸟图标的 DragEnter/DragLeave/Drop 事件会被触发
                var data = new DataObject(typeof(TaskbarItem), _draggedShortcut);
                DragDrop.DoDragDrop(MainGrid, data, DragDropEffects.Move);

                // 拖拽结束后，清理状态
                EndDrag();
                e.Handled = true;
                return;
            }

            // ★★★ 移除直接删除逻辑，删除由 MainWindow.DragDrop.cs 的 IconText_Drop 处理 ★★★
        }

        private void OnMainGridMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_pendingClickItem != null && !_isDragging)
            {
                ExecuteClick(_pendingClickItem);
            }
            EndDrag();
            e.Handled = true;
        }

        private void ExecuteClick(TaskbarItem item)
        {
            if (item == null) return;

            if (item.Type == TaskbarItemType.Shortcut)
            {
                if (item.IsRunning && item.Handle.HasValue)
                {
                    WindowAction.SwitchTo(item.Handle.Value);
                    return;
                }
                if (string.IsNullOrEmpty(item.Path)) return;

                try
                {
                    string path = item.Path;
                    if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                    {
                        string target = ResolveShortcutTarget(path);
                        if (!string.IsNullOrEmpty(target)) path = target;
                    }
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(psi);
                }
                catch
                {
                    try
                    {
                        var psi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = item.Path,
                            UseShellExecute = true
                        };
                        System.Diagnostics.Process.Start(psi);
                    }
                    catch { }
                }
            }
        }

        private string ResolveShortcutTarget(string shortcutPath)
        {
            try
            {
                var shell = new Shell32.Shell();
                var folder = shell.NameSpace(System.IO.Path.GetDirectoryName(shortcutPath));
                var folderItem = folder.ParseName(System.IO.Path.GetFileName(shortcutPath));
                if (folderItem != null && folderItem.IsLink)
                {
                    var link = folderItem.GetLink;
                    if (link != null) return link.Path;
                }
                return shortcutPath;
            }
            catch { return shortcutPath; }
        }

        private int GetTargetIndex(Point position)
        {
            var hitResult = VisualTreeHelper.HitTest(MainGrid, position);
            if (hitResult == null) return -1;

            var dep = hitResult.VisualHit;
            while (dep != null)
            {
                if (dep is FrameworkElement fe && fe.DataContext is TaskbarItem item)
                    return _shortcuts.IndexOf(item);
                dep = VisualTreeHelper.GetParent(dep);
            }
            return -1;
        }

        private void EndDrag()
        {
            _draggedShortcut = null;
            _isDragging = false;
            _pendingClickItem = null;

            try
            {
                if (MainGrid.IsMouseCaptured)
                    MainGrid.ReleaseMouseCapture();
                if (Mouse.Captured != null)
                    Mouse.Capture(null);
            }
            catch { }
            Mouse.OverrideCursor = null;
        }
    }
}