using DynamicBird.Animation;
using DynamicBird.Core.Detection;
using DynamicBird.Core.Services.Configuration;
using System;
using System.Windows;

namespace DynamicBird.Core.Controllers
{
    public class EdgeTriggerController : IEdgeTriggerController
    {
        private readonly Window _window;
        private readonly ShapeAnimator _shapeAnimator;
        private readonly WindowSizeController _sizeController;
        private readonly PanelVisibilityController _visibilityController;
        private readonly ISettingsService _settings;

        // ★★★ 底部边界（任务栏顶部） ★★★
        private readonly double _bottomBoundary;

        // ========== 状态追踪 ==========
        private string _currentEdge = "";
        private string _lastRegionType = "";
        private double _lastLeft = -1;
        private double _lastTop = -1;
        private EdgeRegion _lastProcessedRegion = EdgeRegion.Unknown;
        private string _currentRegionKey = "";

        // ========== 拖拽状态 ==========
        private bool _isDragging = false;
        public bool IsDragging
        {
            get => _isDragging;
            set => _isDragging = value;
        }

        // ========== 尺寸缓存 ==========
        private double _cachedWidth = 0;
        private double _cachedHeight = 0;

        // ========== 防抖 ==========
        private DateTime _lastRegionChangeTime = DateTime.MinValue;
        private EdgeRegion _lastDebounceRegion = EdgeRegion.Unknown;

        // ========== 飞行 ==========
        private bool _isFlying = false;
        private bool _flyCompletedTriggered = false;
        private string _flyingTargetRegionType = "";
        private string _flyingTargetRegionKey = "";

        // ========== 小鸟依人 ==========
        private bool _isClinging = false;
        private DateTime _clingStartTime = DateTime.MinValue;

        // ========== IEdgeTriggerController 接口实现 ==========
        public EdgeRegion CurrentRegion => _lastProcessedRegion;
        public string CurrentEdge => _currentEdge;
        public bool IsFlying => _isFlying;
        public bool IsClinging => _isClinging;
        public bool IsSticking => false;
        public bool IsPanelVisible { get; set; }

        public event Action<EdgeRegion, string>? ModeSwitchRequested;
        public event Action<double, double, bool>? PositionUpdateRequested;
        public event Action<double, double>? JumpToPositionRequested;
        public event Action<string>? ShowPanelRequested;
        public event Action? HidePanelRequested;
        public event Action? StartHideDelayRequested;
        public event Action? CancelHideDelayRequested;
        public event Action? StartClingingRequested;
        public event Action? StopClingingRequested;
        public event Action? StickToMouseRequested;
        public event Action? FlyCompleted;
        public event Action<EdgeRegion, double, double>? FlyRequested;
        public event Action<string>? RegionChanged;

        public EdgeTriggerController(
            Window window,
            ShapeAnimator shapeAnimator,
            WindowSizeController sizeController,
            PanelVisibilityController visibilityController,
            ISettingsService settings,
            double bottomBoundary)
        {
            _window = window;
            _shapeAnimator = shapeAnimator;
            _sizeController = sizeController;
            _visibilityController = visibilityController;
            _settings = settings;
            _bottomBoundary = bottomBoundary;

            _shapeAnimator.FlyCompleted += OnFlyCompleted;

            _cachedWidth = _window.Width;
            _cachedHeight = _window.Height;
        }

        // ========================================
        //  IEdgeTriggerController 接口方法
        // ========================================

        public void OnMouseMove(EdgeRegion region, double mouseX, double mouseY, bool isInsidePanel)
        {
        }

        public void Reset()
        {
            ClearEdge();
            _isFlying = false;
            _isClinging = false;
        }

        public bool ShouldPreventAutoHide()
        {
            return _isClinging || _isFlying || _isDragging;
        }

        public void SetClingModeEnabled(bool enabled) { }

        public void OnFlyCompleted()
        {
            if (!_isFlying || _flyCompletedTriggered) return;

            _flyCompletedTriggered = true;
            _isFlying = false;

            if (!string.IsNullOrEmpty(_flyingTargetRegionType))
            {
                _lastRegionType = _flyingTargetRegionType;
                _currentRegionKey = _flyingTargetRegionKey;
                _sizeController.SetMode(_flyingTargetRegionType, _flyingTargetRegionKey);
                RegionChanged?.Invoke(_flyingTargetRegionType);

                var (w, h) = GetTargetSizeForRegion(_flyingTargetRegionType, _flyingTargetRegionKey);
                _cachedWidth = w;
                _cachedHeight = h;

                double screenW = SystemParameters.PrimaryScreenWidth;
                double targetLeft = Math.Max(0, Math.Min(_shapeAnimator.CurrentLeft, screenW - w));
                double targetTop = _bottomBoundary - h;

                // ★ 飞行完成：尺寸直接跳转（位置已由飞行系统到达）
                _shapeAnimator.SetSizeDirect(w, h);
            }

            _flyingTargetRegionType = "";
            _flyingTargetRegionKey = "";
            FlyCompleted?.Invoke();
        }

