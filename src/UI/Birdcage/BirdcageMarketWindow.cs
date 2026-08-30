using DynamicBird.Core.Models;
using DynamicBird.Core.Services.Configuration;
using DynamicBird.UI.Widgets.Dynamic;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DynamicBird.UI.Birdcage
{
    /// <summary>
    /// 其他鸟笼 · 共享平台：
    /// - 本地：导出当前预设/整套预设为 .dbp，导入时 Defender 扫描 + 权限弹窗 + 沙箱编译；
    /// - 在线：从主仓库 market/ 目录（jsDelivr CDN）拉取包列表，下载源码安装——
    ///   同样走权限重新检测（不信 manifest）+ TrustedSource=false 沙箱编译。
    /// </summary>
    public sealed class BirdcageMarketWindow : Window
    {
        /// <summary>在线市场 CDN 根（与 market/index.json 的 marketBase 一致；仓库默认分支为 master）。</summary>
        public const string MarketBase = "https://cdn.jsdelivr.net/gh/timecolors/DynamicBird@master/market";

        private readonly DynamicBird.UI.Settings.Pages.BirdcagePage _page;
        private readonly TextBlock _status = new()
        {
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 10, 0, 0)
        };
        private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };
        private readonly ListBox _onlineList = new()
        {
            MaxHeight = 190,
            Margin = new Thickness(0, 6, 0, 6),
            DisplayMemberPath = "Display"
        };
        private readonly List<MarketItem> _onlinePackages = new();

        /// <summary>在线市场条目（列表展示）。</summary>
        public sealed class MarketItem
        {
            public string Id = "";
            public string Name = "";
            public string Kind = "Widget";
            public string Version = "";
            public string Author = "";
            public string Description = "";
            public List<string> Permissions = new();

            public string Display => Name + " · " + Author +
                (Permissions.Count > 0 ? " · ⚠ " + string.Join(",", Permissions) : "");
        }

        public BirdcageMarketWindow(DynamicBird.UI.Settings.Pages.BirdcagePage page)
        {
            _page = page;
            Title = "🕊️ 其他鸟笼 · 共享平台";
            Width = 500;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;

            var desc = new TextBlock
            {
                Text = "把预设/功能导出为 .dbp 分享（导出时自动标注联网等权限）；导入/在线安装都会重新检测权限、Defender 扫描、沙箱编译，确认后才写入。",
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(90, 90, 90))
            };

            var export = new Button { Content = "导出当前预设", Width = 130, Height = 30, FontSize = 12, Margin = new Thickness(0, 0, 8, 0) };
            export.Click += Export_Click;
            var import = new Button { Content = "导入 .dbp…", Width = 130, Height = 30, FontSize = 12, Margin = new Thickness(0, 0, 8, 0) };
            import.Click += Import_Click;
            var btns = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            btns.Children.Add(export);
            btns.Children.Add(import);

            // ===== 在线市场区 =====
            var onlineTitle = new TextBlock
            {
                Text = "🛒 在线市场（GitHub 仓库 + jsDelivr CDN）",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 14, 0, 0)
            };
            var onlineHint = new TextBlock
            {
                Text = "包随主仓库 market/ 目录发布，CI 自动编译验证。安装走沙箱 + 权限确认，不信任包内声明。",
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120)),
                Margin = new Thickness(0, 2, 0, 0)
            };
            var refresh = new Button { Content = "刷新列表", Width = 90, Height = 26, FontSize = 11, Margin = new Thickness(0, 0, 8, 0) };
            refresh.Click += RefreshOnline_Click;
            var install = new Button { Content = "下载并安装选中包", Width = 130, Height = 26, FontSize = 11 };
            install.Click += DownloadInstall_Click;
            var onlineBtns = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            onlineBtns.Children.Add(refresh);
            onlineBtns.Children.Add(install);

            var panel = new StackPanel { Margin = new Thickness(16) };
            panel.Children.Add(desc);
            panel.Children.Add(btns);
            panel.Children.Add(onlineTitle);
            panel.Children.Add(onlineHint);
            panel.Children.Add(_onlineList);
            panel.Children.Add(onlineBtns);
            panel.Children.Add(_status);
            Content = panel;

            // 打开即刷新一次列表
            Loaded += async (_, _) => await RefreshOnlineAsync();
        }

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

                // ★ L1：Windows Defender 扫描
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

        /// <summary>风险权限确认（有权限才弹窗）。返回 true 继续。</summary>
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

        /// <summary>安装解析结果（写入 CustomPanels，TrustedSource=false 走沙箱编译）。</summary>
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
                TrustedSource = false,   // ★ 市场来源：编译走沙箱（拦截危险 API）
                CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
            });
            settings.CustomPanels = list;
            _page.RefreshAll();
            _status.Text = "✅ 已安装「" + result.Name + "」 · " + scanNote +
                " · 权限：" + WidgetPermissions.Describe(result.Permissions);
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
                _onlinePackages.Clear();
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
                        if (p.TryGetProperty("permissions", out var perms) && perms.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var perm in perms.EnumerateArray())
                            {
                                string? s = perm.GetString();
                                if (!string.IsNullOrEmpty(s) && !item.Permissions.Contains(s)) item.Permissions.Add(s);
                            }
                        }
                        if (!string.IsNullOrEmpty(item.Id)) _onlinePackages.Add(item);
                    }
                }
                _onlineList.ItemsSource = _onlinePackages;
                _status.Text = _onlinePackages.Count > 0
                    ? "✅ 在线市场 " + _onlinePackages.Count + " 个包"
                    : "⚠ 在线市场暂无包（market/packages/ 为空）";
            }
            catch (Exception ex)
            {
                _status.Text = "❌ 连接在线市场失败：" + ex.Message + "（需联网；如网络受限可稍后重试）";
            }
        }

        private async void DownloadInstall_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_onlineList.SelectedItem is not MarketItem item)
                {
                    _status.Text = "请先在列表选择一个包";
                    return;
                }
                _status.Text = "正在下载「" + item.Name + "」…";
                string manifestJson = await _http.GetStringAsync(
                    MarketBase + "/packages/" + item.Id + "/manifest.json");
                string source = await _http.GetStringAsync(
                    MarketBase + "/packages/" + item.Id + "/main.cs");

                // 构建与 .dbp 同构的解析结果
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
                // ★ 重新检测权限（不信任 manifest / index 声明）
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
