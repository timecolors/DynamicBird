using DynamicBird.Core.Services;
using DynamicBird.Core.Services.Ai;
using DynamicBird.Core.Services.Configuration;
using DynamicBird.Infrastructure.Utils;
using DynamicBird.Infrastructure.WinApi;
using DynamicBird.src.core.Services.Shortcuts;
using DynamicBird.UI.Widgets.Dynamic;
using DynamicBird.UI.Settings.Pages;
using DynamicBird.UI.Theme;
using DynamicBird.UI.Localization;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WinForms = System.Windows.Forms;

namespace DynamicBird.UI.Settings
{
    public partial class SettingsWindow
    {
        // ========== 小工具市场（左侧竖排列表 + 右侧精调） ==========

        private static readonly Dictionary<string, string> _builtinLocKeys = new()
        {
            ["Clipboard"] = "UI_SettingsWindow_317",
            ["Note"] = "UI_SettingsWindow_318",
            ["Timer"] = "UI_SettingsWindow_319",
            ["Calculator"] = "UI_SettingsWindow_320",
            ["TextAi"] = "UI_SettingsWindow_321",
            ["Web"] = "WidgetTabs_Web",
        };

        private string _selectedWidgetKey = "";

        /// <summary>刷新左侧小组件列表（内置 + 用户插件），保持当前选中项。</summary>
        /// <summary>在系统文件管理器中打开小组件文件夹。</summary>
        private void BtnOpenWidgetFolder_Click(object sender, RoutedEventArgs e)
        {
            DynamicBird.UI.Widgets.Dynamic.WidgetPluginStore.OpenFolder();
        }

        private void RefreshWidgetMarket()
        {
            if (WidgetMarketList == null) return;
            WidgetPluginStore.Reload();
            WidgetMarketList.Children.Clear();

            foreach (var kv in _builtinLocKeys)
                AddMarketItem(kv.Key, LocalizationManager.Instance[kv.Value]);
            foreach (var plugin in WidgetPluginStore.Installed)
                AddPluginMarketItem(plugin);
            // ★ 鸟笼保存的小组件变体（BaseType=Widget）：作为启停项列出（前缀区分）
            foreach (var cp in _settings.CustomPanels)
            {
                if (cp.Kind == "Config" || (cp.BaseType ?? "") != "Widget") continue;
                WidgetMarketList.Children.Add(BuildMarketRow("Birdcage_" + cp.Id, cp.Name, null));
            }

            if (string.IsNullOrEmpty(_selectedWidgetKey) || !KeyExists(_selectedWidgetKey))
                _selectedWidgetKey = "Clipboard";
            SelectWidget(_selectedWidgetKey);
        }

        private bool KeyExists(string key)
        {
            if (_builtinLocKeys.ContainsKey(key)) return true;
            if (WidgetPluginStore.Installed.Any(p => "Widget_" + p.Id == key)) return true;
            if (_settings.CustomPanels.Any(p => p.Kind != "Config" && (p.BaseType ?? "") == "Widget" && "Birdcage_" + p.Id == key)) return true;
            return false;
        }
        private void AddMarketItem(string key, string name)
        {
            WidgetMarketList.Children.Add(BuildMarketRow(key, name, null));
        }

        private void AddPluginMarketItem(WidgetPlugin plugin)
        {
            WidgetMarketList.Children.Add(BuildMarketRow("Widget_" + plugin.Id, plugin.Name, plugin));
        }

        /// <summary>构建左侧列表项：勾选框（启用，即时生效）+ 名称按钮（左键选中精调，右键菜单）。</summary>
        private Grid BuildMarketRow(string key, string name, WidgetPlugin? plugin)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var chk = new CheckBox
            {
                IsChecked = _settings.IsWidgetEnabled(key),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0)
            };
            chk.Checked += (_, _) => _settings.SetWidgetEnabled(key, true);
            chk.Unchecked += (_, _) => _settings.SetWidgetEnabled(key, false);
            row.Children.Add(chk);

