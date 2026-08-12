using DynamicBird.Animation;
using DynamicBird.Core.Detection;
using DynamicBird.Core.Services;
using DynamicBird.Core.Services.Configuration;
using System;
using System.Windows;
using System.Windows.Input;

namespace DynamicBird.Core.Controllers
{
    public class PanelVisibilityController : IPanelVisibilityController
    {
        private readonly Window _window;
        private readonly FrameworkElement _mainPanel;
        private readonly ShapeAnimator _shapeAnimator;
        private readonly MouseLeaveDetector _mouseLeaveDetector;
        private readonly PanelLockManager _lockManager;
        private readonly ISettingsService _settings;

        private double _opacity = 0.85;
        private string _currentEdge = "";

        // 延时用时间戳
        private DateTime _hideDelayStart = DateTime.MinValue;
        private int _hideDelayMs = 0;
        private bool _isInHideDelay = false;

        public event Action? PanelHidden;
        public event Action? PanelShown;

        public bool IsLocked => _lockManager.IsLocked;
        public bool IsVisible => _mainPanel.Opacity > 0.5;
        public bool IsInHideDelay => _isInHideDelay;
        public double Opacity
        {
            get => _opacity;
            set
            {
                _opacity = value;
                if (_mainPanel.Opacity > 0)
                {
                    _mainPanel.Opacity = value;
                }
            }
        }

        public PanelVisibilityController(
            Window window,
            FrameworkElement mainPanel,
            ShapeAnimator shapeAnimator,
            ISettingsService settings,
            double taskbarHeight = 40)
        {
            _window = window;
            _mainPanel = mainPanel;
            _shapeAnimator = shapeAnimator;
            _settings = settings;
            _mouseLeaveDetector = new MouseLeaveDetector(window, mainPanel, settings, taskbarHeight);
            _lockManager = new PanelLockManager();

            _lockManager.LockChanged += (locked) =>
            {
                if (locked) CancelHide();
            };

            _mainPanel.Opacity = 0;
        }

        public void SetPanelLock(bool locked) => _lockManager.SetLock(locked);

        public void Show(string edge = "")
        {
            CancelHide();
            _currentEdge = edge;

            _shapeAnimator.SetOpacityTarget(_opacity);

            PanelShown?.Invoke();
        }

        public void Show()
        {
            Show("");
        }

        public void Hide()
        {
            if (_lockManager.IsLocked) return;
            CancelHide();

            if (_mainPanel.Opacity > 0)
            {
                _shapeAnimator.SetOpacityTarget(0);
                PanelHidden?.Invoke();
            }
        }

        public void ForceHide()
        {
            CancelHide();
            if (_mainPanel.Opacity > 0)
            {
                _shapeAnimator.SetOpacityDirect(0);
                PanelHidden?.Invoke();
            }
        }

        public void HideWithDelay()
        {
            if (_lockManager.IsLocked) return;
            if (Mouse.LeftButton == MouseButtonState.Pressed) return;
            if (_mouseLeaveDetector.IsMouseNearPanel()) return;
            if (IsVisible == false) return;

            _hideDelayMs = _settings.HideDelayMs;
            _hideDelayStart = DateTime.Now;
            _isInHideDelay = true;
        }

        public void CancelHide()
        {
            _isInHideDelay = false;
            _hideDelayStart = DateTime.MinValue;
        }

        public bool CheckHideDelayTimeout()
        {
            if (!_isInHideDelay) return false;
            if (_lockManager.IsLocked) return false;
            if (Mouse.LeftButton == MouseButtonState.Pressed) return false;
            if (_mouseLeaveDetector.IsMouseNearPanel())
            {
                CancelHide();
                return false;
            }

            if ((DateTime.Now - _hideDelayStart).TotalMilliseconds >= _hideDelayMs)
            {
                Hide();
                return true;
            }
            return false;
        }

        public bool IsMouseNearPanel() => _mouseLeaveDetector.IsMouseNearPanel();

        public void UpdateEdge(string edge)
        {
            _currentEdge = edge;
        }
    }
}