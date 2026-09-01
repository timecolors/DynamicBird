using ShoreHue.Core.Services;
using ShoreHue.Infrastructure.WinApi;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ShoreHue.UI.Panels
{
    public partial class TaskbarView
    {
        // ★★★ 在 TaskbarView.xaml.cs 的 OnLoaded 中调用此方法 ★★★
        private void InitializeDragDropEvents()
        {
            MainGrid.PreviewMouseLeftButtonDown += OnMainGridMouseDown;
            MainGrid.PreviewMouseMove += OnMainGridMouseMove;
            MainGrid.PreviewMouseLeftButtonUp += OnMainGridMouseUp;
            MainGrid.DragOver += OnMainGridDragOver;
            MainGrid.Drop += OnMainGridDrop;
        }

        // 状态变量
        private TaskbarItem? _pendingItem = null;
        private bool _isDragging = false;
        private Point _dragStartPoint;
        private TaskbarItem? _pendingClickItem = null;

        private void OnMainGridMouseDown(object sender, MouseButtonEventArgs e)
        {
            var hit = VisualTreeHelper.HitTest(MainGrid, e.GetPosition(MainGrid));
            if (hit == null) return;

            // 点按发生在按钮内部（如窗口关闭按钮）时不启动拖拽
            if (IsInsideButton(hit.VisualHit)) return;

            var dep = hit.VisualHit;
            while (dep != null)
            {
                if (dep is FrameworkElement fe && fe.DataContext is TaskbarItem item)
                {
                    _pendingItem = item;
                    _dragStartPoint = e.GetPosition(MainGrid);
                    _isDragging = false;
                    _pendingClickItem = item;
                    MainGrid.CaptureMouse();
                    e.Handled = true;
                    return;
                }
                dep = VisualTreeHelper.GetParent(dep);
            }
        }

        private void OnMainGridMouseMove(object sender, MouseEventArgs e)
        {
            if (_pendingItem == null) return;

            var currentPos = e.GetPosition(MainGrid);
            double dx = currentPos.X - _dragStartPoint.X;
            double dy = currentPos.Y - _dragStartPoint.Y;

            if (!_isDragging && (Math.Abs(dx) > 5 || Math.Abs(dy) > 5))
            {
                _isDragging = true;
                _pendingClickItem = null;

                // ★★★ 启动系统拖拽流程 ★★★
                // 这样 ShoreHue 图标的 DragEnter/DragLeave/Drop 事件会被触发
                var data = new DataObject(typeof(TaskbarItem), _pendingItem);
                // ★ 允许 Copy|Move：任务栏内排序用 Move，拖到左侧图标固定应用用 Copy
                DragDrop.DoDragDrop(MainGrid, data, DragDropEffects.Copy | DragDropEffects.Move);

                // 拖拽结束后，清理状态
                EndDrag();
                e.Handled = true;
                return;
            }

            // ★★★ 移除直接删除逻辑，删除由 MainWindow.DragDrop.cs 的 IconText_Drop 处理 ★★★
        }

        private void OnMainGridMouseUp(object sender, MouseButtonEventArgs e)
        {
            bool handled = false;
            if (_pendingClickItem != null && !_isDragging)
            {
                ExecuteClick(_pendingClickItem);
                handled = true;
            }
            EndDrag();
            // 只有确实处理了点击/拖拽时才吞掉事件，避免挡住关闭按钮等内部控件
            if (handled) e.Handled = true;
        }

        private void OnMainGridDragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(typeof(TaskbarItem))
                ? DragDropEffects.Move
                : DragDropEffects.None;
            e.Handled = true;
        }

        private void OnMainGridDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(TaskbarItem))) return;
            if (e.Data.GetData(typeof(TaskbarItem)) is not TaskbarItem dragged) return;

            var target = FindTaskbarItemAt(e.GetPosition(MainGrid));
            if (target == null || ReferenceEquals(target, dragged)) return;

            if (dragged.Type == TaskbarItemType.Window && target.Type == TaskbarItemType.Window)
            {
                int from = _windows.IndexOf(dragged);
                int to = _windows.IndexOf(target);
                if (from >= 0 && to >= 0 && from != to)
                {
                    _windows.Move(from, to);
                }
            }
            else if (dragged.Type == TaskbarItemType.Shortcut)
            {
                var shortcutItems = _shortcuts.Where(i => i.Type == TaskbarItemType.Shortcut).ToList();
                int from = shortcutItems.IndexOf(dragged);
                int to = target.Type == TaskbarItemType.Shortcut
                    ? shortcutItems.IndexOf(target)
                    : ComputeShortcutInsertIndex(target);
                if (from >= 0 && to >= 0 && from != to)
                {
                    // 排序由 ShortcutManager 持久化，并通过 ShortcutsChanged 自动刷新视图
                    _shortcutManager.MoveShortcut(from, to);
                }
            }
            e.Handled = true;
        }

        /// <summary>
        /// 目标不是快捷方式时（如窗口项），计算“插入到该位置之前”的快捷方式索引。
        /// </summary>
        private int ComputeShortcutInsertIndex(TaskbarItem target)
        {
            int allIndex = _shortcuts.IndexOf(target);
            if (allIndex < 0) return -1;

            int shortcutIndex = 0;
            for (int i = 0; i < allIndex && i < _shortcuts.Count; i++)
            {
                if (_shortcuts[i].Type == TaskbarItemType.Shortcut) shortcutIndex++;
            }
            return shortcutIndex;
        }

        private void ExecuteClick(TaskbarItem item)
        {
            if (item == null) return;

            if (item.Type == TaskbarItemType.Window && item.Handle.HasValue)
            {
                WindowAction.ToggleMinimize(item.Handle.Value);
                return;
            }

            if (item.Type == TaskbarItemType.Shortcut)
            {
                if (!string.IsNullOrEmpty(item.Path))
                {
                    RecentAppTracker.RecordLaunch(item.Path);
                }
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
                        string target = ShortcutLinkResolver.Resolve(path);
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

        private TaskbarItem? FindTaskbarItemAt(Point position)
        {
            var hitResult = VisualTreeHelper.HitTest(MainGrid, position);
            if (hitResult == null) return null;

            var dep = hitResult.VisualHit;
            while (dep != null)
            {
                if (dep is FrameworkElement fe && fe.DataContext is TaskbarItem item)
                    return item;
                dep = VisualTreeHelper.GetParent(dep);
            }
            return null;
        }

        internal static bool IsInsideButton(DependencyObject? visual)
        {
            var dep = visual;
            while (dep != null)
            {
                if (dep is Button) return true;
                dep = VisualTreeHelper.GetParent(dep);
            }
            return false;
        }

        private void EndDrag()
        {
            _pendingItem = null;
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