            var btn = new System.Windows.Controls.Button
            {
                Content = name,
                Tag = key,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(6, 5, 6, 5),
                FontSize = 12
            };
            btn.Click += (_, _) => SelectWidget(key);
            btn.ContextMenu = BuildMarketMenu(key, plugin);
            row.Children.Add(btn);
            System.Windows.Controls.Grid.SetColumn(btn, 1);
            return row;
        }

        /// <summary>右键菜单：仅用户插件提供编辑/删除（启停已由左侧勾选框承担）。</summary>
        private ContextMenu BuildMarketMenu(string key, WidgetPlugin? plugin)
        {
            var menu = new ContextMenu();
            if (plugin != null)
            {
                var miDelete = new MenuItem { Header = LocalizationManager.Instance["WidgetMkt_Delete"] };
                miDelete.Click += (_, _) => DeletePlugin(plugin);
                menu.Items.Add(miDelete);
            }
            return menu;
        }

        /// <summary>左键选中：切换右侧精调面板 + 列表高亮。</summary>
        private void SelectWidget(string key)
        {
            _selectedWidgetKey = key;
            foreach (var row in WidgetMarketList.Children.OfType<Grid>())
            {
                var btn = row.Children.OfType<System.Windows.Controls.Button>().FirstOrDefault();
                if (btn == null) continue;
                btn.Background = (btn.Tag as string) == key
                    ? new SolidColorBrush(Color.FromRgb(229, 241, 255))
                    : System.Windows.Media.Brushes.Transparent;
            }
            DetailClipboard.Visibility = key == "Clipboard" ? Visibility.Visible : Visibility.Collapsed;
            DetailNote.Visibility = key == "Note" ? Visibility.Visible : Visibility.Collapsed;
            DetailTimer.Visibility = key == "Timer" ? Visibility.Visible : Visibility.Collapsed;
            DetailCalc.Visibility = key == "Calculator" ? Visibility.Visible : Visibility.Collapsed;
            DetailTextAi.Visibility = key == "TextAi" ? Visibility.Visible : Visibility.Collapsed;
            DetailWeb.Visibility = key == "Web" ? Visibility.Visible : Visibility.Collapsed;
            if (key.StartsWith("Widget_"))
            {
                DetailPlugin.Visibility = Visibility.Visible;
                FillPluginDetail(key.Substring("Widget_".Length));
            }
            else if (key.StartsWith("Birdcage_"))
            {
                DetailPlugin.Visibility = Visibility.Visible;
                FillBirdcageDetail(key.Substring("Birdcage_".Length));
            }
            else
            {
                DetailPlugin.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>精调区：鸟笼小组件变体的信息（编辑/删除请到鸟笼页）。</summary>
        private void FillBirdcageDetail(string id)
        {
            DetailPlugin.Children.Clear();
            var cp = _settings.CustomPanels.FirstOrDefault(p => p.Id == id);
            if (cp == null) return;
            DetailPlugin.Children.Add(new TextBlock
            {
                Text = cp.Name,
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 4)
            });
            DetailPlugin.Children.Add(new TextBlock
            {
                Text = "鸟笼小组件变体（动态编译）",
                FontSize = 10.5,
                Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)),
                Margin = new Thickness(0, 0, 0, 10),
                TextWrapping = TextWrapping.Wrap
            });
            DetailPlugin.Children.Add(new TextBlock
            {
                Text = "编辑源码、编译与删除请在「鸟笼」页操作；此处仅控制启用/停用。",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(90, 90, 90)),
                TextWrapping = TextWrapping.Wrap
            });
        }

        /// <summary>精调区：用户插件的状态/权限/启用/编辑/删除。</summary>
        private void FillPluginDetail(string id)
        {
            DetailPlugin.Children.Clear();
            var plugin = WidgetPluginStore.GetById(id);
            if (plugin == null) return;
            string key = "Widget_" + plugin.Id;

            DetailPlugin.Children.Add(new TextBlock
            {
                Text = plugin.Name,
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 4)
            });

            bool compileOk = string.IsNullOrEmpty(WidgetCompiler.Validate(plugin.Id, plugin.Source));
            var permText = plugin.Permissions.Count == 0
                ? LocalizationManager.Instance["WidgetMkt_None"]
                : string.Join(" · ", plugin.Permissions.Select(WidgetPluginStore.PermissionLabel));
            DetailPlugin.Children.Add(new TextBlock
            {
                Text = (compileOk ? "✅  " : "⚠ 编译失败  ") + permText,
                FontSize = 10.5,
                Foreground = new SolidColorBrush(compileOk
                    ? (plugin.Permissions.Count > 0 ? Color.FromRgb(255, 170, 90) : Color.FromRgb(136, 136, 136))
                    : Color.FromRgb(200, 80, 70)),
                Margin = new Thickness(0, 0, 0, 10),
                TextWrapping = TextWrapping.Wrap
            });

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal };
            var btnDel = new System.Windows.Controls.Button
            {
                Content = LocalizationManager.Instance["WidgetMkt_Delete"],
                Style = (Style)FindResource("Win11Button"),
                Width = 76,
                Height = 26,
                FontSize = 11,
                Margin = new Thickness(8, 0, 0, 0)
            };
            btnDel.Click += (_, _) => DeletePlugin(plugin);
            btnRow.Children.Add(btnDel);
            DetailPlugin.Children.Add(btnRow);
        }

        /// <summary>卸载已安装的插件小组件。</summary>
        private void DeletePlugin(WidgetPlugin plugin)
        {
            // ★ 灵动鸟内置文件保护：官方随附的小组件删除前警告（用户自定义/领养的不适用）
            if (DynamicBird.UI.Widgets.Dynamic.WidgetPluginStore.IsBuiltin(plugin))
            {
                var warn = new DynamicBird.UI.Birdcage.ConfirmDialog(
                    "删除灵动鸟内部文件",
                    "「" + plugin.Name + "」是灵动鸟内部文件，删除可能导致运行异常。\n\n确定要删除吗？（删除自定义功能与卸载灵动鸟不受此提示影响）",
                    "确定删除", "取消")
                {
                    Owner = this
                };
                if (warn.ShowDialog() != true) return;
            }
            else if (MessageBox.Show(string.Format(LocalizationManager.Instance["WidgetMkt_DeleteConfirm"], plugin.Name),
                    LocalizationManager.Instance["WidgetMkt_Confirm"],
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }
            _settings.SetWidgetEnabled("Widget_" + plugin.Id, false);
            WidgetPluginStore.Delete(plugin.Id);
            RefreshWidgetMarket();
        }
    }
}
