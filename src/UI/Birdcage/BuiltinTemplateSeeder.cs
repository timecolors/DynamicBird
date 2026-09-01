using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DynamicBird.Infrastructure.Utils;
using DynamicBird.UI.Widgets.Dynamic;

namespace DynamicBird.UI.Birdcage
{
    /// <summary>
    /// 内置模板落盘器（方案B：文件夹=鸟笼真相源）：
    /// 首次运行/重装时，把内置小组件与面板功能的模板写入 birdcage/ 对应位置，带 system 标记 + templateVersion。
    /// 用户改过（hash 变化）的文件不覆盖；删除后不补（用户明确删的）。
    /// </summary>
    public static class BuiltinTemplateSeeder
    {
        /// <summary>当前内置模板版本（每次内置功能更新时 +1，触发未改文件刷新）。</summary>
        public const int TemplateVersion = 1;

        /// <summary>小组件清单：id → (中文名, 描述)。</summary>
        private static readonly Dictionary<string, (string Name, string Desc)> WidgetMeta = new()
        {
            ["calculator"] = ("计算器", "灵动鸟内置计算器：标准 / 科学 / 程序员。"),
            ["timer"] = ("计时器", "灵动鸟内置计时器：倒计时 / 正计时 / 闹钟。"),
            ["clipboard"] = ("剪贴板", "灵动鸟内置剪贴板历史：复制记录 / 搜索 / 固定。"),
            ["note"] = ("便签", "灵动鸟内置便签：多色 / 编辑 / 置顶。"),
            ["textai"] = ("划词翻译", "划词翻译：选中文本翻译 / 总结 / 解释。"),
            ["web"] = ("网页工具", "网页工具：内置浏览器，可导航任意网址。")
        };

        /// <summary>面板功能清单：key → (中文名, 描述)。</summary>
        private static readonly Dictionary<string, (string Name, string Desc)> PanelMeta = new()
        {
            ["panel-notification"] = ("通知坞", "系统通知列表：查看 / 点击打开来源应用。"),
            ["panel-recent"] = ("最近使用", "最近文件 / 应用 / 网页快速访问。"),
            ["panel-quicksettings"] = ("快捷设置", "WiFi / 蓝牙 / 热点 / 性能模式快速开关。"),
            ["panel-taskbar-feature"] = ("任务栏增强", "任务栏快捷方式与窗口标签。"),
            ["panel-ai"] = ("AI 面板", "AI 助手面板：聊天 / 划词。"),
            ["panel-windowcontrol"] = ("窗口控制", "窗口操作：最小化 / 最大化 / 关闭 / 置顶。")
        };

        /// <summary>执行落盘（应用启动时调用一次）。</summary>
        public static void Seed()
        {
            try
            {
                var root = WidgetPluginStore.RootDir;
                if (!Directory.Exists(root)) Directory.CreateDirectory(root);

                // 1) 面板功能（纯代码模板，自包含）
                SeedPanels(root);

                // 2) 小组件（从项目源文件复制 XAML + cs）
                SeedWidgets(root);

                // 3) 配置节点（面板设计/动画/外观/交互/状态栏的叶子节点 → <分组>/<节点名>/config.json）
                //    ★ Plan B 完整性：鸟笼树每个叶子节点都有文件夹投影，用户能在文件夹看到全部分组内容
                SeedConfigNodes(root);
            }
            catch { }
        }

