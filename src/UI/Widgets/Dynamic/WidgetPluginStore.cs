using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using DynamicBird.Infrastructure.Utils;

namespace DynamicBird.UI.Widgets.Dynamic
{
    /// <summary>已安装的 C# 插件小组件（manifest + 源码）。</summary>
    public class WidgetPlugin
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Author { get; set; } = "";
        public string Description { get; set; } = "";
        public List<string> Permissions { get; set; } = new();
        [JsonIgnore]
        public string Source { get; set; } = "";
    }

    /// <summary>
    /// 本地插件仓库：每个小组件一个目录
    /// %LOCALAPPDATA%\DynamicBird\widgets\<id>\（main.cs 源码 + manifest.json 元信息）。
    /// </summary>
    public static class WidgetPluginStore
    {
        /// <summary>校验小组件 id（仅英文/数字/下划线/连字符）。</summary>
        public static bool IsValidId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length < 2 || id.Length > 32) return false;
            foreach (char c in id)
            {
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-')) return false;
            }
            return true;
        }

        /// <summary>权限 → 显示标签。</summary>
        public static string PermissionLabel(string p) => p switch
        {
            "network" => "🌐 联网",
            "clipboard" => "📋 剪贴板",
            "file" => "📁 本地文件",
            _ => "🔒 无权限"
        };

        /// <summary>列表变化（安装/删除）时触发，供 WidgetSwitcher 重建标签。</summary>
        public static event Action? Changed;

        private static string RootDir => Path.Combine(AppPaths.DataRoot, "widgets");

        private static List<WidgetPlugin>? _cache;

        public static List<WidgetPlugin> Installed
        {
            get
            {
                if (_cache == null) Reload();
                return _cache ?? new List<WidgetPlugin>();
            }
        }

        public static void Reload()
        {
            var list = new List<WidgetPlugin>();
            try
            {
                if (!Directory.Exists(RootDir)) Directory.CreateDirectory(RootDir);
                foreach (var dir in Directory.GetDirectories(RootDir))
                {
                    try
                    {
                        string main = Path.Combine(dir, "main.cs");
                        if (!File.Exists(main)) continue;
                        string id = Path.GetFileName(dir);
                        string source = File.ReadAllText(main);
                        string name = id, author = "", desc = "";
                        var perms = new List<string>();
                        string mf = Path.Combine(dir, "manifest.json");
                        if (File.Exists(mf))
                        {
                            var m = JsonSerializer.Deserialize<WidgetManifest>(File.ReadAllText(mf));
                            if (m != null)
                            {
                                if (!string.IsNullOrEmpty(m.Name)) name = m.Name;
                                author = m.Author ?? "";
                                desc = m.Description ?? "";
                                perms = m.Permissions ?? new List<string>();
                            }
                        }
                        list.Add(new WidgetPlugin
                        {
                            Id = id, Name = name, Author = author, Description = desc,
                            Permissions = perms, Source = source
                        });
                    }
                    catch { }
                }
            }
            catch { }
            _cache = list.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        public static WidgetPlugin? GetById(string id) => Installed.FirstOrDefault(p => p.Id == id);

        /// <summary>保存（新建或覆盖）。返回错误信息，成功为空串。</summary>
        public static string Save(WidgetPlugin plugin)
        {
            if (string.IsNullOrWhiteSpace(plugin.Id) || !IsValidId(plugin.Id))
                return "Id 无效：仅允许英文/数字/下划线/连字符（2-32 字符）";
            if (string.IsNullOrWhiteSpace(plugin.Source))
                return "源码为空";
            try
            {
                string dir = Path.Combine(RootDir, plugin.Id);
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "main.cs"), plugin.Source);
                var m = new WidgetManifest
                {
                    Name = plugin.Name, Author = plugin.Author,
                    Description = plugin.Description, Permissions = plugin.Permissions
                };
                File.WriteAllText(Path.Combine(dir, "manifest.json"),
                    JsonSerializer.Serialize(m, new JsonSerializerOptions { WriteIndented = true }));
                Reload();
                Changed?.Invoke();
                return "";
            }
            catch (Exception ex)
            {
                return "保存失败：" + ex.Message;
            }
        }

        public static bool Delete(string id)
        {
            try
            {
                string dir = Path.Combine(RootDir, id);
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, true);
                    Reload();
                    Changed?.Invoke();
                    return true;
                }
            }
            catch { }
            return false;
        }

        private class WidgetManifest
        {
            public string? Name { get; set; }
            public string? Author { get; set; }
            public string? Description { get; set; }
            public List<string>? Permissions { get; set; }
        }
    }
}
