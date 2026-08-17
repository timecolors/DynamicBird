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

        // ★ 热键钉住：Ctrl+Alt+B 呼出的面板不随鼠标位置自动隐藏，
        //   直到再次按热键、鼠标移到边缘/面板、或进入勿扰模式。
        private bool _hotkeyPinned;

        // 显示状态（目标态）：避免定时器每 30ms 重复触发动画目标导致闪烁/透明度不稳
        private bool _visible;
        private double _lastAnchorLeft = double.NaN;
        private double _lastAnchorTop = double.NaN;

        public event Action? PanelHidden;
        public event Action? PanelShown;

        public bool IsLocked => _lockManager.IsLocked;
        public bool IsVisible => _mainPanel.Opacity > 0.5;
        // 是否已请求显示（滑入/滑出动画期间也保持 true/false，用于避免定时器重复触发目标）
        public bool IsShown => _visible;
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

        /// <summary>设置“热键钉住”状态：钉住期间面板不自动隐藏，直到解除或再次手动隐藏。</summary>
        public void SetHotkeyPinned(bool pinned)
        {
            _hotkeyPinned = pinned;
            if (pinned) CancelHide();
        }

        /// <summary>是否处于热键钉住状态。</summary>
        public bool IsHotkeyPinned => _hotkeyPinned;

        public void Show(string edge = "")
        {
            CancelHide();
            _currentEdge = edge;
            EnsureWindowVisible();

            if (_visible && _mainPanel.Opacity > 0.55) return;
            _visible = true;
            _shapeAnimator.SetOpacityTarget(_opacity);

            PanelShown?.Invoke();
        }

        public void Show()
        {
            Show("");
        }

        /// <summary>
        /// 以指定锚点滑入显示：从屏幕外滑到锚点并淡入。
        /// 同一锚点重复调用是幂等的（避免重置动画速度）。
        /// </summary>
        public void ShowAt(double left, double top)
        {
            CancelHide();
            EnsureWindowVisible();

            if (_visible && Math.Abs(left - _lastAnchorLeft) < 1 && Math.Abs(top - _lastAnchorTop) < 1)
            {
                // 同一锚点：若面板正在滑出/滑入途中（位置偏离锚点），重新设置完整目标恢复显示；
                // 已在锚点则只确保透明度目标。
                double dx = _window.Left - left;
                double dy = _window.Top - top;
                if (Math.Abs(dx) > 8 || Math.Abs(dy) > 8)
                {
                    _shapeAnimator.SetShowHideTarget(left, top, _opacity, allowOffscreen: true);
                }
                else if (_mainPanel.Opacity < _opacity - 0.05)
                {
                    _shapeAnimator.SetOpacityTarget(_opacity);
                }
                return;
            }

            _visible = true;
            _lastAnchorLeft = left;
            _lastAnchorTop = top;
            _shapeAnimator.SetShowHideTarget(left, top, _opacity, allowOffscreen: true);
            PanelShown?.Invoke();
        }

        /// <summary>启动时窗口整体透明（Opacity=0），首次显示时恢复。</summary>
        private void EnsureWindowVisible()
        {
            try
            {
                if (_window.Opacity < 1.0) _window.Opacity = 1.0;
            }
            catch { }
        }

        public void Hide()
        {
            if (_lockManager.IsLocked) return;
            CancelHide();

            if (!_visible && _mainPanel.Opacity <= 0.01) return;
            _visible = false;
            _lastAnchorLeft = double.NaN;
            _lastAnchorTop = double.NaN;

            // 滑出屏幕（方向取决于当前贴边边缘）
            var (lx, ly) = GetSlideOutTarget();
            _shapeAnimator.SetShowHideTarget(lx, ly, 0, allowOffscreen: true);
            PanelHidden?.Invoke();
        }

        public void ForceHide()
        {
            _hotkeyPinned = false;
            if (_lockManager.IsLocked) return;
            if (!_visible && _mainPanel.Opacity <= 0.01) return;

            CancelHide();
            _visible = false;
            _lastAnchorLeft = double.NaN;
            _lastAnchorTop = double.NaN;

            // 内容立即透明（避免黑块残留），同时把位置物理目标设为屏幕外，
            // ShapeAnimator 会把窗口真正滑出屏幕（而不是留在原位变成黑块）
            _shapeAnimator.SetOpacityDirect(0);
            var (lx, ly) = GetSlideOutTarget();
            _shapeAnimator.SetShowHideTarget(lx, ly, 0, allowOffscreen: true);
            PanelHidden?.Invoke();
        }

        public void HideWithDelay()
        {
            if (_lockManager.IsLocked) return;
            if (_hotkeyPinned) return; // ★ 热键呼出后面板不自动隐藏
            if (Mouse.LeftButton == MouseButtonState.Pressed) return;
            if (_mouseLeaveDetector.IsMouseNearPanel()) return;
            if (IsVisible == false) return;

            _hideDelayMs = _settings.HideDelayMs;

            // ★ 延时隐藏设为 0 = 取消延时，鼠标一离开立即隐藏
            if (_hideDelayMs <= 0)
            {
                Hide();
                return;
            }

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

        private (double left, double top) GetSlideOutTarget()
        {
            double sw = SystemParameters.PrimaryScreenWidth;
            double sh = SystemParameters.PrimaryScreenHeight;
            double w = _window.Width;
            double h = _window.Height;
            const double margin = 12;

            return _currentEdge switch
            {
                "Top" => (_window.Left, -h - margin),
                "Left" => (-w - margin, _window.Top),
                "Right" => (sw + margin, _window.Top),
                _ => (_window.Left, sh + margin)
            };
        }
    }
}
