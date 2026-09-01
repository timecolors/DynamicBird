using ShoreHue.Core.Services;
using ShoreHue.src.core.Services.Shortcuts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;

namespace ShoreHue.UI.Panels
{
    public class TaskbarShortcutManager
    {
        private readonly ObservableCollection<TaskbarItem> _items = new();
        private readonly Dictionary<string, ImageSource> _iconCache = new();
        private readonly IShortcutService _shortcutService;

        public ObservableCollection<TaskbarItem> Items => _items;

        public event EventHandler? ItemsChanged;

        public TaskbarShortcutManager(IShortcutService shortcutService)
        {
            _shortcutService = shortcutService;
            _shortcutService.ShortcutsChanged += OnShortcutsChanged;
            LoadShortcuts();
        }

        private void OnShortcutsChanged(object? sender, EventArgs e)
        {
            LoadShortcuts();
            ItemsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void LoadShortcuts()
        {
            _items.Clear();
            _iconCache.Clear();

            var shortcuts = _shortcutService.Shortcuts.Where(s => s.IsVisible).OrderBy(s => s.Order);

            foreach (var data in shortcuts)
            {
                var icon = GetOrLoadIcon(data.Path);
                var item = TaskbarItem.FromShortcut(data, icon);
                _items.Add(item);
            }
        }

        private ImageSource? GetOrLoadIcon(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            if (_iconCache.TryGetValue(path, out var cached))
                return cached;

            var icon = _shortcutService.GetIcon(path);
            if (icon != null)
            {
                _iconCache[path] = icon;
            }
            return icon;
        }

        public void RefreshIcons()
        {
            _iconCache.Clear();
            foreach (var item in _items.Where(i => i.Type == TaskbarItemType.Shortcut && !string.IsNullOrEmpty(i.Path)))
            {
                item.Icon = GetOrLoadIcon(item.Path!);
            }
        }

        public void Reload()
        {
            LoadShortcuts();
            ItemsChanged?.Invoke(this, EventArgs.Empty);
        }

        public bool RemoveShortcut(string id)
        {
            return _shortcutService.RemoveShortcut(id);
        }

        public void MoveShortcut(int fromIndex, int toIndex)
        {
            var shortcutItems = _items.Where(i => i.Type == TaskbarItemType.Shortcut).ToList();
            if (fromIndex < 0 || fromIndex >= shortcutItems.Count ||
                toIndex < 0 || toIndex >= shortcutItems.Count)
                return;

            var fromItem = shortcutItems[fromIndex];
            var toItem = shortcutItems[toIndex];

            var allShortcuts = _shortcutService.Shortcuts.ToList();
            int fromOrderIndex = allShortcuts.FindIndex(s => s.Id == fromItem.Id);
            int toOrderIndex = allShortcuts.FindIndex(s => s.Id == toItem.Id);

            if (fromOrderIndex >= 0 && toOrderIndex >= 0)
            {
                _shortcutService.MoveShortcut(fromOrderIndex, toOrderIndex);
            }
        }

        /// <summary>
        /// 保存快捷方式排序
        /// </summary>
        public void SaveShortcutsOrder()
        {
            _shortcutService.SaveShortcutsOrder();
        }

        public int IndexOf(TaskbarItem item)
        {
            return _items.IndexOf(item);
        }

        public int ShortcutCount => _items.Count(i => i.Type == TaskbarItemType.Shortcut);
    }
}
