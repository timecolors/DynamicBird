using ShoreHue.Core.Models;
using ShoreHue.Core.Services;
using ShoreHue.Core.Services.Configuration;
using ShoreHue.UI.Seabed;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;

namespace ShoreHue.UI.Settings.Pages
{
    /// <summary>
    /// 海床页：左侧配置树（面板设计/动画/外观/交互/状态栏 → 二级/三级），
    /// 右侧以 JSON 编辑选中节点的配置片段；可应用/恢复，后续支持 AI 提示词一键复制。
    /// </summary>
    public partial class SeabedPage : UserControl
    {
        /// <summary>新建自定义面板的默认源码模板（实现 IWidget，动态编译运行）。</summary>
        private const string DefaultPanelSource = @"using System;
using System.Windows;
using System.Windows.Controls;
using ShoreHue.UI.Widgets;

// 自定义面板源码：实现 IWidget 接口，CreateView() 返回面板内容（任意 WPF UI）。
// 支持任意 C# 与 WPF 能力（布局/样式/动画/事件/计时器…）。
public class CustomPanel : UserControl, IWidget
{
    public CustomPanel()
    {
        var text = new TextBlock
        {
            Text = ""我是自定义面板"",
            FontSize = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = System.Windows.Media.Brushes.White
        };
        Content = text;
    }

    public string Name => ""自定义面板"";
    public UserControl CreateView() => this;
    public void OnActivated() { }
    public void OnDeactivated() { }
}
";

        /// <summary>树节点（含缩进层级，供 ListBox 展示）。三级与二级用图标/缩进区分。
        /// ★ IsAdd/HasDelete 必须是属性（WPF 绑定只认属性，字段绑定失败 → ⊕/✕ 按钮不显示）。</summary>
        private sealed class FlatNode
        {
            public ConfigNode Node;
            public int Level;
            public bool IsAdd { get; set; }      // 每级末尾的"⊕ 新建同级"占位行
            public bool HasDelete { get; set; }  // 用户新建项：可删除（行末 ×）
            public string Display => IsAdd
                ? ""   // 占位行只显示 ⊕ 按钮，不再显示文字
                : Level switch
                {
                    0 => Node.Name,
                    1 => "▸ " + Node.Name,
                    _ => "• " + Node.Name
                };
            public double Indent => Level switch { 0 => 0, 1 => 14, _ => 30 };
            public System.Windows.Thickness IndentMargin => new System.Windows.Thickness(Indent, 0, 0, 0);