        public void OnStickToMouseSuccess() { }

        // ========================================
        //  核心业务方法
        // ========================================

        public void ProcessRegion(EdgeRegion region, double mouseX, double mouseY, double screenWidth, double screenHeight)
        {
            if (region == EdgeRegion.Unknown || _isFlying || _isDragging) return;

            if (_isClinging)
            {
                StopClinging();
            }

            bool isCorner = region == EdgeRegion.TopLeft || region == EdgeRegion.TopRight ||
                            region == EdgeRegion.BottomLeft || region == EdgeRegion.BottomRight;

            if (isCorner)
            {
                ProcessCorner(region, screenWidth, screenHeight);
                return;
            }

            string currentEdgeName = GetEdgeName(region);
            _currentEdge = currentEdgeName;
            _sizeController.UpdateHandlePosition(_currentEdge);

            // 防抖
            if (_lastDebounceRegion != EdgeRegion.Unknown && _lastDebounceRegion == region)
            {
                int debounceMs = _settings.RegionDebounceMs;
                if ((DateTime.Now - _lastRegionChangeTime).TotalMilliseconds < debounceMs)
                {
                    return;
                }
            }
            else
            {
                _lastDebounceRegion = region;
                _lastRegionChangeTime = DateTime.Now;
            }

            string regionType = GetRegionTypeFromEnum(region);
            string regionKey = GetRegionKey(region);

            // 跨边飞行检测
            bool isCrossEdge = _lastProcessedRegion != EdgeRegion.Unknown &&
                               GetEdgeName(_lastProcessedRegion) != GetEdgeName(region) &&
                               _visibilityController.IsVisible;

            if (isCrossEdge)
            {
                StartFlying(region, mouseX, mouseY, screenWidth, screenHeight, regionType, regionKey);
                _lastProcessedRegion = region;
                _lastRegionChangeTime = DateTime.Now;
                return;
            }

            // 判断是否切换区域
            bool regionChanged = (regionType != _lastRegionType);

            // 计算目标尺寸
            double targetW, targetH;
            if (regionChanged)
            {
                (targetW, targetH) = GetTargetSizeForRegion(regionType, regionKey);
            }
            else
            {
                targetW = _cachedWidth > 0 ? _cachedWidth : _window.Width;
                targetH = _cachedHeight > 0 ? _cachedHeight : _window.Height;
            }

            // 计算目标位置（使用目标尺寸）
            var (targetLeft, targetTop) = CalculatePosition(region, mouseX, mouseY, screenWidth, screenHeight, targetW, targetH);

            if (regionChanged)
            {
                _lastRegionType = regionType;
                _currentRegionKey = regionKey;
                _sizeController.SetMode(regionType, regionKey);
                RegionChanged?.Invoke(regionType);

                _cachedWidth = targetW;
                _cachedHeight = targetH;

                // ★★★ 四边统一：位置和尺寸同时瞬间跳转（贴边侧永远固定） ★★★
                _shapeAnimator.JumpTo(targetLeft, targetTop, targetW, targetH);

                ApplyRegionChange(regionType, regionKey, _visibilityController.IsVisible);
            }
            else
            {
                // ★★★ 同区域：只更新位置（物理跟随，尺寸不变） ★★★
                _shapeAnimator.SetPositionTargetWithoutReset(targetLeft, targetTop);
            }

            _lastProcessedRegion = region;
            _lastRegionChangeTime = DateTime.Now;
        }

        public void ClearEdge()
        {
            _currentEdge = "";
            _lastRegionType = "";
            _lastLeft = -1;
            _lastTop = -1;
            _lastProcessedRegion = EdgeRegion.Unknown;
            _lastDebounceRegion = EdgeRegion.Unknown;
            _currentRegionKey = "";
            _isClinging = false;
        }

        public void ApplySizeStrategy()
        {
            _sizeController.ApplySizeForCurrentMode();
        }

        // ========================================
        //  小鸟依人
        // ========================================

