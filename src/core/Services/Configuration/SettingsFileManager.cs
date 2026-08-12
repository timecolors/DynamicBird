using System;
using System.IO;
using System.Text.Json;
using DynamicBird.Core.Services;
using DynamicBird.Core.Services.Configuration;
namespace DynamicBird.Core.Services
{
    /// <summary>
    /// 配置文件读写专用
    /// </summary>
    public static class SettingsFileManager
    {
        private static readonly string ConfigPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

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
                return data ?? new SettingsData();
            }
            catch
            {
                return new SettingsData();
            }
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