            // 高亮：编译报错 → 叉的红色；未启用的面板 → Windows 主题色；
            // 被预设覆盖（未启用）→ 灰色加删除线；否则默认
            public bool IsError { get; set; }
            public bool IsUnused { get; set; }
            public bool IsOverridden { get; set; }
            public bool IsApplied { get; set; }   // 该单预设当前处于"已应用"状态 → 高亮
            public System.Windows.Media.Brush TextBrush
            {
                get
                {
                    if (IsError) return _errBrush;
                    if (IsApplied) return _accentBrush;
                    if (IsUnused) return _accentBrush;
                    if (IsOverridden) return _dimBrush;
                    return System.Windows.Media.Brushes.Black;
                }
            }
            public System.Windows.TextDecorationCollection? TextDecor => IsOverridden
                ? System.Windows.TextDecorations.Strikethrough
                : null;
            private static readonly System.Windows.Media.Brush _dimBrush =
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xA0, 0xA0, 0xA0));
            private static readonly System.Windows.Media.Brush _errBrush =
                new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE0, 0x60, 0x60));
            private static readonly System.Windows.Media.Brush _accentBrush = AccentBrush();

            /// <summary>Windows 主题色（随系统强调色变化），取不到时用蓝色。</summary>
            private static System.Windows.Media.Brush AccentBrush()
            {
                try
                {
                    var settings = new Windows.UI.ViewManagement.UISettings();
                    var c = settings.GetColorValue(Windows.UI.ViewManagement.UIColorType.Accent);
                    return new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromArgb(c.A, c.R, c.G, c.B));
                }
                catch
                {
                    return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4A, 0x90, 0xD9));
                }
            }
        }

        private readonly ISettingsService _settings;
        private List<FlatNode> _flatNodes = new();
        private ConfigNode? _selected;

        // 再点一次删除：第一次点击进入确认态（3 秒），再点同一项才真正删除（无弹窗）
        private string? _armDeleteId;
        private DateTime _armDeleteAt;
        private Button? _armDeleteBtn;
        private System.Windows.Threading.DispatcherTimer? _armDeleteTimer;

        public SeabedPage(ISettingsService settings)
        {
            InitializeComponent();
            _settings = settings;
            LoadTree();
            RefreshPresets();
        }

        /// <summary>外部（设置页解除覆盖后）刷新整页：树高亮/删除线、预设列表、当前选中编辑框。</summary>
        public void RefreshAll()
        {
            try
            {
                LoadTree();
                RefreshPresets();
                if (_selected != null) txtJsonEditor.Text = ExtractJson(_selected);
            }
            catch { }
        }

        // ========== 其他海床 · 共享平台（导出/导入） ==========

        internal ISettingsService SettingsService => _settings;

        /// <summary>树中当前选中的自定义单预设（无则 null）。</summary>
        internal ShoreHue.Core.Models.CustomPanelDefinition? CurrentSelectedCustom()
        {
            if (_selected?.CustomId == null) return null;
            return _settings.CustomPanels.FirstOrDefault(p => p.Id == _selected.CustomId);
        }

        /// <summary>下拉框中当前选中的整套预设名（无则 null）。</summary>
        internal string? CurrentSelectedPresetName => cmbPresets.SelectedItem as string;

        private void LoadTree()
        {
            ResetArm();   // 树重建后按钮被替换，清除残留的确认态
            // ★ 每次用全新 List（复用同一 List 会触发 ItemsControl "项源不一致"异常）
            var nodes = new List<FlatNode>();
            var root = ConfigTreeBuilder.Build();
            var customs = _settings.CustomPanels;
            // ★ 当前已应用的单预设名（AppliedPresets 的值）→ 树中对应项高亮
            var appliedNames = new System.Collections.Generic.HashSet<string>(
                (_settings.AppliedPresets ?? new System.Collections.Generic.Dictionary<string, string>()).Values);

            foreach (var c1 in root.Children)
            {
                c1.Parent = root;
                nodes.Add(StdNode(c1, 0));
                foreach (var c2 in c1.Children)
                {
                    c2.Parent = c1;
                    nodes.Add(StdNode(c2, 1));
                    if (c2.Children.Count > 0)
                    {
                        // 三级 + 三级下新建项
                        foreach (var c3 in c2.Children)
                        {
                            c3.Parent = c2;
                            nodes.Add(StdNode(c3, 2));
                            foreach (var cp in customs.Where(p => p.ParentKey == c3.Key))
                            {
                                nodes.Add(CustomNode(cp, 3, appliedNames));   // 三级新建项在该三级下
                            }
                        }
                        // 二级下新建项：显示在该二级列表末尾（⊕ 之前），⊕ 始终当级最下面
                        foreach (var cp in customs.Where(p => p.ParentKey == c2.Key))
                        {
                            nodes.Add(CustomNode(cp, 2, appliedNames));
                        }
                        // 该二级下三级末尾 ⊕
                        nodes.Add(new FlatNode { Node = c2, Level = 2, IsAdd = true });
                    }
                    else
                    {
                        // 无三级：二级下新建项直接显示在该二级末尾（⊕ 之前）
                        foreach (var cp in customs.Where(p => p.ParentKey == c2.Key))
                        {
                            nodes.Add(CustomNode(cp, 2, appliedNames));
                        }
                    }
                }
                // 一级下新建项：显示在该一级列表末尾（⊕ 之前）
                foreach (var cp in customs.Where(p => p.ParentKey == c1.Key))
                {
                    nodes.Add(CustomNode(cp, 1, appliedNames));
                }
                // 该一级下二级末尾 ⊕（新建二级功能）
                nodes.Add(new FlatNode { Node = c1, Level = 1, IsAdd = true });
            }
            // 第一级新建项：显示在第一级末尾（⊕ 之前）
            foreach (var cp in customs.Where(p => p.ParentKey == "root"))
            {
                nodes.Add(CustomNode(cp, 0, appliedNames));
            }
            // 整个第一级末尾：一个 ⊕（新建一级分类）
            nodes.Add(new FlatNode { Node = root, Level = 0, IsAdd = true });

            _flatNodes = nodes;
            lstConfigTree.ItemsSource = _flatNodes;
        }

        /// <summary>标准节点（编译失败按 Key 红色高亮；被预设覆盖→灰色删除线）。</summary>
        private FlatNode StdNode(ConfigNode node, int level)
        {
            var overrides = _settings.AppliedPresets;
            bool overridden = overrides != null && overrides.ContainsKey(node.Key);
            return new FlatNode
            {
                Node = node,
                Level = level,
                IsError = _errorNodeKey == node.Key,
                IsOverridden = overridden
            };
        }

        /// <summary>用户新建项节点（CustomId + 可删除标记 + 高亮）。</summary>
        private FlatNode CustomNode(CustomPanelDefinition cp, int level, System.Collections.Generic.HashSet<string> appliedNames)
        {
            return new FlatNode
            {
                Node = new ConfigNode
                {
                    Key = "custom_" + cp.Id,
                    Name = cp.Name,
                    Category = cp.Category,
                    CustomId = cp.Id,
                    Kind = cp.Kind
                },
                Level = level,
                HasDelete = true,
                IsError = _errorCustomId == cp.Id,
                IsUnused = cp.Kind != "Config" && (cp.BaseType ?? "") != "Widget" && !IsPanelInUse(cp),
                IsApplied = appliedNames.Contains(cp.Name)
            };
        }

        /// <summary>该面板是否已被某个区域启用（RegionPanel_* 引用到它）。</summary>
        private static bool IsPanelInUse(CustomPanelDefinition cp)
        {
            try
            {
                var data = SettingsFileManager.Load();
                foreach (var p in typeof(SettingsData).GetProperties())
                {
                    if (!p.Name.StartsWith("RegionPanel_")) continue;
                    var v = p.GetValue(data) as string;
                    if (!string.IsNullOrEmpty(v) &&
                        (v == cp.Name || v == cp.Id || v == "custom_" + cp.Id || v == "Custom:" + cp.Id))
                        return true;
                }
            }
            catch { }
            return false;
        }

        private bool _creatingSibling; // 防重入：避免弹窗期间 SelectionChanged 再次触发创建
        private string? _errorCustomId;  // 编译失败的新建项：红色高亮提示
        private string? _errorNodeKey;   // 编译失败的标准节点（按 Key）：红色高亮提示

        private void LstConfigTree_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstConfigTree.SelectedItem is not FlatNode fn) return;

            // 占位行（⊕ 新建同级）：点击即创建（防重入；点 ⊕ 按钮也会先触发本事件）
            if (fn.IsAdd)
            {
                if (_creatingSibling) { lstConfigTree.SelectedItem = null; return; }
                _creatingSibling = true;
                try { CreateSiblingAtLevel(fn.Node.Key, fn.Level); }
                finally { _creatingSibling = false; }
                lstConfigTree.SelectedItem = null;
                return;
            }

            _selected = fn.Node;
            txtNodeTitle.Text = fn.Node.Name;
            UpdateOpenFolderButton();
            bool hasTemplate = ShoreHue.UI.Seabed.BuiltinFeatureSources.Sources.ContainsKey(fn.Node.Key);
            txtNodeHint.Text = !string.IsNullOrEmpty(fn.Node.CustomId)
                ? BuildCustomHint(fn.Node)
                : hasTemplate
                    ? "功能源码（可在原代码基础上修改，保存当前节点→同级新建）"
                    : fn.Node.IsLeaf
                        ? $"共 {fn.Node.FieldNames.Count} 项可编辑"
                        : "分组（含子项，编辑需选中子项）";
            txtJsonEditor.Text = ExtractJson(fn.Node);
            txtJsonStatus.Text = "";
        }

        /// <summary>自定义项提示：源码类型。</summary>
        private string BuildCustomHint(ConfigNode node)
        {
            var cp = _settings.CustomPanels.FirstOrDefault(p => p.Id == node.CustomId);
            string typeText = (cp?.Kind ?? "") switch
            {
                "Widget" => "小组件变体源码（C#，实现 IWidget，编译进小组件标签）",
                "Category" => "新分类（仅树结构）",
                "Config" => "单预设配置代码（C#，可应用并标记冲突）",
                _ => "面板源码（C#，实现 IWidget，编译后注册到区域面板）"
            };
            return typeText;
        }

        /// <summary>进入"再点一次删除"确认态（3 秒内再点同一项生效），无弹窗。</summary>
        private void ArmDelete(Button btn, string customId)
        {
            ResetArm();   // 先恢复上一次的确认态
            _armDeleteId = customId;
            _armDeleteAt = DateTime.Now;
            _armDeleteBtn = btn;
            var brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE0, 0x60, 0x60));
            btn.Content = "再点一次删除";
            btn.Width = double.NaN;              // Auto 撑开文字
            btn.Padding = new Thickness(6, 0, 6, 0);
            btn.Foreground = System.Windows.Media.Brushes.White;
            btn.Background = brush;
            btn.BorderThickness = new Thickness(1);
            // 把行末列从 26px 撑到 Auto：按钮向右扩展，不覆盖左侧文本
            if (btn.Parent is Grid grid && grid.ColumnDefinitions.Count > 1)
                grid.ColumnDefinitions[1].Width = new GridLength(0, GridUnitType.Auto);
            _armDeleteTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3),
                IsEnabled = true
            };
            _armDeleteTimer.Tick += (_, _) => ResetArm();
        }

        /// <summary>恢复删除按钮原样（超时 / 点击其他删除按钮时调用）。</summary>
        private void ResetArm()
        {
            if (_armDeleteTimer != null)
            {
                _armDeleteTimer.Stop();
                _armDeleteTimer = null;
            }
            if (_armDeleteBtn != null)
            {
                var btn = _armDeleteBtn;
                btn.Content = "✕";
                btn.Width = 20;
                btn.Padding = new Thickness(0);
                btn.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE0, 0x60, 0x60));
                btn.Background = System.Windows.Media.Brushes.Transparent;
                btn.BorderThickness = new Thickness(0);
                if (btn.Parent is Grid grid && grid.ColumnDefinitions.Count > 1)
                    grid.ColumnDefinitions[1].Width = new GridLength(26, GridUnitType.Pixel);
            }
            _armDeleteBtn = null;
            _armDeleteId = null;
        }

        /// <summary>是否处于同一项的"再点一次删除"确认态（3 秒内）。</summary>
        private bool IsArmed(string customId) =>
            _armDeleteId == customId && (DateTime.Now - _armDeleteAt).TotalSeconds <= 3;

        /// <summary>用户新建项行末的 ×：第一次点击进入确认态，再点一次删除整个项。</summary>
        private void BtnDeleteNode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.DataContext is not FlatNode fn) return;
            string customId = fn.Node.CustomId ?? "";
            if (string.IsNullOrEmpty(customId)) return;

            if (IsArmed(customId))
            {
                ResetArm();
                try
                {
                    string name = fn.Node.Name;
                    var list = _settings.CustomPanels;
                    list.RemoveAll(p => p.Id == customId);
                    _settings.CustomPanels = list;
                    // ★ 树↔文件夹同步：删除对应文件夹（按 manifest.id 匹配）
                    ShoreHue.UI.Widgets.Dynamic.WidgetPluginStore.DeleteNodeFolder(customId);

                    // ★ 删除已应用的单预设 → 清除其冲突标记（对应设置页变灰解除）
                    var overrides = _settings.AppliedPresets;
                    if (overrides != null && overrides.Count > 0 && overrides.Values.Contains(name))
                    {
                        bool changed = false;
                        foreach (var k in overrides.Where(kv => kv.Value == name).Select(kv => kv.Key).ToList())
                        {
                            overrides.Remove(k);
                            changed = true;
                        }
                        if (changed)
                        {
                            _settings.AppliedPresets = overrides;
                            _settings.Reload();
                            RefreshOwnerSettingsDimming();
                        }
                    }

                    if (_selected?.CustomId == customId)
                    {
                        _selected = null;
                        txtNodeTitle.Text = "";
                        UpdateOpenFolderButton();
                        txtJsonEditor.Text = "";
                    }
                    LoadTree();
                    txtJsonStatus.Text = "已删除单预设：" + name;
                }
                catch (Exception ex)
                {
                    ShoreHue.Core.Infrastructure.Logging.LogManager.Error("删除单预设失败", ex);
                    txtJsonStatus.Text = ex.Message;
                }
                return;
            }
            ArmDelete(btn, customId);
        }

        /// <summary>占位行的 ⊕ 按钮：创建同级新功能（Click 只触发一次）。</summary>
        private void BtnAddNode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.DataContext is not FlatNode fn || !fn.IsAdd) return;
            if (_creatingSibling) return;   // 防重入
            _creatingSibling = true;
            try { CreateSiblingAtLevel(fn.Node.Key, fn.Level); }
            finally { _creatingSibling = false; }
        }

        /// <summary>在指定分组创建一个同级新功能（ParentKey 记录归属，显示在点击的分组下）。</summary>
        private void CreateSiblingAtLevel(string parentKey, int level)
        {
            try
            {
                string levelName = level switch { 0 => "第一级（新分类）", 1 => "第二级（新功能）", _ => "第三级（新实例）" };
                // 像新建文件夹一样：同一父级分组内单独计数，从「预设1」开始递增（不同层级/分组互不影响）
                int maxNum = 0;
                foreach (var p in _settings.CustomPanels)
                {
                    if (p.ParentKey == parentKey && p.Name.StartsWith("预设") && int.TryParse(p.Name.Substring(2), out int n))
                    {
                        if (n > maxNum) maxNum = n;
                    }
                }
                string defaultName = "预设" + (maxNum + 1);
                var dlg = new InputDialog("海床 · 创建同级新功能", $"新建{levelName}，输入名称：", defaultName);
                dlg.Owner = System.Windows.Window.GetWindow(this);
                bool? dlgResult = dlg.ShowDialog();
                if (dlgResult != true) { txtJsonStatus.Text = "已取消新增"; return; }

                string input = dlg.ResultText;
                if (string.IsNullOrEmpty(input)) { txtJsonStatus.Text = "名称不能为空"; return; }

                // 新建自动归类：按父级分组决定类型（不用用户选）
                //  - 小组件分组（panel-widgets）或小组件叶子下 → 小组件变体 Widget
                //  - 其他配置分组 → 配置代码项 Config（仅海床）
                //  - 第一级末尾（root）→ 新分类 Category（仅树结构）
                bool isWidgetParent = parentKey == "panel-widgets" || parentKey.StartsWith("widget-", StringComparison.Ordinal);
                string baseType = parentKey == "root" ? "Category" : isWidgetParent ? "Widget" : "Config";
                string kind = parentKey == "root" ? "Category" : isWidgetParent ? "Widget" : "Config";

                var list = _settings.CustomPanels;
                list.Add(new ShoreHue.Core.Models.CustomPanelDefinition
                {
                    Id = "custom_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                    Name = input,
                    Category = "面板设计",
                    ParentKey = parentKey,
                    BaseType = baseType,
                    Kind = kind,
                    ConfigJson = "{}",
                    Source = isWidgetParent ? DefaultPanelSource : "",
                    CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
                });
                _settings.CustomPanels = list;
                // ★ 树↔文件夹同步：新节点落盘到 seabed/ 对应分组文件夹（manifest + 内容文件）
                var created = list.FirstOrDefault(p => p.Name == input && p.ParentKey == parentKey);
                if (created != null) ShoreHue.UI.Widgets.Dynamic.WidgetPluginStore.SaveNodeToFolder(created);
                LoadTree();
                txtJsonStatus.Text = $"已创建{levelName}：{input}（在「自定义功能」下编辑，文件已写入小组件文件夹）";
            }
            catch (Exception ex)
            {
                ShoreHue.Core.Infrastructure.Logging.LogManager.Error("创建同级功能失败", ex);
                txtJsonStatus.Text = ex.Message;
            }
        }



        /// <summary>从当前配置中提取节点绑定字段，生成 JSON 片段。</summary>
        private string ExtractJson(ConfigNode node)
        {
            try
            {
                // 自定义面板：编辑源码（C#，动态编译运行）
                if (!string.IsNullOrEmpty(node.CustomId))
                {
                    var cp = _settings.CustomPanels.FirstOrDefault(p => p.Id == node.CustomId);
                    return cp?.Source ?? DefaultPanelSource;
                }

                // 功能节点（有源码模板）：显示纯代码版源码，可在原代码基础上修改
                if (ShoreHue.UI.Seabed.BuiltinFeatureSources.Sources.TryGetValue(node.Key, out var tpl))
                {
                    return tpl;
                }
                // 纯配置节点：生成可执行的配置代码（C#，编译校验，不写回）
                return BuildConfigCode(node);
            }
            catch (Exception ex)
            {
                return "// 提取失败: " + ex.Message;
            }
        }

        /// <summary>根据节点绑定的字段生成可执行配置代码模板（赋值语句，随当前配置值）。
        /// 完整可执行：编译后（ConfigCode.Apply）可应用写回，海床里"保存当前预设→应用"即生效。</summary>
        public static string BuildConfigCode(ConfigNode node)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("// " + node.Category + " · " + node.Name + "（配置代码，编译校验后生效）");
            sb.AppendLine("// 说明：");
            sb.AppendLine("//  - 每行一个赋值语句，字段名必须与 SettingsData 属性一致；");
            sb.AppendLine("//  - 每行上方为字段说明注释，方便理解字段含义；");
            sb.AppendLine("//  - 点「编译」仅校验语法/字段，不写回当前配置；");
            sb.AppendLine("//  - 点「保存当前节点」在同级创建「" + node.Name + "N」新项，原节点保持不变。");
            sb.AppendLine();
            sb.AppendLine("public static class ConfigCode");
            sb.AppendLine("{");
            sb.AppendLine("    public static void Apply(ShoreHue.Core.Services.Configuration.SettingsData data)");
            sb.AppendLine("    {");
            try
            {
                var data = SettingsFileManager.Load();
                var json = JsonSerializer.Serialize(data);
                using var doc = JsonDocument.Parse(json);
                foreach (var f in node.FieldNames)
                {
                    AppendConfigFieldLine(sb, doc, f);
                }
            }
            catch
            {
                foreach (var f in node.FieldNames)
                {
                    AppendConfigFieldLine(sb, null, f);
                }
            }
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        /// <summary>写入一行"字段说明注释 + 赋值语句"。值取不到时写 default（字段说明仍保留）。</summary>
        private static void AppendConfigFieldLine(System.Text.StringBuilder sb, JsonDocument? doc, string field)
        {
            string docText = ShoreHue.UI.Seabed.SettingsFieldDocs.DocOrName(field);
            sb.AppendLine("        // " + field + "：" + docText);
            if (doc != null && doc.RootElement.TryGetProperty(field, out var v))
            {
                sb.AppendLine("        data." + field + " = " + ToCSharpLiteral(v) + ";");
            }
            else
            {
                sb.AppendLine("        data." + field + " = default;");
            }
        }

        /// <summary>JsonElement → C# 字面量（字符串/数字/布尔）。</summary>
        private static string ToCSharpLiteral(JsonElement v)
        {
            switch (v.ValueKind)
            {
                case JsonValueKind.String:
                    return "\"" + v.GetString()?.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
                case JsonValueKind.Number:
                    return v.GetRawText();
                case JsonValueKind.True:
                    return "true";
                case JsonValueKind.False:
                    return "false";
                case JsonValueKind.Null:
                    return "null";
                default:
                    return v.GetRawText();
            }
        }

        /// <summary>编译：校验当前节点配置并写入。编译失败 → 该项红色高亮提示。</summary>
        private void BtnCompile_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null) return;
            try
            {
                // 自定义面板：校验 C# 源码（Roslyn），通过后保存并注册
                if (!string.IsNullOrEmpty(_selected.CustomId))
                {
                    var list = _settings.CustomPanels;
                    var cp = list.FirstOrDefault(p => p.Id == _selected.CustomId);
                    if (cp != null)
                    {
                        string src = txtJsonEditor.Text ?? "";
                        // ★ 沙箱：市场来源先拦截危险 API（本地自写不受限）
                        if (!cp.TrustedSource)
                        {
                            string sandboxErr = ShoreHue.UI.Widgets.Dynamic.WidgetCompiler.SandboxErrors(src);
                            if (sandboxErr.Length > 0)
                            {
                                _errorCustomId = cp.Id;
                                LoadTree();
                                txtJsonStatus.Text = "沙箱拦截：" + sandboxErr;
                                return;
                            }
                        }
                        string err = ShoreHue.UI.Widgets.Dynamic.WidgetCompiler.Validate("panel_" + cp.Id, src);
                        if (err.Length > 0)
                        {
                            _errorCustomId = cp.Id;
                            LoadTree();
                            txtJsonStatus.Text = "编译失败：" + err;
                            return;
                        }
                        cp.Source = src;
                        _settings.CustomPanels = list;
                        // ★ 树↔文件夹同步：更新文件
                        ShoreHue.UI.Widgets.Dynamic.WidgetPluginStore.SaveNodeToFolder(cp);
                        _errorCustomId = null;   // 编译通过，清除错误高亮
                        LoadTree();
                        txtJsonStatus.Text = cp.Kind == "Config"
                            ? "编译通过（配置代码项，未注册为面板）"
                            : "编译通过，面板已注册（可在 设置→区域面板 中选用）";
                        return;
                    }
                }

                // 标准节点：编译校验配置代码（Roslyn），不写回当前配置
                string code = txtJsonEditor.Text ?? "";
                string codeErr = ValidateConfigCode(code);
                if (codeErr.Length > 0)
                {
                    _errorNodeKey = _selected.Key;   // 编译失败：该项红色高亮
                    LoadTree();
                    txtJsonStatus.Text = "编译失败：" + codeErr;
                    return;
                }
                _errorCustomId = null;
                _errorNodeKey = null;
                LoadTree();
                txtJsonStatus.Text = "编译通过（未写回）。点「保存当前节点」在同级创建「" + _selected.Name + "N」新项。";
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrEmpty(_selected.CustomId))
                {
                    _errorCustomId = _selected.CustomId;  // 编译失败：该项红色高亮
                }
                else
                {
                    _errorNodeKey = _selected.Key;
                }
                LoadTree();
                txtJsonStatus.Text = "编译失败：" + ex.Message;
            }
        }

        /// <summary>把用户代码包成可编译类并用 Roslyn 校验（仅校验，不执行不写回）。</summary>
        private static string ValidateConfigCode(string userCode)
        {
            try
            {
                string wrapped = "using ShoreHue.Core.Services.Configuration;" + System.Environment.NewLine + userCode;
                return ShoreHue.UI.Widgets.Dynamic.WidgetCompiler.Validate("config_" + Guid.NewGuid().ToString("N").Substring(0, 8), wrapped);
            }
            catch (Exception ex)
            {
                return "编译异常：" + ex.Message;
            }
        }

        /// <summary>
        /// 恢复 = 一键复原所有设置（内置默认值），不清除已创建的预设/变体；
        /// 解除全部变灰（清空 AppliedPresets），需要时可在预设列表重新应用。
        /// </summary>
        private void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var defaults = new ShoreHue.Core.Services.Configuration.SettingsData();
                var old = SettingsFileManager.Load();
                // 保留编程模式相关数据（不删用户已创建的预设/变体）
                defaults.ProgrammingModeEnabled = old.ProgrammingModeEnabled;
                defaults.CustomPanels = old.CustomPanels;
                SettingsFileManager.Save(defaults);
                _settings.Reload();
                _errorCustomId = null;
                _errorNodeKey = null;
                LoadTree();
                RefreshOwnerSettingsDimming();
                txtJsonStatus.Text = "已一键复原所有设置（预设/变体保留，变灰已解除）";
                if (_selected != null) txtJsonEditor.Text = ExtractJson(_selected);
            }
            catch (Exception ex) { txtJsonStatus.Text = ex.Message; }
        }

        /// <summary>更新「打开文件夹」按钮可用性（选中任意节点即启用：内置/自定义都定位到对应目录）。</summary>
        private void UpdateOpenFolderButton()
        {
            if (btnOpenNodeFolder == null) return;
            btnOpenNodeFolder.IsEnabled = _selected != null;
        }

        /// <summary>在系统文件管理器中打开当前选中功能的文件夹（源码/配置/清单所在目录）。</summary>
        private void BtnOpenNodeFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // ★ 未选中节点也兜底打开根目录（避免"点击有反馈但没动作"）
                if (_selected == null)
                {
                    ShoreHue.UI.Widgets.Dynamic.WidgetPluginStore.OpenFolder();
                    return;
                }
                var sel = _selected;
                // 自定义项：按 manifest.id 定位
                if (!string.IsNullOrEmpty(sel.CustomId))
                {
                    var cp = _settings.CustomPanels.FirstOrDefault(p => p.Id == sel.CustomId);
                    if (cp != null)
                    {
                        ShoreHue.UI.Widgets.Dynamic.WidgetPluginStore.OpenNodeFolder(cp.Id, cp.Name, cp.Category);
                        return;
                    }
                }
                // 内置/配置节点：按 分类 + 节点Key/名称 定位到对应分组目录
                ShoreHue.UI.Widgets.Dynamic.WidgetPluginStore.OpenNodeFolder("", sel.Name, sel.Category);
            }
            catch (Exception ex)
            {
                txtJsonStatus.Text = ex.Message;
            }
        }

        /// <summary>打开 AI 编程指南文档（docs/AI-PROGRAMMING.md，发布时随 exe 携带）。</summary>
        private void BtnAiGuide_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 运行目录优先（发布携带），回退项目根（开发期）
                string? doc = Path.Combine(AppContext.BaseDirectory, "docs", "AI-PROGRAMMING.md");
                if (!File.Exists(doc))
                {
                    string? root = AppContext.BaseDirectory;
                    for (int i = 0; i < 8 && root != null; i++)
                    {
                        if (File.Exists(Path.Combine(root, "ShoreHue.csproj")))
                        {
                            string p = Path.Combine(root, "docs", "AI-PROGRAMMING.md");
                            if (File.Exists(p)) { doc = p; break; }
                        }
                        root = Path.GetDirectoryName(root);
                    }
                }
                if (doc == null || !File.Exists(doc))
                {
                    txtJsonStatus.Text = "未找到 AI-PROGRAMMING.md（文档未随程序发布）";
                    return;
                }
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(doc) { UseShellExecute = true });
            }
            catch (Exception ex) { txtJsonStatus.Text = ex.Message; }
        }

        private void BtnCopyPrompt_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null) return;
            string prompt = PromptGenerator.Generate(_selected, ExtractJson(_selected));
            Clipboard.SetText(prompt);
            txtJsonStatus.Text = "提示词已复制，粘贴到 AI 生成配置后粘回编程框";
        }

        // ========== 预设 ==========

        private void RefreshPresets()
        {
            cmbPresets.ItemsSource = PresetManager.ListPresets();
            cmbPresets.SelectedIndex = cmbPresets.Items.Count > 0 ? 0 : -1;
        }

        /// <summary>整套预设：弹窗命名后保存（只有整套才需要命名）。</summary>
        private void BtnSaveFull_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new InputDialog("海床 · 保存整套预设", "整套预设保存全部配置，输入预设名称：", "整套预设");
            dlg.Owner = System.Windows.Window.GetWindow(this);
            if (dlg.ShowDialog() != true) { txtJsonStatus.Text = "已取消保存整套预设"; return; }
            string name = dlg.ResultText.Trim();
            if (string.IsNullOrEmpty(name)) { txtJsonStatus.Text = "预设名称不能为空"; return; }
            try
            {
                PresetManager.SaveFull(name, SettingsFileManager.Load());
                RefreshPresets();
                txtJsonStatus.Text = "整套预设已保存：" + name;
            }
            catch (Exception ex) { txtJsonStatus.Text = ex.Message; }
        }

        /// <summary>
        /// 保存当前节点：
        ///  A) 标准设置节点（无 CustomId）→ 同级新建「名称N」项并保存（原节点不动）；
        ///  B) 已有自定义项（有 CustomId）→ 直接保存编程框内容到该项，不再新建。
        /// </summary>
        private void BtnSavePartial_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null) { txtJsonStatus.Text = "请先选中一个节点"; return; }
            string code = txtJsonEditor.Text ?? "";
            if (string.IsNullOrWhiteSpace(code)) { txtJsonStatus.Text = "编程框内容为空，无法保存"; return; }

            // ===== 场景 B：已有自定义项 → 直接保存到该项 =====
            if (!string.IsNullOrEmpty(_selected.CustomId))
            {
                try
                {
                    var list = _settings.CustomPanels;
                    var cp = list.FirstOrDefault(p => p.Id == _selected.CustomId);
                    if (cp == null) { txtJsonStatus.Text = "找不到该项，无法保存"; return; }
                    cp.Source = code;
                    _settings.CustomPanels = list;
                    // ★ 树↔文件夹同步：更新文件
                    ShoreHue.UI.Widgets.Dynamic.WidgetPluginStore.SaveNodeToFolder(cp);
                    _errorCustomId = null;
                    LoadTree();
                    txtJsonStatus.Text = $"已更新单预设「{cp.Name}」";
                }
                catch (Exception ex) { txtJsonStatus.Text = ex.Message; }
                return;
            }

            // ===== 场景 A：标准设置节点 → 同级新建并保存 =====
            // 找父级 Key：标准节点用树中父节点 Key
            string parentKey = _selected.Parent?.Key ?? "root";

            // 同级命名「节点名N」：名字尾部有数字则递增（预设1→预设2），无数字从 1 开始（计时器→计时器1）
            string newName = NextSiblingName(_selected.Name,
                _settings.CustomPanels.Where(p => p.ParentKey == parentKey).Select(p => p.Name));

            // 功能节点（有内置源码模板）→ 变体：面板功能 → Kind=Panel（进区域面板下拉）；
            // 小组件 → Kind=Widget（进小组件标签）；纯配置节点 → Kind=Config（仅编辑）
            bool isFeature = ShoreHue.UI.Seabed.BuiltinFeatureSources.Sources.ContainsKey(_selected.Key);
            bool isPanel = ShoreHue.UI.Seabed.BuiltinFeatureSources.PanelKeys.Contains(_selected.Key);
            string baseType = isPanel ? "Panel" : isFeature ? "Widget" : "Config";
            string kind = isPanel ? "Panel" : isFeature ? "Widget" : "Config";
            try
            {
                var list = _settings.CustomPanels;
                list.Add(new ShoreHue.Core.Models.CustomPanelDefinition
                {
                    Id = "custom_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                    Name = newName,
                    Category = _selected.Category,
                    ParentKey = parentKey,
                    BaseType = baseType,
                    Kind = kind,
                    ConfigJson = "{}",
                    Source = code,
                    SourceKey = _selected.Key,   // 记录来源：应用时若冲突则原节点高亮/设置页变灰
                    CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
                });
                _settings.CustomPanels = list;
                // ★ 树↔文件夹同步：变体落盘到 seabed/ 对应分组文件夹
                var savedCp = list.LastOrDefault(p => p.Name == newName && p.ParentKey == parentKey);
                if (savedCp != null) ShoreHue.UI.Widgets.Dynamic.WidgetPluginStore.SaveNodeToFolder(savedCp);
                LoadTree();
                txtJsonStatus.Text = isPanel
                    ? $"已保存单预设「{newName}」（面板变体，可在 设置→区域面板 中选用）"
                    : isFeature
                        ? $"已保存单预设「{newName}」（小组件变体，可在 小组件 页签选用）"
                        : $"已保存单预设「{newName}」（原内置节点未改动）";
            }
            catch (Exception ex) { txtJsonStatus.Text = ex.Message; }
        }

        /// <summary>同级命名：名字尾部数字递增（预设1→预设2）；无数字则从 1 开始（计时器→计时器1）。</summary>
        private static string NextSiblingName(string baseName, System.Collections.Generic.IEnumerable<string> existing)
        {
            // 分离尾部数字
            int i = baseName.Length - 1;
            while (i >= 0 && char.IsDigit(baseName[i])) i--;
            string prefix = baseName.Substring(0, i + 1);
            int start = i < baseName.Length - 1
                ? (int.TryParse(baseName.Substring(i + 1), out int v) ? v + 1 : 1)
                : 1;
            var used = new System.Collections.Generic.HashSet<string>(existing);
            for (int n = start; ; n++)
            {
                string candidate = prefix + n;
                if (!used.Contains(candidate)) return candidate;
            }
        }

        private void BtnApplyPreset_Click(object sender, RoutedEventArgs e)
        {
            // ★ 树里选中了单预设（用户新建项）→ 应用单预设（执行其代码并标记冲突）
            if (_selected?.CustomId != null)
            {
                ApplyCustomPreset(_selected);
                return;
            }

            if (cmbPresets.SelectedItem is not string name) return;
            bool ok = PresetManager.ApplyPreset(name, _settings);
            if (!ok) { txtJsonStatus.Text = "应用失败（预设可能损坏）"; return; }

            // ★ 记录冲突来源：预设覆盖了哪些内置节点 → 海床左侧高亮、设置页对应分组变灰
            var applied = PresetManager.AppliedFields(name);
            var overrides = _settings.AppliedPresets ?? new System.Collections.Generic.Dictionary<string, string>();
            foreach (var field in applied)
            {
                var chain = ShoreHue.UI.Seabed.ConfigTreeBuilder.FindNodeChain(field);
                if (chain.Count == 0) continue;
                // 记录整条链：一级(页签) + 二级(项) + 三级(小组件内容)；最后应用的覆盖先前
                foreach (var node in chain)
                {
                    overrides[node.Key] = name;
                }
            }
            _settings.AppliedPresets = overrides;
            _settings.Reload();
            LoadTree();
            RefreshOwnerSettingsDimming();
            txtJsonStatus.Text = $"预设已应用：{name}（冲突的内置设置已置灰，海床左侧对应项高亮）";
            if (_selected != null) txtJsonEditor.Text = ExtractJson(_selected);
        }

        /// <summary>应用树中选中的单预设：执行其配置代码写回设置，并按代码赋值字段标记冲突。</summary>
        private void ApplyCustomPreset(ConfigNode node)
        {
            var cp = _settings.CustomPanels.FirstOrDefault(p => p.Id == node.CustomId);
            if (cp == null) { txtJsonStatus.Text = "找不到该单预设"; return; }

            // 小组件变体：本身就是小组件（编译进小组件标签），无需"应用"，仅提示
            if (cp.Kind == "Widget" || (cp.BaseType ?? "") == "Widget")
            {
                txtJsonStatus.Text = $"「{cp.Name}」是小组件变体，已在小组件标签中启用，无需应用";
                return;
            }

            string code = cp.Source ?? "";
            if (string.IsNullOrWhiteSpace(code)) { txtJsonStatus.Text = "该单预设源码为空"; return; }

            // ★ 沙箱：市场来源先拦截危险 API（配置代码同样受限）
            if (!cp.TrustedSource)
            {
                string sandboxErr = ShoreHue.UI.Widgets.Dynamic.WidgetCompiler.SandboxErrors(code);
                if (sandboxErr.Length > 0)
                {
                    txtJsonStatus.Text = "沙箱拦截：" + sandboxErr;
                    return;
                }
            }

            var apply = ShoreHue.UI.Widgets.Dynamic.WidgetCompiler.CompileConfigApply(code, out string err);
            if (apply == null) { txtJsonStatus.Text = "应用失败：" + err; return; }

            try
            {
                var data = SettingsFileManager.Load();
                apply(data);   // ★ 执行用户配置代码，就地修改字段值

                // ★ 冲突标记：代码里赋值过的字段 → 节点链；来源节点本身也标记
                var overrides = data.AppliedPresets ?? new System.Collections.Generic.Dictionary<string, string>();
                if (!string.IsNullOrEmpty(cp.SourceKey)) overrides[cp.SourceKey] = cp.Name;
                foreach (var f in ExtractAssignedFields(code))
                {
                    var chain = ConfigTreeBuilder.FindNodeChain(f);
                    foreach (var n in chain) overrides[n.Key] = cp.Name;
                }
                data.AppliedPresets = overrides;

                // ★ 整体替换 + 落盘 + 通知（SettingsManager.Apply 语义）
                _settings.Apply(data);
                _errorCustomId = null;
                _errorNodeKey = null;
                LoadTree();
                RefreshOwnerSettingsDimming();
                txtJsonStatus.Text = $"已应用单预设「{cp.Name}」（冲突的内置设置已置灰）";
                if (_selected != null) txtJsonEditor.Text = ExtractJson(_selected);
            }
            catch (Exception ex)
            {
                txtJsonStatus.Text = ex.Message;
            }
        }

        /// <summary>从配置代码中提取被赋值的 SettingsData 字段名（data.XXX = …）。</summary>
        private static System.Collections.Generic.List<string> ExtractAssignedFields(string code)
        {
            var result = new System.Collections.Generic.List<string>();
            if (string.IsNullOrEmpty(code)) return result;
            foreach (System.Text.RegularExpressions.Match m in
                System.Text.RegularExpressions.Regex.Matches(code, @"data.([A-Za-z_][A-Za-z0-9_]*)s*="))
            {
                string f = m.Groups[1].Value;
                if (!result.Contains(f)) result.Add(f);
            }
            return result;
        }

        /// <summary>刷新所属设置窗口的预设变灰（应用/删除单预设后调用）。</summary>
        private void RefreshOwnerSettingsDimming()
        {
            try
            {
                if (System.Windows.Window.GetWindow(this) is ShoreHue.UI.Settings.SettingsWindow sw)
                {
                    sw.RefreshPresetDimming();
                }
            }
            catch { }
        }

        private void BtnDeletePreset_Click(object sender, RoutedEventArgs e)
        {
            if (cmbPresets.SelectedItem is not string name) return;
            // ★ 预设是 ShoreHue 管理的内部数据：删除前警告（已应用的设置会回退）
            if (!ShoreHue.UI.Seabed.SeabedFileGuard.ConfirmDelete(
                    System.Windows.Window.GetWindow(this), name,
                    "删除预设",
                    "「" + name + "」是一个已保存的预设。删除后：\n• 已应用的该预设配置会回退到默认\n• 无法再恢复\n\n确定要删除吗？"))
            {
                return;
            }
            PresetManager.DeletePreset(name);
            // 删除预设 → 清除其覆盖标记（对应变灰解除）
            var overrides = _settings.AppliedPresets;
            if (overrides != null && overrides.Count > 0)
            {
                bool changed = false;
                foreach (var k in overrides.Where(kv => kv.Value == name).Select(kv => kv.Key).ToList())
                {
                    overrides.Remove(k);
                    changed = true;
                }
                if (changed)
                {
                    _settings.AppliedPresets = overrides;
                    _settings.Reload();
                    LoadTree();
                    RefreshOwnerSettingsDimming();
                }
            }
            RefreshPresets();
            txtJsonStatus.Text = "已删除预设：" + name + "（其覆盖标记已清除）";
        }

        /// <summary>其他海床：共享平台（导出 .dbp 包 / 导入并提示风险权限）。</summary>
        private void BtnOtherSeabed_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var win = new ShoreHue.UI.Seabed.SeabedMarketWindow(this)
                {
                    Owner = System.Windows.Window.GetWindow(this)
                };
                win.ShowDialog();
            }
            catch (Exception ex)
            {
                txtJsonStatus.Text = ex.Message;
            }
        }
    }
}
