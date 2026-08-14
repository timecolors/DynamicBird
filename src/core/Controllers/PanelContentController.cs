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

        // ★★★ 新增事件 ★★★
        public event Action? LoadingStarted;
        public event Action? LoadingCompleted;

        public event Action? ContentChanged;

        public string CurrentRegionType => _currentRegionType;

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
                    newContent = new TaskbarView(_shortcutService, _settings);
                    break;

                case "Widget":
                    // ★ 小组件实例缓存：呼出面板时保留计时/便签/剪贴板状态
                    _cachedWidgetSwitcher ??= new WidgetSwitcher(_settings, _clipboardService, _noteService);
                    var widgetSwitcher = _cachedWidgetSwitcher;
                    newContent = widgetSwitcher;
                    break;

                case "AppHelper":
                    _cachedAppHelper ??= new AppHelperView();
                    newContent = _cachedAppHelper;
                    break;

                case "Notification":
                    newContent = new NotificationDockView();
                    break;

                case "Recent":
                    newContent = new RecentItemsView();
                    break;

                case "QuickSettings":
                    newContent = new QuickSettingsView();
                    break;

                case "Placeholder":
                    // ★ 四角分工：右下通知坞 / 左下最近使用 / 左上系统开关
                    newContent = regionKey switch
                    {
                        "BottomLeft" => new RecentItemsView(),
                        "TopLeft" => new QuickSettingsView(),
                        _ => new NotificationDockView()
                    };
                    break;

                default:
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
    }
}
