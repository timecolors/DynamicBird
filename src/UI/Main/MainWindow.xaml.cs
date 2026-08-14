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

        public MainWindow()
        {
            Icon = AppIconHelper.LoadAppIcon();
            try
            {
                InitializeComponent();
                // ★ 窗口尺寸/位置变化时立即重设圆角区域，避免刚触发面板时短暂显示直角
                SizeChanged += (_, _) =>
                {
                    bool atBottom = Math.Abs((Top + Height) - SystemParameters.PrimaryScreenHeight) < 1.0;
                    ApplyWindowRegion(atBottom && Height > BottomStripClickThroughPx + 2);
                };
                // ★ 非透明窗口：句柄创建后应用圆角窗口区域（尺寸变化由周期刷新覆盖）；
                //   Win11 22H2+ 尝试启用 Mica 背景，让面板透出系统毛玻璃材质
                SourceInitialized += (_, _) =>
                {
                    // ★ 启动时把窗口移到屏幕外右下角：即使透明度异常也不会在屏幕上露头或拦截鼠标
                    Left = SystemParameters.PrimaryScreenWidth + 60;
                    Top = SystemParameters.PrimaryScreenHeight + 60;

                    if (TryApplyMicaBackdrop())
                    {
                        Background = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromArgb(2, 0x2D, 0x2D, 0x2D));
                        MainPanel.Background = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromArgb(0xEE, 0x2D, 0x2D, 0x2D));
                    }
                    ApplyWindowRegion(false);
                    var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
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

                // ★★★ Step 6: 创建其余控制器 ★★★
                _contentController = new PanelContentController(
                    ContentContainer, _settingsService, _shortcutService, _noteService, _clipboardService, _modeService);
                _contentController.ContentChanged += OnPanelContentChanged;

                _dragController = new DragController(
                    this, MainPanel, _edgeController, _visibilityController, _settingsService);

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
                    // 每约 150ms 刷新一次任务栏边界（任务栏自动隐藏/升起时跟随）
                    _edgeTickCount++;
                    if (_edgeTickCount % 5 == 0)
                    {
                        RefreshTaskbarBoundary();
                    }

                    if (_modeService.IsDoNotDisturb) return;

                if (!_edgeController.IsDragging && _visibilityController.CheckHideDelayTimeout())
                {
                    // 面板已隐藏，跳过后续处理
                }

                try
                {
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

                    double screenW = SystemParameters.PrimaryScreenWidth;
                    double screenH = SystemParameters.PrimaryScreenHeight;

                    EdgeRegion region = EdgeStateDetector.DetectRegion(mouseX, mouseY, screenW, screenH);

                    // ★ 触发位置设置过滤：被勾掉的边缘/角落不呼出面板
                    if (region != EdgeRegion.Unknown && !IsRegionEnabledBySettings(region))
                    {
                        region = EdgeRegion.Unknown;
                    }

                    bool isInsidePanel = IsMouseInsidePanel(mouseX, mouseY);

                    if (_edgeController.IsInClinging())
                    {
                        _edgeController.UpdateClinging(mouseX, mouseY);
                    }

                    // ★ 鼠标在面板内时保持当前模式：不再响应边缘切换，
                    //   避免“从角落/边缘划向面板时碰到其他边导致内容被意外切换”；
                    //   但仍跟随边缘滑动实时更新位置
                    if (isInsidePanel && _visibilityController.IsVisible)
                    {
                        _visibilityController.CancelHide();
                        _visibilityController.UpdateEdge(_edgeController.CurrentEdge);
                        _edgeController.FollowMouseInPanel(region, mouseX, mouseY, screenW, screenH);
                    }
                    else if (region != EdgeRegion.Unknown)
                    {
                        _edgeController.ProcessRegion(region, mouseX, mouseY, screenW, screenH);
                        _visibilityController.UpdateEdge(_edgeController.CurrentEdge);

                        // ★ 右上角由 ProcessRegion 显式隐藏，这里不能再 Show，否则会闪烁；
                        //   显示统一走滑入锚点动画（IsShown 状态避免动画途中重复触发）
                        if (!_visibilityController.IsShown && region != EdgeRegion.TopRight)
                        {
                            _edgeController.ShowPanelAtAnchor();
                        }
                    }
                    else
                    {
                        if (isInsidePanel)
                        {
                            _visibilityController.CancelHide();
                            _visibilityController.UpdateEdge(_edgeController.CurrentEdge);
                        }
                        else
                        {
                            // ★ 尺寸调整/拖拽期间及刚结束后不因鼠标位置触发隐藏
                            if (_edgeController.IsDragging || _edgeController.IsRecentlyDragged)
                            {
                                _visibilityController.CancelHide();
                            }
                            else if (_settingsService.ClingModeEnabled && _visibilityController.IsVisible && !_edgeController.IsInClinging())
                            {
                                _edgeController.StartClinging(mouseX, mouseY);
                            }
                            else if (!_settingsService.ClingModeEnabled || !_visibilityController.IsVisible)
                            {
                                _visibilityController.HideWithDelay();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Timer error: {ex.Message}");
                }
            };
            _edgeTimer.Start();
            LogManager.Debug("边缘检测定时器已启动 (30ms)");
        }

        private bool IsMouseInsidePanel(double mouseX, double mouseY)
        {
            double panelLeft = this.Left - 5;
            double panelTop = this.Top - 5;
            double panelRight = this.Left + this.Width + 10;
            double panelBottom = this.Top + this.Height + 10;

            return mouseX >= panelLeft && mouseX <= panelRight &&
                   mouseY >= panelTop && mouseY <= panelBottom;
        }

        private void ShowOnboardingIfNeeded()
        {
            try
            {
                if (_settingsService.OnboardingCompleted) return;

                var onboarding = new DynamicBird.UI.Onboarding.OnboardingWindow(
                    () => _settingsService.OnboardingCompleted = true);
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
                Title = isDndMode ? "灵动鸟 (勿扰模式)" : "灵动鸟";
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
            _contentController.LoadContentForRegion(regionType, regionKey);
        }

        private void OnPanelShown()
        {
            // 面板显示时清理残留状态
            DynamicBird.Infrastructure.WinApi.ToastMonitor.SetPanelVisible(true);
        }

        private void OnPanelHidden()
        {
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
