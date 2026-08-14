using System;
using System.Windows;
using DynamicBird.Core.Services;
using DynamicBird.Infrastructure.WinApi;
using DynamicBird.UI.AppHelper;
using DynamicBird.UI.Panels;
using System.Windows.Controls;

namespace DynamicBird.UI.Main
{
    public partial class MainWindow
    {
        private enum IconState
        {
            Default,
            AddMode,
            DeleteMode
        }

        private IconState _currentIconState = IconState.Default;
        private bool _isHovering = false;
        private AppHelperView? _appHelperView;

        private void UpdateIconTextInternal()
        {
            switch (_currentIconState)
            {
                case IconState.AddMode:
                    SetIcon("IconPlus", accent: true);
                    return;
                case IconState.DeleteMode:
                    SetIcon("IconTrash", accent: true);
                    return;
            }

            if (_isHovering && !_modeService.IsDoNotDisturb)
            {
                SetIcon("IconLogo", accent: true);
                return;
            }

            string iconPath = _settingsService.CustomIconPath;
            if (!string.IsNullOrEmpty(iconPath) && System.IO.File.Exists(iconPath))
            {
                try
                {
                    IconImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(iconPath));
                    IconImage.Visibility = System.Windows.Visibility.Visible;
                    IconPath.Visibility = System.Windows.Visibility.Collapsed;
                    return;
                }
                catch { }
            }

            IconImage.Visibility = System.Windows.Visibility.Collapsed;
            IconPath.Visibility = System.Windows.Visibility.Visible;
            SetIcon("IconLogo", accent: false);
        }

        private void SetIcon(string resourceKey, bool accent)
        {
            if (FindResource(resourceKey) is System.Windows.Media.Geometry g)
            {
                IconPath.Data = g;
            }
            IconPath.SetResourceReference(
                System.Windows.Shapes.Path.StrokeProperty,
                accent ? "AccentBrush" : "TextPrimaryBrush");
            IconImage.Visibility = System.Windows.Visibility.Collapsed;
            IconPath.Visibility = System.Windows.Visibility.Visible;
        }

        private void ResetIconToDefault()
        {
            _currentIconState = IconState.Default;
            UpdateIconTextInternal();
        }

