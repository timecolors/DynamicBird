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
        private DynamicBird.UI.AI.AiChatView? _aiChatView;

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
                var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
                bool isAiPanel = _contentController?.CurrentRegionType == "AI";
                bool hasFile = files is { Length: > 0 };
                bool isImage = hasFile && IsImageFile(files![0]);

                if (isAiPanel)
                {
                    // ★ AI 面板：接受文件（图片/文本/代码/docx 上传给 AI，不支持的给提示）
                    e.Effects = DragDropEffects.Copy;
                    _currentIconState = IconState.AddMode;
                    UpdateIconTextInternal();
                }
                else
                {
                    e.Effects = DragDropEffects.Copy;
                    _currentIconState = IconState.AddMode;
                    UpdateIconTextInternal();
                }
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

        private static bool IsImageFile(string path)
        {
            string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            return ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".webp" or ".tiff" or ".tif";
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

                        // ★ AI 面板：拖入文件 = 上传给 AI（图片/文本/代码/docx，内部按类型分发）
                        if (_contentController?.CurrentRegionType == "AI" && _aiChatView != null)
                        {
                            _ = _aiChatView.SendFileAsync(path);
                            return;
                        }

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

                // ★ AI 面板：点击图标不触发勿扰模式（避免面板内容被误隐藏）
                if (_contentController?.CurrentRegionType == "AI")
                {
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
                Header = DynamicBird.UI.Localization.LocalizationManager.Instance["Dnd_Dnd"],
                IsCheckable = true,
                IsChecked = _modeService.IsDoNotDisturb
            };
            dnd.Click += (_, _) => ToggleWindow();
            menu.Items.Add(dnd);

            if (_contentController?.CurrentRegionType == "Taskbar")
            {
                var refresh = new MenuItem { Header = DynamicBird.UI.Localization.LocalizationManager.Instance["Dnd_Refresh"] };
                refresh.Click += (_, _) => RefreshTaskbarView();
                menu.Items.Add(refresh);
            }

            menu.Items.Add(new Separator());

            var settings = new MenuItem { Header = DynamicBird.UI.Localization.LocalizationManager.Instance["Tray_Settings"] };
            settings.Click += (_, _) => OpenSettings();
            menu.Items.Add(settings);

            var exit = new MenuItem { Header = DynamicBird.UI.Localization.LocalizationManager.Instance["Tray_Exit"] };
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
            _aiChatView = ContentContainer.Content as DynamicBird.UI.AI.AiChatView;
            UpdateIconTooltip();

            // ★ 小组件内容（含内部切标签）变化后重新测量并自适应面板尺寸：
            //   内容尽量显示全，剪贴板/便签已内部限高
            //   （切换动画/图标中置期间跳过 AutoSize：保持"触发的尺寸"不变，
            //     尺寸由稳定后的形变动画（目标尺寸）统一更新）
            if (_contentController.CurrentRegionType == "Widget")
            {
                // 内容变了 → 目标尺寸缓存始终失效（下次切换重新测量）
                _edgeController.InvalidateTargetSizeCache("Widget");
                // ★ 直接加载流程中（内容就位 → 尺寸由切换分支统一测量并形变）跳过
                //   原子跳变，避免"动画形变 + 原子 SetWindowPos"打架闪烁
                if (!_shapeAnimator.IsTransformAnimating && !_iconCentered &&
                    !_edgeController.IsDirectLoadInProgress)
                {
                    _sizeController.ApplySizeStrategyForWidget();
                }
            }
        }

        private void UpdateIconTooltip()
        {
            if (_contentController == null) return;

            string tooltip = _contentController.CurrentRegionType switch
            {
                "AppHelper" =>
                    DynamicBird.UI.Localization.LocalizationManager.Instance["Dnd_TipHelper"],

                "AI" =>
                    DynamicBird.UI.Localization.LocalizationManager.Instance["Dnd_TipAi"],

                "Taskbar" =>
                    DynamicBird.UI.Localization.LocalizationManager.Instance["Dnd_TipTaskbar"],

                _ =>
                    DynamicBird.UI.Localization.LocalizationManager.Instance["Dnd_TipDnd"]
            };

            IconPath.ToolTip = tooltip;
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
