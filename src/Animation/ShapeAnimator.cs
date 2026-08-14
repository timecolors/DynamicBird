using DynamicBird.Core.Services.Configuration;
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DynamicBird.Infrastructure.Utils;

namespace DynamicBird.Animation
{
    public class ShapeAnimator : IDisposable
    {
        private readonly Window _window;
        private readonly FrameworkElement _panel;
        private ISettingsService? _settings;

        // ========== 位置物理系统 ==========
        private double _targetLeft;
        private double _targetTop;
        private bool _hasPositionTarget = false;
        // 滑入/滑出动画允许位置在屏幕外（隐藏时滑出屏幕、显示时从屏幕外滑入）
        private bool _allowOffscreen = false;
        private double _vX = 0;
        private double _vY = 0;
        public double PosStiffness { get; private set; } = 180.0;
        public double PosDamping { get; private set; } = 30.0;

        // ========== 透明度物理系统 ==========
        private double _targetOpacity = 1.0;
        private bool _hasOpacityTarget = false;
        private double _opacityV = 0;
        public double OpacityStiffness { get; private set; } = 25.0;
        public double OpacityDamping { get; private set; } = 10.0;

        // ========== 飞行系统（独立） ==========
        private bool _isFlying = false;
        private double _flyStartLeft;
        private double _flyStartTop;
        private double _flyTargetLeft;
        private double _flyTargetTop;
        private DateTime _flyStartTime;
        private int _flyDurationMs = 500;
        private static readonly CubicEase _flyEase = new CubicEase { EasingMode = EasingMode.EaseOut };

        // ========== 动画总开关 ==========
        private bool _animationsEnabled = true;

        private const double MaxVelocity = 4000.0;
        private const double ConvergeThreshold = 0.3;
        private const double OpacityConvergeThreshold = 0.005;

        private DateTime _lastRenderTime;
        private bool _isRunning = false;

        public event Action? FlyCompleted;

        public double CurrentLeft => _window.Left;
        public double CurrentTop => _window.Top;
        public double CurrentWidth => _window.Width;
        public double CurrentHeight => _window.Height;

        public ShapeAnimator(Window window, FrameworkElement panel)
        {
            _window = window;
            _panel = panel;
            _targetLeft = _window.Left;
            _targetTop = _window.Top;
            _targetOpacity = _panel.Opacity;
        }

        // ============================================================
        //  ★★★ 设置管理与参数更新 ★★★
        // ============================================================

        public void SetSettings(ISettingsService settings)
        {
            _settings = settings;
            UpdateParametersFromSettings();
        }

        private void UpdateParametersFromSettings()
        {
            if (_settings == null) return;

            // ★ 透明度参数：从 ShowHideDurationMs 映射
            double opacityDuration = Math.Max(30, _settings.ShowHideDurationMs);
            double omega = 2 * Math.PI / (opacityDuration / 1000.0);
            OpacityStiffness = omega * omega;
            OpacityDamping = 2 * omega * 0.55;

            // ★ 位置参数：从 TransformDurationMs 映射
            double posDuration = Math.Max(30, _settings.TransformDurationMs);
            double posOmega = 2 * Math.PI / (posDuration / 1000.0);
            PosStiffness = posOmega * posOmega;
            PosDamping = 2 * posOmega * 0.55;

            // 根据缓动类型调整阻尼
            string easing = _settings.TransformEasingType;
            if (easing == "ElasticEase")
            {
                PosDamping *= 0.7;
            }
            else if (easing == "BounceEase")
            {
                PosDamping *= 0.6;
            }
            else if (easing == "BackEase")
            {
                PosDamping *= 0.8;
            }
        }

        // ============================================================
        //  动画开关
        // ============================================================

        public void SetAnimationsEnabled(bool enabled)
        {
            _animationsEnabled = enabled;
        }

        // ============================================================
        //  飞行
        // ============================================================

        public void SetFlyParameters(int durationMs)
        {
            _flyDurationMs = Math.Max(50, durationMs);
            _isFlying = true;
        }

        public void StartFly(double targetLeft, double targetTop)
        {
            _flyStartLeft = _window.Left;
            _flyStartTop = _window.Top;
            _flyTargetLeft = targetLeft;
            _flyTargetTop = targetTop;
            _flyStartTime = DateTime.Now;
            _isFlying = true;
            _hasPositionTarget = false;
            EnsureRunning();
        }

        // ============================================================
        //  位置控制（物理收敛）
        // ============================================================

        public void SetPositionTargetWithoutReset(double left, double top)
        {
            if (_isFlying) return;
            UpdateParametersFromSettings();

            _allowOffscreen = false;
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            double currentWidth = _window.Width;
            double currentHeight = _window.Height;

            left = Math.Max(0, Math.Min(left, screenWidth - currentWidth));
            top = Math.Max(0, Math.Min(top, screenHeight - currentHeight));

            _targetLeft = left;
            _targetTop = top;
            _hasPositionTarget = true;
            EnsureRunning();
        }

