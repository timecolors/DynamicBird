using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using DynamicBird.Core.Services.Configuration;

namespace DynamicBird.Animation
{
    /// <summary>
    /// 面板形变动画器（WPF 原生动画 + 渲染帧驱动）：
    /// - 切换（区域切换/飞行落地）：尺寸一次到位（窗口仅 resize 一次，避免 Mica/内容每帧重绘闪烁），
    ///   内容用 ScaleTransform 平滑缩放过渡 + 位置 WPF 动画（渲染帧丝滑）；
    /// - 滑入/滑出：位置 + 透明度 WPF 动画（尺寸不变，本就丝滑）；
    /// - 连续跟随（贴边滑动/小鸟依人）：CompositionTarget.Rendering 渲染帧驱动（60fps 跟手）。
    /// 设置中 TransformDurationMs/TransformEasingType、ShowHideDurationMs/ShowHideEasingType 真实映射为动画参数。
    /// </summary>
    public class ShapeAnimator : IDisposable
    {
        private readonly Window _window;
        private readonly FrameworkElement _panel;
        private ISettingsService? _settings;
        private bool _animationsEnabled = true;
        private bool _disposed;

        // ===== 设置映射 =====
        private int _transformDurationMs = 250;
        private int _showHideDurationMs = 150;
        private IEasingFunction? _transformEasing;
        private IEasingFunction? _showHideEasing;

        // ===== 飞行 =====
        private int _flyDurationMs = 500;

        // ===== 渲染帧循环（跟随 + 小鸟依人）=====
        private bool _renderingActive;
        private bool _followActive;            // 贴边跟随：每帧调用 provider 实时跟手（Windows 拖拽式）
        private bool _clingMode;               // 小鸟依人：每帧 lerp 平滑趋近
        private double _clingTargetLeft;
        private double _clingTargetTop;

        // ★ 跟随松紧（由设置 FlyDurationMs 映射）：拉满=1.0 绝对跟手（实时），调小=缓慢飞追
        private double _followLerp = 1.0;
        // ★ 中置状态（乱逛/切换期间）覆盖：强制绝对跟手（图标跟着鼠标逛）
        private double? _followLerpOverride;

        /// <summary>跟随目标提供者：渲染帧每帧调用，返回面板应处的 (left, top)。由业务层设置。</summary>
        public Func<(double left, double top)>? FollowPositionProvider { get; set; }
        private const double ClingLerp = 0.18;
        private const double ClingStopDist = 0.5;

        // ===== 切换动画状态 =====
        private bool _switching;

        // ===== Mica backdrop 临时控制（尺寸动画期间禁用，避免 DWM 重采样闪烁）=====
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
        private const int DWMSBT_NONE = 1;
        private const int DWMSBT_MAINWINDOW = 2;
        private bool _micaMode;         // Win11 Mica 模式（MainWindow 告知）
        private bool _micaSuspended;    // 尺寸动画期间是否已临时禁用

        /// <summary>设置窗口是否为 Win11 Mica 模式（MainWindow 在启用 Fluent 材质后调用）。</summary>
        public void SetMicaBackdropEnabled(bool enabled) => _micaMode = enabled;

