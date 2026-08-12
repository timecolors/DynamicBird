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
using DynamicBird.UI.Panels;
using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
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

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                AllowsTransparency = true;

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
                };

                StartEdgeTimer();

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

                // ★★★ Step 3: 创建 EdgeTriggerController（传入 bottomBoundary） ★★★
                _edgeController = new EdgeTriggerController(
                    this, _shapeAnimator, null!, _visibilityController, _settingsService, bottomBoundary);

                // ★★★ Step 4: 创建 WindowSizeController（传入 _edgeController） ★★★
                _sizeController = new WindowSizeController(
                    this, ContentContainer, MainPanel, _currentTaskbarHeight, _settingsService, _edgeController);

                // ★★★ Step 5: 用反射把 _sizeController 补回 EdgeTriggerController ★★★
                var field = typeof(EdgeTriggerController).GetField("_sizeController",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(_edgeController, _sizeController);
                }

                // ★★★ Step 6: 创建其余控制器 ★★★
                _contentController = new PanelContentController(
                    ContentContainer, _settingsService, _shortcutService, _noteService, _clipboardService, _modeService);

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
                if (_modeService.IsDoNotDisturb) return;

                if (_visibilityController.CheckHideDelayTimeout())
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
                    bool isInsidePanel = IsMouseInsidePanel(mouseX, mouseY);

                    if (_edgeController.IsInClinging())
                    {
                        _edgeController.UpdateClinging(mouseX, mouseY);
                    }

                    if (region != EdgeRegion.Unknown)
                    {
                        _edgeController.ProcessRegion(region, mouseX, mouseY, screenW, screenH);

                        if (!_visibilityController.IsVisible)
                        {
                            _visibilityController.Show();
                        }
                    }
                    else
                    {
                        if (isInsidePanel)
                        {
                            _visibilityController.CancelHide();
                        }
                        else
                        {
                            if (_settingsService.ClingModeEnabled && _visibilityController.IsVisible && !_edgeController.IsInClinging())
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

        private void OnRegionChanged(string regionType)
        {
            _contentController.LoadContentForRegion(regionType);
        }

        private void OnPanelShown()
        {
            // 面板显示时清理残留状态
        }

        private void OnPanelHidden()
        {
            _edgeController.ClearEdge();
            ContentContainer.Content = null;
        }

        private void OnWindowClosed()
        {
            LogManager.Info("主窗口关闭");
            _edgeTimer?.Stop();
            _edgeTimer = null;

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