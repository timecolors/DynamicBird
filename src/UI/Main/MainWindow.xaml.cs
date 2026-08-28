using DynamicBird.Animation;
using DynamicBird.Core;
using DynamicBird.Core.Controllers;
using DynamicBird.Core.Detection;
using DynamicBird.Core.Infrastructure.Logging;
using DynamicBird.Core.Infrastructure.Service;
using DynamicBird.Core.Services;
using DynamicBird.Core.Services.Configuration;
using DynamicBird.src.core.Services.Clipboard;
using DynamicBird.src.core.Services.Notes;
using DynamicBird.src.core.Services.Shortcuts;
using DynamicBird.src.core.Services.System;
using DynamicBird.UI.Theme;
using DynamicBird.UI.Panels;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace DynamicBird.UI.Main
{
    public partial class MainWindow : Window
    {
        private ISettingsService _settingsService = null!;
        private IModeService _modeService = null!;
        private IShortcutService _shortcutService = null!;
        private INoteService _noteService = null!;
        private IClipboardService _clipboardService = null!;
        private TrayIconManager _trayManager = null!;

        private EdgeTriggerController _edgeController = null!;
        private PanelVisibilityController _visibilityController = null!;
        private WindowSizeController _sizeController = null!;
        private PanelContentController _contentController = null!;
        private ShapeAnimator _shapeAnimator = null!;
        private DragController _dragController = null!;

        private double _currentTaskbarHeight = 40;
        private DispatcherTimer? _edgeTimer;
        private bool _isDragging = false;
        private int _edgeTickCount = 0;

        // ★ 图标中置状态（内容切换期间的视觉锚点：图标居中 + 内容静默，防抖稳定后归位）
        private bool _iconCentered;
        private System.Windows.Threading.DispatcherTimer? _stabilizeTimer;
        private bool _stabilizeSizeHookActive;
        private DateTime _lastFollowMoveTime = DateTime.MinValue;

        // ★ 自适应 tick 频率：鼠标静止时降频省 CPU（30ms → 100ms），移动时恢复
        private int _lastTickMouseX = int.MinValue;
        private int _lastTickMouseY = int.MinValue;
        private int _idleTickCount = 0;
        private const int IdleThresholdTicks = 10;   // 静止约 300ms 后降频
        private const int ActiveIntervalMs = 30;
        private const int IdleIntervalMs = 100;

        public MainWindow()
        {
            Icon = AppIconHelper.LoadAppIcon();
            try
            {
                InitializeComponent();
                // ★ 窗口尺寸/位置变化时立即重设圆角区域，避免刚触发面板时短暂显示直角
                SizeChanged += (_, _) =>
                {
                    // ★ Win11 22H2+：DWM 原生圆角由系统维护，无需（也不能）用 SetWindowRgn 重设
                    if (_useDwmCorner) return;
                    // ★ atBottom 判断用主屏整屏高度（SystemParameters.PrimaryScreenHeight 稳定精确）。
                    //   底部点击穿透条只服务主任务栏呼出条，多屏副屏无需挖条；此处若用带缓存的
                    //   Screen 查询会在贴边高频触发时因缓存延迟造成圆角区域闪烁/漏挖。
                    bool atBottom = Math.Abs((Top + Height) - SystemParameters.PrimaryScreenHeight) < 1.0;
                    ApplyWindowRegion(atBottom && Height > BottomStripClickThroughPx + 2);
                };
                // ★ 非透明窗口：句柄创建后应用圆角窗口区域（尺寸变化由周期刷新覆盖）；
                //   Win11 22H2+ 尝试启用 Mica 背景，让面板透出系统毛玻璃材质
                SourceInitialized += (_, _) =>
                {
                    // ★ 启动时把窗口移到屏幕外右下角：即使透明度异常也不会在屏幕上露头或拦截鼠标
                    //   主屏屏幕外（启动时窗口尚未挂到任何显示器，主屏是唯一确定选择）
                    Left = SystemParameters.PrimaryScreenWidth + 60;
                    Top = SystemParameters.PrimaryScreenHeight + 60;

                    var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                    if (TryApplyWin11FluentMaterial(hwnd))
                    {
                        // ★ Win11 22H2+：Fluent 材质 —— 半透明深色背景透出 Mica，MainPanel 让出背景，
                        //   通知动画器：尺寸动画期间需临时禁用 backdrop 防闪烁
                        _shapeAnimator?.SetMicaBackdropEnabled(true);
                        //   DWM 原生圆角替代 SetWindowRgn（圆角外透明+点击穿透，无白角/黑块）。
                        //   窗口与面板背景统一半透明：Mica 透出量 = alpha 层（0xE0 ≈ 12%）。
                        Background = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromArgb(0xE0, 0x2D, 0x2D, 0x2D));
                        MainPanel.Background = System.Windows.Media.Brushes.Transparent;
                        MainPanel.CornerRadius = new CornerRadius(8);
                    }
                    else
                    {
                        // ★ Win10/旧版：不透明深色 + SetWindowRgn 圆角（无 Mica，行为稳定）
                        ApplyWindowRegion(false);
                    }
                    if (hwnd != IntPtr.Zero)
                    {
                        System.Windows.Interop.HwndSource.FromHwnd(hwnd)?.AddHook(HotkeyWndProc);
                        RegisterGlobalHotkey(hwnd);
                    }
                };

                LogManager.Info("=== 灵动鸟启动 ===");

                if (!InitializeCoreServices())
                {
                    MessageBox.Show("核心服务初始化失败，请查看日志文件", "灵动鸟启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
                    Application.Current.Shutdown();
                    return;
                }

                InitializeUIComponents();
                InitializeExtensions();

                Closed += (s, e) => OnWindowClosed();

                LogManager.Info("主窗口初始化完成");
            }
            catch (Exception ex)
            {
                LogManager.Fatal("窗口启动失败", ex);
                MessageBox.Show($"启动失败:\n{ex.Message}", "灵动鸟错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        private bool InitializeCoreServices()
        {
            try
            {
                LogManager.Info("=== 第一阶段：初始化核心服务 ===");

                _settingsService = new SettingsManager();
                _modeService = new ModeManager(_settingsService);
                _shortcutService = new ShortcutManager();
                _noteService = new NoteManager(_settingsService);
                _clipboardService = new ClipboardManager(_settingsService);
                _trayManager = new TrayIconManager(this, OpenSettings, ToggleWindow, ExitApp);

                ServiceManager.Instance
                    .Register((IService)_settingsService)
                    .Register((IService)_modeService)
                    .Register((IService)_shortcutService)
                    .Register((IService)_noteService)
                    .Register((IService)_clipboardService)
                    .Register(_trayManager);

                ServiceManager.Instance.InitializeAll();

                if (ServiceManager.Instance.HasFailedServices())
                {
                    var failed = ServiceManager.Instance.GetFailedServices();
                    LogManager.Warning($"以下服务初始化失败:");
                    foreach (var (service, error) in failed)
                        LogManager.Warning($"  - {service.Name}: {error.Message}");

                    if (failed.Any(f => f.Service is SettingsManager || f.Service is ModeManager))
                    {
                        LogManager.Fatal("核心服务初始化失败");
                        return false;
                    }
                }

                _modeService.ModeChanged += OnModeChanged;
                _settingsService.SettingsChanged += OnSettingsChanged;

                return true;
            }
            catch (Exception ex)
            {
                LogManager.Fatal("核心服务初始化异常", ex);
                return false;
            }
        }

        private void InitializeUIComponents()
        {
            try
            {
                LogManager.Info("=== 第二阶段：初始化 UI 组件 ===");

                ApplyMinSize();
                InitializeControllers();
                InitializeContent();

                _edgeController.RegionChanged += OnRegionChanged;
                // ★ 切换触发（内容待加载）：立即进入"图标中置 + 内容静默"，移动期间不加载内容
                _edgeController.SwitchStarted += (_, _) => EnterCenteredState();

                // ★ AI 面板内的“打开设置”按钮
                DynamicBird.UI.AI.AiChatView.OpenSettingsRequested += OpenSettings;
                // ★ 划词翻译 小组件内的“打开设置”按钮
                DynamicBird.UI.Widgets.TextAi.TextAiWidget.OpenSettingsRequested += OpenSettings;

                // ★ 划词翻译 热键注册（SourceInitialized 时服务尚未初始化，这里补一次）
                ReapplyTextAiHotkey();

                _sizeController.UserResizeStarted += (started) =>
                {
                    _isDragging = started;
                    // ★ 拖拽开始立即停止位置/透明度动画，
                    //   避免 ShapeAnimator 物理系统与拖拽同时改位置导致抽搐
                    if (started)
                    {
                        _shapeAnimator.StopAll();
                    }
                };

                StartEdgeTimer();
                DynamicBird.Infrastructure.WinApi.ToastMonitor.Start();
                DynamicBird.Infrastructure.WinApi.RecentAppTracker.Start();
                // ★ 剪贴板监听应用级常驻：任何来源的复制（含 AI 面板“复制”按钮）都会进入历史
                _clipboardService.StartListening();
                CheckForUpdatesAsync();
                // 首次启动：等界面就绪后弹出引导窗口
                Dispatcher.BeginInvoke(new Action(ShowOnboardingIfNeeded),
                    System.Windows.Threading.DispatcherPriority.ApplicationIdle);

                LogManager.Info("UI 组件初始化完成");
            }
            catch (Exception ex)
            {
                LogManager.Error("UI 组件初始化失败", ex);
            }
        }

        private void InitializeExtensions()
        {
            try
            {
                LogManager.Info("=== 第三阶段：初始化扩展功能 ===");

                try
                {
                    _trayManager.Initialize();
                    LogManager.Debug("托盘图标初始化成功");
                }
                catch (Exception ex)
                {
                    LogManager.Error("托盘图标初始化失败，继续运行", ex);
                }

                LogManager.Info("扩展功能初始化完成");
            }
            catch (Exception ex)
            {
                LogManager.Error("扩展功能初始化失败", ex);
            }
        }

        private void ApplyMinSize()
        {
            try
            {
                double iconSize = Math.Max(22, _settingsService.TaskbarIconSize);
                MinHeight = Math.Max(80, 4 + 12 + 34 + iconSize + 8);
                MinWidth = Math.Max(120, 22 + 12 + 10 + 60);
            }
            catch (Exception ex)
            {
                LogManager.Error("计算最小尺寸失败", ex);
                MinHeight = 80;
                MinWidth = 120;
            }
        }

        private void InitializeControllers()
        {
            try
            {
                // ★★★ Step 1: 创建 ShapeAnimator 和 VisibilityController ★★★
                _shapeAnimator = new ShapeAnimator(this, MainPanel);
                _shapeAnimator.SetSettings(_settingsService);
                _shapeAnimator.SetAnimationsEnabled(_settingsService.AnimationsEnabled);
                _currentTaskbarHeight = GetTaskbarHeight();

                _visibilityController = new PanelVisibilityController(
                    this, MainPanel, _shapeAnimator, _settingsService, _currentTaskbarHeight);
                _visibilityController.PanelHidden += OnPanelHidden;
                _visibilityController.PanelShown += OnPanelShown;

                // ★★★ Step 2: ★★★ 获取底部边界（任务栏顶部坐标，DIP 单位） ★★★
                double bottomBoundary = GetTaskbarTopInDips();

                // ★★★ 任务栏高度统一换算为 DIP（GetTaskbarHeight 返回的是物理像素） ★★★
                double dpiScale = 1.0;
                try
                {
                    dpiScale = VisualTreeHelper.GetDpi(this).DpiScaleX;
                }
                catch { }
                if (dpiScale <= 0 || double.IsNaN(dpiScale) || double.IsInfinity(dpiScale))
                {
                    dpiScale = 1.0;
                }
                // 注意：_currentTaskbarHeight 保持物理像素（MouseLeaveDetector 使用屏幕物理坐标）；
                // 尺寸计算统一使用 DIP 高度。
                double taskbarHeightDips = GetTaskbarHeight() / dpiScale;

                // ★★★ Step 3: 创建 EdgeTriggerController（传入 bottomBoundary 与任务栏 DIP 高度） ★★★
                _edgeController = new EdgeTriggerController(
                    this, _shapeAnimator, null, _visibilityController, _settingsService, bottomBoundary, taskbarHeightDips);

                // ★★★ Step 4: 创建 WindowSizeController（传入 _edgeController） ★★★
                _sizeController = new WindowSizeController(
                    this, ContentContainer, MainPanel, taskbarHeightDips, _settingsService, _edgeController, bottomBoundary);

                // ★★★ Step 5: 附加尺寸控制器（解除循环依赖，替代反射注入） ★★★
                _edgeController.SetSizeController(_sizeController);

                // ★★★ Step 5b: 面板贴屏幕边侧的拖拽手柄在"有效边缘触发带"内让位 ★★★
                _sizeController.SetEdgeBandRegionEnabled(IsRegionEnabledBySettings);

                // ★★★ Step 6: 创建其余控制器 ★★★
                _contentController = new PanelContentController(
                    ContentContainer, _settingsService, _shortcutService, _noteService, _clipboardService, _modeService);
                _contentController.ContentChanged += OnPanelContentChanged;

                _dragController = new DragController(
                    this, MainPanel, _edgeController, _visibilityController, _settingsService);
                // ★ 面板拖动同样在有效边缘触发带内让位
                _dragController.RegionEnabledCheck = IsRegionEnabledBySettings;

                LogManager.Debug($"所有控制器已初始化, 底部边界 = {bottomBoundary}");
            }
            catch (Exception ex)
            {
                LogManager.Error("控制器初始化失败", ex);
                throw;
            }
        }

        private void InitializeContent()
        {
            try
            {
                _contentController.LoadContentForRegion("Taskbar");
                Left = SystemParameters.WorkArea.Width - Width - 10;
                Top = SystemParameters.WorkArea.Height - Height - 10;
                ApplyAppearance();
                RefreshSystemStatus();
                LogManager.Debug("内容初始化完成");
            }
            catch (Exception ex)
            {
                LogManager.Error("内容初始化失败", ex);
                throw;
            }
        }

        private void StartEdgeTimer()
        {
                _edgeTimer = new DispatcherTimer();
                _edgeTimer.Interval = TimeSpan.FromMilliseconds(30);
                _edgeTimer.Tick += (s, e) =>
                {
                    // ★ 自适应频率：鼠标静止时降频（100ms），移动时恢复（30ms）。
                    //   静止期间面板不需要高频跟随，省 CPU；移动时立即回到 30ms 保证跟手。
                    // ★ 自适应频率：鼠标静止时降频（100ms），移动时恢复（30ms）。
                    //   静止判定带迟滞（连续 IdleThresholdTicks 次静止才降频，移动立即恢复），
                    //   避免鼠标微抖（2-4px 抖动）导致 30/100ms 间隔乒乓切换。
                    var cursorNow = System.Windows.Forms.Cursor.Position;
                    // ★ 用 long 计算差值：_lastTickMouseX/Y 初始为 int.MinValue，
                    //   鼠标移到屏幕顶部/左边（坐标 0）时 int 减法会溢出回绕成 int.MinValue，
                    //   再 Math.Abs(int.MinValue) 抛 OverflowException（未处理异常弹窗）。
                    bool mouseMoved =
                        _lastTickMouseX == int.MinValue || _lastTickMouseY == int.MinValue ||
                        Math.Abs((long)cursorNow.X - _lastTickMouseX) > 4 ||
                        Math.Abs((long)cursorNow.Y - _lastTickMouseY) > 4;
                    _lastTickMouseX = cursorNow.X;
                    _lastTickMouseY = cursorNow.Y;

                    if (mouseMoved)
                    {
                        _idleTickCount = 0;
                        if (_edgeTimer.Interval.TotalMilliseconds != ActiveIntervalMs)
                            _edgeTimer.Interval = TimeSpan.FromMilliseconds(ActiveIntervalMs);
                        // ★ 中置状态：记录"上次移动时间"（绕圈/乱逛持续移动 → 永不判稳）
                        if (_iconCentered) _lastFollowMoveTime = DateTime.Now;
                    }
                    else if (_iconCentered &&
                             (DateTime.Now - _lastFollowMoveTime).TotalMilliseconds > _settingsService.ContentStabilizeMs)
                    {
                        // ★ 鼠标真正停下（超过稳定时长无移动）→ 结束中置：加载内容 + 形变 + 归位 + 变实
                        ExitCenteredState();
                    }
                    else if (_idleTickCount++ > IdleThresholdTicks &&
                             _edgeTimer.Interval.TotalMilliseconds != IdleIntervalMs)
                    {
                        _edgeTimer.Interval = TimeSpan.FromMilliseconds(IdleIntervalMs);
                    }

                    // 每约 150ms 刷新一次任务栏边界（任务栏自动隐藏/升起时跟随）
                    _edgeTickCount++;
                    if (_edgeTickCount % 5 == 0)
                    {
                        RefreshTaskbarBoundary();
                    }

                    if (_modeService.IsDoNotDisturb) return;

                try
                {
                    // ★ 按住穿透修饰键（Ctrl/Alt/Shift）时，面板窗口鼠标穿透，可点击面板下方的屏幕内容
                    UpdatePassthroughState();

                    var point = System.Windows.Forms.Cursor.Position;

                    // DPI 缩放
                    double dpiScale = 1.0;
                    var presentationSource = PresentationSource.FromVisual(this);
                    if (presentationSource?.CompositionTarget != null)
                    {
                        dpiScale = presentationSource.CompositionTarget.TransformToDevice.M11;
                    }
                    if (dpiScale <= 0 || double.IsNaN(dpiScale) || double.IsInfinity(dpiScale))
                    {
                        dpiScale = 1.0;
                    }

                    double mouseX = point.X / dpiScale;
                    double mouseY = point.Y / dpiScale;

                    // ★ 多显示器：屏幕边界取"鼠标所在显示器"的工作区，而非主屏常量。
                    //   面板跟随鼠标时跨屏触发正确落在对应显示器边缘。
                    var workArea = DynamicBird.Infrastructure.Utils.ScreenMetrics
                        .GetCachedScreenForPoint(mouseX, mouseY);
                    double screenW = workArea.Width;
                    double screenH = workArea.Height;

                    // ★ 右上角：仅当显式配置为"窗口操作中心"时放行触发（否则保持安全区不呼出）
                    bool allowTopRight = _settingsService.GetRegionPanel("TopRight") == "WindowControl";
                    EdgeRegion region = EdgeStateDetector.DetectRegion(mouseX, mouseY, screenW, screenH,
                        _settingsService.TriggerDistancePx, allowTopRight);

                    // ★ 触发位置设置过滤：被勾掉的边缘/角落不呼出面板
                    if (region != EdgeRegion.Unknown && !IsRegionEnabledBySettings(region))
                    {
                        region = EdgeRegion.Unknown;
                    }

                    bool isInsidePanel = IsMouseInsidePanel(mouseX, mouseY);

                    // ★ 面板贴着屏幕边缘时，边缘触发带优先：鼠标在屏幕边缘（region 有效）
                    //   即使落在面板贴边区域，也按边缘处理（可切换内容/呼出），
                    //   避免"想切内容却变成拖拽面板"。面板内侧（离边缘超过触发距离）仍为正常交互区。
                    //   例外：小鸟依人模式开启时此逻辑不生效——面板追上鼠标停在边缘带内时，
                    //   不能被 ProcessRegion 拉去贴边（否则"跟随→贴边→再跟随"反复横跳）。
                    if (isInsidePanel && region != EdgeRegion.Unknown && !_settingsService.ClingModeEnabled)
                    {
                        isInsidePanel = false;
                    }

                    // ★ 小鸟依人进行中：面板专心跟随鼠标，本 tick 不再响应边缘触发，
                    //   避免"跟随到屏幕边缘 → 立即被贴边逻辑接管"的打架。
                    if (_edgeController.IsInClinging())
                    {
                        _edgeController.UpdateClinging(mouseX, mouseY);
                        return;
                    }

                    // ★ 鼠标在面板内时保持当前模式：不再响应边缘切换，
                    //   避免“从角落/边缘划向面板时碰到其他边导致内容被意外切换”；
                    //   但仍跟随边缘滑动实时更新位置
                    if (isInsidePanel && _visibilityController.IsVisible)
                    {
                        // 用户已直接用鼠标与面板交互：解除热键钉住，恢复自动隐藏
                        _visibilityController.SetHotkeyPinned(false);
                        _visibilityController.CancelHide();
                        _visibilityController.UpdateEdge(_edgeController.CurrentEdge);
                        // ★ 鼠标在面板内且不在边缘（region Unknown）→ 停止贴边跟随，
                        //   否则面板会被跟随逻辑拉回边缘，用户无法在面板内操作
                        if (region == EdgeRegion.Unknown) _edgeController.StopFollowPosition();
                        _edgeController.FollowMouseInPanel(region, mouseX, mouseY, screenW, screenH);
                    }
                    else if (region != EdgeRegion.Unknown)
                    {
                        // 鼠标回到屏幕边缘：恢复正常的边缘触发行为。
                        // ★ 显示/切换/跟随/延时全部由 ProcessRegion 内聚处理，主窗口不再做显示决策
                        _visibilityController.SetHotkeyPinned(false);
                        // ★ 鼠标仍贴在有效边缘上 = 面板继续使用中：
                        //   MouseLeave 可能在"鼠标离开面板但仍在边缘带内"时启动了隐藏延时，
                        //   若不清除，鼠标滑到右上角安全区等 region 变 Unknown 的瞬间，
                        //   CheckHideDelayTimeout 会立即到期隐藏（绕圈经过右上角面板闪没）。
                        //   鼠标真正离开边缘后由下方 else 分支重新 HideWithDelay 起算，语义不变。
                        _visibilityController.CancelHide();
                        _edgeController.ProcessRegion(region, mouseX, mouseY, screenW, screenH);
                    }
                    else
                    {
                        // ★ 右上角安全区（保留"不呼出面板"的语义，但已显示的面板不隐藏）：
                        //   鼠标快速滑过右上角时，DetectRegion 返回 Unknown 会落入本分支，
                        //   若走下方隐藏逻辑，已显示的面板会滑出再滑回（绕圈闪没）。
                        //   安全区内只停止跟随（面板停在安全区外，不遮挡关闭按钮），
                        //   不触发隐藏；鼠标离开安全区后由 ProcessRegion 恢复正常跟随。
                        bool allowTopRightPanel = _settingsService.GetRegionPanel("TopRight") == "WindowControl";
                        if (!allowTopRightPanel &&
                            mouseX >= screenW - DynamicBird.Core.Detection.EdgeStateDetector.TOP_RIGHT_SAFE_ZONE_X &&
                            mouseY <= DynamicBird.Core.Detection.EdgeStateDetector.TOP_RIGHT_SAFE_ZONE_Y)
                        {
                            _edgeController.StopFollowPosition();
                            _visibilityController.CancelHide();
                            return;
                        }

                        // ★ 鼠标不在边缘/面板内：停止贴边跟随（渲染帧循环释放）
                        _edgeController.StopFollowPosition();
                        if (isInsidePanel)
                        {
                            _visibilityController.CancelHide();
                            _visibilityController.UpdateEdge(_edgeController.CurrentEdge);
                        }
                        else
                        {
                            // ★ 隐藏延时检查：鼠标不在任何有效边缘、也不在面板内时才允许隐藏。
                            //   这样"延时期间贴到远边"会先走上面的 ProcessRegion（跨边飞行），
                            //   只有鼠标真的离开了所有边缘（未贴到远边）才按延时隐藏。
                            //   cling 进行中也照常检查：跟随中鼠标若重新进入面板附近会 CancelHide，
                            //   追不上的隐藏由 cling 内部超时（ClingGiveUpMs）管理，两者不冲突。
                            if (!_edgeController.IsDragging && !_edgeController.IsFlying &&
                                _visibilityController.CheckHideDelayTimeout())
                            {
                                // 延时到期：面板已隐藏，本 tick 不再处理（避免同 tick 被边缘触发重新显示）
                                return;
                            }

                            // ★ 尺寸调整/拖拽期间及刚结束后不因鼠标位置触发隐藏
                            if (_edgeController.IsDragging || _edgeController.IsRecentlyDragged)
                            {
                                _visibilityController.CancelHide();
                            }
                            else if (_settingsService.ClingModeEnabled &&
                                     _settingsService.PerformanceMode != "PowerSaver" &&
                                     _visibilityController.IsVisible && !_edgeController.IsInClinging())
                            {
                                // ★ 尝试启动小鸟依人跟随；若因鼠标贴近边缘等被拒绝（false），
                                //   回退到正常隐藏延时——否则面板会悬在屏幕中永不隐藏。
                                bool clinging = _edgeController.StartClinging(mouseX, mouseY);
                                if (!clinging)
                                {
                                    _edgeController.ResetTriggerDelay();
                                    _visibilityController.HideWithDelay();
                                }
                            }
                            else if (!_edgeController.IsFlying &&
                                     (!_settingsService.ClingModeEnabled || !_visibilityController.IsVisible))
                            {
                                // ★ 鼠标离开边缘区域：重置触发延时计时（重新进入需重新停留）
                                _edgeController.ResetTriggerDelay();
                                // ★ 飞行中不触发隐藏，避免隐藏动画与飞行落位冲突（飞完才允许隐藏）
                                _visibilityController.HideWithDelay();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Timer error: {ex.Message}");
                    DynamicBird.Core.Infrastructure.Logging.LogManager.Error("边缘定时器异常", ex);
                }
            };
            _edgeTimer.Start();
            LogManager.Debug("边缘检测定时器已启动 (30ms)");
        }

        private bool IsMouseInsidePanel(double mouseX, double mouseY)
        {
            // ★ 严格面板边界：鼠标离开面板即视为"已离开"（隐藏计时开始）。
            //   原 +5/+10 外扩会让鼠标在面板边缘外 6-10px 仍被当"在面板内"，
            //   导致"停在面板边上不隐藏，移远一点才隐藏"。
            double panelLeft = this.Left;
            double panelTop = this.Top;
            double panelRight = this.Left + this.Width;
            double panelBottom = this.Top + this.Height;

            return mouseX >= panelLeft && mouseX <= panelRight &&
                   mouseY >= panelTop && mouseY <= panelBottom;
        }

        private void ShowOnboardingIfNeeded()
        {
            try
            {
                if (_settingsService.OnboardingCompleted) return;

                var onboarding = new DynamicBird.UI.Onboarding.OnboardingWindow(
                    noMore => _settingsService.OnboardingCompleted = noMore,
                    _settingsService);
                // ★ 非模态显示：引导期间面板功能保持可用（边缘触发等不受影响）
                onboarding.Show();
            }
            catch { }
        }

        /// <summary>启动后异步检查 GitHub 更新；发现新版本时通知坞弹出更新通知。</summary>
        private async void CheckForUpdatesAsync()
        {
            try
            {
                if (!_settingsService.AutoCheckUpdate) return;

                var current = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version
                              ?? new Version(1, 0, 0);
                var info = await DynamicBird.Infrastructure.WinApi.UpdateService
                    .CheckForUpdateAsync(current);
                if (info != null)
                {
                    DynamicBird.Infrastructure.WinApi.ToastMonitor.NotifyUpdateAvailable(info);
                }
            }
            catch { }
        }

        private bool IsRegionEnabledBySettings(EdgeRegion region)
        {
            return region switch
            {
                EdgeRegion.Top_Left or EdgeRegion.Top_Center or EdgeRegion.Top_Right =>
                    _settingsService.IsEdgeEnabled("Top"),
                EdgeRegion.Bottom_Left or EdgeRegion.Bottom_Center or EdgeRegion.Bottom_Right =>
                    _settingsService.IsEdgeEnabled("Bottom"),
                EdgeRegion.Left_Top or EdgeRegion.Left_Center or EdgeRegion.Left_Bottom =>
                    _settingsService.IsEdgeEnabled("Left"),
                EdgeRegion.Right_Top or EdgeRegion.Right_Center or EdgeRegion.Right_Bottom =>
                    _settingsService.IsEdgeEnabled("Right"),
                EdgeRegion.TopLeft => _settingsService.IsCornerEnabled("TopLeft"),
                EdgeRegion.TopRight => _settingsService.IsCornerEnabled("TopRight"),
                EdgeRegion.BottomLeft => _settingsService.IsCornerEnabled("BottomLeft"),
                EdgeRegion.BottomRight => _settingsService.IsCornerEnabled("BottomRight"),
                _ => true
            };
        }

        private void OnWindowMouseLeave(object sender, MouseEventArgs e)
        {
            if (_modeService.IsDoNotDisturb) return;
            if (!_visibilityController.IsVisible) return;
            if (_visibilityController.IsLocked) return;

            if (!_visibilityController.IsMouseNearPanel())
            {
                // ★ 鼠标仍在有效边缘触发带内：不启动隐藏延时（快速沿边滑动的跟手滞后
                //   会让鼠标短暂离开窗口矩形，但边缘触发带仍有效——隐藏只允许在
                //   真正离开面板/所有边缘后计时）
                if (IsCursorInActiveEdgeZone()) return;
                _visibilityController.HideWithDelay();
            }
        }

        private void MainPanel_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_sizeController != null && _sizeController.HandleMouseDown(sender, e))
                e.Handled = true;
        }

        private void MainPanel_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_sizeController != null)
                _sizeController.HandleMouseMove(sender, e);
        }

        private void MainPanel_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_sizeController != null)
                _sizeController.HandleMouseUp(sender, e);
        }

        private void OnModeChanged(bool isDndMode)
        {
            try
            {
                if (isDndMode && _visibilityController.IsVisible)
                    _visibilityController.ForceHide();
                Title = isDndMode ? DynamicBird.UI.Localization.LocalizationManager.Instance["Main_TitleDnd"] : DynamicBird.UI.Localization.LocalizationManager.Instance["UI_MainWindow_49"];
                UpdateIconTextInternal();
            }
            catch (Exception ex)
            {
                LogManager.Error("勿扰模式切换处理失败", ex);
            }
        }

        private void OnSettingsChanged()
        {
            try
            {
                LogManager.Debug("配置已变更");
                ApplyAppearance();
                RefreshSystemStatus();
                _sizeController?.RefreshMinSizeCache();
                // ★ 设置变化（形状/尺寸等）→ 目标尺寸缓存失效，下次切换重新测量
                _edgeController?.InvalidateTargetSizeCache();
                // ★ 划词翻译 热键随设置变化重新注册（保存后立即生效）
                ReapplyTextAiHotkey();

                // ★★★ 同步设置到 ShapeAnimator ★★★
                _shapeAnimator?.SetSettings(_settingsService);
                _shapeAnimator?.SetAnimationsEnabled(_settingsService.AnimationsEnabled);
            }
            catch (Exception ex)
            {
                LogManager.Error("配置变更刷新失败", ex);
            }
        }

        private void OnRegionChanged(string regionType, string regionKey)
        {
            // ★ 内容加载（稳定后由 CompletePendingSwitch 触发）：只加载内容，不再进入中置（中置已由 SwitchStarted 触发）
            _contentController.LoadContentForRegion(regionType, regionKey);
        }

        // ============================================================
        //  内容切换锚点：图标中置 + 内容静默加载，防抖稳定后归位（内容由虚变实）
        // ============================================================

        /// <summary>
        /// 内容切换时调用：主图标平移到面板几何中心（跟随面板移动/尺寸保持居中），
        /// 内容区虚化（几乎不可见，静默加载），防抖计时（多次快速切换重置）。
        /// 稳定（防抖到期）后 ExitCenteredState 归位。
        /// </summary>
        /// <summary>重置稳定防抖计时（鼠标移动时调用）：只有鼠标真正停下才到期加载内容。</summary>
        private void ResetStabilizeTimer()
        {
            if (_stabilizeTimer == null || !_iconCentered) return;
            _stabilizeTimer.Stop();
            _stabilizeTimer.Start();
        }

        private void EnterCenteredState()
        {
            // ★ 幂等：已中置（快速连续切换）不重启动画/不重复虚化——动画重启是快速切换抽搐源之一
            if (_iconCentered) return;
            _iconCentered = true;
            _lastFollowMoveTime = DateTime.Now;
            // ★ 中置状态（乱逛/切换期间）：跟随强制绝对跟手（图标实时跟着鼠标逛）
            _shapeAnimator.SetFollowAbsolute(true);

            // 图标中置（面板级动画，150ms 缓动）
            double targetX = GetCenteredIconShift();
            var anim = new System.Windows.Media.Animation.DoubleAnimation(
                targetX, new Duration(TimeSpan.FromMilliseconds(Math.Max(60, _settingsService.ShowHideDurationMs))))
            {
                EasingFunction = new System.Windows.Media.Animation.CubicEase
                {
                    EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                },
                FillBehavior = System.Windows.Media.Animation.FillBehavior.HoldEnd
            };
            IconShift.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, anim);

            // 内容完全不可见（图标模态：只显示居中主图标，内容静默加载）
            ContentContainer.BeginAnimation(System.Windows.UIElement.OpacityProperty, null);
            ContentContainer.Opacity = 0;

            // 中置期间监听面板尺寸变化（切换动画中面板尺寸在变 → 图标保持几何居中）
            if (!_stabilizeSizeHookActive)
            {
                _stabilizeSizeHookActive = true;
                MainPanel.SizeChanged += OnCenteredPanelSizeChanged;
            }

            // 防抖计时：多次快速切换重置
            _stabilizeTimer ??= new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(Math.Max(200, _settingsService.ContentStabilizeMs))
            };
            _stabilizeTimer.Stop();
            _stabilizeTimer.Tick -= OnStabilizeTimerTick;
            _stabilizeTimer.Tick += OnStabilizeTimerTick;
            _stabilizeTimer.Start();
        }

        private void OnStabilizeTimerTick(object? sender, EventArgs e)
        {
            // ★ 只有鼠标真正停下（超过稳定时长无移动）才结束中置；
            //   移动期间 Timer 每次到期都继续等待（不退出、不加载内容）
            double ago = (DateTime.Now - _lastFollowMoveTime).TotalMilliseconds;
            if (ago > _settingsService.ContentStabilizeMs)
            {
                _stabilizeTimer?.Stop();
                ExitCenteredState();
            }
        }

        /// <summary>
        /// 防抖到期（内容已稳定）：正常切换恢复形变动画——
        /// 尺寸平滑形变到目标尺寸 + 图标归位同时进行，形变完成后内容 10ms 由虚变实。
        /// （快速切换期间只做位置动画不形变；稳定后仍要形变，符合正常切换预期）
        /// </summary>
        private void ExitCenteredState()
        {
            if (!_iconCentered) return;   // ★ 幂等：防抖到期/面板隐藏都可能触发
            _iconCentered = false;
            // ★ 退出中置：恢复设置映射的跟随松紧（拉满=跟手，调小=缓慢飞追）
            _shapeAnimator.SetFollowAbsolute(false);

            if (_stabilizeSizeHookActive)
            {
                _stabilizeSizeHookActive = false;
                MainPanel.SizeChanged -= OnCenteredPanelSizeChanged;
            }
            _stabilizeTimer?.Stop();

            // ★ 鼠标已稳定：加载最终内容 + 尺寸形变。
            //   按性能模式区分并行/串行：
            //   - Smooth/Normal（性能足）：并行——先启动形变动画（时钟开始走），
            //     内容加载交给 Dispatcher 紧随其后（内容在完全不可见状态下替换，无感知），
            //     总时延 ≈ 形变时长，内容加载被动画时间掩盖；
            //   - PowerSaver（省电）：串行——先加载内容再形变，避免同时执行两件事的峰值负载。
            bool parallel = _settingsService.PerformanceMode != DynamicBird.Core.Services.Configuration.PerformancePresets.PowerSaver;

            if (parallel)
            {
                // 并行：先启动形变（内容不可见状态下替换，无感知），内容加载紧随其后
                var (tw, th) = _edgeController.LastTargetSize;
                _shapeAnimator.AnimateSizeTo(tw, th, null);
                Dispatcher.BeginInvoke(
                    new Action(() =>
                    {
                        _edgeController.CompletePendingSwitch();
                        // ★ 内容落位后独立恢复透明度：不挂形变动画回调——
                        //   回调可能被 CompletePendingSwitch 的尺寸接续动画打断，
                        //   导致内容一直透明（空面板/黑面板）
                        RestoreContentOpacity();
                    }),
                    System.Windows.Threading.DispatcherPriority.Loaded);
            }
            else
            {
                // 串行：先加载内容（同 type 复用缓存），内容落位后变实，再形变
                _edgeController.CompletePendingSwitch();
                RestoreContentOpacity();
                var (tw, th) = _edgeController.LastTargetSize;   // 内容落位后读取（尺寸正确）
                _shapeAnimator.AnimateSizeTo(tw, th, null);
            }

            // 图标归位（与尺寸形变同步；内容变实在尺寸完成后，避免复合闪烁）
            var back = new System.Windows.Media.Animation.DoubleAnimation(
                0, new Duration(TimeSpan.FromMilliseconds(Math.Max(60, _settingsService.ShowHideDurationMs))))
            {
                EasingFunction = new System.Windows.Media.Animation.CubicEase
                {
                    EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                },
                FillBehavior = System.Windows.Media.Animation.FillBehavior.HoldEnd
            };
            back.Completed += (_, _) =>
            {
                IconShift.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, null);
                IconShift.X = 0;
            };
            IconShift.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, back);
        }

        /// <summary>
        /// 恢复内容透明度（图标中置退出后内容由虚变实）。
        /// ★ 独立执行、不依赖形变动画回调：回调可能被后续 AnimateSizeTo 打断
        ///   导致内容一直透明（空面板/黑面板）。
        /// </summary>
        private void RestoreContentOpacity()
        {
            ContentContainer.BeginAnimation(System.Windows.UIElement.OpacityProperty, null);
            ContentContainer.Opacity = 1.0;
        }

        /// <summary>图标中置的 X 偏移：使图标几何中心对齐面板几何中心。</summary>
        private double GetCenteredIconShift()
        {
            try
            {
                double panelW = MainPanel.ActualWidth;
                if (panelW <= 0) return 0;
                // 图标原始中心（相对 MainPanel）。
                // ★ TranslatePoint 返回的坐标包含当前平移（IconShift.RenderTransform）：
                //   图标已居中（偏移 180）时按渲染位置算会得到 0 → 把图标归零跳回左侧。
                //   减掉当前偏移即图标原始位置，再算"需要平移到面板中心的量"。
                var pt = IconContainer.TranslatePoint(
                    new Point(IconContainer.ActualWidth / 2, IconContainer.ActualHeight / 2), MainPanel);
                return panelW / 2 - (pt.X - IconShift.X);
            }
            catch { return 0; }
        }

        private void OnCenteredPanelSizeChanged(object sender, SizeChangedEventArgs e)
        {
            // 面板尺寸变化（切换动画中）→ 保持图标几何居中
            if (_iconCentered)
            {
                // ★ 先清掉 EnterCenteredState 的 HoldEnd 动画再设值：
                //   WPF 动画优先级高于本地值，直接设 IconShift.X 不生效 →
                //   面板尺寸变化后图标停留在旧居中位置（偏左/偏右）
                IconShift.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, null);
                IconShift.X = GetCenteredIconShift();
            }
        }

        private void OnPanelShown()
        {
            // 面板显示时清理残留状态
            DynamicBird.Infrastructure.WinApi.ToastMonitor.SetPanelVisible(true);
        }

        private void OnPanelHidden()
        {
            // ★ 面板隐藏：立即结束"图标中置 + 内容虚化"状态（防止内容残留虚化/图标中置）
            ExitCenteredState();
            _edgeController.ClearEdge();
            DynamicBird.Infrastructure.WinApi.ToastMonitor.SetPanelVisible(false);

            // 滑出动画期间保留内容，动画结束后（或再次显示前）再释放
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!_visibilityController.IsVisible)
                {
                    // ★ 应用辅助（画中画嵌入/视频播放/媒体控制）保留内容不释放，
                    //   避免面板隐藏导致嵌入窗口被解除、播放中断
                    if (_contentController.CurrentRegionType == "AppHelper") return;
                    ContentContainer.Content = null;
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private void OnWindowClosed()
        {
            try
            {
                UnregisterGlobalHotkey(new System.Windows.Interop.WindowInteropHelper(this).Handle);
            }
            catch { }

            LogManager.Info("主窗口关闭");
            _edgeTimer?.Stop();
            _edgeTimer = null;
            DynamicBird.Infrastructure.WinApi.ToastMonitor.Stop();
            DynamicBird.Infrastructure.WinApi.RecentAppTracker.Stop();
            try { _clipboardService.StopListening(); } catch { }

            try { _dragController?.Detach(); } catch { }
            try { (_shapeAnimator as IDisposable)?.Dispose(); } catch { }

            try
            {
                ServiceManager.Instance.ShutdownAll();
                ServiceManager.Instance.Dispose();
            }
            catch { }

            LogManager.Shutdown();
        }

        internal ISettingsService SettingsService => _settingsService!;
        internal IModeService ModeService => _modeService!;
        internal IShortcutService ShortcutService => _shortcutService!;
        internal INoteService NoteService => _noteService!;
        internal IClipboardService ClipboardService => _clipboardService!;
        internal EdgeTriggerController EdgeController => _edgeController!;
        internal PanelVisibilityController VisibilityController => _visibilityController!;
        internal WindowSizeController SizeController => _sizeController!;
        internal ShapeAnimator ShapeAnimator => _shapeAnimator!;
        internal DragController DragController => _dragController!;
    }
}