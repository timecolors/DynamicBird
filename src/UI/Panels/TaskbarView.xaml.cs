using DynamicBird.Core.Services;
using DynamicBird.Core.Services.Configuration;
using DynamicBird.src.core.Services.Shortcuts;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace DynamicBird.UI.Panels
{
    public partial class TaskbarView : UserControl
    {
        private readonly TaskbarShortcutManager _shortcutManager;
        private readonly ISettingsService _settings;
        private DispatcherTimer? _refreshTimer;
        private DateTime _lastRefresh = DateTime.MinValue;

        // 数据集合
        private readonly ObservableCollection<TaskbarItem> _shortcuts = new();
        private readonly ObservableCollection<TaskbarItem> _windows = new();

        public ObservableCollection<TaskbarItem> Shortcuts => _shortcuts;
        public ObservableCollection<TaskbarItem> Windows => _windows;

        // 布局状态
        internal int _shortcutRows = 1;
        internal int _windowRows = 1;
        internal int _totalRows = 1;
        internal bool _isSingleRowLayout = true;

        // 分隔线相关
        internal Border? _dividerElement = null;
        internal FrameworkElement? _dividerContainer = null;
        internal bool _isDividerDragging = false;
        internal double _dividerStartPos;
        internal double _dividerStartSize;
        internal double _dividerTotalSize;

        // 滚动查看器
        private ScrollViewer? _shortcutScrollViewer;
        private ScrollViewer? _windowScrollViewer;

        // 滚动处理器
        private TaskbarScrollHandler? _shortcutScrollHandler;
        private TaskbarScrollHandler? _windowScrollHandler;

        public double IconSize
        {
            get => (double)GetValue(IconSizeProperty);
            set => SetValue(IconSizeProperty, value);
        }

        public static readonly DependencyProperty IconSizeProperty =
            DependencyProperty.Register(nameof(IconSize), typeof(double), typeof(TaskbarView),
                new PropertyMetadata(28.0, OnIconSizeChanged));

        private static void OnIconSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TaskbarView view) view.UpdateLayout();
        }

        public TaskbarView(IShortcutService shortcutService, ISettingsService settings)
        {
            _settings = settings;
            InitializeComponent();
            DataContext = this;

            VerticalAlignment = VerticalAlignment.Stretch;
            HorizontalAlignment = HorizontalAlignment.Stretch;

            MinHeight = 36;

            IconSize = _settings.TaskbarIconSize;
            _shortcutManager = new TaskbarShortcutManager(shortcutService);
            _shortcutManager.ItemsChanged += OnItemsChanged;

            LoadItems();
            UpdateLayout();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            SizeChanged += OnSizeChanged;
        }

        public void RefreshData() => LoadItems();

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            InitializeDragDropEvents();

            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(1);
            _refreshTimer.Tick += (s, args) => RefreshWindows();
            _refreshTimer.Start();
            UpdateLayout();

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_shortcutScrollViewer != null && _shortcutScrollHandler == null)
                {
                    _shortcutScrollHandler = new TaskbarScrollHandler(_shortcutScrollViewer, "快捷方式");
                }
                if (_windowScrollViewer != null && _windowScrollHandler == null)
                {
                    _windowScrollHandler = new TaskbarScrollHandler(_windowScrollViewer, "任务标签");
                }
            }), DispatcherPriority.Loaded);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _refreshTimer?.Stop();
            _refreshTimer = null;

            _shortcutScrollHandler?.Detach();
            _shortcutScrollHandler = null;
            _windowScrollHandler?.Detach();
            _windowScrollHandler = null;
        }

        private void OnItemsChanged(object? sender, EventArgs e) => LoadItems();

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateLayout();
        }
    }
}