        public void StartClinging(double mouseX, double mouseY)
        {
            if (!_settings.ClingModeEnabled) return;
            if (_isClinging) return;
            if (_isFlying) return;
            if (!_visibilityController.IsVisible) return;

            _isClinging = true;
            _clingStartTime = DateTime.Now;

            double halfW = _window.Width / 2;
            double halfH = _window.Height / 2;
            _shapeAnimator.SetPositionTargetWithoutReset(mouseX - halfW, mouseY - halfH);
            StartClingingRequested?.Invoke();
        }

        public void UpdateClinging(double mouseX, double mouseY)
        {
            if (!_isClinging) return;
            if (_isFlying) return;

            // ★ 只检查追上，超时由 PanelVisibilityController 的延时计时器统一处理
            double halfW = _window.Width / 2;
            double halfH = _window.Height / 2;
            double cx = _window.Left + halfW;
            double cy = _window.Top + halfH;

            double dx = mouseX - cx;
            double dy = mouseY - cy;

            if (Math.Abs(dx) < halfW && Math.Abs(dy) < halfH)
            {
                // 追上了 → 取消延时，面板停住
                _visibilityController.CancelHide();
                StopClinging();
                _visibilityController.Show();
                StickToMouseRequested?.Invoke();
                OnStickToMouseSuccess();
                return;
            }

            _shapeAnimator.SetPositionTargetWithoutReset(mouseX - halfW, mouseY - halfH);
        }

        private void StopClinging()
        {
            _isClinging = false;
            _clingStartTime = DateTime.MinValue;
            StopClingingRequested?.Invoke();
        }

        public bool IsInClinging() => _isClinging;

        // ========================================
        //  飞行
        // ========================================

        private void StartFlying(EdgeRegion target, double mx, double my, double sw, double sh, string type, string key)
        {
            _isFlying = true;
            _flyCompletedTriggered = false;

            double w = _window.Width;
            double h = _window.Height;
            var (left, top) = CalculatePosition(target, mx, my, sw, sh, w, h);

            int duration = _settings.FlyDurationMs;

            _shapeAnimator.SetFlyParameters(duration);
            _shapeAnimator.SetSizeDirect(w, h);
            _shapeAnimator.StartFly(left, top);

            _flyingTargetRegionType = type;
            _flyingTargetRegionKey = key;
            _lastProcessedRegion = target;
        }

        // ========================================
        //  内部辅助方法
        // ========================================

        private void ProcessCorner(EdgeRegion region, double sw, double sh)
        {
            string type = "Placeholder";
            string key = region.ToString();

            ApplyRegionChange(type, key, _visibilityController.IsVisible);

            double size = Math.Max(100, Math.Min(sw * 2.0 / 7.0, sw * 0.4));
            _cachedWidth = size;
            _cachedHeight = size;

            double left = 0, top = 0;
            switch (region)
            {
                case EdgeRegion.TopLeft: break;
                case EdgeRegion.TopRight: left = sw - size; break;
                case EdgeRegion.BottomLeft: top = _bottomBoundary - size; break;
                case EdgeRegion.BottomRight: left = sw - size; top = _bottomBoundary - size; break;
                default: return;
            }

            _lastLeft = left;
            _lastTop = top;

            _shapeAnimator.SetSizeDirect(size, size);
            _shapeAnimator.SetPositionTargetWithoutReset(left, top);
            _lastProcessedRegion = region;
        }

        private void ProcessCornerInternal(EdgeRegion region, double screenWidth, double screenHeight)
        {
            string regionType = "Placeholder";
            string regionKey = region.ToString();

            ApplyRegionChange(regionType, regionKey, _visibilityController.IsVisible);

            double width = _window.Width;
            double height = _window.Height;
            double left = 0, top = 0;

            switch (region)
            {
                case EdgeRegion.TopLeft: left = 0; top = 0; break;
                case EdgeRegion.TopRight: left = screenWidth - width; top = 0; break;
                case EdgeRegion.BottomLeft: left = 0; top = _bottomBoundary - height; break;
                case EdgeRegion.BottomRight: left = screenWidth - width; top = _bottomBoundary - height; break;
                default: return;
            }

            _window.BeginAnimation(Window.LeftProperty, null);
            _window.BeginAnimation(Window.TopProperty, null);
            _window.Left = left;
            _window.Top = top;
            _window.UpdateLayout();

            _lastProcessedRegion = region;
        }

