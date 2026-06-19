using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LingDongBird.ViewModels;
using LingDongBird.Core;

namespace LingDongBird.Features.WindowSwitcher
{
    public partial class WindowSwitcherView : UserControl
    {
        private readonly WindowSwitcherViewModel _viewModel;
        private WindowListProvider.WindowItem? _draggedItem;
        private int _dragStartIndex = -1;
        private bool _isDragging;
        private bool _wasDragging;
        private Point _mouseDownPoint;
        private Border? _currentBorder;
        private WrapPanel? _wrapPanel;
        private double _lastWidth = 0;

        public WindowSwitcherView()
        {
            InitializeComponent();
            _viewModel = new WindowSwitcherViewModel();
            WindowList.ItemsSource = _viewModel.Windows;
            Unloaded += (s, e) => _viewModel.Stop();

            this.Loaded += (s, e) =>
            {
                // 获取 WrapPanel 引用
                _wrapPanel = FindWrapPanel(WindowList);
                UpdateLayoutOrientation();
            };
            this.SizeChanged += (s, e) => UpdateLayoutOrientation();

            this.DataContext = this;
        }

        public double TagWidth => SettingsManager.TagWidth;

        private void UpdateLayoutOrientation()
        {
            try
            {
                if (_wrapPanel == null) return;

                double panelWidth = this.ActualWidth;
                double screenWidth = SystemParameters.PrimaryScreenWidth;
                double threshold = SettingsManager.HorizontalLayoutThreshold;

                bool shouldBeHorizontal = panelWidth > screenWidth * threshold;
                var newOrientation = shouldBeHorizontal ? Orientation.Horizontal : Orientation.Vertical;

                if (_wrapPanel.Orientation != newOrientation)
                {
                    _wrapPanel.Orientation = newOrientation;

                    // 调整滚动条
                    MainScrollViewer.VerticalScrollBarVisibility = shouldBeHorizontal ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
                    MainScrollViewer.HorizontalScrollBarVisibility = shouldBeHorizontal ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
                }
            }
            catch { }
        }

        private WrapPanel? FindWrapPanel(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is WrapPanel wp) return wp;
                var result = FindWrapPanel(child);
                if (result != null) return result;
            }
            return null;
        }

        // ---------- 点击切换 ----------
        private void OnItemClick(object sender, MouseButtonEventArgs e)
        {
            if (_wasDragging)
            {
                _wasDragging = false;
                return;
            }

            if (sender is Border border && border.DataContext is WindowListProvider.WindowItem item)
            {
                WindowAction.ToggleMinimize(item.Handle);
            }
        }

        // ---------- 关闭 ----------
        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is WindowListProvider.WindowItem item)
                _viewModel.Close(item.Handle);
        }

        // ---------- 拖拽事件 ----------
        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;

            _currentBorder = sender as Border;
            if (_currentBorder == null) return;

            _draggedItem = _currentBorder.DataContext as WindowListProvider.WindowItem;
            if (_draggedItem == null) return;

            _dragStartIndex = _viewModel.Windows.IndexOf(_draggedItem);
            _isDragging = false;
            _wasDragging = false;
            _mouseDownPoint = e.GetPosition(null);

            if (_dragStartIndex >= 0)
            {
                (Application.Current.MainWindow as MainWindow)?.SetPanelLock(true);
                _currentBorder.CaptureMouse();
            }
        }

        private void Border_MouseMove(object sender, MouseEventArgs e)
        {
            if (_draggedItem == null || e.LeftButton != MouseButtonState.Pressed)
                return;

            var currentPos = e.GetPosition(null);
            if (Math.Abs(currentPos.X - _mouseDownPoint.X) < 5 &&
                Math.Abs(currentPos.Y - _mouseDownPoint.Y) < 5)
                return;

            _isDragging = true;
            _wasDragging = true;
            _mouseDownPoint = currentPos;

            var listBox = WindowList;
            var hitTestResult = VisualTreeHelper.HitTest(listBox, e.GetPosition(listBox));
            if (hitTestResult == null) return;

            var dependencyObject = hitTestResult.VisualHit;
            while (dependencyObject != null && !(dependencyObject is Border))
            {
                dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
            }

            if (dependencyObject is Border targetBorder && targetBorder != _currentBorder)
            {
                var targetData = targetBorder.DataContext as WindowListProvider.WindowItem;
                if (targetData != null && targetData != _draggedItem)
                {
                    int targetIndex = _viewModel.Windows.IndexOf(targetData);
                    if (targetIndex >= 0 && targetIndex != _dragStartIndex)
                    {
                        _viewModel.Windows.Move(_dragStartIndex, targetIndex);
                        _dragStartIndex = targetIndex;
                    }
                }
            }

            if (_isDragging)
            {
                this.Cursor = Cursors.SizeAll;
            }
        }

        private void Border_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_currentBorder != null)
            {
                _currentBorder.ReleaseMouseCapture();
            }

            if (_wasDragging)
            {
                e.Handled = true;
            }

            _draggedItem = null;
            _isDragging = false;
            _dragStartIndex = -1;
            _currentBorder = null;
            this.Cursor = Cursors.Arrow;
            (Application.Current.MainWindow as MainWindow)?.SetPanelLock(false);
        }
    }
}