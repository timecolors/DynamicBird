using System;
using System.IO;
using System.Text.Json;
using DynamicBird.Infrastructure.Utils;
using DynamicBird.Core.Services;
using DynamicBird.Core.Services.Configuration;
namespace DynamicBird.Core.Services
{
    /// <summary>
    /// 配置文件读写专用
    /// </summary>
    public static class SettingsFileManager
    {
        private static readonly string ConfigPath = AppPaths.ConfigPath;

        /// <summary>
        /// 从文件加载配置
        /// </summary>
        public static SettingsData Load()
        {
            if (!File.Exists(ConfigPath))
                return new SettingsData();

            try
            {
                string json = File.ReadAllText(ConfigPath);
                var data = JsonSerializer.Deserialize<SettingsData>(json);
                data ??= new SettingsData();
                // ★ 旧版"呼出/隐藏共用"→ 新版"触发/隐藏分设"动画迁移（仅一次），迁移后落盘保留
                if (MigrateAnimationSettings(data))
                {
                    Save(data);
                }
                return data;
            }
            catch
            {
                return new SettingsData();
            }
        }

        /// <summary>旧 ShowHide 设置 → 新 Show/Hide 动画（保留用户已有 ElasticEase/时长）。</summary>
        private static bool MigrateAnimationSettings(SettingsData d)
        {
            bool changed = false;
            try
            {
                if (string.IsNullOrEmpty(d.ShowAnimationType) || d.ShowAnimationDurationMs <= 0)
                {
                    d.ShowAnimationType = d.ShowHideEasingType switch
                    {
                        "ElasticEase" => "Elastic",
                        "BackEase" => "Elastic",
                        _ => "Slide"
                    };
                    d.ShowAnimationDurationMs = d.ShowHideDurationMs;
                    changed = true;
                }
                if (string.IsNullOrEmpty(d.HideAnimationType) || d.HideAnimationDurationMs <= 0)
                {
                    d.HideAnimationType = d.ShowAnimationType;
                    d.HideAnimationDurationMs = d.ShowAnimationDurationMs;
                    changed = true;
                }
            }
            catch { }
            return changed;
        }

        /// <summary>
        /// 保存配置到文件
        /// </summary>
        public static void Save(SettingsData data)
        {
            try
            {
                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(ConfigPath, json);
            }
            catch { }
        }
    }
}
