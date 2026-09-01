using ShoreHue.Core.Services.Configuration;
using ShoreHue.Infrastructure.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ShoreHue.Core.Services.Configuration
{
    /// <summary>
    /// 预设管理（海床编程模式）：把配置保存为命名预设。
    /// - 整套预设：SettingsData 全量快照；
    /// - 局部预设：只保存指定字段子集（如单个面板/功能的配置）；
    /// 应用预设 = 反序列化 → 写回配置 → SettingsManager.Reload 全量生效。
    /// 预设存储于 %LOCALAPPDATA%\ShoreHue\Presets\&lt;名称&gt;.json。
    /// </summary>
    public static class PresetManager
    {
        /// <summary>测试注入的预设目录（单测用临时目录，避免污染真实用户数据）。</summary>
        internal static string? TestPresetsDir;

        public static string PresetsDir => TestPresetsDir ?? AppPaths.PresetsDir;

        private static string FileFor(string name) => Path.Combine(PresetsDir, Sanitize(name) + ".json");

        private static string Sanitize(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            string s = new string(name.Where(c => !invalid.Contains(c)).ToArray()).Trim();
            return string.IsNullOrEmpty(s) ? "未命名" : s;
        }

        /// <summary>列出所有预设名（按修改时间倒序）。</summary>
        public static List<string> ListPresets()
        {
            try
            {
                if (!Directory.Exists(PresetsDir)) return new List<string>();
                return Directory.GetFiles(PresetsDir, "*.json")
                    .Select(Path.GetFileNameWithoutExtension)
                    .Where(n => !string.IsNullOrEmpty(n))
                    .OrderByDescending(n => File.GetLastWriteTime(Path.Combine(PresetsDir, n + ".json")))
                    .ToList()!;
            }
            catch { return new List<string>(); }
        }

        /// <summary>保存整套预设（SettingsData 全量）。</summary>
        public static void SaveFull(string name, SettingsData data)
        {
            Directory.CreateDirectory(PresetsDir);
            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(FileFor(name), json);
        }

        /// <summary>保存局部预设（只含指定字段子集，可局部替换）。</summary>
        public static void SavePartial(string name, SettingsData data, IEnumerable<string> fields)
        {
            Directory.CreateDirectory(PresetsDir);
            var full = JsonNode.Parse(JsonSerializer.Serialize(data))!.AsObject();
            var subset = new JsonObject();
            foreach (var f in fields)
            {
                if (full.TryGetPropertyValue(f, out var v) && v != null)
                {
                    subset[f] = v.DeepClone();
                }
            }
            File.WriteAllText(FileFor(name),
                subset.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }

        /// <summary>读取预设（整套：完整 SettingsData；局部：仅字段子集的 SettingsData）。</summary>
        public static SettingsData? LoadPreset(string name)
        {
            try
            {
                string file = FileFor(name);
                if (!File.Exists(file)) return null;
                string json = File.ReadAllText(file);
                return JsonSerializer.Deserialize<SettingsData>(json);
            }
            catch { return null; }
        }

        /// <summary>
        /// 应用预设到当前配置（写盘 + SettingsManager.Reload 生效）。
        /// 局部预设（字段子集，小预设）合并回当前配置；整套预设（大预设）整体替换。
        /// </summary>
        public static bool ApplyPreset(string name, ISettingsService settings)
        {
            try
            {
                string file = FileFor(name);
                if (!File.Exists(file)) return false;
                string json = File.ReadAllText(file);
                var presetObj = JsonNode.Parse(json)?.AsObject();
                if (presetObj == null) return false;

                var data = JsonSerializer.Deserialize<SettingsData>(json);
                if (data == null) return false;

                // 字段少（<20）= 局部预设：合并回当前配置
                if (presetObj.Count < 20)
                {
                    var current = SettingsFileManager.Load();
                    var curObj = JsonNode.Parse(JsonSerializer.Serialize(current))!.AsObject();
                    foreach (var (k, v) in presetObj)
                    {
                        if (v != null) curObj[k] = v.DeepClone();
                    }
                    data = JsonSerializer.Deserialize<SettingsData>(curObj.ToJsonString());
                    if (data == null) return false;
                }

                SettingsFileManager.Save(data);
                settings.Reload();
                return true;
            }
            catch { return false; }
        }

        /// <summary>返回预设文件覆盖的字段名列表（整套预设返回全部 SettingsData 字段，局部返回子集字段）。</summary>
        public static System.Collections.Generic.List<string> AppliedFields(string name)
        {
            var result = new System.Collections.Generic.List<string>();
            try
            {
                string file = FileFor(name);
                if (!File.Exists(file)) return result;
                using var doc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(file));
                foreach (var p in doc.RootElement.EnumerateObject())
                {
                    result.Add(p.Name);
                }
            }
            catch { }
            return result;
        }

        public static bool DeletePreset(string name)
        {
            try
            {
                string file = FileFor(name);
                if (File.Exists(file)) { File.Delete(file); return true; }
            }
            catch { }
            return false;
        }
    }
}
