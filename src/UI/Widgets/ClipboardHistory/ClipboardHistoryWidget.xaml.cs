using DynamicBird.Core.Services;
using DynamicBird.src.core.Services.Clipboard;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DynamicBird.UI.Widgets.ClipboardHistory
{
    public partial class ClipboardHistoryWidget : UserControl, IWidget
    {
        private readonly IClipboardService _clipboardService;
        private readonly List<ClipboardManager.ClipboardItem> _selectedItems = new();

        private Button? _btnDeleteSelected;
        private TextBlock? _statusText;

        public ClipboardHistoryWidget(IClipboardService clipboardService)
        {
            _clipboardService = clipboardService;
            InitializeComponent();
            HistoryList.ItemsSource = _clipboardService.History;
            _clipboardService.HistoryChanged += (s, e) => UpdateUI();
            UpdateUI();
        }

        public new string Name => "剪贴板历史";

        public UserControl CreateView() => this;

        public void OnActivated()
        {
            _clipboardService.StartListening();
            UpdateUI();
        }

        public void OnDeactivated()
        {
            _clipboardService.StopListening();
        }

        public FrameworkElement GetFooterControl()
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };

            _btnDeleteSelected = new Button
            {
                Content = "🗑️ 删除选中",
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
                Content = "清空全部",
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
                Text = "就绪",
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

        private void UpdateUI()
        {
            if (_btnDeleteSelected != null)
                _btnDeleteSelected.IsEnabled = _selectedItems.Count > 0;
            if (_statusText != null)
                _statusText.Text = $"{_clipboardService.History.Count} 条记录";
        }

        private void Item_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is ClipboardManager.ClipboardItem item)
            {
                if (e.OriginalSource is CheckBox || e.OriginalSource is Button)
                    return;

                _clipboardService.CopyToClipboard(item);
                if (_statusText != null)
                {
                    _statusText.Text = "✅ 已复制";
                    var timer = new System.Windows.Threading.DispatcherTimer();
                    timer.Interval = TimeSpan.FromSeconds(1.5);
                    timer.Tick += (s, args) => { timer.Stop(); UpdateUI(); };
                    timer.Start();
                }
            }
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
                _statusText.Text = $"🗑️ 已删除 {items.Count} 项";
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            if (_clipboardService.History.Count == 0) return;
            if (MessageBox.Show("清空所有剪贴板历史？", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _clipboardService.ClearAll();
                _selectedItems.Clear();
                UpdateUI();
                if (_statusText != null)
                    _statusText.Text = "🗑️ 已清空";
            }
        }
    }
}