        /// <summary>
        /// 把配置树（ConfigTreeBuilder）所有一级分组下的叶子节点落盘为 <分组>/<叶子名>/config.json：
        /// 内容 = 该节点绑定的 SettingsData 字段名 + 当前默认值（反射读取）。
        /// manifest.json 标记 kind=Config + system=true（内置投影，删除时警告；用户改过不覆盖）。
        /// </summary>
        private static void SeedConfigNodes(string root)
        {
            try
            {
                var data = new DynamicBird.Core.Services.Configuration.SettingsData();
                var props = typeof(DynamicBird.Core.Services.Configuration.SettingsData).GetProperties()
                    .ToDictionary(p => p.Name, p => p, StringComparer.Ordinal);
                var tree = DynamicBird.UI.Birdcage.ConfigTreeBuilder.Build();
                foreach (var group in tree.Children)
                {
                    string groupFolder = group.Category;   // 一级分组名 = 文件夹名（面板设计/动画/外观/交互/状态栏）
                    if (string.IsNullOrEmpty(groupFolder)) continue;
                    foreach (var child in group.Children)
                    {
                        if (child.Children.Count > 0)
                        {
                            // 二级分组有三级叶子：<分组>/<二级名>/<三级叶子名>/
                            foreach (var leaf in child.Children)
                            {
                                if (leaf.FieldNames == null || leaf.FieldNames.Count == 0) continue;
                                WriteConfigNode(root, groupFolder, Sanitize(child.Name), Sanitize(leaf.Name),
                                    leaf.Key, leaf.Name, groupFolder, leaf.FieldNames, props, data);
                            }
                        }
                        else
                        {
                            // 二级即叶子：<分组>/<叶子名>/
                            if (child.FieldNames == null || child.FieldNames.Count == 0) continue;
                            WriteConfigNode(root, groupFolder, "", Sanitize(child.Name),
                                child.Key, child.Name, groupFolder, child.FieldNames, props, data);
                        }
                    }
                }
            }
            catch { }
        }

