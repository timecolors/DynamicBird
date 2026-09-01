using ShoreHue.Core.Services;
using ShoreHue.src.core.Services.Clipboard;
using ShoreHue.UI.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ShoreHue.UI.Widgets.ClipboardHistory
{
    /// <summary>
    /// 剪贴板记忆库：跨重启保留 + 收藏（不被清理）+ 实时搜索 + 分类过滤（全部/收藏/文本/链接/图片/文件）。
    /// </summary>
    public partial class ClipboardHistoryWidget : UserControl, IWidget
    {
        private readonly IClipboardService _clipboardService;
        private readonly List<ClipboardManager.ClipboardItem> _selectedItems = new();

        private Button? _btnDeleteSelected;
        private TextBlock? _statusText;

        private string _filterType = "All";
        private string _searchQuery = "";

        public ClipboardHistoryWidget(IClipboardService clipboardService)
        {
            _clipboardService = clipboardService;
            InitializeComponent();
            _clipboardService.HistoryChanged += (s, e) => RefreshList();
            RefreshList();
        }

        public new string Name => LocalizationManager.Instance["WidgetTabs_Clipboard"];

        public UserControl CreateView() => this;

        public void OnActivated()
        {
            // 剪贴板监听已由主窗口应用级常驻（保证 AI 面板复制等也进入历史）
            RefreshList();
        }

        public void OnDeactivated()
        {
            // 不再停用全局监听
        }

        public FrameworkElement GetFooterControl()
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };

            _btnDeleteSelected = new Button
            {
                Content = LocalizationManager.Instance["Clip_DeleteSelected"],
                Width = 110,
                Height = 26,
                FontSize = 11,
                Background = new SolidColorBrush(Color.FromRgb(85, 51, 51)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                IsEnabled = false
            };
            _btnDeleteSelected.Click += DeleteSelected_Click;

            var btnClearAll = new Button
            {
                Content = LocalizationManager.Instance["Clip_ClearAll"],
                Width = 80,
                Height = 26,
                FontSize = 11,
                Background = new SolidColorBrush(Color.FromRgb(85, 51, 51)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(8, 0, 0, 0)
            };
            btnClearAll.Click += ClearAll_Click;

            _statusText = new TextBlock
            {
                Text = LocalizationManager.Instance["Clip_Ready"],
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0)
            };

            panel.Children.Add(_btnDeleteSelected);
            panel.Children.Add(btnClearAll);
            panel.Children.Add(_statusText);

            return panel;
        }

        // ========== 记忆库：过滤 / 排序 ==========

        /// <summary>按分类 + 搜索词过滤，收藏优先置顶，重建列表。</summary>
        private void RefreshList()
        {
            if (HistoryList == null) return;
            var q = _searchQuery?.Trim() ?? "";
            var list = _clipboardService.History
                .Where(i => MatchesType(i) && MatchesQuery(i, q))
                .OrderByDescending(i => i.IsPinned)
                .ThenByDescending(i => i.Timestamp)
                .ToList();
            HistoryList.ItemsSource = list;
            UpdateUI();
        }

        private bool MatchesType(ClipboardManager.ClipboardItem item)
        {
            switch (_filterType)
            {
                case "Pinned": return item.IsPinned;
                case "Text": return item.Type == "Text" && !IsLink(item);
                case "Link": return IsLink(item);
                case "Image": return item.Type == "Image";
                case "File": return item.Type == "File";
                default: return true; // All（Html 也归入全部）
            }
        }

        private static bool IsLink(ClipboardManager.ClipboardItem item)
        {
            if (item.Type != "Text") return false;
            var t = item.FullText ?? item.DisplayText ?? "";
            return t.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || t.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesQuery(ClipboardManager.ClipboardItem item, string q)
        {
            if (q.Length == 0) return true;
            var hay = (item.FullText ?? "") + " " + (item.DisplayText ?? "");
            return hay.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchQuery = SearchBox.Text;
            if (SearchPlaceholder != null)
                SearchPlaceholder.Visibility = string.IsNullOrEmpty(_searchQuery) ? Visibility.Visible : Visibility.Collapsed;
            RefreshList();
        }

        private void Filter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string t)
            {
                _filterType = t;
                if (FilterPanel != null)
                {
                    foreach (var child in FilterPanel.Children)
                    {
                        if (child is Button b)
                        {
                            bool active = (b.Tag as string) == t;
                            b.Background = active
                                ? new SolidColorBrush(Color.FromRgb(0, 120, 212))
                                : new SolidColorBrush(Color.FromRgb(45, 45, 45));
                            b.Foreground = active ? Brushes.White : new SolidColorBrush(Color.FromRgb(204, 204, 204));
                        }
                    }
                }
                RefreshList();
            }
        }

        private void Pin_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ClipboardManager.ClipboardItem item)
            {
                _clipboardService.SetPinned(item, !item.IsPinned);
                // SetPinned 触发 HistoryChanged → RefreshList（收藏优先置顶即时生效）
            }
        }

        // ========== 原有交互 ==========

        private void UpdateUI()
        {
            if (_btnDeleteSelected != null)
                _btnDeleteSelected.IsEnabled = _selectedItems.Count > 0;
            if (_statusText != null)
                _statusText.Text = string.Format(LocalizationManager.Instance["Clip_Count"], _clipboardService.History.Count);
        }

        private void Item_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is ClipboardManager.ClipboardItem item)
            {
                // 点击发生在复选框或按钮内部（含其子元素）时不触发复制
                if (IsInsideInteractiveControl(e.OriginalSource as DependencyObject))
                    return;

                _clipboardService.CopyToClipboard(item);
                if (_statusText != null)
                {
                    _statusText.Text = LocalizationManager.Instance["Clip_Copied"];
                    var timer = new System.Windows.Threading.DispatcherTimer();
                    timer.Interval = TimeSpan.FromSeconds(1.5);
                    timer.Tick += (s, args) => { timer.Stop(); UpdateUI(); };
                    timer.Start();
                }
            }
        }

        private static bool IsInsideInteractiveControl(DependencyObject? visual)
        {
            var dep = visual;
            while (dep != null)
            {
                if (dep is CheckBox || dep is Button) return true;
                dep = VisualTreeHelper.GetParent(dep);
            }
            return false;
        }

        private void Item_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.DataContext is ClipboardManager.ClipboardItem item)
            {
                if (!_selectedItems.Contains(item)) _selectedItems.Add(item);
                UpdateUI();
            }
        }

        private void Item_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.DataContext is ClipboardManager.ClipboardItem item)
            {
                _selectedItems.Remove(item);
                UpdateUI();
            }
        }

        private void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is ClipboardManager.ClipboardItem item)
            {
                _clipboardService.RemoveItem(item);
                _selectedItems.Remove(item);
                UpdateUI();
            }
        }

        private void DeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedItems.Count == 0) return;
            var items = _selectedItems.ToList();
            _clipboardService.RemoveItems(items);
            _selectedItems.Clear();
            UpdateUI();
            if (_statusText != null)
                _statusText.Text = string.Format(LocalizationManager.Instance["Clip_Deleted"], items.Count);
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            if (_clipboardService.History.Count == 0) return;
            if (MessageBox.Show(LocalizationManager.Instance["Clip_ClearConfirm"],
                    LocalizationManager.Instance["Clip_Confirm"], MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _clipboardService.ClearAll();
                _selectedItems.Clear();
                UpdateUI();
                if (_statusText != null)
                    _statusText.Text = LocalizationManager.Instance["Clip_Cleared"];
            }
        }
    }
}