        public void SetPositionTarget(double left, double top, bool resetVelocity = false)
        {
            if (resetVelocity) { _vX = 0; _vY = 0; }
            SetPositionTargetWithoutReset(left, top);
        }

        public void JumpToPosition(double left, double top)
        {
            _allowOffscreen = false;
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            double currentWidth = _window.Width;
            double currentHeight = _window.Height;

            left = Math.Max(0, Math.Min(left, screenWidth - currentWidth));
            top = Math.Max(0, Math.Min(top, screenHeight - currentHeight));

            _window.Left = left;
            _window.Top = top;
            _targetLeft = left;
            _targetTop = top;
            _hasPositionTarget = true;
            _vX = 0;
            _vY = 0;
        }

        public void FollowWithInertia(double targetLeft, double targetTop)
        {
            UpdateParametersFromSettings();
            SetPositionTarget(targetLeft, targetTop, false);
        }

        // ============================================================
        //  尺寸：直接跳转（不做收敛）
        // ============================================================

        public void SetSizeDirect(double width, double height)
        {
            _allowOffscreen = false;
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            width = Math.Max(10, Math.Min(width, screenWidth));
            height = Math.Max(10, Math.Min(height, screenHeight));

            double currentLeft = _window.Left;
            double currentTop = _window.Top;
            double newLeft = Math.Max(0, Math.Min(currentLeft, screenWidth - width));
            double newTop = Math.Max(0, Math.Min(currentTop, screenHeight - height));

            // ★ 原子应用：位置 + 尺寸一次 SetWindowPos 完成，避免贴边侧出现中间态
            WindowRect.ApplyAtomic(_window, newLeft, newTop, width, height);
            _targetLeft = newLeft;
            _targetTop = newTop;
        }

        // ============================================================
        //  透明度控制（物理收敛）
        // ============================================================

        public void SetOpacityTarget(double opacity, bool resetVelocity = false)
        {
            UpdateParametersFromSettings();

            opacity = Math.Max(0, Math.Min(1, opacity));
            _targetOpacity = opacity;
            _hasOpacityTarget = true;

            if (resetVelocity)
            {
                _opacityV = 0;
            }

            EnsureRunning();
        }

        public void SetOpacityDirect(double opacity)
        {
            opacity = Math.Max(0, Math.Min(1, opacity));
            _panel.Opacity = opacity;
            _targetOpacity = opacity;
            _hasOpacityTarget = false;
            _opacityV = 0;
        }

        // ============================================================
        //  呼出/隐藏（位置 + 透明度 同时进行）
        // ============================================================

        /// <summary>
        /// 只改尺寸、保持当前位置不变（用于隐藏状态下先就位，随后滑入锚点）。
        /// </summary>
        public void SetSizeKeepPositionDirect(double width, double height)
        {
            width = Math.Max(10, Math.Min(width, SystemParameters.PrimaryScreenWidth));
            height = Math.Max(10, Math.Min(height, SystemParameters.PrimaryScreenHeight));
            WindowRect.ApplyAtomic(_window, _window.Left, _window.Top, width, height);
            _allowOffscreen = false;
        }

        public void SetShowHideTarget(double left, double top, double opacity, bool allowOffscreen = false)
        {
            UpdateParametersFromSettings();

            _allowOffscreen = allowOffscreen;
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            double currentWidth = _window.Width;
            double currentHeight = _window.Height;

            if (!allowOffscreen)
            {
                left = Math.Max(0, Math.Min(left, screenWidth - currentWidth));
                top = Math.Max(0, Math.Min(top, screenHeight - currentHeight));
            }

            _targetLeft = left;
            _targetTop = top;
            _hasPositionTarget = true;

            _targetOpacity = Math.Max(0, Math.Min(1, opacity));
            _hasOpacityTarget = true;

            _vX = 0;
            _vY = 0;
            _opacityV = 0;

            EnsureRunning();
        }

        public void SetShowHideDirect(double left, double top, double opacity)
        {
            _allowOffscreen = false;
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            double currentWidth = _window.Width;
            double currentHeight = _window.Height;

            left = Math.Max(0, Math.Min(left, screenWidth - currentWidth));
            top = Math.Max(0, Math.Min(top, screenHeight - currentHeight));

            // ★ 原子应用位置 + 尺寸
            WindowRect.ApplyAtomic(_window, left, top, currentWidth, currentHeight);
            _targetLeft = left;
            _targetTop = top;
            _hasPositionTarget = false;
            _vX = 0;
            _vY = 0;

            opacity = Math.Max(0, Math.Min(1, opacity));
            _panel.Opacity = opacity;
            _targetOpacity = opacity;
            _hasOpacityTarget = false;
            _opacityV = 0;
        }

