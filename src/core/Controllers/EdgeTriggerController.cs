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
        private WindowSizeController? _sizeController;
        private readonly PanelVisibilityController _visibilityController;
        private readonly ISettingsService _settings;

        // ★★★ 底部边界（任务栏顶部，随任务栏状态动态更新） ★★★
        private double _bottomBoundary;

        // 任务栏高度（DIP，用于统一各模式的条状尺寸）
        private readonly double _taskbarHeightDips;

        // ========== 状态追踪 ==========
        private string _currentEdge = "";
        private string _lastRegionType = "";
        private EdgeRegion _lastProcessedRegion = EdgeRegion.Unknown;
        private string _currentRegionKey = "";

        // ========== 拖拽状态 ==========
        private bool _isDragging = false;
        private DateTime _dragEndCooldownUntil = DateTime.MinValue;
        public bool IsDragging
        {
            get => _isDragging;
            set => _isDragging = value;
        }
        /// <summary>
        /// 拖拽/调整大小刚结束（冷却期内），用于抑制面板隐藏。
        /// </summary>
        public bool IsRecentlyDragged => DateTime.Now < _dragEndCooldownUntil;

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
        private string _flyingTargetEdge = "";

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

        public event Action? StartClingingRequested;
        public event Action? StopClingingRequested;
        public event Action? StickToMouseRequested;
        public event Action? FlyCompleted;
        public event Action<string, string>? RegionChanged;

        public EdgeTriggerController(
            Window window,
            ShapeAnimator shapeAnimator,
            WindowSizeController? sizeController,
            PanelVisibilityController visibilityController,
            ISettingsService settings,
            double bottomBoundary,
            double taskbarHeightDips)
        {
            _window = window;
            _shapeAnimator = shapeAnimator;
            _sizeController = sizeController;
            _visibilityController = visibilityController;
            _settings = settings;
            _bottomBoundary = bottomBoundary;
            _taskbarHeightDips = taskbarHeightDips;

            _shapeAnimator.FlyCompleted += OnFlyCompleted;

            _cachedWidth = _window.Width;
            _cachedHeight = _window.Height;
        }

        /// <summary>
        /// 附加尺寸控制器（解除 MainWindow 初始化时的循环依赖，替代反射注入）。
        /// </summary>
        public void SetSizeController(WindowSizeController controller)
        {
            _sizeController = controller;
        }

        /// <summary>
        /// 更新底部贴边边界（任务栏自动隐藏/升起时由主窗口定时刷新）。
        /// </summary>
        public void UpdateBottomBoundary(double value)
        {
            if (value > 0 && !double.IsNaN(value) && !double.IsInfinity(value))
            {
                _bottomBoundary = value;
            }
        }

        /// <summary>
        /// 当任务栏边界变化（升起/隐藏）时，若面板正贴着底边，立即按新边界重新锚定。
        /// 由主窗口定时器在边界变化时调用，保证面板始终贴住任务栏顶而不遮挡。
        /// </summary>
        public void ReanchorBottomPanel()
        {
            if (!_visibilityController.IsVisible) return;
            if (_isDragging) return;

            bool bottomAnchored =
                _currentEdge == "Bottom" ||
                _lastProcessedRegion == EdgeRegion.Bottom_Left ||
                _lastProcessedRegion == EdgeRegion.Bottom_Center ||
                _lastProcessedRegion == EdgeRegion.Bottom_Right ||
                _lastProcessedRegion == EdgeRegion.BottomLeft ||
                _lastProcessedRegion == EdgeRegion.BottomRight;

            if (!bottomAnchored) return;

            double screenW = SystemParameters.PrimaryScreenWidth;
            double screenH = SystemParameters.PrimaryScreenHeight;
            double w = _window.Width;
            double h = _window.Height;
            double left = Math.Max(0, Math.Min(_window.Left, screenW - w));
            double top = Math.Max(0, Math.Min(_bottomBoundary - h, screenH - h));

            // ★ 任务栏跟随使用平滑物理收敛，而非瞬移（动画关闭时由 ShapeAnimator 自动跳转）
            _shapeAnimator.SetPositionTargetWithoutReset(left, top);
        }

        /// <summary>
        /// 当前区域/边缘对应的锚定位置（面板应贴住的点）。
        /// </summary>
        public (double left, double top) GetCurrentAnchor()
        {
            double sw = SystemParameters.PrimaryScreenWidth;
            double sh = SystemParameters.PrimaryScreenHeight;
            double w = _cachedWidth > 0 ? _cachedWidth : _window.Width;
            double h = _cachedHeight > 0 ? _cachedHeight : _window.Height;

            switch (_currentEdge)
            {
                case "Top":
                    return (Math.Max(0, Math.Min(_window.Left, sw - w)), 0);
                case "Bottom":
                    return (Math.Max(0, Math.Min(_window.Left, sw - w)), _bottomBoundary - h);
                case "Left":
                    return (0, Math.Max(0, Math.Min(_window.Top, sh - h)));
                case "Right":
                    return (sw - w, Math.Max(0, Math.Min(_window.Top, sh - h)));
                default:
                    return (Math.Max(0, Math.Min(_window.Left, sw - w)),
                            Math.Max(0, Math.Min(_window.Top, sh - h)));
            }
        }

        /// <summary>
        /// 以当前锚点滑入显示面板（由主窗口在边缘触发时调用）。
        /// </summary>
        public void ShowPanelAtAnchor()
        {
            var (left, top) = GetCurrentAnchor();
            _visibilityController.ShowAt(left, top);
        }

        /// <summary>
        /// 鼠标在面板内时：仅跟随边缘滑动更新位置，不切换区域/内容。
        /// 解决“面板内移动鼠标位置不实时跟手”的问题。
        /// </summary>
        public void FollowMouseInPanel(EdgeRegion region, double mouseX, double mouseY,
            double screenWidth, double screenHeight)
        {
            if (region == EdgeRegion.Unknown || _isFlying || _isDragging ||
                DateTime.Now < _dragEndCooldownUntil) return;

            string edge = GetEdgeName(region);
            if (string.IsNullOrEmpty(edge) || edge != _currentEdge) return;

            // 角落不在此处理（角落面板不跟随鼠标滑动）
            if (region == EdgeRegion.TopLeft || region == EdgeRegion.TopRight ||
                region == EdgeRegion.BottomLeft || region == EdgeRegion.BottomRight) return;

            // 同一边内滑动到不同区域（如 Bottom_Left → Bottom_Center）：及时切换模态
            string regionKey = GetRegionKey(region);
            string regionType = GetRegionTypeFromEnum(region);

            if (regionType != _lastRegionType || regionKey != _currentRegionKey)
            {
                // 防抖：避免在区域边界来回抖动导致内容反复重建
                if (_lastDebounceRegion == region &&
                    (DateTime.Now - _lastRegionChangeTime).TotalMilliseconds < _settings.RegionDebounceMs)
                {
                    return;
                }
                _lastDebounceRegion = region;
                _lastRegionChangeTime = DateTime.Now;

                _lastRegionType = regionType;
                _currentRegionKey = regionKey;
                SizeController.SetMode(regionType, regionKey);
                RegionChanged?.Invoke(regionType, regionKey);

                var (w, h) = GetTargetSizeForRegion(regionType, regionKey);
                _cachedWidth = w;
                _cachedHeight = h;
                var (left, top) = CalculatePosition(region, mouseX, mouseY, screenWidth, screenHeight, w, h);
                _shapeAnimator.JumpTo(left, top, w, h);
                return;
            }

            // 同区域：位置实时跟随
            double cw = _cachedWidth > 0 ? _cachedWidth : _window.Width;
            double ch = _cachedHeight > 0 ? _cachedHeight : _window.Height;
            var (cLeft, cTop) = CalculatePosition(region, mouseX, mouseY, screenWidth, screenHeight, cw, ch);
            _shapeAnimator.SetPositionTargetWithoutReset(cLeft, cTop);
        }

        private WindowSizeController SizeController =>
            _sizeController ?? throw new InvalidOperationException("WindowSizeController 尚未附加");

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
                SizeController.SetMode(_flyingTargetRegionType, _flyingTargetRegionKey);
                RegionChanged?.Invoke(_flyingTargetRegionType, _flyingTargetRegionKey);

                var (w, h) = GetTargetSizeForRegion(_flyingTargetRegionType, _flyingTargetRegionKey);
                _cachedWidth = w;
                _cachedHeight = h;

                double screenW = SystemParameters.PrimaryScreenWidth;
                double screenH = SystemParameters.PrimaryScreenHeight;

                // ★ 飞行完成：按目标边缘重新锚定后原子应用完整矩形，
                //   避免“位置按旧尺寸飞行、到站后尺寸突变导致贴边错位”
                double left = _shapeAnimator.CurrentLeft;
                double top = _shapeAnimator.CurrentTop;
                switch (_flyingTargetEdge)
                {
                    case "Top": top = 0; break;
                    case "Bottom": top = _bottomBoundary - h; break;
                    case "Left": left = 0; break;
                    case "Right": left = screenW - w; break;
                }
                left = Math.Max(0, Math.Min(left, screenW - w));
                top = Math.Max(0, Math.Min(top, screenH - h));
                _shapeAnimator.JumpTo(left, top, w, h);

                // 以实际生效尺寸回填缓存（WPF 最小尺寸等可能钳制目标值）
                _cachedWidth = _window.Width;
                _cachedHeight = _window.Height;
            }

            _flyingTargetRegionType = "";
            _flyingTargetRegionKey = "";
            _flyingTargetEdge = "";
            FlyCompleted?.Invoke();
        }

        public void OnStickToMouseSuccess() { }

        // ========================================
        //  核心业务方法
        // ========================================

        public void ProcessRegion(EdgeRegion region, double mouseX, double mouseY, double screenWidth, double screenHeight)
        {
            // ★ 拖拽/调整大小结束后短暂冷却，避免鼠标还在屏幕边缘时立即切换区域
            if (region == EdgeRegion.Unknown || _isFlying || _isDragging ||
                DateTime.Now < _dragEndCooldownUntil) return;

            if (_isClinging)
            {
                StopClinging();
            }

            bool isCorner = region == EdgeRegion.TopLeft || region == EdgeRegion.TopRight ||
                            region == EdgeRegion.BottomLeft || region == EdgeRegion.BottomRight;

            // ★ 右上角不呼出面板（避免影响关闭窗口的体验），如已显示则隐藏。
            //   不记录该区域，避免与相邻边缘区域产生“飞行”误判；安全区由 EdgeStateDetector 兜底。
            if (region == EdgeRegion.TopRight)
            {
                if (_visibilityController.IsVisible)
                {
                    _visibilityController.Hide();
                }
                return;
            }

            if (isCorner)
            {
                ProcessCorner(region, screenWidth, screenHeight);
                return;
            }

            string currentEdgeName = GetEdgeName(region);
            _currentEdge = currentEdgeName;
            SizeController.UpdateHandlePosition(_currentEdge);

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
                SizeController.SetMode(regionType, regionKey);
                RegionChanged?.Invoke(regionType, regionKey);

                if (_visibilityController.IsVisible)
                {
                    // ★★★ 已显示：位置和尺寸一次 SetWindowPos 原子跳转（贴边侧永远固定） ★★★
                    _shapeAnimator.JumpTo(targetLeft, targetTop, targetW, targetH);
                }
                else
                {
                    // ★★★ 隐藏→显示：先就地改尺寸，再由滑入动画带向锚点（滑入滑出） ★★★
                    _shapeAnimator.SetSizeKeepPositionDirect(targetW, targetH);
                    _visibilityController.ShowAt(targetLeft, targetTop);
                }

                // 以实际生效尺寸回填缓存（WPF 最小尺寸等可能钳制目标值）
                _cachedWidth = _window.Width;
                _cachedHeight = _window.Height;
            }
            else
            {
                // ★★★ 同区域：只更新位置（物理跟随，尺寸不变） ★★★
                _shapeAnimator.SetPositionTargetWithoutReset(targetLeft, targetTop);
            }

            _lastProcessedRegion = region;
            _lastRegionChangeTime = DateTime.Now;
        }

        /// <summary>
        /// 拖拽/调整大小结束时调用：短暂抑制边缘触发，避免鼠标仍在屏幕边缘时误切换区域。
        /// </summary>
        public void NotifyDragEnded()
        {
            _dragEndCooldownUntil = DateTime.Now.AddMilliseconds(1200);
        }

        public void ClearEdge()
        {
            _currentEdge = "";
            _lastRegionType = "";
            _lastProcessedRegion = EdgeRegion.Unknown;
            _lastDebounceRegion = EdgeRegion.Unknown;
            _currentRegionKey = "";
            _isClinging = false;
        }

        public void ApplySizeStrategy()
        {
            SizeController.ApplySizeForCurrentMode();
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

            // ★ 防横跳：鼠标贴近屏幕边缘时不启动跟随，交给边缘触发处理；
            //   否则“边缘锚定 ↔ 跟随鼠标”会在边缘附近来回切换
            double screenW = SystemParameters.PrimaryScreenWidth;
            double screenH = SystemParameters.PrimaryScreenHeight;
            const double startMargin = 80; // 启动跟随需要离开边缘更远（与停止阈值形成迟滞）
            if (mouseX < startMargin || mouseX > screenW - startMargin ||
                mouseY < startMargin || mouseY > screenH - startMargin)
            {
                return;
            }

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

            // ★ 跟随中鼠标靠近屏幕边缘：停止跟随，交回边缘触发，
            //   避免面板被“吸”到边缘附近时与贴边逻辑反复争夺
            double screenW = SystemParameters.PrimaryScreenWidth;
            double screenH = SystemParameters.PrimaryScreenHeight;
            const double stopMargin = 60;
            if (mouseX < stopMargin || mouseX > screenW - stopMargin ||
                mouseY < stopMargin || mouseY > screenH - stopMargin)
            {
                StopClinging();
                return;
            }

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
            _flyingTargetEdge = GetEdgeName(target);
            _lastProcessedRegion = target;
        }

        // ========================================
        //  内部辅助方法
        // ========================================

        private void ProcessCorner(EdgeRegion region, double sw, double sh)
        {
            string key = region.ToString();
            // ★ 角落面板类型：设置里可自定义，默认按现有分工（通知坞/最近使用/快捷设置）
            string custom = _settings.GetRegionPanel(key);
            string type = custom != "Default" && IsValidPanelType(custom) ? custom : "Placeholder";
            _currentEdge = region is EdgeRegion.BottomLeft or EdgeRegion.BottomRight ? "Bottom" : "Top";

            // ★ 每个角落有独立内容（通知坞 / 最近使用 / 系统开关），仅进入时切换一次
            if (type != _lastRegionType || key != _currentRegionKey)
            {
                _lastRegionType = type;
                _currentRegionKey = key;
                SizeController.SetMode(type, key);
                RegionChanged?.Invoke(type, key);
            }

            // ★ 角落面板默认方形；用户拖拽保存过尺寸则恢复自定义宽高
            double size = Math.Max(100, Math.Min(sw * 2.0 / 7.0, sw * 0.4));
            double w = size, h = size;
            if (!_settings.AutoFitOnTrigger)
            {
                var (userW, userH) = _settings.GetUserSize(key);
                if (userW >= 100) w = Math.Min(userW, sw * 0.8);
                if (userH >= 100) h = Math.Min(userH, sh * 0.8);
            }
            _cachedWidth = w;
            _cachedHeight = h;

            double left = 0, top = 0;
            switch (region)
            {
                case EdgeRegion.TopLeft: break;
                case EdgeRegion.TopRight: left = sw - w; break;
                case EdgeRegion.BottomLeft: top = _bottomBoundary - h; break;
                case EdgeRegion.BottomRight: left = sw - w; top = _bottomBoundary - h; break;
                default: return;
            }

            if (_visibilityController.IsVisible)
            {
                // ★ 已显示：角落切换同样原子跳转，贴边侧不出现中间态
                _shapeAnimator.JumpTo(left, top, w, h);
            }
            else
            {
                // ★ 隐藏→显示：先就地改尺寸，再由滑入动画带向角落
                _shapeAnimator.SetSizeKeepPositionDirect(w, h);
                _visibilityController.ShowAt(left, top);
            }
            _lastProcessedRegion = region;
        }

        private (double w, double h) GetTargetSizeForRegion(string type, string key)
        {
            double sw = SystemParameters.PrimaryScreenWidth;
            double sh = SystemParameters.PrimaryScreenHeight;

            // ★ 区域形状设置（Default/方形/横条/竖条），让设置真正生效
            string regionShape = GetRegionShapeSetting(key);
            if (regionShape == "Square")
            {
                double s = Math.Max(100, Math.Min(sw * 0.4, 420));
                return (s, s);
            }
            if (regionShape == "StripH")
            {
                double hh = Math.Max(60, Math.Min(120, sh * 0.12));
                return (sw * 0.6, hh);
            }
            if (regionShape == "StripV")
            {
                double ww = Math.Max(60, Math.Min(120, sw * 0.12));
                return (ww, sh * 0.6);
            }

            // ★ 固定位置模式的固定形状
            string edge = GetEdgeFromKey(key);
            if (!string.IsNullOrEmpty(edge) && _settings.GetEdgeMode(edge) == "Fixed")
            {
                string fixedShape = _settings.GetFixedShape(edge);
                if (fixedShape == "Square")
                {
                    double s = Math.Max(100, Math.Min(sw * 0.4, 420));
                    return (s, s);
                }
                if (fixedShape == "StripH")
                {
                    return (sw * 0.6, Math.Max(60, Math.Min(120, sh * 0.12)));
                }
                if (fixedShape == "StripV")
                {
                    return (Math.Max(60, Math.Min(120, sw * 0.12)), sh * 0.6);
                }
            }

            if (type == "Taskbar")
            {
                // ★ 允许任务栏面板保存用户调整后的尺寸
                var (taskUserW, taskUserH) = _settings.GetUserSize(key);
                if (!_settings.AutoFitOnTrigger && taskUserW > 0 && taskUserH > 0)
                {
                    return (taskUserW, taskUserH);
                }
                return (sw * 2.0 / 3.0, Math.Max(_window.MinHeight, Math.Max(60, _taskbarHeightDips * 1.75)));
            }

            // ★ 自定义角落面板：通知坞 / 最近使用 / 快捷设置 → 方形
            if (type is "Notification" or "Recent" or "QuickSettings")
            {
                double s = Math.Max(100, Math.Min(sw * 0.4, 420));
                return (s, s);
            }

            if (type == "Placeholder")
            {
                double s = sw * 2.0 / 7.0;
                return (Math.Max(100, Math.Min(s, sw * 0.4)), Math.Max(100, Math.Min(s, sw * 0.4)));
            }

            var (userW, userH) = _settings.GetUserSize(key);
            if (!_settings.AutoFitOnTrigger && userW > 0 && userH > 0)
                return (userW, userH);

            // Widget 360x260（容纳音乐/待办等新小组件），AppHelper 420x340（辅助功能主页）
            return (type == "Widget" ? 360 : 420, type == "Widget" ? 260 : 340);
        }

        private string GetRegionShapeSetting(string key)
        {
            try
            {
                string[] parts = key.Split('_');
                if (parts.Length == 2)
                {
                    return _settings.GetRegionShape(parts[0], parts[1]);
                }
            }
            catch { }
            return "Default";
        }

        private static string GetEdgeFromKey(string key)
        {
            return key.Contains('_') ? key.Split('_')[0] : "";
        }

        private (double left, double top) CalculatePosition(EdgeRegion region, double mx, double my,
            double sw, double sh, double w, double h)
        {
            string edge = GetEdgeName(region);
            double left = 0, top = 0;

            // ★ 固定位置模式：面板不跟随鼠标，按保存的偏移量定位（由拖动面板时保存）
            if (!string.IsNullOrEmpty(edge) && _settings.GetEdgeMode(edge) == "Fixed")
            {
                double offset = _settings.GetFixedOffset(edge);
                switch (edge)
                {
                    case "Top":
                        left = Math.Max(0, Math.Min(sw / 2 - w / 2 + offset, sw - w));
                        return (left, 0);
                    case "Bottom":
                        left = Math.Max(0, Math.Min(sw / 2 - w / 2 + offset, sw - w));
                        return (left, _bottomBoundary - h);
                    case "Left":
                        top = Math.Max(0, Math.Min(sh / 2 - h / 2 + offset, sh - h));
                        return (0, top);
                    case "Right":
                        top = Math.Max(0, Math.Min(sh / 2 - h / 2 + offset, sh - h));
                        return (sw - w, top);
                }
            }

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
            // ★ 区域面板自定义：设置里非 Default 时覆盖默认布局
            string custom = _settings.GetRegionPanel(GetRegionKey(r));
            if (custom != "Default" && IsValidPanelType(custom))
            {
                return custom;
            }

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

        private static bool IsValidPanelType(string type)
        {
            return type is "Taskbar" or "Widget" or "AppHelper" or "Notification" or "Recent" or "QuickSettings";
        }
    }
}
