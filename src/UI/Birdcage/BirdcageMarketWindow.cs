using DynamicBird.Core.Models;
using DynamicBird.Core.Services.Configuration;
using DynamicBird.UI.Widgets.Dynamic;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DynamicBird.UI.Birdcage
{
    /// <summary>
    /// 其他鸟笼 · 共享平台：按分类（对齐鸟笼树）浏览在线市场，Win11 资源管理器式查看（大图标/小图标/列表/详细信息）；
    /// 本地 .dbp 导出/导入；安装走权限确认 + Defender + 沙箱编译。
    /// </summary>
    public sealed class BirdcageMarketWindow : Window
    {
        /// <summary>在线市场 CDN 根（仓库默认分支 master）。</summary>
        public const string MarketBase = "https://cdn.jsdelivr.net/gh/timecolors/DynamicBird@master/market";

        /// <summary>查看模式（Win11 资源管理器式）。</summary>
        private enum ViewMode { LargeIcon, SmallIcon, List, Details }

        /// <summary>市场分类（对齐鸟笼树一级分类；小组件/面板功能单列便于浏览）。</summary>
        private static readonly string[] Categories =
        {
            "全部", "小组件", "面板功能", "面板设计", "动画", "外观", "交互", "状态栏"
        };

        private readonly DynamicBird.UI.Settings.Pages.BirdcagePage _page;
        private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };
        private readonly List<MarketItem> _allPackages = new();
        private readonly ListBox _categoryList = new() { FontSize = 12, BorderThickness = new Thickness(0), Background = Brushes.Transparent };
        private readonly ListBox _content = new() { BorderThickness = new Thickness(0), Background = Brushes.Transparent };
        private readonly TextBlock _empty = new() { Text = "暂无内容", FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 30, 0, 0) };
        private readonly ScrollViewer _contentScroll = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 6, 0, 0) };
        private readonly TextBlock _status = new()
        {
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        };
        private ViewMode _viewMode = ViewMode.LargeIcon;
        private string _category = "全部";

        // ★ 跟随系统主题（与设置窗口一致）：浅色白底 / 深色黑底
        private readonly Color _cBg, _cCard, _cText, _cSub, _cBorder;

        // ===== 详情页 =====
        private readonly Grid _detail = new() { Visibility = Visibility.Collapsed };
        private readonly TextBox _codeBox = new()
        {
            IsReadOnly = true,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11,
            TextWrapping = TextWrapping.NoWrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 8, 0, 8)
        };
        private readonly TextBlock _detailInfo = new() { FontSize = 11, TextWrapping = TextWrapping.Wrap };
        private string _detailSource = "";      // 当前详情源码
        private string _detailManifest = "";    // 当前详情 manifest（供下载）

        /// <summary>在线市场条目。</summary>
        public sealed class MarketItem
        {
            // ★ 用属性（DataTemplate Binding 只能绑定属性，不能绑定字段）
            public string Id { get; set; } = "";
            public string Name { get; set; } = "";
            public string Kind { get; set; } = "Widget";
            public string Category { get; set; } = "小组件";
            public string Version { get; set; } = "";
            public string Author { get; set; } = "";
            public string Description { get; set; } = "";
            public List<string> Permissions { get; set; } = new();

            public string MetaLine =>
                Id + " · " + Author +
                (string.IsNullOrEmpty(Version) ? "" : " · v" + Version) +
                (Permissions.Count > 0 ? " · ⚠ " + string.Join(",", Permissions) : "");

            public string DetailLine =>
                (Id + " · " + Author + (string.IsNullOrEmpty(Version) ? "" : " · v" + Version) +
                 (Permissions.Count > 0 ? " · ⚠ " + string.Join(",", Permissions) : "")) +
                (string.IsNullOrEmpty(Description) ? "" : System.Environment.NewLine + Description);
        }

        public BirdcageMarketWindow(DynamicBird.UI.Settings.Pages.BirdcagePage page)
        {
            _page = page;
            bool light = DynamicBird.Infrastructure.Utils.SystemTheme.IsLightTheme();
            _cBg = light ? Color.FromRgb(0xF9, 0xF9, 0xF9) : Color.FromRgb(0x1E, 0x1E, 0x1E);
            _cCard = light ? Colors.White : Color.FromRgb(0x2A, 0x2A, 0x2A);
            _cText = light ? Color.FromRgb(0x1E, 0x1E, 0x1E) : Colors.White;
            _cSub = light ? Color.FromRgb(0x66, 0x66, 0x66) : Color.FromRgb(0xBB, 0xBB, 0xBB);
            _cBorder = light ? Color.FromRgb(0xE0, 0xE0, 0xE0) : Color.FromRgb(0x40, 0x40, 0x40);

            Title = "其他鸟笼 · 共享平台";
            Width = 760;
            Height = 600;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;   // ★ 固定大小：内容区内部滚动，避免窗口边缘出现调整大小光标
            ShowInTaskbar = false;
            Background = new SolidColorBrush(_cBg);

            // ===== 顶部工具栏（Win11Button 与设置页一致） =====
            var export = new Button { Content = "导出当前预设", Width = 110, Height = 28, FontSize = 11, Margin = new Thickness(0, 0, 6, 0) };
            export.Style = (Style)FindResource("Win11Button");
            export.Click += Export_Click;
            var import = new Button { Content = "导入 .dbp…", Width = 100, Height = 28, FontSize = 11, Margin = new Thickness(0, 0, 10, 0) };
            import.Style = (Style)FindResource("Win11Button");
            import.Click += Import_Click;
            var refresh = new Button { Content = "刷新列表", Width = 80, Height = 28, FontSize = 11, Margin = new Thickness(0, 0, 10, 0) };
            refresh.Style = (Style)FindResource("Win11Button");
            refresh.Click += RefreshOnline_Click;

            // 查看切换（Win11 资源管理器式）
            var viewLabel = new TextBlock { Text = "查看:", FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };
            var btnLarge = MakeViewButton("大图标");
            var btnSmall = MakeViewButton("小图标");
            var btnList = MakeViewButton("列表");
            var btnDetails = MakeViewButton("详细信息");
            btnLarge.Click += (_, _) => SetView(ViewMode.LargeIcon);
            btnSmall.Click += (_, _) => SetView(ViewMode.SmallIcon);
            btnList.Click += (_, _) => SetView(ViewMode.List);
            btnDetails.Click += (_, _) => SetView(ViewMode.Details);
            var views = new StackPanel { Orientation = Orientation.Horizontal };
            views.Children.Add(btnLarge); views.Children.Add(btnSmall); views.Children.Add(btnList); views.Children.Add(btnDetails);

            var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            toolbar.Children.Add(export); toolbar.Children.Add(import); toolbar.Children.Add(refresh);
            toolbar.Children.Add(viewLabel); toolbar.Children.Add(views);

            // ===== 左侧分类导航 =====
            foreach (var c in Categories)
            {
                _categoryList.Items.Add(new ListBoxItem { Content = c, Tag = c, Padding = new Thickness(8, 4, 8, 4), FontSize = 12 });
            }
            _categoryList.SelectedIndex = 0;
            _categoryList.SelectionChanged += (_, _) =>
            {
                if (_categoryList.SelectedItem is ListBoxItem li && li.Tag is string c)
                {
                    _category = c;
                    RefreshContent();
                }
            };
            var categoryPanel = new Border
            {
                Child = _categoryList,
                Width = 150,
                VerticalAlignment = VerticalAlignment.Top,   // ★ 高度按内容自适应，不填满（避免下方空白）
                Background = new SolidColorBrush(_cCard),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(6, 8, 6, 8),
                Margin = new Thickness(0, 0, 10, 0),
                BorderBrush = new SolidColorBrush(_cBorder),
                BorderThickness = new Thickness(1)
            };

            // ===== 右侧内容区 =====
            // ★ 视图项点击 → 打开详情页（不自动下载；详情页可下载/复制）
            _content.SelectionChanged += (_, _) =>
            {
                if (_content.SelectedItem is ListBoxItem lbi && lbi.Tag is MarketItem mi)
                {
                    _content.SelectedItem = null;
                    _ = ShowDetailAsync(mi);
                }
            };
            _contentScroll.Content = _content;   // ★ ListBox 挂进 ScrollViewer（此前丢失导致不渲染）
            var contentPanel = new Border
            {
                Child = new Grid
                {
                    Children = { _contentScroll, _empty }
                },
                Background = new SolidColorBrush(_cCard),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 8, 10, 8),
                BorderBrush = new SolidColorBrush(_cBorder),
                BorderThickness = new Thickness(1)
            };

            // ===== 详情页（点包后切换显示；可下载/复制代码） =====
            var back = new Button { Content = "← 返回列表", Width = 90, Height = 26, FontSize = 11 };
            back.Style = (Style)FindResource("Win11Button");
            back.Click += (_, _) => { _detail.Visibility = Visibility.Collapsed; _contentScroll.Visibility = Visibility.Visible; _empty.Visibility = _allPackages.Count == 0 ? Visibility.Visible : Visibility.Collapsed; };
            var download = new Button { Content = "⬇ 下载安装", Width = 100, Height = 28, FontSize = 11, Margin = new Thickness(0, 0, 8, 0) };
            download.Style = (Style)FindResource("Win11Button");
            download.Click += (_, _) => { if (!string.IsNullOrEmpty(_detailManifest)) _ = InstallFromDetailAsync(); };
            var copy = new Button { Content = "📋 复制代码", Width = 100, Height = 28, FontSize = 11 };
            copy.Style = (Style)FindResource("Win11Button");
            copy.Click += (_, _) =>
            {
                try { if (!string.IsNullOrEmpty(_detailSource)) { System.Windows.Clipboard.SetText(_detailSource); _status.Text = "✅ 代码已复制到剪贴板"; } }
                catch (Exception ex) { _status.Text = "❌ 复制失败：" + ex.Message; }
            };
            var detailBtns = new StackPanel { Orientation = Orientation.Horizontal };
            detailBtns.Children.Add(download);
            detailBtns.Children.Add(copy);
            var detailHead = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            detailHead.Children.Add(back);
            var detailTitle = new TextBlock { FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            detailHead.Children.Add(detailTitle);
            _detail.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _detail.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _detail.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _detail.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _detail.Children.Add(detailHead);
            _detail.Children.Add(_detailInfo);
            _detail.Children.Add(_codeBox);
            _detail.Children.Add(detailBtns);
            Grid.SetRow(detailHead, 0);
            Grid.SetRow(_detailInfo, 1);
            Grid.SetRow(_codeBox, 2);
            Grid.SetRow(detailBtns, 3);

            var contentHost = new Grid();
            contentHost.Children.Add(contentPanel);
            contentHost.Children.Add(_detail);

            var body = new Grid { Margin = new Thickness(0) };
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(categoryPanel, 0);
            Grid.SetColumn(contentHost, 1);
            body.Children.Add(categoryPanel);
            body.Children.Add(contentHost);
            var terms = new TextBlock
            {
                Text = "📜 上传即代表你保证所上传内容拥有全部权利、不侵犯任何第三方版权，并同意灵动鸟在开源许可范围内免费分发。",
                FontSize = 10.5,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(_cSub),
                Margin = new Thickness(0, 8, 0, 0)
            };

            var panel = new Grid { Margin = new Thickness(16) };
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });   // ★ body 占满剩余高度
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(toolbar, 0);
            Grid.SetRow(body, 1);
            Grid.SetRow(_status, 2);
            Grid.SetRow(terms, 3);
            panel.Children.Add(toolbar);
            panel.Children.Add(body);
            panel.Children.Add(_status);
            panel.Children.Add(terms);
            Content = panel;

            // ★ 前景色在配色初始化后设置
            _status.Foreground = new SolidColorBrush(_cSub);
            _empty.Foreground = new SolidColorBrush(_cSub);
            _categoryList.Foreground = new SolidColorBrush(_cText);

            Loaded += async (_, _) => await RefreshOnlineAsync();
        }

        private Button MakeViewButton(string text)
        {
            var b = new Button { Content = text, Width = 60, Height = 26, FontSize = 11, Margin = new Thickness(0, 0, 4, 0), Padding = new Thickness(4, 0, 4, 0) };
            b.Style = (Style)FindResource("Win11Button");   // ★ 与设置页按钮一致（浅色底深色字，浅色主题下清晰可见）
            return b;
        }

        private void SetView(ViewMode mode)
        {
            _viewMode = mode;
            RefreshContent();
        }

        /// <summary>按当前分类 + 视图模式重建内容区。</summary>
        private void RefreshContent()
        {
            IEnumerable<MarketItem> items = _allPackages;
            if (_category != "全部")
            {
                items = _allPackages.Where(p => p.Category == _category);
            }
            var list = items.ToList();
            _empty.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            _contentScroll.Visibility = list.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            // ★ ItemsPanel 不能设为 null（ListBox 默认 StackPanel 面板会被破坏 → 不渲染）；只重置 ItemsSource
            _content.ItemsSource = null;
            _content.ItemsSource = BuildViewItems(list);
            _content.SelectedIndex = -1;
        }

        private System.Collections.IEnumerable BuildViewItems(List<MarketItem> items)
        {
            // ★ 视图的 ItemsPanel：大图标/小图标 → WrapPanel 横向排布；列表/详细 → StackPanel 垂直
            bool wrap = _viewMode == ViewMode.LargeIcon || _viewMode == ViewMode.SmallIcon;
            _content.ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(
                wrap ? typeof(WrapPanel) : typeof(StackPanel)));

            var result = new List<ListBoxItem>();
            foreach (var item in items)
            {
                result.Add(new ListBoxItem
                {
                    Content = BuildItemView(item),
                    Tag = item,          // ★ 用 Tag 携带 MarketItem（SelectedItem 是 ListBoxItem，取 Tag 还原数据）
                    Padding = new Thickness(0),
                    Margin = new Thickness(0, 0, wrap ? 0 : 0, wrap ? 4 : 4),
                    // WrapPanel 里不拉伸（按卡片宽排）；列表/详细占满行
                    HorizontalContentAlignment = wrap ? HorizontalAlignment.Left : HorizontalAlignment.Stretch
                });
            }
            return result;
        }

        /// <summary>手动构建包卡片/行（按视图模式）。</summary>
        private FrameworkElement BuildItemView(MarketItem item)
        {
            if (_viewMode == ViewMode.LargeIcon)
            {
                var card = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0x12, _cText.R, _cText.G, _cText.B)),
                    CornerRadius = new CornerRadius(8),
                    Width = 132,
                    Margin = new Thickness(0, 0, 8, 8),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Padding = new Thickness(8)
                };
                var sp = new StackPanel();
                sp.Children.Add(new TextBlock { Text = item.Name, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(_cText), TextWrapping = TextWrapping.Wrap });
                sp.Children.Add(new TextBlock { Text = item.MetaLine, FontSize = 10, Foreground = new SolidColorBrush(_cSub), Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap });
                card.Child = sp;
                return card;
            }
            if (_viewMode == ViewMode.SmallIcon)
            {
                var b = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(0x12, _cText.R, _cText.G, _cText.B)),
                    CornerRadius = new CornerRadius(6),
                    Margin = new Thickness(0, 0, 6, 6),
                    Padding = new Thickness(6, 4, 6, 4),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                b.Child = new TextBlock { Text = item.Name, FontSize = 11, Foreground = new SolidColorBrush(_cText) };
                return b;
            }
            // 列表 / 详细信息
            var row = new Border
            {
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 6, 8, 6),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            var rp = new StackPanel();
            rp.Children.Add(new TextBlock { Text = item.Name, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(_cText) });
            rp.Children.Add(new TextBlock { Text = item.DetailLine, FontSize = 10.5, Foreground = new SolidColorBrush(_cSub), Margin = new Thickness(0, 2, 0, 0), TextWrapping = TextWrapping.Wrap });
            row.Child = rp;
            return row;
        }
        // ==================== 本地：导出/导入 ====================
        // ==================== 本地：导出/导入 ====================

        private void Export_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var cp = _page.CurrentSelectedCustom();
                if (cp != null)
                {
                    var dlg = new SaveFileDialog
                    {
                        Filter = "灵动鸟预设包 (*.dbp)|*.dbp",
                        FileName = cp.Name + BirdcagePackage.Extension,
                        Title = "导出单预设"
                    };
                    if (dlg.ShowDialog(this) != true) return;
                    string? err = BirdcagePackage.ExportCustom(cp, dlg.FileName);
                    _status.Text = err ?? "✅ 已导出「" + cp.Name + "」 · 权限：" +
                        WidgetPermissions.Describe(WidgetPermissions.Detect(cp.Source ?? ""));
                    return;
                }

                string? presetName = _page.CurrentSelectedPresetName;
                if (!string.IsNullOrEmpty(presetName))
                {
                    var data = PresetManager.LoadPreset(presetName);
                    if (data == null) { _status.Text = "❌ 读取预设失败"; return; }
                    var dlg = new SaveFileDialog
                    {
                        Filter = "灵动鸟预设包 (*.dbp)|*.dbp",
                        FileName = presetName + BirdcagePackage.Extension,
                        Title = "导出整套预设"
                    };
                    if (dlg.ShowDialog(this) != true) return;
                    string? err = BirdcagePackage.ExportFullPreset(presetName, data, dlg.FileName);
                    _status.Text = err ?? "✅ 已导出整套预设「" + presetName + "」";
                    return;
                }

                _status.Text = "请先在鸟笼树选中一个单预设，或在下拉框选整套预设";
            }
            catch (Exception ex) { _status.Text = "❌ " + ex.Message; }
        }

        private async void Import_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new OpenFileDialog
                {
                    Filter = "灵动鸟预设包 (*.dbp)|*.dbp|所有文件|*.*",
                    Title = "导入预设包"
                };
                if (dlg.ShowDialog(this) != true) return;

                _status.Text = "正在使用 Windows Defender 扫描…";
                var scan = await DynamicBird.Infrastructure.WinApi.DefenderScanner.ScanFileAsync(dlg.FileName);
                if (scan.Result == DynamicBird.Infrastructure.WinApi.DefenderScanner.ScanResult.ThreatFound)
                {
                    _status.Text = "❌ " + scan.Detail + "，已阻止导入";
                    return;
                }
                string scanNote = scan.Result == DynamicBird.Infrastructure.WinApi.DefenderScanner.ScanResult.Clean
                    ? "✅ Defender 扫描：未发现已知威胁"
                    : "⚠ Defender 扫描不可用（仍按风险提示导入）";

                var result = BirdcagePackage.Import(dlg.FileName, out string? err);
                if (result == null) { _status.Text = "❌ " + err; return; }
                if (!ConfirmPermissions(result.Name, result.Permissions)) return;
                InstallResult(result, scanNote);
            }
            catch (Exception ex) { _status.Text = "❌ " + ex.Message; }
        }

        private bool ConfirmPermissions(string name, List<string> permissions)
        {
            string perms = WidgetPermissions.Describe(permissions);
            if (permissions.Count > 0)
            {
                var confirm = MessageBox.Show(this,
                    "「" + name + "」声明了以下权限：\n" + perms +
                    "\n\n该代码由他人编写，将运行在你的电脑上。仅从可信来源安装，确定继续吗？",
                    "其他鸟笼 · 权限提示",
                    MessageBoxButton.OKCancel, MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.OK) { _status.Text = "已取消"; return false; }
            }
            return true;
        }

        private void InstallResult(BirdcagePackage.ImportResult result, string scanNote)
        {
            if (result.Kind == "Full")
            {
                if (result.FullData == null) { _status.Text = "❌ 整套预设数据无效"; return; }
                PresetManager.SaveFull(result.Name, result.FullData);
                _page.RefreshAll();
                _status.Text = "✅ 已导入整套预设「" + result.Name + "」";
                return;
            }

            var settings = _page.SettingsService;
            var list = settings.CustomPanels;
            string defaultParent = result.Kind == "Widget" ? "panel-widgets"
                : result.Kind == "Panel" ? "panel-features"
                : result.Kind == "Category" ? "root" : "";
            list.Add(new CustomPanelDefinition
            {
                Id = "custom_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                Name = result.Name,
                Category = "面板设计",
                ParentKey = string.IsNullOrEmpty(result.ParentKey) ? defaultParent : result.ParentKey,
                BaseType = string.IsNullOrEmpty(result.BaseType)
                    ? (result.Kind == "Widget" ? "Widget" : result.Kind == "Panel" ? "Panel" : "Config")
                    : result.BaseType,
                Kind = result.Kind,
                ConfigJson = result.ConfigJson,
                Source = result.Source,
                SourceKey = result.SourceKey ?? "",
                TrustedSource = false,   // ★ 市场来源统一走沙箱（剪贴板已降为权限声明，安装时提示）
                CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
            });
            settings.CustomPanels = list;
            _page.RefreshAll();
            _status.Text = "✅ 已安装「" + result.Name + "」 · " + scanNote +
                " · 权限：" + WidgetPermissions.Describe(result.Permissions);
        }

        // ==================== 详情页 ====================

        /// <summary>点包打开详情：拉取 manifest + main.cs 展示，可下载或复制。</summary>
        private async Task ShowDetailAsync(MarketItem item)
        {
            try
            {
                _status.Text = "正在加载「" + item.Name + "」…";
                _detailManifest = await _http.GetStringAsync(
                    MarketBase + "/packages/" + item.Id + "/manifest.json");
                _detailSource = await _http.GetStringAsync(
                    MarketBase + "/packages/" + item.Id + "/main.cs");

                _detailInfo.Text = "📦 " + item.Name + "  ·  " + item.Id +
                    System.Environment.NewLine + "👤 上传者：" + item.Author +
                    (string.IsNullOrEmpty(item.Version) ? "" : "  ·  v" + item.Version) +
                    "  ·  分类：" + item.Category +
                    (item.Permissions.Count > 0 ? System.Environment.NewLine + "⚠ 权限：" + string.Join(", ", item.Permissions) : "") +
                    (string.IsNullOrEmpty(item.Description) ? "" : System.Environment.NewLine + item.Description);
                _detailInfo.Foreground = new SolidColorBrush(_cSub);
                _codeBox.Text = _detailSource;
                _codeBox.Background = new SolidColorBrush(Color.FromArgb(0x08, _cText.R, _cText.G, _cText.B));
                _codeBox.Foreground = new SolidColorBrush(_cText);
                _codeBox.BorderBrush = new SolidColorBrush(_cBorder);

                _detail.Visibility = Visibility.Visible;
                _contentScroll.Visibility = Visibility.Collapsed;
                _empty.Visibility = Visibility.Collapsed;
                _status.Text = "✅ 已加载，可下载安装或复制代码";
            }
            catch (Exception ex)
            {
                _status.Text = "❌ 加载详情失败：" + ex.Message;
            }
        }

        /// <summary>从详情页下载安装（复用权限确认 + 沙箱流程）。</summary>
        private async Task InstallFromDetailAsync()
        {
            try
            {
                var result = new BirdcagePackage.ImportResult
                {
                    Name = _detailInfo.Text.Contains("📦") ? _detailManifest : "未命名",
                    Kind = "Widget",
                    Source = _detailSource,
                    ConfigJson = "{}"
                };
                using (var doc = JsonDocument.Parse(_detailManifest))
                {
                    var root = doc.RootElement;
                    result.Name = GetStr(root, "name") ?? "未命名";
                    result.Kind = GetStr(root, "kind") ?? "Widget";
                    result.BaseType = GetStr(root, "baseType");
                    result.ParentKey = GetStr(root, "parentKey");
                    result.SourceKey = GetStr(root, "sourceKey");
                }
                result.Permissions = WidgetPermissions.Detect(_detailSource);

                if (!ConfirmPermissions(result.Name, result.Permissions)) return;
                InstallResult(result, "✅ 在线来源（源码直接解析，沙箱编译拦截危险 API）");
            }
            catch (Exception ex)
            {
                _status.Text = "❌ 下载/安装失败：" + ex.Message;
            }
        }

        // ==================== 在线市场 ====================

        private async void RefreshOnline_Click(object sender, RoutedEventArgs e)
            => await RefreshOnlineAsync();

        private async Task RefreshOnlineAsync()
        {
            try
            {
                _status.Text = "正在连接在线市场…";
                string json = await _http.GetStringAsync(MarketBase + "/index.json");
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                _allPackages.Clear();
                if (root.TryGetProperty("packages", out var pkgs) && pkgs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var p in pkgs.EnumerateArray())
                    {
                        var item = new MarketItem
                        {
                            Id = GetStr(p, "id") ?? "",
                            Name = GetStr(p, "name") ?? "未命名",
                            Kind = GetStr(p, "kind") ?? "Widget",
                            Version = GetStr(p, "version") ?? "",
                            Author = GetStr(p, "author") ?? "",
                            Description = GetStr(p, "description") ?? ""
                        };
                        string? cat = GetStr(p, "category");
                        item.Category = string.IsNullOrEmpty(cat)
                            ? MapKindToCategory(item.Kind)
                            : cat;
                        if (p.TryGetProperty("permissions", out var perms) && perms.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var perm in perms.EnumerateArray())
                            {
                                string? s = perm.GetString();
                                if (!string.IsNullOrEmpty(s) && !item.Permissions.Contains(s)) item.Permissions.Add(s);
                            }
                        }
                        if (!string.IsNullOrEmpty(item.Id)) _allPackages.Add(item);
                    }
                }
                _status.Text = _allPackages.Count > 0
                    ? "✅ 在线市场 " + _allPackages.Count + " 个包"
                    : "⚠ 在线市场暂无包（market/packages/ 为空）";
                RefreshContent();
            }
            catch (Exception ex)
            {
                _status.Text = "❌ 连接在线市场失败：" + ex.Message + "（需联网；如网络受限可稍后重试）";
            }
        }

        /// <summary>kind → 默认分类（与鸟笼树一致）。</summary>
        private static string MapKindToCategory(string kind) => kind switch
        {
            "Widget" => "小组件",
            "Panel" => "面板功能",
            "Config" => "面板设计",
            _ => "面板设计"
        };

        private async Task DownloadInstallAsync(MarketItem item)
        {
            try
            {
                _status.Text = "正在下载「" + item.Name + "」…";
                string manifestJson = await _http.GetStringAsync(
                    MarketBase + "/packages/" + item.Id + "/manifest.json");
                string source = await _http.GetStringAsync(
                    MarketBase + "/packages/" + item.Id + "/main.cs");

                var result = new BirdcagePackage.ImportResult
                {
                    Name = item.Name,
                    Kind = item.Kind,
                    Source = source,
                    ConfigJson = "{}"
                };
                using (var doc = JsonDocument.Parse(manifestJson))
                {
                    var root = doc.RootElement;
                    result.BaseType = GetStr(root, "baseType");
                    result.ParentKey = GetStr(root, "parentKey");
                    result.SourceKey = GetStr(root, "sourceKey");
                }
                result.Permissions = WidgetPermissions.Detect(source);

                if (!ConfirmPermissions(result.Name, result.Permissions)) return;
                InstallResult(result, "✅ 在线来源（源码直接解析，沙箱编译拦截危险 API）");
            }
            catch (Exception ex)
            {
                _status.Text = "❌ 下载/安装失败：" + ex.Message;
            }
        }

        private static string? GetStr(JsonElement root, string name)
        {
            return root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        }
    }
}