        private void IconText_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _isHovering = true;
            UpdateIconTextInternal();
            e.Handled = true;
        }

        private void IconText_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _isHovering = false;
            UpdateIconTextInternal();
            e.Handled = true;
        }

        private void IconText_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                _currentIconState = IconState.AddMode;
                UpdateIconTextInternal();
            }
            else if (e.Data.GetDataPresent(typeof(TaskbarItem)))
            {
                var item = e.Data.GetData(typeof(TaskbarItem)) as TaskbarItem;
                if (item != null)
                {
                    if (item.Type == TaskbarItemType.Shortcut)
                    {
                        e.Effects = DragDropEffects.Move;
                        _currentIconState = IconState.DeleteMode;
                        UpdateIconTextInternal();
                    }
                    else if (item.Type == TaskbarItemType.Window)
                    {
                        e.Effects = DragDropEffects.Copy;
                        _currentIconState = IconState.AddMode;
                        UpdateIconTextInternal();
                    }
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void IconText_DragLeave(object sender, DragEventArgs e)
        {
            ResetIconToDefault();
            e.Handled = true;
        }

        private void IconText_DragOver(object sender, DragEventArgs e)
        {
            // ★ 悬停期间持续刷新拖放效果：否则光标会退回“禁止”
            IconText_DragEnter(sender, e);
        }

        private void IconText_Drop(object sender, DragEventArgs e)
        {
            try
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    if (files != null && files.Length > 0)
                    {
                        string path = files[0];
                        if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                            path = ShortcutLinkResolver.Resolve(path);
                        if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
                        {
                            string name = System.IO.Path.GetFileNameWithoutExtension(path);
                            if (_shortcutService.AddShortcut(path, name))
                                RefreshTaskbarView();
                        }
                    }
                }
                else if (e.Data.GetDataPresent(typeof(TaskbarItem)))
                {
                    var item = e.Data.GetData(typeof(TaskbarItem)) as TaskbarItem;
                    if (item != null)
                    {
                        if (item.Type == TaskbarItemType.Shortcut)
                        {
                            if (!string.IsNullOrEmpty(item.Id) && _shortcutService.RemoveShortcut(item.Id))
                                RefreshTaskbarView();
                        }
                        else if (item.Type == TaskbarItemType.Window)
                        {
                            // ★ 固定正在运行的窗口应用：窗口标题会变，用 exe 文件名做快捷方式名
                            string? exe = item.Path;
                            if (string.IsNullOrEmpty(exe) && item.Handle.HasValue)
                                exe = WindowListProvider.GetProcessPathByHandle(item.Handle.Value);
                            if (!string.IsNullOrEmpty(exe) && System.IO.File.Exists(exe))
                            {
                                string name = System.IO.Path.GetFileNameWithoutExtension(exe);
                                if (_shortcutService.AddShortcut(exe, name))
                                    RefreshTaskbarView();
                            }
                            else if (!string.IsNullOrEmpty(item.Path) && _shortcutService.AddShortcut(item.Path, item.DisplayName))
                            {
                                RefreshTaskbarView();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"拖放处理失败: {ex.Message}");
            }
            finally
            {
                ResetIconToDefault();
            }
            e.Handled = true;
        }

        private void IconText_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                // 右键只用于弹出快捷菜单
                if (e.ChangedButton == System.Windows.Input.MouseButton.Right) return;

                // ★ 应用辅助模式：点击灵动鸟循环切换辅助功能页 ★
                if (_contentController?.CurrentRegionType == "AppHelper" && _appHelperView != null)
                {
                    _appHelperView.CyclePage();
                    _isHovering = false;
                    UpdateIconTextInternal();
                    e.Handled = true;
                    return;
                }

                bool newState = !_modeService.IsDoNotDisturb;
                _modeService.IsDoNotDisturb = newState;

                if (newState)
                    _visibilityController.ForceHide();

                _isHovering = false;
                UpdateIconTextInternal();
                e.Handled = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ 勿扰切换失败: {ex.Message}");
            }
        }

        private void IconText_MouseRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var menu = new ContextMenu();

            var dnd = new MenuItem
            {
                Header = "勿扰模式",
                IsCheckable = true,
                IsChecked = _modeService.IsDoNotDisturb
            };
            dnd.Click += (_, _) => ToggleWindow();
            menu.Items.Add(dnd);

            if (_contentController?.CurrentRegionType == "Taskbar")
            {
                var refresh = new MenuItem { Header = "刷新任务栏" };
                refresh.Click += (_, _) => RefreshTaskbarView();
                menu.Items.Add(refresh);
            }

            menu.Items.Add(new Separator());

            var settings = new MenuItem { Header = "设置" };
            settings.Click += (_, _) => OpenSettings();
            menu.Items.Add(settings);

            var exit = new MenuItem { Header = "退出" };
            exit.Click += (_, _) => ExitApp();
            menu.Items.Add(exit);

            menu.PlacementTarget = IconPath;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
            e.Handled = true;
        }

        internal void OnPanelContentChanged()
        {
            _appHelperView = ContentContainer.Content as AppHelperView;
            UpdateIconTooltip();
        }

        private void UpdateIconTooltip()
        {
            if (_contentController == null) return;

            IconPath.ToolTip = _contentController.CurrentRegionType == "AppHelper"
                ? "点击切换辅助功能（媒体控制 / 画中画 / 音乐播放器）\n右键打开快捷菜单"
                : "点击切换勿扰模式\n拖拽应用到图标可固定 / 删除\n右键打开快捷菜单";
        }

        private void RefreshTaskbarView()
        {
            // 刷新任务栏视图
            if (ContentContainer.Content is TaskbarView taskbarView)
            {
                taskbarView.RefreshData();
            }
        }
    }
}
