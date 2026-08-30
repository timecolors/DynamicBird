using DynamicBird.Core.Services;
using DynamicBird.Core.Services.Configuration;
using DynamicBird.src.core.Services.Clipboard;
using DynamicBird.src.core.Services.Notes;
using DynamicBird.src.core.Services.Shortcuts;
using DynamicBird.src.core.Services.System;
using DynamicBird.UI.AppHelper;
using DynamicBird.UI.Panels;
using DynamicBird.UI.Widgets;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace DynamicBird.Core.Controllers
{
    public class PanelContentController
    {
        private readonly ContentControl _contentContainer;
        private readonly ISettingsService _settings;
        private readonly IShortcutService _shortcutService;
        private readonly INoteService _noteService;
        private readonly IClipboardService _clipboardService;
        private readonly IModeService _modeService;

        private IWidget? _currentWidget;
        private string _currentRegionType = "Taskbar";
        // ★ 画中画实例缓存：呼出面板时复用，避免镜像/播放状态被重置
        private DynamicBird.UI.AppHelper.AppHelperView? _cachedAppHelper;
        private DynamicBird.UI.Widgets.WidgetSwitcher? _cachedWidgetSwitcher;
        private DynamicBird.UI.AI.AiChatView? _cachedAiChat;
        // ★ 任务栏视图缓存：贴边切换频繁触发 LoadContent，重建快捷方式布局是卡顿主因
        private DynamicBird.UI.Panels.TaskbarView? _cachedTaskbarView;
        // ★ 自定义面板实例缓存：id → 视图，编译一次复用（源码变化时由设置重载重建）
        private readonly System.Collections.Generic.Dictionary<string, FrameworkElement> _customPanelCache = new();
        private string _customPanelsSignature = "";

        // ★★★ 新增事件 ★★★
        public event Action? LoadingStarted;
        public event Action? LoadingCompleted;

        public event Action? ContentChanged;

        public string CurrentRegionType => _currentRegionType;

        /// <summary>当前缓存的小组件切换器（可能为 null，尚未创建）。</summary>
        public DynamicBird.UI.Widgets.WidgetSwitcher? WidgetSwitcher => _cachedWidgetSwitcher;

        /// <summary>
        /// 显示小组件面板并切换到指定标签（如划词热键跳转到 TextAi）。
        /// 面板当前是其他内容时强制切到小组件，标签不存在时保持当前标签。
        /// </summary>
        public void ShowWidgetTab(string tab)
        {
            LoadContentForRegion("Widget");
            _cachedWidgetSwitcher?.SelectTab(tab);
        }

        public PanelContentController(
            ContentControl contentContainer,
            ISettingsService settings,
            IShortcutService shortcutService,
            INoteService noteService,
            IClipboardService clipboardService,
            IModeService modeService)
        {
            _contentContainer = contentContainer;
            _settings = settings;
            _shortcutService = shortcutService;
            _noteService = noteService;
            _clipboardService = clipboardService;
            _modeService = modeService;
        }

        public void LoadContentForRegion(string regionType, string regionKey = "")
        {
            DynamicBird.Core.Infrastructure.Logging.LogManager.Debug($"LoadContent type={regionType} key={regionKey}");

            // ★ 同类型不重建：同一边内滑动（如 Left_Top → Left_Center）内容相同，
            //   直接复用当前实例，避免每次 new TaskbarView 导致的贴边切换卡顿
            if (regionType == _currentRegionType && _currentWidget != null)
            {
                // ★ 隐藏时 OnPanelHidden 会把 ContentContainer.Content 清空（滑出动画后释放视觉树），
                //   同类型复用（隐藏后回到同一边）必须重新挂载缓存实例，否则面板空白
                if (_contentContainer.Content == null && _currentWidget is System.Windows.FrameworkElement cached)
                {
                    _contentContainer.Content = cached;
                    _currentWidget.OnActivated();
                    ContentChanged?.Invoke();
                }
                return;
            }

            _currentRegionType = regionType;

            // ★★★ 通知开始加载 ★★★
            LoadingStarted?.Invoke();

            if (_currentWidget != null)
            {
                _currentWidget.OnDeactivated();
                _currentWidget = null;
            }

            FrameworkElement newContent;

            switch (regionType)
            {
                case "Taskbar":
                    // ★ 缓存任务栏视图：切走再切回不重建（快捷方式布局/窗口列表保持）
                    _cachedTaskbarView ??= new DynamicBird.UI.Panels.TaskbarView(_shortcutService, _settings);
                    newContent = _cachedTaskbarView;
                    break;

                case "Widget":
                    // ★ 小组件实例缓存：呼出面板时保留计时/便签/剪贴板状态
                    if (_cachedWidgetSwitcher == null)
                    {
                        _cachedWidgetSwitcher = new WidgetSwitcher(_settings, _clipboardService, _noteService);
                        // ★ 小组件内部切标签 → 面板按新内容重新自适应尺寸（延迟到布局完成，测量更准确）
                        _cachedWidgetSwitcher.ContentSizeChanged += () =>
                            System.Windows.Application.Current?.Dispatcher.BeginInvoke(
                                () => ContentChanged?.Invoke(),
                                System.Windows.Threading.DispatcherPriority.Loaded);
                    }
                    var widgetSwitcher = _cachedWidgetSwitcher;
                    newContent = widgetSwitcher;
                    break;

                case "AppHelper":
                    _cachedAppHelper ??= new AppHelperView();
                    newContent = _cachedAppHelper;
                    break;

                case "AI":
                    // ★ AI 助手面板缓存：保持对话状态
                    _cachedAiChat ??= new DynamicBird.UI.AI.AiChatView();
                    _cachedAiChat.RefreshSettings();
                    newContent = _cachedAiChat;
                    break;

                case "WindowControl":
                    // ★ 右上角窗口操作中心（不缓存：每次显示都刷新前台窗口信息）
                    newContent = new DynamicBird.UI.Widgets.WindowControlView();
                    break;

                case "Notification":
                    newContent = new NotificationDockView();
                    break;

                case "Recent":
                    newContent = new RecentItemsView();
                    break;

                case "QuickSettings":
                    newContent = new QuickSettingsView(_settings);
                    break;

                case "Placeholder":
                    // ★ 四角分工：右下通知坞 / 左下最近使用 / 左上系统开关
                    newContent = regionKey switch
                    {
                        "BottomLeft" => new RecentItemsView(),
                        "TopLeft" => new QuickSettingsView(_settings),
                        _ => new NotificationDockView()
                    };
                    break;

                default:
                    if (regionType.StartsWith("Custom:", StringComparison.Ordinal))
                    {
                        newContent = LoadCustomPanel(regionType);
                        break;
                    }
                    newContent = new NotificationDockView();
                    break;
            }

            // ★★★ 应用内容 ★★★
            _contentContainer.Content = newContent;
            ContentChanged?.Invoke();

            // ★★★ 记录当前组件并激活（WidgetSwitcher 内部再管理自己的标签页） ★★★
            _currentWidget = newContent as IWidget;
            _currentWidget?.OnActivated();

            // ★★★ 通知加载完成 ★★★
            LoadingCompleted?.Invoke();
        }

        /// <summary>
        /// 加载用户自定义面板：按 "Custom:面板Id" 从 CustomPanels 取源码，
        /// 用 WidgetCompiler 动态编译（实现 IWidget → CreateView()），实例缓存复用。
        /// 编译失败回退默认通知坞并写日志。
        /// </summary>
        private FrameworkElement LoadCustomPanel(string regionType)
        {
            string panelId = regionType.Substring("Custom:".Length);
            try
            {
                // 源码变化时清缓存（签名对比）
                string sig = string.Join("|",
                    _settings.CustomPanels.Select(p => p.Id + ":" + (p.Source ?? "").Length));
                if (sig != _customPanelsSignature)
                {
                    _customPanelsSignature = sig;
                    _customPanelCache.Clear();
                }

                if (_customPanelCache.TryGetValue(panelId, out var cached)) return cached;

                var cp = _settings.CustomPanels.FirstOrDefault(p => p.Id == panelId);
                if (cp == null || string.IsNullOrWhiteSpace(cp.Source))
                    return new NotificationDockView();

                // ★ 沙箱：市场来源（TrustedSource=false）先拦截危险 API
                if (!cp.TrustedSource)
                {
                    string sandboxErr = DynamicBird.UI.Widgets.Dynamic.WidgetCompiler.SandboxErrors(cp.Source ?? "");
                    if (sandboxErr.Length > 0)
                    {
                        DynamicBird.Core.Infrastructure.Logging.LogManager.Error(
                            $"自定义面板 [{cp.Name}] 市场来源被沙箱拦截: {sandboxErr}");
                        return new NotificationDockView();
                    }
                }
                var (widget, err) = DynamicBird.UI.Widgets.Dynamic.WidgetCompiler.Compile(
                    "panel_" + cp.Id, cp.Source);
                if (widget == null)
                {
                    DynamicBird.Core.Infrastructure.Logging.LogManager.Error(
                        $"自定义面板 [{cp.Name}] 编译失败: {err}");
                    return new NotificationDockView();
                }

                var view = widget.CreateView();
                _customPanelCache[panelId] = view;
                return view;
            }
            catch (Exception ex)
            {
                DynamicBird.Core.Infrastructure.Logging.LogManager.Error(
                    "自定义面板加载异常", ex);
                return new NotificationDockView();
            }
        }
    }
}
