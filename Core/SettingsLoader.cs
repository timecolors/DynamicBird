using System;
using System.IO;
using System.Text.Json;

namespace LingDongBird.Core
{
    public static class SettingsLoader
    {
        private static readonly string ConfigPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "config.json"
        );

        public static SettingsData Load()
        {
            if (!File.Exists(ConfigPath))
            {
                return new SettingsData();
            }

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

        public static void Save(SettingsData data)
        {
            try
            {
                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);
            }
            catch { }
        }
    }
}