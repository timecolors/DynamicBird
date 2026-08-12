using DynamicBird.Core.Services;
using DynamicBird.src.core.Services.Shortcuts;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DynamicBird.UI.Settings.Pages
{
    public partial class ShortcutManagementPage : Page
    {
        private readonly IShortcutService _shortcutService;
        private readonly ObservableCollection<ShortcutItem> _items = new();

        public ShortcutManagementPage(IShortcutService shortcutService)
        {
            _shortcutService = shortcutService;
            InitializeComponent();
            LoadShortcuts();
            ShortcutList.ItemsSource = _items;
        }

        private void LoadShortcuts()
        {
            _items.Clear();
            var shortcuts = _shortcutService.Shortcuts.OrderBy(s => s.Order);
            foreach (var data in shortcuts)
            {
                var icon = _shortcutService.GetIcon(data.Path);
                _items.Add(new ShortcutItem
                {
                    Id = data.Id,
                    Name = data.Name,
                    Path = data.Path,
                    Icon = icon,
                    Order = data.Order
                });
            }
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            TxtStatus.Text = $"{_items.Count} 个快捷方式";
        }

        private void BtnAddShortcut_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "应用程序|*.exe|所有文件|*.*",
                Title = "选择要添加的应用"
            };

            if (dialog.ShowDialog() == true)
            {
                string path = dialog.FileName;
                string name = System.IO.Path.GetFileNameWithoutExtension(path);

                if (_shortcutService.AddShortcut(path, name))
                {
                    LoadShortcuts();
                    TxtStatus.Text = $"✅ 已添加: {name}";
                }
                else
                {
                    TxtStatus.Text = "⚠️ 已存在或添加失败";
                }
            }
        }

        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            if (_items.Count == 0) return;
            if (MessageBox.Show("确定要清空所有快捷方式吗？", "确认",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                var ids = _items.Select(i => i.Id).ToList();
                foreach (var id in ids)
                {
                    _shortcutService.RemoveShortcut(id);
                }
                LoadShortcuts();
                TxtStatus.Text = "🗑️ 已清空";
            }
        }

        private void BtnMoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ShortcutItem item)
            {
                int index = _items.IndexOf(item);
                if (index > 0)
                {
                    _shortcutService.MoveShortcut(index, index - 1);
                    LoadShortcuts();
                }
            }
        }

        private void BtnMoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ShortcutItem item)
            {
                int index = _items.IndexOf(item);
                if (index < _items.Count - 1)
                {
                    _shortcutService.MoveShortcut(index, index + 1);
                    LoadShortcuts();
                }
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ShortcutItem item)
            {
                _shortcutService.RemoveShortcut(item.Id);
                LoadShortcuts();
                TxtStatus.Text = $"🗑️ 已删除: {item.Name}";
            }
        }
    }

    public class ShortcutItem
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public ImageSource? Icon { get; set; }
        public int Order { get; set; }
    }
}