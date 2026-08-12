using DynamicBird.Core.Services;
using DynamicBird.Core.Services.Configuration;
using DynamicBird.src.core.Services.Clipboard;
using DynamicBird.src.core.Services.Notes;
using DynamicBird.src.core.Services.Shortcuts;
using DynamicBird.src.core.Services.System;
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

        public void LoadContentForRegion(string regionType)
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
                    var widgetSwitcher = new WidgetSwitcher(_settings, _clipboardService, _noteService);
                    newContent = widgetSwitcher;
                    break;

                case "AppHelper":
                    newContent = new TextBlock
                    {
                        Text = "⚡ 应用辅助模式\n(开发中)",
                        Foreground = System.Windows.Media.Brushes.White,
                        TextAlignment = TextAlignment.Center,
                        FontSize = 16,
                        FontWeight = FontWeights.SemiBold
                    };
                    break;

                case "Placeholder":
                    newContent = new TextBlock
                    {
                        Text = "📍 待定",
                        Foreground = System.Windows.Media.Brushes.White,
                        TextAlignment = TextAlignment.Center,
                        FontSize = 16,
                        FontWeight = FontWeights.SemiBold
                    };
                    break;

                default:
                    newContent = new TextBlock
                    {
                        Text = "📍 待定",
                        Foreground = System.Windows.Media.Brushes.White,
                        TextAlignment = TextAlignment.Center,
                        FontSize = 16,
                        FontWeight = FontWeights.SemiBold
                    };
                    break;
            }

            // ★★★ 应用内容 ★★★
            _contentContainer.Content = newContent;
            ContentChanged?.Invoke();

            // ★★★ 通知加载完成 ★★★
            LoadingCompleted?.Invoke();
        }
    }
}