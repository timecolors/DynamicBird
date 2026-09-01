using ShoreHue.Core.Models;
using ShoreHue.Core.Services.Configuration;
using ShoreHue.UI.Widgets.Dynamic;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace ShoreHue.UI.Seabed
{
    /// <summary>
    /// 其他海床 · 预设/功能包（线上市场托管前的本地文件共享形态）：
    /// zip 包（.dbp）= manifest.json（元信息 + 权限标注）+ main.cs（源码）+ config.json（配置片段/整套数据）。
    /// - 导出（= 上传前的打包）：用 WidgetPermissions.Detect 在导出时刻检测源码权限并写入 manifest；
    /// - 导入：重新检测权限（不信任包内声明，防篡改），有风险权限时由调用方弹窗提示用户确认后才写入。
    /// </summary>
    public static class SeabedPackage
    {
        public const string Extension = ".dbp";
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        public sealed class ImportResult
        {
            public string Name = "未命名";
            public string Kind = "Config";          // Config/Widget/Panel/Category/Full
            public string? BaseType;
            public string? ParentKey;
            public string? SourceKey;
            public string Source = "";
            public string ConfigJson = "{}";
            public List<string> Permissions = new();
            public SettingsData? FullData;          // Kind==Full：整套预设数据
        }

        /// <summary>导出单预设（树中自定义项）为 .dbp 包。成功返回 null，失败返回错误信息。</summary>
        public static string? ExportCustom(CustomPanelDefinition cp, string path)
        {
            try
            {
                using var fs = File.Create(path);
                using var zip = new ZipArchive(fs, ZipArchiveMode.Create);
                var manifest = new Dictionary<string, object?>
                {
                    ["format"] = 1,
                    ["name"] = cp.Name,
                    ["kind"] = cp.Kind,
                    ["baseType"] = cp.BaseType,
                    ["parentKey"] = cp.ParentKey,
                    ["sourceKey"] = cp.SourceKey,
                    ["createdAt"] = cp.CreatedAt,
                    // ★ 导出（上传）时刻检测权限，随包下发
                    ["permissions"] = WidgetPermissions.Detect(cp.Source ?? "")
                };
                WriteEntry(zip, "manifest.json", JsonSerializer.Serialize(manifest, JsonOptions));
                WriteEntry(zip, "main.cs", cp.Source ?? "");
                WriteEntry(zip, "config.json", cp.ConfigJson ?? "{}");
                return null;
            }
            catch (Exception ex) { return "导出失败：" + ex.Message; }
        }

        /// <summary>导出整套预设为 .dbp 包。成功返回 null，失败返回错误信息。</summary>
        public static string? ExportFullPreset(string presetName, SettingsData data, string path)
        {
            try
            {
                using var fs = File.Create(path);
                using var zip = new ZipArchive(fs, ZipArchiveMode.Create);
                var manifest = new Dictionary<string, object?>
                {
                    ["format"] = 1,
                    ["name"] = presetName,
                    ["kind"] = "Full",
                    ["permissions"] = new List<string>()
                };
                WriteEntry(zip, "manifest.json", JsonSerializer.Serialize(manifest, JsonOptions));
                WriteEntry(zip, "config.json", JsonSerializer.Serialize(data, JsonOptions));
                return null;
            }
            catch (Exception ex) { return "导出失败：" + ex.Message; }
        }

        /// <summary>解析 .dbp 包。失败返回 null 并给出 error。</summary>
        public static ImportResult? Import(string path, out string? error)
        {
            error = null;
            try
            {
                using var fs = File.OpenRead(path);
                using var zip = new ZipArchive(fs, ZipArchiveMode.Read);
                string? manifestJsonRaw = ReadEntry(zip, "manifest.json");
                string manifestJson = manifestJsonRaw ?? "";
                if (string.IsNullOrEmpty(manifestJson)) { error = "包内缺少 manifest.json"; return null; }

                using var doc = JsonDocument.Parse(manifestJson);
                var root = doc.RootElement;
                var result = new ImportResult
                {
                    Name = GetStr(root, "name") ?? "未命名",
                    Kind = GetStr(root, "kind") ?? "Config",
                    BaseType = GetStr(root, "baseType"),
                    ParentKey = GetStr(root, "parentKey"),
                    SourceKey = GetStr(root, "sourceKey"),
                    Source = ReadEntry(zip, "main.cs") ?? "",
                    ConfigJson = ReadEntry(zip, "config.json") ?? "{}"
                };
                if (root.TryGetProperty("permissions", out var perms) && perms.ValueKind == JsonValueKind.Array)
                {
                    foreach (var p in perms.EnumerateArray())
                    {
                        string? s = p.GetString();
                        if (!string.IsNullOrEmpty(s) && !result.Permissions.Contains(s)) result.Permissions.Add(s);
                    }
                }
                if (result.Kind == "Full")
                {
                    result.FullData = JsonSerializer.Deserialize<SettingsData>(result.ConfigJson);
                }
                // ★ 导入时刻重新检测权限（不信任包内声明，防篡改）；配置代码/源码才需要
                if (!string.IsNullOrEmpty(result.Source))
                {
                    result.Permissions = WidgetPermissions.Detect(result.Source);
                }
                return result;
            }
            catch (Exception ex) { error = "导入失败：" + ex.Message; return null; }
        }

        private static string? GetStr(JsonElement root, string name)
        {
            return root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        }

        private static void WriteEntry(ZipArchive zip, string name, string content)
        {
            var entry = zip.CreateEntry(name);
            using var sw = new StreamWriter(entry.Open(), new System.Text.UTF8Encoding(false));
            sw.Write(content ?? "");
        }

        /// <summary>zip 条目读取，带大小上限（防恶意包塞超大文件内存炸弹）。</summary>
        private const int MaxEntryBytes = 2 * 1024 * 1024;   // 单条目 2MB 上限（源码/配置足够）

        private static string? ReadEntry(ZipArchive zip, string name)
        {
            var entry = zip.GetEntry(name);
            if (entry == null) return null;
            if (entry.Length > MaxEntryBytes) throw new InvalidOperationException("包内条目过大: " + name);
            using var sr = new StreamReader(entry.Open(), System.Text.Encoding.UTF8);
            return sr.ReadToEnd();
        }
    }
}