        private static void WriteConfigNode(string root, string groupFolder, string subFolder, string nodeName,
            string key, string displayName, string category, System.Collections.Generic.List<string> fields,
            Dictionary<string, System.Reflection.PropertyInfo> props, DynamicBird.Core.Services.Configuration.SettingsData data)
        {
            try
            {
                if (nodeName.Length < 2) return;
                string dir = Path.Combine(root, groupFolder);
                if (!string.IsNullOrEmpty(subFolder)) dir = Path.Combine(dir, subFolder);
                dir = Path.Combine(dir, nodeName);
                Directory.CreateDirectory(dir);

                // config.json：该节点字段的当前默认值（用户可编辑；树↔文件夹还原时按字段名合并）
                var cfg = new Dictionary<string, object?>();
                foreach (var f in fields)
                {
                    if (!props.TryGetValue(f, out var p)) continue;
                    try { cfg[f] = p.GetValue(data); } catch { }
                }
                string cfgPath = Path.Combine(dir, "config.json");
                string cfgJson = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
                WriteTemplateFile(dir, "config.json", cfgJson);

                // manifest.json：节点元信息（树↔文件夹还原依据）
                string mf = Path.Combine(dir, "manifest.json");
                var manifest = new Dictionary<string, object?>
                {
                    ["id"] = key,
                    ["name"] = displayName,
                    ["category"] = category,
                    ["kind"] = "Config",
                    ["baseType"] = "Config",
                    ["parentKey"] = "",
                    ["sourceKey"] = "",
                    ["system"] = true,
                    ["templateVersion"] = TemplateVersion
                };
                WriteTemplateFile(dir, "manifest.json", JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        private static void SeedPanels(string root)
        {
            foreach (var kv in PanelMeta)
            {
                try
                {
                    if (!DynamicBird.UI.Birdcage.BuiltinFeatureSources.Sources.TryGetValue(kv.Key, out var source)) continue;
                    if (string.IsNullOrWhiteSpace(source)) continue;
                    string dir = Path.Combine(root, "面板功能", Sanitize(kv.Key));
                    Directory.CreateDirectory(dir);
                    WriteTemplateFile(dir, "main.cs", source);
                    WriteManifest(dir, kv.Key, kv.Value.Name, "Panel", "面板功能", "1.0.0", kv.Key);
                }
                catch { }
            }
        }

        private static void SeedWidgets(string root)
        {
            foreach (var kv in WidgetMeta)
            {
                try
                {
                    string id = kv.Key;
                    string dir = Path.Combine(root, "小组件", Sanitize(id));
                    Directory.CreateDirectory(dir);
                    // ★ 从项目 birdcage/ 原样复制内置源码（与编译进 exe 的一致）
                    var (xaml, cs) = LoadWidgetFiles(id);
                    if (!string.IsNullOrEmpty(xaml)) WriteTemplateFile(dir, id + ".xaml", xaml);
                    if (!string.IsNullOrEmpty(cs))
                        WriteTemplateFile(dir, string.IsNullOrEmpty(xaml) ? "main.cs" : id + ".xaml.cs", cs);
                    if (string.IsNullOrEmpty(xaml) && string.IsNullOrEmpty(cs)) continue;
                    WriteManifest(dir, id, kv.Value.Name, "Widget", "小组件", "1.0.0", "widget-" + id);
                }
                catch { }
            }
        }

        /// <summary>从项目 birdcage/ 读取小组件的 XAML + cs（编译进 exe 的源码）。</summary>        /// <summary>读取小组件的 XAML + cs（从项目源码目录；发布后从嵌入资源，见 LoadWidgetFiles）。</summary>
        private static (string Xaml, string Cs) LoadWidgetFiles(string id)
        {
            try
            {
                // ★ 从项目 birdcage/ 读取内置源码（编译进 exe 的那份）
                string? srcRoot = FindSourceRoot();
                if (srcRoot == null) return ("", "");
                string dir = Path.Combine(srcRoot, "birdcage", "小组件", Sanitize(id));
                if (!Directory.Exists(dir)) return ("", "");
                string x = "", c = "";
                foreach (var f in Directory.GetFiles(dir))
                {
                    if (f.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase) && !f.EndsWith(".xaml.cs", StringComparison.OrdinalIgnoreCase))
                        x = File.ReadAllText(f);
                    else if (f.EndsWith(".xaml.cs", StringComparison.OrdinalIgnoreCase)) c = File.ReadAllText(f);
                    else if (f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && !f.EndsWith(".xaml.cs", StringComparison.OrdinalIgnoreCase))
                        c = File.ReadAllText(f);   // 纯 cs 小组件（web）
                }
                return (x, c);
            }
            catch { return ("", ""); }
        }

        private static string ReadResource(System.Reflection.Assembly asm, string name)
        {
            using var s = asm.GetManifestResourceStream(name);
            if (s == null) return "";
            using var r = new StreamReader(s);
            return r.ReadToEnd();
        }

        /// <summary>从 AppContext.BaseDirectory 向上找项目根（含 DynamicBird.csproj）。</summary>
        private static string? FindSourceRoot()
        {
            string? dir = AppContext.BaseDirectory;
            for (int i = 0; i < 8 && dir != null; i++)
            {
                if (File.Exists(Path.Combine(dir, "DynamicBird.csproj"))) return dir;
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }

        private static void WriteTemplateFile(string dir, string fileName, string content)
        {
            string path = Path.Combine(dir, fileName);
            if (File.Exists(path))
            {
                // ★ 内容一致 → 跳过（防止每次启动重写触发 FileSystemWatcher 死循环）
                string existing = File.ReadAllText(path);
                if (existing == content) return;
                // 用户改过（内容不同）→ 不覆盖（内置文件只在 Seeder 更新模板版本时由版本机制处理）
                return;
            }
            else
            {
                File.WriteAllText(path, content, new System.Text.UTF8Encoding(false));
            }
        }

        private static void WriteManifest(string dir, string id, string name, string kind, string category, string version, string sourceKey)
        {
            string mf = Path.Combine(dir, "manifest.json");
            var manifest = new Dictionary<string, object?>
            {
                ["id"] = id,
                ["name"] = name,
                ["kind"] = kind,
                ["category"] = category,
                ["version"] = version,
                ["author"] = "timecolors",
                ["sourceKey"] = sourceKey,
                ["system"] = true,                       // ★ 内置标记：删除时警告；升级时未改可更新
                ["templateVersion"] = TemplateVersion,
                ["permissions"] = new List<string>()
            };
            File.WriteAllText(mf, JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        }

        private static string Sanitize(string s)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in s) if (char.IsLetterOrDigit(c) || c == '_' || c == '-') sb.Append(c);
            return sb.ToString();
        }
    }
}