        private void SuspendMicaBackdrop()
        {
            if (!_micaMode || _micaSuspended) return;
            try
            {
                // ★ 只切不透明背景，不动 backdrop：
                //   - 不透明背景盖住 Mica → DWM 重采样不可见（无闪烁感知）；
                //   - backdrop 保持开启 → 无材质切换/重新生成延迟（开关 backdrop 才是闪烁源）；
                //   动画结束恢复半透明，毛玻璃直接透出。
                _window.Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x2D));
                _micaSuspended = true;
            }
            catch { }
        }

        private void RestoreMicaBackdrop()
        {
            if (!_micaMode || !_micaSuspended) return;
            try
            {
                // 恢复半透明深色背景 → Mica 毛玻璃透出（backdrop 一直在，无重建延迟）
                _window.Background = new SolidColorBrush(Color.FromArgb(0xE0, 0x2D, 0x2D, 0x2D));
                _micaSuspended = false;
            }
            catch { }
        }

        /// <summary>
        /// 恢复面板内容布局：解除动画期间的"固定尺寸 + 缩放跟随"，让内容按窗口实际尺寸真实布局。
        /// 任何中断/完成路径都必须调用，避免内容层残留固定尺寸导致布局错误。
        /// </summary>
        private void RestorePanelContentLayout()
        {
            try
            {
                _panel.RenderTransform = null;
                _panel.Width = double.NaN;
                _panel.Height = double.NaN;
                _panel.HorizontalAlignment = HorizontalAlignment.Stretch;
                _panel.VerticalAlignment = VerticalAlignment.Stretch;
            }
            catch { }
            RestoreMicaBackdrop();
        }

        public event Action? FlyCompleted;

        public double CurrentLeft => _window.Left;
        public double CurrentTop => _window.Top;
        public double CurrentWidth => _window.Width;
        public double CurrentHeight => _window.Height;

        public ShapeAnimator(Window window, FrameworkElement panel)
        {
            _window = window;
            _panel = panel;
        }

        // ============================================================
        //  缓动映射
        // ============================================================

        private static IEasingFunction? CreateEasing(string? type) => type switch
        {
            "Linear" => null,
            "QuadraticEase" => new QuadraticEase { EasingMode = EasingMode.EaseOut },
            "QuarticEase" => new QuarticEase { EasingMode = EasingMode.EaseOut },
            "QuinticEase" => new QuinticEase { EasingMode = EasingMode.EaseOut },
            "ElasticEase" => new ElasticEase { EasingMode = EasingMode.EaseOut },
            "BackEase" => new BackEase { EasingMode = EasingMode.EaseOut },
            "BounceEase" => new BounceEase { EasingMode = EasingMode.EaseOut },
            _ => new CubicEase { EasingMode = EasingMode.EaseOut },
        };

        // ============================================================
        //  动画辅助
        // ============================================================

        private void StopPositionAnimations()
        {
            _window.BeginAnimation(Window.LeftProperty, null);
            _window.BeginAnimation(Window.TopProperty, null);
        }

        private void StopSizeAnimations()
        {
            _window.BeginAnimation(Window.WidthProperty, null);
            _window.BeginAnimation(Window.HeightProperty, null);
        }

        private void StopOpacityAnimation()
        {
            _panel.BeginAnimation(UIElement.OpacityProperty, null);
        }

        private void StopAllAnimations()
        {
            StopPositionAnimations();
            StopSizeAnimations();
            StopOpacityAnimation();
        }

        /// <summary>WPF 动画到目标，结束时锁定终值并移除动画时钟。</summary>
        private static void Animate(IAnimatable target, DependencyProperty prop, double to, int ms,
                                    IEasingFunction? easing, EventHandler? completed = null)
        {
            var anim = new DoubleAnimation(to, new Duration(TimeSpan.FromMilliseconds(Math.Max(1, ms))))
            {
                EasingFunction = easing,
                FillBehavior = FillBehavior.HoldEnd
            };
            anim.Completed += (_, _) =>
            {
                target.BeginAnimation(prop, null);
                ((DependencyObject)target).SetValue(prop, to);
            };
            if (completed != null) anim.Completed += completed;
            target.BeginAnimation(prop, anim);
        }

        private void SetDirect(double? left = null, double? top = null, double? width = null, double? height = null, double? opacity = null)
        {
            StopAllAnimations();
            StopRenderingLoop();
            RestorePanelContentLayout();
            _followActive = false;
            _switching = false;
            if (left.HasValue) _window.Left = left.Value;
            if (top.HasValue) _window.Top = top.Value;
            if (width.HasValue) _window.Width = width.Value;
            if (height.HasValue) _window.Height = height.Value;
            if (opacity.HasValue) _panel.Opacity = opacity.Value;
        }

        // ============================================================
        //  渲染帧循环（跟随 / 小鸟依人）
        // ============================================================

        private void EnsureRenderingLoop()
        {
            if (_renderingActive) return;
            _renderingActive = true;
            CompositionTarget.Rendering += OnRendering;
        }

        private void StopRenderingLoop()
        {
            _renderingActive = false;
            _clingMode = false;
            CompositionTarget.Rendering -= OnRendering;
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            // ★ 位置/尺寸动画进行中：渲染循环让位，不写窗口位置——
            //   每帧写本地值会干扰 Window.Left/Top 的动画时钟（滑入/滑出动画被"瞬间化"）。
            //   动画完成后（HasAnimatedProperties=false）再接管位置。
            if (_followActive && _window.HasAnimatedProperties) return;

            if (_followActive && FollowPositionProvider != null)
            {
                var (l, t) = FollowPositionProvider();
                double k = _followLerpOverride ?? _followLerp;
                if (k >= 0.999)
                {
                    // ★ 绝对跟手（FlyDurationMs 拉满）：每帧直接设置——拖窗口手感
                    if (Math.Abs(_window.Left - l) > 0.01 || Math.Abs(_window.Top - t) > 0.01)
                    {
                        _window.Left = l;
                        _window.Top = t;
                    }
                }
                else
                {
                    // ★ 缓慢飞追（FlyDurationMs 调小）：每帧向目标靠近（lerp），面板缓慢追随鼠标
                    double nl = _window.Left + (l - _window.Left) * k;
                    double nt = _window.Top + (t - _window.Top) * k;
                    if (Math.Abs(nl - l) < 0.5 && Math.Abs(nt - t) < 0.5)
                    {
                        _window.Left = l;
                        _window.Top = t;
                    }
                    else
                    {
                        _window.Left = nl;
                        _window.Top = nt;
                    }
                }
            }
            else if (_clingMode)
            {
                // 小鸟依人：每帧 lerp 平滑趋近（慢速追随，无振荡）
                double nl = _window.Left + (_clingTargetLeft - _window.Left) * ClingLerp;
                double nt = _window.Top + (_clingTargetTop - _window.Top) * ClingLerp;
                if (Math.Abs(nl - _clingTargetLeft) < ClingStopDist && Math.Abs(nt - _clingTargetTop) < ClingStopDist)
                {
                    _window.Left = _clingTargetLeft;
                    _window.Top = _clingTargetTop;
                    _clingMode = false;
                    StopRenderingLoop();
                }
                else
                {
                    _window.Left = nl;
                    _window.Top = nt;
                }
            }
        }

        // ============================================================
        //  设置
        // ============================================================

        public void SetSettings(ISettingsService settings)
        {
            _settings = settings;
            UpdateParametersFromSettings();
        }

        private void UpdateParametersFromSettings()
        {
            if (_settings == null) return;
            _transformDurationMs = Math.Max(30, _settings.TransformDurationMs);
            _showHideDurationMs = Math.Max(30, _settings.ShowHideDurationMs);
            // ★ 跟随松紧：FlyDurationMs=0（不飞）→ 1.0 绝对跟手（立即到达）；调大 → 缓慢飞追
            //   语义：飞行时长越短越跟手（0 时长 = 立即跟随），越长追得越慢
            _followLerp = Math.Max(0.05, Math.Min(1.0, 1.0 - _settings.FlyDurationMs / 2000.0));
            _transformEasing = CreateEasing(_settings.TransformEasingType);
            _showHideEasing = CreateEasing(_settings.ShowHideEasingType);
        }

        public void SetAnimationsEnabled(bool enabled) => _animationsEnabled = enabled;

        // ============================================================
        //  飞行
        // ============================================================

        public void SetFlyParameters(int durationMs) => _flyDurationMs = Math.Max(50, durationMs);

        public void StartFly(double targetLeft, double targetTop)
        {
            if (!_animationsEnabled)
            {
                _window.Left = targetLeft;
                _window.Top = targetTop;
                FlyCompleted?.Invoke();
                return;
            }
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            int ms = _flyDurationMs;
            Animate(_window, Window.LeftProperty, targetLeft, ms, ease);
            Animate(_window, Window.TopProperty, targetTop, ms, ease, (_, _) => FlyCompleted?.Invoke());
        }

        // ============================================================
        //  位置（连续跟随：渲染帧驱动）
        // ============================================================

        /// <summary>激活贴边跟随（渲染帧实时跟手）。调用方设置 FollowPositionProvider 后调用。</summary>
        public bool IsFollowActive => _followActive;

        /// <summary>跟随紧度覆盖：中置状态（乱逛/切换）强制绝对跟手；false 恢复设置映射。</summary>
        public void SetFollowAbsolute(bool absolute)
        {
            _followLerpOverride = absolute ? 1.0 : (double?)null;
        }

        public void StartFollowPosition()
        {
            if (_followActive) return;   // ★ 幂等：跟随已激活不重复停动画/启动循环
            // ★ 不打断位置动画：滑入/滑出动画完整跑完（呼出/隐藏的 800ms 时长真实生效），
            //   动画完成后 provider 自然接管位置。StopPositionAnimations 会把
            //   滑入动画掐断 → 面板瞬间到位（动画时长失效）；也会掐断滑出 → 卡半路黑框。
            RestorePanelContentLayout();
            _clingMode = false;
            _followActive = true;
            EnsureRenderingLoop();
        }

        /// <summary>停止贴边跟随。</summary>
        public void StopFollowPosition()
        {
            _followActive = false;
            if (!_clingMode) StopRenderingLoop();
        }

        public void SetPositionTargetWithoutReset(double left, double top)
        {
            // ★ 贴边跟随：短时长（30ms）WPF 动画重定向——渲染帧 60fps 插值，
            //   目标由 tick（30ms）更新，动画在两次目标间连续插值 → 位置丝滑跟手（无 33fps 步进感）。
            //   每次调用替换旧动画（从当前位置开始），保持连续性。
            StopRenderingLoop();
            StopPositionAnimations();
            // 线性（null easing）→ 30ms 内匀速插值，跟手
            Animate(_window, Window.LeftProperty, left, 30, null);
            Animate(_window, Window.TopProperty, top, 30, null);
        }

        public void SetPositionTarget(double left, double top, bool resetVelocity = false)
        {
            SetPositionTargetWithoutReset(left, top);
        }

        public void JumpToPosition(double left, double top)
        {
            SetDirect(left: left, top: top);
        }

        public void FollowWithInertia(double targetLeft, double targetTop)
        {
            if (!_animationsEnabled)
            {
                _window.Left = targetLeft;
                _window.Top = targetTop;
                return;
            }
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            Animate(_window, Window.LeftProperty, targetLeft, 180, ease);
            Animate(_window, Window.TopProperty, targetTop, 180, ease);
        }

        // ============================================================
        //  小鸟依人（渲染帧 lerp）
        // ============================================================

        public void SetClingParameters()
        {
            StopAllAnimations();
            RestorePanelContentLayout();
            _followActive = false;
            _switching = false;
            _clingMode = true;
            EnsureRenderingLoop();
        }

        public void ExitClingParameters()
        {
            _clingMode = false;
            StopRenderingLoop();
        }

        public void SetClingTarget(double left, double top)
        {
            _clingTargetLeft = left;
            _clingTargetTop = top;
            // ★ 跟随让位给 cling：隐藏滑出/小鸟依人由渲染帧循环接管位置（不可被打断）
            _followActive = false;
            _clingMode = true;
            EnsureRenderingLoop();
        }

        // ============================================================
        //  尺寸
        // ============================================================

        public void SetSizeDirect(double width, double height)
        {
            var (screenWidth, screenHeight) = ScreenSize;
            width = Math.Max(10, Math.Min(width, screenWidth));
            height = Math.Max(10, Math.Min(height, screenHeight));
            SetDirect(width: width, height: height);
        }

        public void SetSizeKeepPositionDirect(double width, double height)
        {
            var (screenWidth, screenHeight) = ScreenSize;
            width = Math.Max(10, Math.Min(width, screenWidth));
            height = Math.Max(10, Math.Min(height, screenHeight));
            StopSizeAnimations();
            RestorePanelContentLayout();
            _window.Width = width;
            _window.Height = height;
        }

        // ============================================================
        //  透明度
        // ============================================================

        public void SetOpacityTarget(double opacity, bool resetVelocity = false)
        {
            opacity = Math.Max(0, Math.Min(1, opacity));
            if (!_animationsEnabled)
            {
                _panel.Opacity = opacity;
                return;
            }
            Animate(_panel, UIElement.OpacityProperty, opacity, _showHideDurationMs, _showHideEasing);
        }

        public void SetOpacityDirect(double opacity)
        {
            _panel.Opacity = Math.Max(0, Math.Min(1, opacity));
            StopOpacityAnimation();
        }

        // ============================================================
        //  滑入 / 滑出（尺寸不变 → WPF 动画本就丝滑）
        // ============================================================

        /// <summary>
        /// 呼出/隐藏动画（触发/隐藏类型）：Slide=滑入滑出+淡入淡出；Fade=仅透明度（位置直接到位）；
        /// Zoom=缩放（zoom→1 或 1→zoom）+淡入淡出；Elastic=滑入滑出带弹性；Custom=占位（暂按 Slide）。
        /// </summary>
        public void SetShowHideTarget(double left, double top, double opacity, bool allowOffscreen = false,
            string animType = "Slide", int durationMs = 0,
            double zoom = 0.5, int oscillations = 3, double springiness = 3)
        {
            _panel.RenderTransform = null;
            StopRenderingLoop();
            RestorePanelContentLayout();
            _followActive = false;
            _switching = false;
            opacity = Math.Max(0, Math.Min(1, opacity));
            if (!_animationsEnabled)
            {
                _window.Left = left;
                _window.Top = top;
                _panel.Opacity = opacity;
                return;
            }
            int ms = Math.Max(30, durationMs > 0 ? durationMs : _showHideDurationMs);
            // ★ 平滑缓动（滑入/淡入/缩放用；不再用旧 ShowHideEasingType——那可能是 ElasticEase，
            //   起步极快导致位置瞬间到位，动画时长观感失效）
            var smooth = new CubicEase { EasingMode = EasingMode.EaseOut };
            switch (animType)
            {
                case "Fade":
                    // 淡入/淡出：位置直接到位，仅透明度动画
                    _window.Left = left;
                    _window.Top = top;
                    Animate(_panel, UIElement.OpacityProperty, opacity, ms, smooth);
                    break;

                case "Zoom":
                    // 缩放：位置到位 + 面板缩放（呼出 zoom→1，隐藏 1→zoom）+ 透明度
                    _window.Left = left;
                    _window.Top = top;
                    var st = new ScaleTransform(zoom, zoom, _panel.ActualWidth / 2, _panel.ActualHeight / 2);
                    _panel.RenderTransform = st;
                    Animate(st, ScaleTransform.ScaleXProperty, opacity <= 0 ? zoom : 1.0, ms, smooth);
                    Animate(st, ScaleTransform.ScaleYProperty, opacity <= 0 ? zoom : 1.0, ms, smooth);
                    Animate(_panel, UIElement.OpacityProperty, opacity, ms, smooth);
                    break;

                case "Elastic":
                    // 弹性：滑入滑出带弹性回弹（振荡/弹性强度由特化参数决定）
                    var elastic = new ElasticEase
                    {
                        EasingMode = EasingMode.EaseOut,
                        Oscillations = Math.Max(1, oscillations),
                        Springiness = Math.Max(1, springiness)
                    };
                    Animate(_window, Window.LeftProperty, left, ms, elastic);
                    Animate(_window, Window.TopProperty, top, ms, elastic);
                    Animate(_panel, UIElement.OpacityProperty, opacity, ms, elastic);
                    break;

                case "Slide":
                case "Custom":
                default:
                    // ★ 滑入滑出：纯位置动画，不带透明度渐变（淡入淡出是"淡入/淡出"类型的特性）。
                    //   呼出：透明度直接设为不透明（无渐变），滑入过程完整可见；
                    //   隐藏：透明度保持不变（滑出过程可见），窗口滑出屏幕后自然不可见。
                    if (opacity > 0.5) _panel.Opacity = opacity;
                    Animate(_window, Window.LeftProperty, left, ms, smooth);
                    Animate(_window, Window.TopProperty, top, ms, smooth);
                    break;
            }
        }

        public void SetShowHideDirect(double left, double top, double opacity)
        {
            SetDirect(left: left, top: top, opacity: opacity);
        }

        // ============================================================
        //  切换（区域切换 / 飞行落地）：
        //  尺寸一次到位（窗口仅 resize 一次，Mica/内容不每帧重绘 → 不闪烁），
        //  内容 ScaleTransform 平滑缩放过渡 + 位置 WPF 动画（渲染帧丝滑）
        // ============================================================

        public bool IsTransformAnimating => _switching;

        /// <summary>
        /// 位置动画 + 尺寸直接到位（图标中置方案：切换期间不做尺寸形变动画，减少卡顿）。
        /// 尺寸一次 resize（Mica 背景暂切不透明，无重采样闪烁），位置平滑移动。
        /// </summary>
        public void SetPositionAnimateSizeDirect(double left, double top, double width, double height)
        {
            if (!_animationsEnabled)
            {
                SetDirect(left: left, top: top, width: width, height: height);
                return;
            }
            StopRenderingLoop();
            StopAllAnimations();
            _followActive = false;
            RestorePanelContentLayout();

            var (screenWidth, screenHeight) = ScreenSize;
            double cw = Math.Max(10, Math.Min(width, screenWidth));
            double ch = Math.Max(10, Math.Min(height, screenHeight));

            // 尺寸直接到位（一次 resize）：背景切不透明避免 Mica 重采样闪烁
            SuspendMicaBackdrop();
            _window.Width = cw;
            _window.Height = ch;

            int ms = Math.Max(1, _transformDurationMs);
            var ease = _transformEasing;
            _switching = true;
            Animate(_window, Window.LeftProperty, left, ms, ease);
            Animate(_window, Window.TopProperty, top, ms, ease, (_, _) =>
            {
                RestoreMicaBackdrop();
                _switching = false;
            });
        }

        /// <summary>仅位置动画（尺寸不动）：快速切换期间不形变，跟手；尺寸由稳定后的形变动画负责。</summary>
        public void SetPositionAnimate(double left, double top)
        {
            if (!_animationsEnabled)
            {
                _window.Left = left;
                _window.Top = top;
                return;
            }
            StopRenderingLoop();
            StopAllAnimations();
            _followActive = false;
            RestorePanelContentLayout();

            int ms = Math.Max(1, _transformDurationMs);
            var ease = _transformEasing;
            _switching = true;
            Animate(_window, Window.LeftProperty, left, ms, ease);
            Animate(_window, Window.TopProperty, top, ms, ease, (_, _) => _switching = false);
        }

        /// <summary>仅尺寸形变动画（防抖稳定后调用）：平滑形变到目标尺寸，完成后回调（用于内容变实）。</summary>
        public void AnimateSizeTo(double width, double height, Action? completed = null)
        {
            var (screenWidth, screenHeight) = ScreenSize;
            double cw = Math.Max(10, Math.Min(width, screenWidth));
            double ch = Math.Max(10, Math.Min(height, screenHeight));
            if (!_animationsEnabled)
            {
                _window.Width = cw;
                _window.Height = ch;
                completed?.Invoke();
                return;
            }
            // ★ 形变期间置 _switching：IsTransformAnimating=true，
            //   业务层（如 OnPanelContentChanged 的 ApplyAutoSize）据此跳过
            //   原子尺寸跳变——避免"动画形变 + 原子 SetWindowPos"同时执行导致尺寸横跳闪烁
            SuspendMicaBackdrop();
            _switching = true;
            int ms = Math.Max(1, _transformDurationMs);
            var ease = _transformEasing;
            Animate(_window, Window.WidthProperty, cw, ms, ease);
            Animate(_window, Window.HeightProperty, ch, ms, ease, (_, _) =>
            {
                _switching = false;
                RestoreMicaBackdrop();
                completed?.Invoke();
            });
        }

        public void SetPositionAndSizeTarget(double left, double top, double width, double height)
        {
            if (!_animationsEnabled)
            {
                SetDirect(left: left, top: top, width: width, height: height);
                return;
            }
            StopRenderingLoop();
            StopAllAnimations();
            _followActive = false;
            RestorePanelContentLayout();

            // ★ 面板与内容解耦的尺寸动画：
            //   面板层：窗口真实尺寸+位置 WPF 动画（渲染帧 60fps 插值），
            //           Mica 动画期间背景切不透明（盖住重采样，不动 backdrop）；
            //   内容层：动画期间固定布局尺寸 + ScaleTransform 缩放跟随窗口比例
            //           （内容不每帧重排，布局开销不阻塞动画帧率），
            //           动画结束一次性真实布局——内容不影响面板丝滑。
            double oldW = Math.Max(1, _window.Width);
            double oldH = Math.Max(1, _window.Height);

            var (screenWidth, screenHeight) = ScreenSize;
            double cw = Math.Max(10, Math.Min(width, screenWidth));
            double ch = Math.Max(10, Math.Min(height, screenHeight));

            SuspendMicaBackdrop();

            // 内容层：固定尺寸 + 居中 + 缩放跟随（ScaleX/ScaleY 与窗口动画同缓动同时长 → 每帧同步）
            _panel.Width = oldW;
            _panel.Height = oldH;
            _panel.HorizontalAlignment = HorizontalAlignment.Center;
            _panel.VerticalAlignment = VerticalAlignment.Center;
            var st = new ScaleTransform(1, 1);
            _panel.RenderTransform = st;
            _panel.RenderTransformOrigin = new Point(0.5, 0.5);

            int ms = Math.Max(1, _transformDurationMs);
            var ease = _transformEasing;
            _switching = true;

            var animSX = new DoubleAnimation(cw / oldW, new Duration(TimeSpan.FromMilliseconds(ms)))
            {
                EasingFunction = ease,
                FillBehavior = FillBehavior.HoldEnd
            };
            var animSY = new DoubleAnimation(ch / oldH, new Duration(TimeSpan.FromMilliseconds(ms)))
            {
                EasingFunction = ease,
                FillBehavior = FillBehavior.HoldEnd
            };
            animSX.Completed += (_, _) => { };
            animSY.Completed += (_, _) => { };
            st.BeginAnimation(ScaleTransform.ScaleXProperty, animSX);
            st.BeginAnimation(ScaleTransform.ScaleYProperty, animSY);

            // ★ 完成判定绑定尺寸动画（Width）：位置动画可能 0 距离瞬间完成（如仅横向切换），
            //   不能作为"切换完成"依据（否则 switching 提前复位、跟随提前恢复、Mica 提前恢复）
            Animate(_window, Window.WidthProperty, cw, ms, ease, (_, _) =>
            {
                // 动画完成：内容层恢复真实布局（拉伸 + 缩放归零），恢复 Mica 毛玻璃
                _panel.RenderTransform = null;
                _panel.Width = double.NaN;
                _panel.Height = double.NaN;
                _panel.HorizontalAlignment = HorizontalAlignment.Stretch;
                _panel.VerticalAlignment = VerticalAlignment.Stretch;
                RestoreMicaBackdrop();
                _switching = false;
            });
            Animate(_window, Window.HeightProperty, ch, ms, ease);
            Animate(_window, Window.LeftProperty, left, ms, ease);
            Animate(_window, Window.TopProperty, top, ms, ease);
        }

        public void SetSizeTarget(double width, double height)
        {
            if (!_animationsEnabled)
            {
                var (screenWidth, screenHeight) = ScreenSize;
                _window.Width = Math.Max(10, Math.Min(width, screenWidth));
                _window.Height = Math.Max(10, Math.Min(height, screenHeight));
                return;
            }
            SetPositionAndSizeTarget(_window.Left, _window.Top, width, height);
        }

        public void SetPositionAndSizeDirect(double left, double top, double width, double height)
        {
            var (screenWidth, screenHeight) = ScreenSize;
            width = Math.Max(10, Math.Min(width, screenWidth));
            height = Math.Max(10, Math.Min(height, screenHeight));
            SetDirect(left: left, top: top, width: width, height: height);
        }

        public void SetPositionAndSizeWithoutReset(double left, double top, double width, double height)
        {
            SetPositionAndSizeDirect(left, top, width, height);
        }

        public void JumpTo(double left, double top, double width, double height)
        {
            var (screenWidth, screenHeight) = ScreenSize;
            width = Math.Max(10, Math.Min(width, screenWidth));
            height = Math.Max(10, Math.Min(height, screenHeight));
            SetDirect(left: left, top: top, width: width, height: height);
        }

        // ============================================================
        //  停止 / 兼容入口
        // ============================================================

        public void StopAll()
        {
            StopAllAnimations();
            StopRenderingLoop();
            RestorePanelContentLayout();
            _followActive = false;
            _switching = false;
        }

        public void SetTarget(double left, double top, double width, double height, bool resetVelocity = true)
        {
            SetPositionAndSizeTarget(left, top, width, height);
        }

        public void SetTargetPositionAndSizeWithoutReset(double left, double top, double width, double height)
        {
            SetPositionAndSizeTarget(left, top, width, height);
        }

        public void SetTargetSizeWithoutReset(double width, double height)
        {
            SetSizeTarget(width, height);
        }

        public void SetImmediate(double width, double height, double left, double top)
        {
            SetPositionAndSizeDirect(left, top, width, height);
        }

        public void AnimateTo(double width, double height, double left, double top, int durationMs)
        {
            SetFlyParameters(durationMs);
            StartFly(left, top);
        }

        // ============================================================
        //  屏幕尺寸
        // ============================================================

        private (double width, double height) ScreenSize
        {
            get
            {
                try
                {
                    var wa = DynamicBird.Infrastructure.Utils.ScreenMetrics.GetCachedScreenForWindow(
                        _window.Left, _window.Top, _window.Width, _window.Height);
                    return (wa.Width, wa.Height);
                }
                catch
                {
                    return (SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            CompositionTarget.Rendering -= OnRendering;
            StopAllAnimations();
        }
    }
}
