using DynamicBird.Animation;
using DynamicBird.Core.Calculators;
using DynamicBird.Core.Services.Configuration;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DynamicBird.Core.Controllers
{
    public class WindowSizeController
    {
        private readonly Window _window;
        private readonly ContentControl _contentContainer;
        private readonly FrameworkElement _mainPanel;
        private readonly double _taskbarHeight;
        private readonly ISettingsService _settings;
        private readonly EdgeTriggerController _edgeController;

        private string _currentEdge = "";
        private string _currentMode = "Taskbar";
        private string _currentRegionKey = "Taskbar";
        private bool _isUserResizing = false;

        private readonly SizeCalculator _sizeCalculator;
        private readonly SizePositionCalculator _positionCalculator;
        private readonly SizeDragHandler _dragHandler;

        public event Action<bool>? UserResizeStarted;
        public event Action? SizeChanged;
        public event Action? ResizeEnded;
        public event Action<bool>? LockRequest;

        public string CurrentMode => _currentMode;

        public WindowSizeController(
            Window window,
            ContentControl contentContainer,
            FrameworkElement mainPanel,
            double taskbarHeight,
            ISettingsService settings,
            EdgeTriggerController edgeController)
        {
            _window = window;
            _contentContainer = contentContainer;
            _mainPanel = mainPanel;
            _taskbarHeight = taskbarHeight;
            _settings = settings;
            _edgeController = edgeController;

            _sizeCalculator = new SizeCalculator(_window, _contentContainer);
            _positionCalculator = new SizePositionCalculator(_window);
            _dragHandler = new SizeDragHandler(_window, _mainPanel, this, _edgeController);

            _dragHandler.UserResizeStarted += (started) =>
            {
                _isUserResizing = started;
                UserResizeStarted?.Invoke(started);
            };
            _dragHandler.ResizeEnded += () =>
            {
                _isUserResizing = false;
                ResizeEnded?.Invoke();
                SaveCurrentSize();
            };
            _dragHandler.LockRequest += (locked) => LockRequest?.Invoke(locked);
        }

        public void SetMode(string mode, string regionKey = "")
        {
            _currentMode = mode;
            if (!string.IsNullOrEmpty(regionKey))
            {
                _currentRegionKey = regionKey;
            }
            _mainPanel.Cursor = Cursors.Arrow;
        }

        public void UpdateRegion(string edge, string regionType)
        {
            _currentEdge = edge;
            _currentRegionKey = string.IsNullOrEmpty(edge) ? regionType : edge + "_" + regionType;
        }

        public void ApplySizeForCurrentMode()
        {
            if (_isUserResizing) return;

            var (userW, userH) = _settings.GetUserSize(_currentRegionKey);

            if (_currentMode == "Taskbar")
            {
                ApplyTaskbarSize();
                return;
            }

            if (_currentMode == "Placeholder")
            {
                ApplyPlaceholderSize();
                return;
            }

            if (_settings.AutoFitOnTrigger)
            {
                ApplyAutoSize();
                return;
            }

            if (userW > 0 && userH > 0)
            {
                ApplyUserSize(userW, userH);
            }
            else
            {
                ApplyAutoSize();
            }
        }

        public void ApplySizeStrategyForWidget()
        {
            ApplySizeForCurrentMode();
        }

        private void ApplyTaskbarSize()
        {
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double targetWidth = screenWidth * 2.0 / 3.0;
            double targetHeight = Math.Max(60, _taskbarHeight * 1.75);

            if (targetWidth < 200) targetWidth = 200;
            if (targetHeight < 40) targetHeight = 40;

            _window.Width = targetWidth;
            _window.Height = targetHeight;
            _window.UpdateLayout();
            SizeChanged?.Invoke();
        }

        private void ApplyPlaceholderSize()
        {
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double targetSize = screenWidth * 2.0 / 7.0;
            targetSize = Math.Max(100, Math.Min(targetSize, screenWidth * 0.4));

            _window.Width = targetSize;
            _window.Height = targetSize;
            _window.UpdateLayout();
            SizeChanged?.Invoke();
        }

        private void ApplyAutoSize()
        {
            var (contentWidth, contentHeight) = _sizeCalculator.MeasureContent();
            if (contentWidth < 10 || contentHeight < 10)
            {
                contentWidth = 280;
                contentHeight = 160;
            }

            var (targetWidth, targetHeight) = _sizeCalculator.CalculateTargetSize(
                contentWidth, contentHeight, _currentMode);

            if (targetWidth < 100) targetWidth = 100;
            if (targetHeight < 60) targetHeight = 60;

            var (newLeft, newTop) = _positionCalculator.CalculatePosition(
                targetWidth, targetHeight, _currentEdge, _window.Left, _window.Top, _window.Width, _window.Height);

            _window.Width = targetWidth;
            _window.Height = targetHeight;
            _window.Left = newLeft;
            _window.Top = newTop;
            _window.UpdateLayout();
            SizeChanged?.Invoke();
        }

        private void ApplyUserSize(double width, double height)
        {
            if (width < 100) width = 280;
            if (height < 60) height = 160;

            _window.Width = width;
            _window.Height = height;
            _window.UpdateLayout();
            SizeChanged?.Invoke();
        }

        public void ApplyMinSize()
        {
            if (_isUserResizing) return;
            if (_currentMode != "Widget") return;
            var (minWidth, minHeight) = _sizeCalculator.CalculateMinSize();
            _window.Width = minWidth;
            _window.Height = minHeight;
            _window.UpdateLayout();
            SaveCurrentSize();
            SizeChanged?.Invoke();
        }

        public void RefreshMinSizeCache()
        {
            _sizeCalculator.RefreshCache();
        }

        public void ResetForNewTrigger()
        {
            _dragHandler.Reset();
            if (_isUserResizing) return;
            if (_currentMode != "Taskbar")
                ApplySizeForCurrentMode();
        }

        public void TriggerAutoSize(string currentEdge)
        {
            if (_isUserResizing) return;
            _currentEdge = currentEdge;
            _window.Dispatcher.BeginInvoke(new Action(() =>
            {
                ApplySizeForCurrentMode();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        public void UpdateHandlePosition(string edge)
        {
            _currentEdge = edge;
            _dragHandler.UpdateHandlePosition(edge);
        }

        public void RestoreAutoSize()
        {
            if (_isUserResizing) return;
            if (_currentMode == "Taskbar")
            {
                ApplyTaskbarSize();
                return;
            }
            ApplySizeForCurrentMode();
        }

        public void SaveCurrentSize()
        {
            if (_settings.AutoFitOnTrigger) return;
            if (_currentMode == "Taskbar") return;

            _settings.SetUserSize(_currentRegionKey, _window.Width, _window.Height);
        }

        public void SaveCurrentSizeWithDelay()
        {
            if (_isUserResizing) return;
            _window.Dispatcher.BeginInvoke(new Action(() =>
            {
                SaveCurrentSize();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        public bool HandleMouseDown(object sender, MouseButtonEventArgs e)
        {
            bool handled = _dragHandler.HandleMouseDown(sender, e, _currentMode);
            if (handled)
            {
                _isUserResizing = true;
                _settings.UseAutoSize = false;
            }
            return handled;
        }

        public void HandleMouseMove(object sender, MouseEventArgs e)
        {
            _dragHandler.HandleMouseMove(sender, e, _currentMode);
        }

        public void HandleMouseUp(object sender, MouseButtonEventArgs e)
        {
            _dragHandler.HandleMouseUp(sender, e);
            _isUserResizing = false;
            _settings.UseAutoSize = false;
            SaveCurrentSizeWithDelay();
        }

        internal void OnSizeChanged() => SizeChanged?.Invoke();
        internal void OnResizeEnded() => ResizeEnded?.Invoke();
    }
}