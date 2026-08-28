using DynamicBird.Animation;
using DynamicBird.Core.Detection;
using DynamicBird.Core.Services;
using DynamicBird.Core.Services.Configuration;
using DynamicBird.Infrastructure.Utils;
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
        // ★ 基于 _visible 标志而非 Opacity：透视模式（按住穿透键）下 MainPanel.Opacity=0.3，
        //   若用 Opacity>0.5 判定会把"显示中的面板"误判为隐藏 → ShowAt 反复触发
        //   RepositionOffscreenForSide 瞬移（快速滑动时面板横跳闪烁）。
        public bool IsVisible => _visible;

        /// <summary>当前边缘区域键（隐藏延时按区域读取）。</summary>
        public string CurrentRegionKey { get; set; } = "";
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

        /// <summary>穿透提示期间不重置窗口透明度（MainWindow 在穿透键按下时设置）。</summary>
        public bool SuppressOpacityReset { get; set; }

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
        public void ShowAt(double left, double top, string edge = "")
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
                else if (_mainPanel.Opacity < _opacity - 0.05 && !SuppressOpacityReset)
                {
                    _shapeAnimator.SetOpacityTarget(_opacity);
                }
                return;
            }

            _visible = true;
            _lastAnchorLeft = left;
            _lastAnchorTop = top;
            // ★ 跨边修复：面板若不在目标边附近（如上次在上边外隐藏），
            //   先瞬移到目标边对应的屏幕外位置，再从该边滑入，避免"从屏幕中间飞过来"。
            //   滑入方向按"触发边"判定（左边缘→左侧，底部→下方……）。
            RepositionOffscreenForSide(left, top, edge);
            // ★ 呼出动画：用"触发动画"类型与参数
            _shapeAnimator.SetShowHideTarget(left, top, _opacity, allowOffscreen: true,
                _settings.ShowAnimationType, _settings.ShowAnimationDurationMs,
                _settings.ShowAnimationZoomFrom, _settings.ShowAnimationOscillations, _settings.ShowAnimationSpringiness);
            PanelShown?.Invoke();
        }

        /// <summary>
        /// 显示前把面板预置到目标贴边方向的屏幕外，保证从正确的边滑入。
        /// 隐藏时面板会滑到某边的屏幕外；若下次显示的是另一边（跨边），
        /// 直接从旧位置做动画会"飞"过整个屏幕。此方法检测跨边并先瞬移。
        /// 同边（当前位置已在该边屏幕外附近）则保持原有滑入动画。
        /// </summary>
        private void RepositionOffscreenForSide(double targetLeft, double targetTop, string edge = "")
        {
            try
            {
                var wa = ScreenMetrics.GetCachedScreenForWindow(
                    _window.Left, _window.Top, _window.Width, _window.Height);
                double sw = wa.Width;
                double sh = wa.Height;
                double w = _window.Width;
                double h = _window.Height;

                double preLeft = _window.Left, preTop = _window.Top;
                if (!string.IsNullOrEmpty(edge))
                {
                    // ★ 按触发边判定滑入方向（底部左侧面板从下方滑入、左边缘从左侧滑入……）
                    switch (edge)
                    {
                        case "Left": preLeft = -w; preTop = targetTop; break;
                        case "Right": preLeft = sw; preTop = targetTop; break;
                        case "Top": preLeft = targetLeft; preTop = -h; break;
                        case "Bottom": preLeft = targetLeft; preTop = sh; break;
                        default: return; // 无明确边缘：不预置
                    }
                }
                else
                {
                    // 无触发边信息：按目标位置推断（垂直边优先）
                    bool atLeft = targetLeft <= 0;
                    bool atRight = targetLeft >= sw - w - 1;
                    bool atTop = !atLeft && !atRight && targetTop <= 0;
                    bool atBottom = !atLeft && !atRight && targetTop >= sh - h - 1;
                    if (!atTop && !atBottom && !atLeft && !atRight) return; // 非贴边，不预置
                    if (atLeft) { preLeft = -w; preTop = targetTop; }
                    else if (atRight) { preLeft = sw; preTop = targetTop; }
                    else if (atTop) { preLeft = targetLeft; preTop = -h; }
                    else if (atBottom) { preLeft = targetLeft; preTop = sh; }
                }

                // 已在目标边屏幕外附近：不瞬移，保持原有滑入动画
                if (Math.Abs(_window.Left - preLeft) < w * 0.5 &&
                    Math.Abs(_window.Top - preTop) < h * 0.5)
                {
                    return;
                }

                // 跨边：先原子瞬移到目标边的屏幕外，再滑入
                _shapeAnimator.JumpTo(preLeft, preTop, w, h);
            }
            catch { }
        }

        /// <summary>启动时窗口整体透明（Opacity=0），首次显示时恢复。</summary>
        private void EnsureWindowVisible()
        {
            try
            {
                // ★ 穿透提示期间（SuppressOpacityReset=true）不重置窗口透明度（穿透时窗口半透明提示）
            if (_window.Opacity < 1.0 && !SuppressOpacityReset) _window.Opacity = 1.0;
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

            // ★ 隐藏：设置动画滑出（用"隐藏动画"类型与参数）+ 渲染循环兜底——
            //   动画正常则由设置参数驱动（顺滑滑出）；动画被其他逻辑打断时，
            //   渲染循环（cling）接管把位置推到屏幕外（到位自动停）→ 永不卡半路（无黑框）。
            var (lx, ly) = GetSlideOutTarget();
            _shapeAnimator.SetShowHideTarget(lx, ly, 0, allowOffscreen: true,
                _settings.HideAnimationType, _settings.HideAnimationDurationMs,
                _settings.HideAnimationZoomTo, _settings.HideAnimationOscillations, _settings.HideAnimationSpringiness);
            // ★ 不再加 cling 兜底：渲染循环每帧写本地值会干扰 Window.Left 动画时钟
            //   （800ms 动画被打成瞬间到位）。动画本身由 Animate 完成回调锁定终值，
            //   打断源（StartFollowPosition 等）已修复 → 滑出动画自然完成、无黑框。
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
            _shapeAnimator.SetShowHideTarget(lx, ly, 0, allowOffscreen: true,
                _settings.HideAnimationType, _settings.HideAnimationDurationMs,
                _settings.HideAnimationZoomTo, _settings.HideAnimationOscillations, _settings.HideAnimationSpringiness);
            // ★ 不再加 cling 兜底：渲染循环每帧写本地值会干扰 Window.Left 动画时钟
            //   （800ms 动画被打成瞬间到位）。动画本身由 Animate 完成回调锁定终值，
            //   打断源（StartFollowPosition 等）已修复 → 滑出动画自然完成、无黑框。
            PanelHidden?.Invoke();
        }

        public void HideWithDelay()
        {
            if (_lockManager.IsLocked) return;
            if (_hotkeyPinned) return; // ★ 热键呼出后面板不自动隐藏
            if (Mouse.LeftButton == MouseButtonState.Pressed) return;
            if (_mouseLeaveDetector.IsMouseNearPanel()) return;
            if (IsVisible == false) return;
            // ★ 已在延时中：不重置计时（tick 每 30ms 重复调用本方法，
            //   若每次重置 _hideDelayStart，200ms 延时永远到不了 → 面板永不隐藏）
            if (_isInHideDelay) return;

            _hideDelayMs = _settings.GetHideDelay(CurrentRegionKey);

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
            var wa = ScreenMetrics.GetCachedScreenForWindow(
                _window.Left, _window.Top, _window.Width, _window.Height);
            double sw = wa.Width;
            double sh = wa.Height;
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