        // ============================================================
        //  组合方法
        // ============================================================

        public void SetPositionAndSizeDirect(double left, double top, double width, double height)
        {
            _allowOffscreen = false;
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            width = Math.Max(10, Math.Min(width, screenWidth));
            height = Math.Max(10, Math.Min(height, screenHeight));

            // ★ 飞行期间只原子改尺寸，位置由飞行系统接管
            if (_isFlying)
            {
                WindowRect.ApplyAtomic(_window, _window.Left, _window.Top, width, height);
                return;
            }

            left = Math.Max(0, Math.Min(left, screenWidth - width));
            top = Math.Max(0, Math.Min(top, screenHeight - height));
            WindowRect.ApplyAtomic(_window, left, top, width, height);
            _targetLeft = left;
            _targetTop = top;
            _hasPositionTarget = true;
            EnsureRunning();
        }

        public void SetPositionAndSizeWithoutReset(double left, double top, double width, double height)
        {
            _allowOffscreen = false;
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            width = Math.Max(10, Math.Min(width, screenWidth));
            height = Math.Max(10, Math.Min(height, screenHeight));

            // ★ 飞行期间只原子改尺寸，位置由飞行系统接管
            if (_isFlying)
            {
                WindowRect.ApplyAtomic(_window, _window.Left, _window.Top, width, height);
                return;
            }

            left = Math.Max(0, Math.Min(left, screenWidth - width));
            top = Math.Max(0, Math.Min(top, screenHeight - height));
            WindowRect.ApplyAtomic(_window, left, top, width, height);
            _targetLeft = left;
            _targetTop = top;
            _hasPositionTarget = true;
            EnsureRunning();
        }

        public void JumpTo(double left, double top, double width, double height)
        {
            _allowOffscreen = false;
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            left = Math.Max(0, Math.Min(left, screenWidth - width));
            top = Math.Max(0, Math.Min(top, screenHeight - height));

            width = Math.Max(10, width);
            height = Math.Max(10, height);

            // ★★★ 原子应用：位置和尺寸一次 SetWindowPos 同时设置，贴边侧永远固定 ★★★
            WindowRect.ApplyAtomic(_window, left, top, width, height);

            _targetLeft = left;
            _targetTop = top;
            _hasPositionTarget = true;
            _vX = 0;
            _vY = 0;
            _isFlying = false;
            StopRunning();
        }

        public void StopAll()
        {
            _hasPositionTarget = false;
            _hasOpacityTarget = false;
            _isFlying = false;
            _vX = 0;
            _vY = 0;
            _opacityV = 0;
            StopRunning();
        }

        // ============================================================
        //  兼容旧接口
        // ============================================================

        public void SetTarget(double left, double top, double width, double height, bool resetVelocity = true)
        {
            if (resetVelocity) { _vX = 0; _vY = 0; }
            SetPositionAndSizeWithoutReset(left, top, width, height);
        }

        public void SetTargetPositionAndSizeWithoutReset(double left, double top, double width, double height)
        {
            SetPositionAndSizeWithoutReset(left, top, width, height);
        }

        public void SetTargetSizeWithoutReset(double width, double height)
        {
            SetSizeDirect(width, height);
        }

        public void SetParameters(double posStiffness, double posDamping, double sizeStiffness, double sizeDamping)
        {
            PosStiffness = posStiffness;
            PosDamping = posDamping;
        }

        public void SetImmediate(double width, double height, double left, double top)
        {
            JumpTo(left, top, width, height);
        }

        public void AnimateTo(double width, double height, double left, double top, int durationMs)
        {
            SetFlyParameters(durationMs);
            SetSizeDirect(width, height);
            StartFly(left, top);
        }

        // ============================================================
        //  内部引擎
        // ============================================================

        private void EnsureRunning()
        {
            if (!_isRunning)
            {
                _isRunning = true;
                _lastRenderTime = DateTime.Now;
                CompositionTarget.Rendering += OnRendering;
            }
        }