        private void ApplyRegionChange(string regionType, string regionKey, bool isVisible)
        {
            if (regionType != _lastRegionType)
            {
                _lastRegionType = regionType;
                _currentRegionKey = regionKey;
                _sizeController.SetMode(regionType, regionKey);
                RegionChanged?.Invoke(regionType);

                // ★★★ 尺寸已经由 JumpTo 设置，不需要再次应用 ★★★
                // 删除 _sizeController.ApplySizeForCurrentMode()

                if (!isVisible)
                {
                    _visibilityController.Show();
                }
            }
        }

        private (double w, double h) GetTargetSizeForRegion(string type, string key)
        {
            double sw = SystemParameters.PrimaryScreenWidth;

            if (type == "Taskbar")
                return (sw * 2.0 / 3.0, Math.Max(60, 40 * 1.75));

            if (type == "Placeholder")
            {
                double s = sw * 2.0 / 7.0;
                return (Math.Max(100, Math.Min(s, sw * 0.4)), Math.Max(100, Math.Min(s, sw * 0.4)));
            }

            var (userW, userH) = _settings.GetUserSize(key);
            if (!_settings.AutoFitOnTrigger && userW > 0 && userH > 0)
                return (userW, userH);

            return (type == "Widget" ? 340 : 400, type == "Widget" ? 220 : 300);
        }

        private (double left, double top) CalculatePosition(EdgeRegion region, double mx, double my,
            double sw, double sh, double w, double h)
        {
            string edge = GetEdgeName(region);
            double left = 0, top = 0;

            switch (edge)
            {
                case "Top":
                    left = mx - w / 2;
                    top = 0;
                    break;
                case "Bottom":
                    left = mx - w / 2;
                    top = _bottomBoundary - h;
                    break;
                case "Left":
                    left = 0;
                    top = my - h / 2;
                    break;
                case "Right":
                    left = sw - w;
                    top = my - h / 2;
                    break;
                default:
                    return (0, 0);
            }

            left = Math.Max(0, Math.Min(left, sw - w));
            top = Math.Max(0, Math.Min(top, sh - h));
            return (left, top);
        }

        private string GetEdgeName(EdgeRegion r) => r switch
        {
            EdgeRegion.Top_Left or EdgeRegion.Top_Center or EdgeRegion.Top_Right => "Top",
            EdgeRegion.Bottom_Left or EdgeRegion.Bottom_Center or EdgeRegion.Bottom_Right => "Bottom",
            EdgeRegion.Left_Top or EdgeRegion.Left_Center or EdgeRegion.Left_Bottom => "Left",
            EdgeRegion.Right_Top or EdgeRegion.Right_Center or EdgeRegion.Right_Bottom => "Right",
            _ => ""
        };

        private string GetRegionKey(EdgeRegion r)
        {
            string edge = GetEdgeName(r);
            if (string.IsNullOrEmpty(edge)) return r.ToString();

            string sub = r switch
            {
                EdgeRegion.Top_Left or EdgeRegion.Bottom_Left => "Left",
                EdgeRegion.Top_Center or EdgeRegion.Bottom_Center or EdgeRegion.Left_Center or EdgeRegion.Right_Center => "Center",
                EdgeRegion.Top_Right or EdgeRegion.Bottom_Right => "Right",
                EdgeRegion.Left_Top or EdgeRegion.Right_Top => "Top",
                EdgeRegion.Left_Bottom or EdgeRegion.Right_Bottom => "Bottom",
                _ => r.ToString()
            };
            return edge + "_" + sub;
        }

        private string GetRegionTypeFromEnum(EdgeRegion r)
        {
            bool isHorizontal = r == EdgeRegion.Top_Left || r == EdgeRegion.Top_Center || r == EdgeRegion.Top_Right ||
                                 r == EdgeRegion.Bottom_Left || r == EdgeRegion.Bottom_Center || r == EdgeRegion.Bottom_Right;
            bool isCenter = r == EdgeRegion.Top_Center || r == EdgeRegion.Bottom_Center ||
                            r == EdgeRegion.Left_Center || r == EdgeRegion.Right_Center;
            if (isCenter) return "AppHelper";
            bool isVertical = r == EdgeRegion.Left_Top || r == EdgeRegion.Left_Center || r == EdgeRegion.Left_Bottom ||
                              r == EdgeRegion.Right_Top || r == EdgeRegion.Right_Center || r == EdgeRegion.Right_Bottom;
            if (isVertical) return "Widget";
            if (isHorizontal) return "Taskbar";
            return "Placeholder";
        }
    }
}