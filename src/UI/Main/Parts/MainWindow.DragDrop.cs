using System;
using System.Windows;
using DynamicBird.Core.Services;
using DynamicBird.UI.Panels;

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

        private void UpdateIconTextInternal()
        {
            switch (_currentIconState)
            {
                case IconState.AddMode:
                    IconText.Text = "➕";
                    return;
                case IconState.DeleteMode:
                    IconText.Text = "🗑️";
                    return;
            }

            if (_isHovering && !_modeService.IsDoNotDisturb)
            {
                IconText.Text = "😴";
                return;
            }

            string iconPath = _settingsService.CustomIconPath;
            IconText.Text = (!string.IsNullOrEmpty(iconPath) && System.IO.File.Exists(iconPath)) ? "🖼️" : "🐦";
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
                            path = ResolveShortcutTarget(path);
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
                        else if (item.Type == TaskbarItemType.Window && !string.IsNullOrEmpty(item.Path))
                        {
                            if (_shortcutService.AddShortcut(item.Path, item.DisplayName))
                                RefreshTaskbarView();
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
                    if (link != null)
                        return link.Path;
                }
                return shortcutPath;
            }
            catch
            {
                return shortcutPath;
            }
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