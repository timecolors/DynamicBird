using ShoreHue.Core.Services;
using ShoreHue.Core.Services.Configuration;
using ShoreHue.src.core.Services.Clipboard;
using ShoreHue.src.core.Services.Notes;
using ShoreHue.UI.Localization;
using System;
using ShoreHue.UI.Panels;
using ShoreHue.UI.Widgets.Calculator;
using ShoreHue.UI.Widgets.ClipboardHistory;
using ShoreHue.UI.Widgets.Notes;
using ShoreHue.UI.Widgets.Dynamic;
using ShoreHue.UI.Widgets.TextAi;
using ShoreHue.UI.Widgets.Timer;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ShoreHue.UI.Widgets
{
    /// <summary>
    /// 小组件切换器：标签按设置中启用的小组件动态生成，
    /// 用户可在设置中自行选择面板保留哪些功能。
    /// </summary>
    public partial class WidgetSwitcher : UserControl, IWidget
    {
        private sealed class WidgetTab
        {
            public string Key = "";
            public string IconKey = "";
            public string LocKey = "";
            public IWidget Widget = null!;
        }

        private readonly ISettingsService _settings;
        private readonly ClipboardHistoryWidget _clipboardWidget;
        private readonly NoteWidget _noteWidget;
        private readonly TimerWidget _timerWidget;
        private readonly CalculatorWidget _calculatorWidget;
        private readonly TextAiWidget _textAiWidget;
        private readonly WebViewWidget _webWidget;

        private readonly List<WidgetTab> _tabs = new();
        private readonly TaskbarScrollHandler _tabScrollHandler;
        private string _currentTab = "";
        private bool _rebuilding;
        private string _dynamicSignature = ""; // 插件列表签名：面板激活时对比，变化则重建标签
        private string? _cachedPluginSignature; // 签名缓存：插件列表/源码变化时才重算，避免激活时重复哈希

        public WidgetSwitcher(ISettingsService settings, IClipboardService clipboardService, INoteService noteService)
        {
            _settings = settings;
            InitializeComponent();

            _clipboardWidget = new ClipboardHistoryWidget(clipboardService);
            _noteWidget = new NoteWidget(noteService, settings);
            _timerWidget = new TimerWidget();
            _calculatorWidget = new CalculatorWidget();
            _textAiWidget = new TextAiWidget();
            _webWidget = new WebViewWidget(settings);

            _tabs.Add(new WidgetTab { Key = "Clipboard", IconKey = "IconClipboard", LocKey = "WidgetTabs_Clipboard", Widget = _clipboardWidget });
            _tabs.Add(new WidgetTab { Key = "Note", IconKey = "IconNote", LocKey = "WidgetTabs_Notes", Widget = _noteWidget });
            _tabs.Add(new WidgetTab { Key = "Timer", IconKey = "IconTimer", LocKey = "WidgetTabs_Timer", Widget = _timerWidget });
            _tabs.Add(new WidgetTab { Key = "Calculator", IconKey = "IconCalc", LocKey = "WidgetTabs_Calculator", Widget = _calculatorWidget });
            _tabs.Add(new WidgetTab { Key = "TextAi", IconKey = "IconAi", LocKey = "WidgetTabs_TextAi", Widget = _textAiWidget });
            _tabs.Add(new WidgetTab { Key = "Web", IconKey = "IconWeb", LocKey = "WidgetTabs_Web", Widget = _webWidget });

            // ★ 用户安装的 C# 插件小组件：每个成为一个标签（编译失败跳过）
            RebuildDynamicTabs();
            // ★ 签名立即初始化：避免首次激活时因 _dynamicSignature 为空而重复编译（同名程序集冲突）
            _dynamicSignature = BuildPluginSignature();

            // 小组件安装/删除：重建动态标签。
            // ★ 先失效签名缓存再比签名：Installed 已 Reload 出新列表，不失效会拿到旧签名
            //   误判"未变化"而漏建；失效后重算（SHA256 全源 ≈ 几 ms）很便宜，
            //   真正的重活（Roslyn 沙箱编译 + 插件编译）被 RebuildIfSignatureChanged 按签名挡住。
            WidgetPluginStore.Changed += () => Dispatcher.Invoke(() =>
            {
                _cachedPluginSignature = null;
                if (!RebuildIfSignatureChanged()) RebuildTabs();
            });

            // 设置变化（含小组件开关/海床保存小组件变体）时重建标签栏，不丢失各小组件内部状态。
            // ★ 性能：设置窗口任何控件变化都会触发 SettingsChanged，必须先比签名——
            //   旧实现无条件全量重建 = 每次对全部插件跑 Roslyn 沙箱编译（实测 2-4s UI 冻结）。
            //   签名未变（颜色/动画/帧率等无关设置）→ 只重建标签按钮（反映启停开关），不碰编译。
            _settings.SettingsChanged += () => Dispatcher.Invoke(() =>
            {
                _cachedPluginSignature = null;
                if (!RebuildIfSignatureChanged()) RebuildTabs();
            });

            // ★ 标签栏自动滚动：鼠标移到左/右边缘自动滚动（与任务栏一致）
            _tabScrollHandler = new TaskbarScrollHandler(TabScroll, "小组件标签", isHorizontal: true);

            _currentTab = _settings.LastWidgetTab;
            if (!_tabs.Any(t => t.Key == _currentTab && IsTabEnabled(t)))
            {
                _currentTab = _tabs.FirstOrDefault(IsTabEnabled)?.Key ?? "";
            }
            RebuildTabs();
        }

        /// <summary>重建用户 C# 插件小组件标签（安装/删除后调用，内置标签保留）。</summary>
        private void RebuildDynamicTabs()
        {
            _tabs.RemoveAll(t => t.Key.StartsWith("Widget_", StringComparison.Ordinal));
            foreach (var plugin in WidgetPluginStore.Installed)
            {
                // ★ 只把 Widget 类当作小组件标签：Panel/Config/Category 是区域面板/配置项，
                //   编译成标签既错（面板功能出现在小组件栏）又白耗 Roslyn 编译（每次激活/设置变更全量跑）。
                //   旧文件夹项（无 manifest，Kind 为空）是历史小组件，照常作为标签。
                  if (plugin.Kind is "Panel" or "Config" or "Category" or "StatusProvider" or "Animation") continue;
                // ★ 沙箱只对市场来源（TrustedSource=false）执行：本地编程不检测（HANDOFF 设计），
                //   内置模板构建时已验证且合理使用黑名单 API（如 panel-recent 的 Process.Start/FileInfo），
                //   无条件沙箱会把它们全拦掉（2026-08 误杀回归）且每次重建白跑 Roslyn 编译。
                //   结果已按源码哈希缓存（SandboxErrors），未变化源码的重复重建零成本。
                if (!plugin.TrustedSource)
                {
                    string sandboxErr = WidgetCompiler.SandboxErrors(plugin.Source);
                    if (sandboxErr.Length > 0)
                    {
                        ShoreHue.Core.Infrastructure.Logging.LogManager.Warning(
                            $"小组件 [{plugin.Id}] 被沙箱拦截: {sandboxErr}");
                        continue;
                    }
                }
                // ★ 形态分流：XAML 形态（.xaml + .xaml.cs）走 CompileXaml；否则纯代码 Compile
                var (widget, err) = !string.IsNullOrEmpty(plugin.Xaml) && !string.IsNullOrEmpty(plugin.XamlCs)
                    ? WidgetCompiler.CompileXaml(plugin.Id, plugin.Xaml, plugin.XamlCs)
                    : WidgetCompiler.Compile(plugin.Id, plugin.Source);
                if (widget != null)
                {
                    _tabs.Add(new WidgetTab
                    {
                        Key = "Widget_" + plugin.Id,
                        IconKey = "IconApp",
                        LocKey = "",
                        Widget = widget
                    });
                      ShoreHue.Core.Infrastructure.Logging.LogManager.Debug($"[插件] 小组件编译成功: " + plugin.Id);
                }
                else
                {
                    ShoreHue.Core.Infrastructure.Logging.LogManager.Warning(
                        $"小组件 [{plugin.Id}] 编译失败: {err}");
                }
            }
            // ★ 海床保存的小组件变体（BaseType=Widget）：编译后作为标签加入
            _tabs.RemoveAll(t => t.Key.StartsWith("Seabed_", StringComparison.Ordinal));
            foreach (var cp in _settings.CustomPanels)
            {
                if (cp.Kind == "Config" || (cp.BaseType ?? "") != "Widget") continue;
                if (string.IsNullOrWhiteSpace(cp.Source)) continue;
                // ★ 把变体名注入源码（模板 Name 写死，编译前替换为变体实际名字）
                string src = WidgetCompiler.InjectWidgetName(cp.Source, cp.Name);
                // ★ 沙箱：市场来源（TrustedSource=false）先拦截危险 API，恶意代码编译不过
                if (!cp.TrustedSource)
                {
                    string sandboxErr = WidgetCompiler.SandboxErrors(src);
                    if (sandboxErr.Length > 0)
                    {
                        ShoreHue.Core.Infrastructure.Logging.LogManager.Warning(
                            $"海床小组件 [{cp.Name}] 市场来源被沙箱拦截: {sandboxErr}");
                        continue;
                    }
                }
                var (widget, err) = WidgetCompiler.Compile("seabed_" + cp.Id, src);
                if (widget != null)
                {
                    _tabs.Add(new WidgetTab
                    {
                        Key = "Seabed_" + cp.Id,
                        IconKey = "IconApp",
                        LocKey = "",
                        Widget = widget
                    });
                }
                else
                {
                    ShoreHue.Core.Infrastructure.Logging.LogManager.Warning(
                        $"海床小组件 [{cp.Name}] 编译失败: {err}");
                }
            }
        }

        /// <summary>插件签名：id + 源码哈希（源码编辑保存后也会触发重建）。带缓存，插件变化时失效。
        /// ★ 包含海床小组件变体（CustomPanels Kind=Widget），保证保存后激活时重建标签。</summary>
        private string BuildPluginSignature()
        {
            _cachedPluginSignature ??= string.Join(",",
                WidgetPluginStore.Installed
                    .Select(p => "W:" + p.Id + ":" + ShoreHue.UI.Widgets.Dynamic.WidgetCompiler.SourceHash(p.Source))
                    .Concat(
                        _settings.CustomPanels
                            .Where(cp => cp.Kind != "Config" && (cp.BaseType ?? "") == "Widget")
                            .Select(cp => "B:" + cp.Id + ":" + ShoreHue.UI.Widgets.Dynamic.WidgetCompiler.SourceHash(cp.Source))));
            return _cachedPluginSignature;
        }

        /// <summary>
        /// 签名变化时重建动态标签（含沙箱/编译）；未变化返回 false（跳过昂贵的全量重建）。
        /// ★ 性能关键：SettingsChanged 对无关设置（颜色/动画/帧率…）也触发，签名未变时
        ///   全量重建 = 对全部插件重复 Roslyn 编译（每次 2-4s UI 冻结），必须按签名跳过。
        /// </summary>
        private bool RebuildIfSignatureChanged()
        {
            string sig = BuildPluginSignature();
            if (sig == _dynamicSignature) return false;
            _dynamicSignature = sig;
            RebuildDynamicTabs();
            RebuildTabs();
            return true;
        }

        /// <summary>划词翻译 小组件实例（供主窗口热键处理调用）。</summary>
        public TextAiWidget TextAiWidget => _textAiWidget;

        /// <summary>当前小组件标签内容变化（切换 tab / 激活）：面板据此重新自适应尺寸。</summary>
        public event Action? ContentSizeChanged;

        /// <summary>当前启用的标签键列表（供外部校验）。</summary>
        public IReadOnlyList<string> EnabledKeys => _tabs.Where(IsTabEnabled).Select(t => t.Key).ToList();

        /// <summary>各标签上次的稳定测量（内容未就绪时复用，保证测量确定性）。</summary>
        private readonly Dictionary<string, (double width, double height)> _stableSizeByTab = new();

        /// <summary>
        /// 测量当前小组件内容的理想尺寸（DIP）。
        /// 直接测量 ContentContainer.Content（真正的剪贴板/便签/计时器等控件），
        /// 绕开外层 ScrollViewer 的视口限制——否则面板被量窄（ScrollViewer 对
        /// 无限空间测量返回视口宽而非内容宽）。
        /// ★ 测量前先 UpdateLayout 强制一次布局：内容刚换入时模板未应用、
        ///   DesiredSize=0 → 测出保底小尺寸（"有时窄"）；布局就绪后测量结果确定。
        ///   （与 WPF SizeToContent 同思路：布局就绪后再取 DesiredSize；
        ///   注意方向：只能"先布局后测量"——测量后绝不能再 UpdateLayout，
        ///   否则 DesiredSize 会被当前面板约束覆盖成小尺寸）
        /// </summary>
        public (double width, double height)? MeasureContentSize()
        {
            // ★ WebView2 标签：独立进程渲染，WPF Measure 无法得到有效内容尺寸（返回 0/异常值导致面板变小/消失）
            //   → 用固定面板尺寸（网页自适应，用户可手动拖面板大小）
            if (_currentTab == "Web")
            {
                return (480, 360);
            }
            try
            {
                if (ContentContainer.Content is System.Windows.FrameworkElement fe)
                {
                    fe.UpdateLayout();
                    fe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    double w = fe.DesiredSize.Width;
                    double h = fe.DesiredSize.Height;
                    if (w >= 10 && h >= 10)
                    {
                        _stableSizeByTab[_currentTab] = (w, h);
                        return (w, h);
                    }
                }
            }
            catch { }
            // ★ 内容未就绪：返回该标签上次的稳定测量（确定性）；无历史则 null（调用方保底）
            return _stableSizeByTab.TryGetValue(_currentTab, out var s) ? s : null;
        }

        private bool IsTabEnabled(WidgetTab tab) => _settings.IsWidgetEnabled(tab.Key);

        /// <summary>切换到指定标签（外部调用，如划词热键跳转到 TextAi）。</summary>
        public void SelectTab(string tab)
        {
            if (!_tabs.Any(t => t.Key == tab && IsTabEnabled(t))) return;
            if (_currentTab == tab) return;

            DeactivateCurrent();
            _currentTab = tab;
            _settings.LastWidgetTab = tab;
            ApplyTabButtonStyles();
            ShowContent();
            // ★ 内容切换后通知面板重新测量自适应（内容高度可能变化）
            ContentSizeChanged?.Invoke();
        }

        private void RebuildTabs()
        {
            if (_rebuilding) return;
            _rebuilding = true;
            try
            {
                ButtonPanel.Children.Clear();

                var enabled = _tabs.Where(IsTabEnabled).ToList();
                EmptyHint.Visibility = enabled.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                foreach (var tab in enabled)
                {
                    ButtonPanel.Children.Add(CreateTabButton(tab));
                }

                if (enabled.Count == 0)
                {
                    DeactivateCurrent();
                    ContentContainer.Content = null;
                    FooterPanel.Child = null;
                    return;
                }

                // 当前标签被关闭时回退到第一个启用标签
                if (!enabled.Any(t => t.Key == _currentTab))
                {
                    _currentTab = enabled[0].Key;
                    _settings.LastWidgetTab = _currentTab;
                }

                ApplyTabButtonStyles();
                ShowContent();
            }
            finally
            {
                _rebuilding = false;
            }
        }

        private Button CreateTabButton(WidgetTab tab)
        {
            var btn = new Button
            {
                Height = 28,
                Padding = new Thickness(8, 0, 8, 0),
                Margin = ButtonPanel.Children.Count == 0 ? new Thickness(0) : new Thickness(8, 0, 0, 0),
                Tag = tab.Key
            };
            btn.Click += (_, _) => SelectTab(tab.Key);

            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            sp.Children.Add(new Path
            {
                Style = (Style)FindResource("LineIcon"),
                Data = (Geometry)FindResource(tab.IconKey),
                Margin = new Thickness(0, 0, 5, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            var label = new TextBlock { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            if (string.IsNullOrEmpty(tab.LocKey))
            {
                label.Text = tab.Widget.Name; // 动态小组件：名称来自配置
            }
            else
            {
                label.SetBinding(TextBlock.TextProperty,
                    new Binding("Item[" + tab.LocKey + "]") { Source = LocalizationManager.Instance });
            }
            sp.Children.Add(label);

            btn.Content = sp;
            return btn;
        }

        private void ApplyTabButtonStyles()
        {
            foreach (var child in ButtonPanel.Children)
            {
                if (child is Button b && b.Tag is string key)
                {
                    b.Style = (Style)FindResource(key == _currentTab ? "AccentButton" : "FlatButton");
                }
            }
        }

        private void ShowContent()
        {
            // ★ 动态小组件标签：Widget_<id>（插件）与 Seabed_<id>（海床变体）
            if (_currentTab.StartsWith("Widget_", StringComparison.Ordinal) ||
                _currentTab.StartsWith("Seabed_", StringComparison.Ordinal))
            {
                var tab = _tabs.FirstOrDefault(t => t.Key == _currentTab);
                if (tab != null)
                {
                    ContentContainer.Content = tab.Widget;
                    tab.Widget.OnActivated();
                    FooterPanel.Child = null;
                    return;
                }
            }

            switch (_currentTab)
            {
                case "Clipboard":
                    ContentContainer.Content = _clipboardWidget;
                    _clipboardWidget.OnActivated();
                    FooterPanel.Child = _clipboardWidget.GetFooterControl();
                    break;
                case "Note":
                    ContentContainer.Content = _noteWidget;
                    _noteWidget.OnActivated();
                    FooterPanel.Child = _noteWidget.GetFooterControl();
                    break;
                case "Timer":
                    ContentContainer.Content = _timerWidget;
                    _timerWidget.OnActivated();
                    FooterPanel.Child = _timerWidget.GetFooterControl();
                    break;
                case "TextAi":
                    ContentContainer.Content = _textAiWidget;
                    _textAiWidget.OnActivated();
                    FooterPanel.Child = _textAiWidget.GetFooterControl();
                    break;
                case "Web":
                    ContentContainer.Content = _webWidget.CreateView();
                    _webWidget.OnActivated();
                    FooterPanel.Child = null;
                    break;
                case "Calculator":
                default:
                    ContentContainer.Content = _calculatorWidget;
                    _calculatorWidget.OnActivated();
                    FooterPanel.Child = _calculatorWidget.GetFooterControl();
                    break;
            }

            // ★ 内容切换后延迟通知面板重测尺寸：布局完成后再测量，
            //   避免"从 AI 划到小组件时用未布局的保底尺寸"导致面板过小。
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(
                new Action(() => ContentSizeChanged?.Invoke()),
                System.Windows.Threading.DispatcherPriority.Loaded);
        }

        public new string Name => ShoreHue.UI.Localization.LocalizationManager.Instance["Widget_GroupName"];

        public UserControl CreateView() => this;

        public void OnActivated()
        {
            // ★ 插件列表/源码变化（保存时 WidgetSwitcher 可能尚未创建，订阅丢失）：
            //   每次面板激活时对比签名（含源码哈希），变化则重建动态标签，保证新增/修改的小组件一定出现
            RebuildIfSignatureChanged();

            switch (_currentTab)
            {
                case "Clipboard": _clipboardWidget.OnActivated(); break;
                case "Note": _noteWidget.OnActivated(); break;
                case "Timer": _timerWidget.OnActivated(); break;
                case "Calculator": _calculatorWidget.OnActivated(); break;
                case "TextAi": _textAiWidget.OnActivated(); break;
                case "Web": _webWidget.OnActivated(); break;
            }
        }

        public void OnDeactivated()
        {
            DeactivateCurrent();
        }

        private void DeactivateCurrent()
        {
            if (_currentTab.StartsWith("Widget_", StringComparison.Ordinal) ||
                _currentTab.StartsWith("Seabed_", StringComparison.Ordinal))
            {
                var tab = _tabs.FirstOrDefault(t => t.Key == _currentTab);
                tab?.Widget.OnDeactivated();
                return;
            }
            switch (_currentTab)
            {
                case "Clipboard": _clipboardWidget.OnDeactivated(); break;
                case "Note": _noteWidget.OnDeactivated(); break;
                case "Timer": _timerWidget.OnDeactivated(); break;
                case "Calculator": _calculatorWidget.OnDeactivated(); break;
                case "TextAi": _textAiWidget.OnDeactivated(); break;
            }
        }
    }
}
