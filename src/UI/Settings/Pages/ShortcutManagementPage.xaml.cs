using ShoreHue.Core.Services;
using ShoreHue.src.core.Services.Shortcuts;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ShoreHue.UI.Settings.Pages
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
            TxtStatus.Text = string.Format(ShoreHue.UI.Localization.LocalizationManager.Instance["Scm_Count"], _items.Count);
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
                    TxtStatus.Text = string.Format(ShoreHue.UI.Localization.LocalizationManager.Instance["Scm_Added"], name);
                }
                else
                {
                    TxtStatus.Text = ShoreHue.UI.Localization.LocalizationManager.Instance["Scm_AddFailed"];
                }
            }
        }

        private void BtnClearAll_Click(object sender, RoutedEventArgs e)
        {
            if (_items.Count == 0) return;
            if (MessageBox.Show(ShoreHue.UI.Localization.LocalizationManager.Instance["Scm_ClearConfirm"], ShoreHue.UI.Localization.LocalizationManager.Instance["Scm_Confirm"],
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                var ids = _items.Select(i => i.Id).ToList();
                foreach (var id in ids)
                {
                    _shortcutService.RemoveShortcut(id);
                }
                LoadShortcuts();
                TxtStatus.Text = ShoreHue.UI.Localization.LocalizationManager.Instance["Scm_Cleared"];
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
                TxtStatus.Text = string.Format(ShoreHue.UI.Localization.LocalizationManager.Instance["Scm_Deleted"], item.Name);
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