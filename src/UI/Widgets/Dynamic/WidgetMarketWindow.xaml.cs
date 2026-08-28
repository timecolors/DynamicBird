using DynamicBird.UI.Theme;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DynamicBird.UI.Widgets.Dynamic
{
    /// <summary>
    /// 组件市场窗口：导出/导入小组件分享文件。
    /// 导出时自动检测源码所需权限（WidgetPermissions）并写入包内标注；
    /// 在线市场即将上线（需要部署服务端），当前以文件分享为主。
    /// </summary>
    public partial class WidgetMarketWindow : Window
    {
        public WidgetMarketWindow()
        {
            Icon = AppIconHelper.LoadAppIcon();
            InitializeComponent();
            RefreshList();
        }

        private void RefreshList()
        {
            WidgetPluginStore.Reload();
            PluginListPanel.Children.Clear();
            foreach (var plugin in WidgetPluginStore.Installed)
            {
                PluginListPanel.Children.Add(BuildRow(plugin));
            }
            if (WidgetPluginStore.Installed.Count == 0)
            {
                PluginListPanel.Children.Add(new TextBlock
                {
                    Text = "还没有安装任何小组件",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)),
                    Margin = new Thickness(0, 6, 0, 0)
                });
            }
        }

        private FrameworkElement BuildRow(WidgetPlugin plugin)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var sp = new StackPanel();
            sp.Children.Add(new TextBlock
            {
                Text = plugin.Name,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold
            });
            string permText = plugin.Permissions.Count == 0
                ? "🔒 无权限"
                : string.Join(" · ", plugin.Permissions.Select(WidgetPluginStore.PermissionLabel));
            sp.Children.Add(new TextBlock
            {
                Text = permText + "  ·  ID: " + plugin.Id,
                FontSize = 10.5,
                Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136))
            });
            grid.Children.Add(sp);

            var btnExport = new Button
            {
                Content = "导出",
                Style = (Style)FindResource("Win11Button"),
                Width = 64,
                Height = 26,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0)
            };
            btnExport.Click += (_, _) => ExportOne(plugin);
            grid.Children.Add(btnExport);
            Grid.SetColumn(btnExport, 1);
            return grid;
        }

        // ========== 导出（自动检测权限并标注） ==========

        private void ExportOne(WidgetPlugin plugin)
        {
            try
            {
                var perms = WidgetPermissions.Detect(plugin.Source);
                var pkg = new WidgetMarketPackage
                {
                    Id = plugin.Id,
                    Name = plugin.Name,
                    Author = plugin.Author,
                    Description = plugin.Description,
                    Permissions = perms,
                    Source = plugin.Source
                };
                string json = JsonSerializer.Serialize(pkg, new JsonSerializerOptions { WriteIndented = true });

                var dlg = new SaveFileDialog
                {
                    Title = "导出小组件",
                    FileName = plugin.Id + ".json",
                    Filter = "灵动鸟小组件 (*.json)|*.json",
                    DefaultExt = ".json"
                };
                if (dlg.ShowDialog(this) == true)
                {
                    File.WriteAllText(dlg.FileName, json);
                    txtStatus.Text = "已导出：" + dlg.FileName + "（权限已自动检测标注）";
                }
            }
            catch (Exception ex)
            {
                txtStatus.Text = "导出失败：" + ex.Message;
            }
        }

        private void BtnExportAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new System.Windows.Forms.FolderBrowserDialog { Description = "选择导出目录" };
                if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                int n = 0;
                foreach (var plugin in WidgetPluginStore.Installed)
                {
                    var pkg = new WidgetMarketPackage
                    {
                        Id = plugin.Id,
                        Name = plugin.Name,
                        Author = plugin.Author,
                        Description = plugin.Description,
                        Permissions = WidgetPermissions.Detect(plugin.Source),
                        Source = plugin.Source
                    };
                    File.WriteAllText(Path.Combine(dlg.SelectedPath, plugin.Id + ".json"),
                        JsonSerializer.Serialize(pkg, new JsonSerializerOptions { WriteIndented = true }));
                    n++;
                }
                txtStatus.Text = $"已导出 {n} 个小组件到 {dlg.SelectedPath}";
            }
            catch (Exception ex)
            {
                txtStatus.Text = "导出失败：" + ex.Message;
            }
        }

        // ========== 导入 ==========

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new OpenFileDialog
                {
                    Title = "导入小组件",
                    Filter = "灵动鸟小组件 (*.json)|*.json",
                    Multiselect = true
                };
                if (dlg.ShowDialog(this) != true) return;
                int ok = 0, conflict = 0;
                foreach (string file in dlg.FileNames)
                {
                    string json = File.ReadAllText(file);
                    var pkg = JsonSerializer.Deserialize<WidgetMarketPackage>(json);
                    if (pkg == null || string.IsNullOrEmpty(pkg.Source)) continue;

                    string id = pkg.Id ?? "";
                    if (WidgetPluginStore.GetById(id) != null)
                    {
                        // 冲突：分配新 id 安装
                        string nid;
                        do { nid = "widget_" + Guid.NewGuid().ToString("N").Substring(0, 6); }
                        while (WidgetPluginStore.GetById(nid) != null);
                        id = nid;
                        conflict++;
                    }
                    var err = WidgetPluginStore.Save(new WidgetPlugin
                    {
                        Id = id,
                        Name = string.IsNullOrEmpty(pkg.Name) ? "导入的小组件" : pkg.Name,
                        Author = pkg.Author ?? "",
                        Description = pkg.Description ?? "",
                        Permissions = pkg.Permissions ?? new List<string>(),
                        Source = pkg.Source
                    });
                    if (err.Length == 0) ok++;
                }
                RefreshList();
                txtStatus.Text = conflict > 0
                    ? $"已导入 {ok} 个（{conflict} 个 ID 冲突已自动改名安装）"
                    : $"已导入 {ok} 个小组件";
            }
            catch (Exception ex)
            {
                txtStatus.Text = "导入失败：" + ex.Message;
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e) => RefreshList();
    }

    /// <summary>市场分享包格式（含源码与权限标注）。</summary>
    public class WidgetMarketPackage
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Author { get; set; }
        public string? Description { get; set; }
        public List<string>? Permissions { get; set; }
        public string? Source { get; set; }
    }
}