        private void StopRunning()
        {
            if (_isRunning)
            {
                _isRunning = false;
                CompositionTarget.Rendering -= OnRendering;
            }
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            if (!_isRunning) return;

            var now = DateTime.Now;
            double dt = Math.Min((now - _lastRenderTime).TotalSeconds, 0.05);
            _lastRenderTime = now;

            if (dt <= 0) return;

            // ===== 飞行（优先，不受动画开关影响） =====
            if (_isFlying)
            {
                double elapsed = (now - _flyStartTime).TotalMilliseconds;
                double progress = Math.Min(1.0, elapsed / _flyDurationMs);
                double eased = _flyEase.Ease(progress);

                double currentLeft = _flyStartLeft + (_flyTargetLeft - _flyStartLeft) * eased;
                double currentTop = _flyStartTop + (_flyTargetTop - _flyStartTop) * eased;

                double screenWidth = SystemParameters.PrimaryScreenWidth;
                double screenHeight = SystemParameters.PrimaryScreenHeight;
                double currentWidth = _window.Width;
                double currentHeight = _window.Height;
                currentLeft = Math.Max(0, Math.Min(currentLeft, screenWidth - currentWidth));
                currentTop = Math.Max(0, Math.Min(currentTop, screenHeight - currentHeight));

                _window.Left = currentLeft;
                _window.Top = currentTop;

                if (progress >= 1.0)
                {
                    _window.Left = _flyTargetLeft;
                    _window.Top = _flyTargetTop;
                    _isFlying = false;
                    FlyCompleted?.Invoke();
                }
                return;
            }

            // ============================================================
            //  动画关闭时：直接跳转
            // ============================================================

            if (!_animationsEnabled)
            {
                if (_hasPositionTarget)
                {
                    _window.Left = _targetLeft;
                    _window.Top = _targetTop;
                    _hasPositionTarget = false;
                    _vX = 0;
                    _vY = 0;
                }
                if (_hasOpacityTarget)
                {
                    _panel.Opacity = _targetOpacity;
                    _hasOpacityTarget = false;
                    _opacityV = 0;
                }
                if (!_hasPositionTarget && !_hasOpacityTarget)
                {
                    StopRunning();
                }
                return;
            }

            // ============================================================
            //  动画开启时：物理收敛
            // ============================================================

            bool anyTarget = false;

            // ===== 位置收敛 =====
            if (_hasPositionTarget)
            {
                anyTarget = true;
                double currentLeft = _window.Left;
                double currentTop = _window.Top;
                double currentWidth = _window.Width;
                double currentHeight = _window.Height;

                double errX = _targetLeft - currentLeft;
                double errY = _targetTop - currentTop;

                double accX = PosStiffness * errX - PosDamping * _vX;
                double accY = PosStiffness * errY - PosDamping * _vY;

                _vX += accX * dt;
                _vY += accY * dt;

                double speed = Math.Sqrt(_vX * _vX + _vY * _vY);
                if (speed > MaxVelocity)
                {
                    _vX = _vX / speed * MaxVelocity;
                    _vY = _vY / speed * MaxVelocity;
                }

                double newLeft = currentLeft + _vX * dt;
                double newTop = currentTop + _vY * dt;

                if (!_allowOffscreen)
                {
                    double screenWidth = SystemParameters.PrimaryScreenWidth;
                    double screenHeight = SystemParameters.PrimaryScreenHeight;
                    newLeft = Math.Max(0, Math.Min(newLeft, screenWidth - currentWidth));
                    newTop = Math.Max(0, Math.Min(newTop, screenHeight - currentHeight));

                    if (newLeft <= 0 || newLeft >= screenWidth - currentWidth) _vX = 0;
                    if (newTop <= 0 || newTop >= screenHeight - currentHeight) _vY = 0;
                }

                _window.Left = newLeft;
                _window.Top = newTop;

                double posDist = Math.Abs(newLeft - _targetLeft) + Math.Abs(newTop - _targetTop);
                double vel = Math.Abs(_vX) + Math.Abs(_vY);

                if (posDist < ConvergeThreshold && vel < ConvergeThreshold)
                {
                    _window.Left = _targetLeft;
                    _window.Top = _targetTop;
                    _vX = 0;
                    _vY = 0;
                    _hasPositionTarget = false;
                }
            }

            // ===== 透明度收敛 =====
            if (_hasOpacityTarget)
            {
                anyTarget = true;
                double currentOpacity = _panel.Opacity;

                double errO = _targetOpacity - currentOpacity;
                double accO = OpacityStiffness * errO - OpacityDamping * _opacityV;
                _opacityV += accO * dt;

                double newOpacity = currentOpacity + _opacityV * dt;
                newOpacity = Math.Max(0, Math.Min(1, newOpacity));
                _panel.Opacity = newOpacity;

                double opacityDist = Math.Abs(newOpacity - _targetOpacity);
                double opacityVel = Math.Abs(_opacityV);

                if (opacityDist < OpacityConvergeThreshold && opacityVel < OpacityConvergeThreshold)
                {
                    _panel.Opacity = _targetOpacity;
                    _opacityV = 0;
                    _hasOpacityTarget = false;
                }
            }

            if (!anyTarget)
            {
                StopRunning();
            }
        }

        public void Dispose()
        {
            StopRunning();
            GC.SuppressFinalize(this);
        }
    }
}
