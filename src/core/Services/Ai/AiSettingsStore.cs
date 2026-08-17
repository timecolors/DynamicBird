using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using DynamicBird.Infrastructure.Utils;

namespace DynamicBird.Core.Services.Ai
{
    /// <summary>
    /// AI 配置与对话历史存储（本地 JSON，无任何上传）。
    /// </summary>
    public static class AiSettingsStore
    {
        private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

        public static AiSettings Load()
        {
            try
            {
                if (File.Exists(AppPaths.AiSettingsPath))
                {
                    string json = File.ReadAllText(AppPaths.AiSettingsPath);
                    var data = JsonSerializer.Deserialize<AiSettings>(json);
                    if (data != null) return data;
                }
            }
            catch { }
            return new AiSettings();
        }

        public static void Save(AiSettings settings)
        {
            try
            {
                string? dir = Path.GetDirectoryName(AppPaths.AiSettingsPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(AppPaths.AiSettingsPath, JsonSerializer.Serialize(settings, Options));
            }
            catch { }
        }

        // ========== 对话历史（最近一轮） ==========

        public static List<ChatMessage> LoadHistory()
        {
            try
            {
                if (File.Exists(AppPaths.AiHistoryPath))
                {
                    string json = File.ReadAllText(AppPaths.AiHistoryPath);
                    var list = JsonSerializer.Deserialize<List<ChatMessage>>(json);
                    if (list != null) return list;
                }
            }
            catch { }
            return new List<ChatMessage>();
        }

        public static void SaveHistory(List<ChatMessage> messages)
        {
            try
            {
                // 只保留最近 40 条，防止文件无限增长
                if (messages.Count > 40)
                    messages.RemoveRange(0, messages.Count - 40);
                string? dir = Path.GetDirectoryName(AppPaths.AiHistoryPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(AppPaths.AiHistoryPath, JsonSerializer.Serialize(messages, Options));
            }
            catch { }
        }
    }
}
