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
                    if (data != null)
                    {
                        // ★ 解密 ApiKey：优先用 DPAPI 加密字段；兼容旧版明文 ApiKey（迁移后下次保存会加密）
                        data.ApiKey = DecryptKey(data.ApiKeyEncrypted);
                        if (string.IsNullOrEmpty(data.ApiKey) && !string.IsNullOrEmpty(data.ApiKeyLegacy))
                        {
                            data.ApiKey = data.ApiKeyLegacy;
                            data.ApiKeyEncrypted = "";
                        }
                        return data;
                    }
                }
            }
            catch { }
            return new AiSettings();
        }

        public static void Save(AiSettings settings)
        {
            try
            {
                // ★ 安全：ApiKey 用 DPAPI（当前用户）加密后落盘，防止同机其他进程/用户读取明文
                settings.ApiKeyEncrypted = EncryptKey(settings.ApiKey);
                settings.ApiKeyLegacy = "";   // 迁移后清掉旧明文
                string? dir = Path.GetDirectoryName(AppPaths.AiSettingsPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(AppPaths.AiSettingsPath, JsonSerializer.Serialize(settings, Options));
            }
            catch { }
        }

        private static string EncryptKey(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return "";
            try
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(plain);
                return Convert.ToBase64String(System.Security.Cryptography.ProtectedData.Protect(
                    bytes, null, System.Security.Cryptography.DataProtectionScope.CurrentUser));
            }
            catch { return ""; }
        }

        private static string DecryptKey(string enc)
        {
            if (string.IsNullOrEmpty(enc)) return "";
            try
            {
                var bytes = Convert.FromBase64String(enc);
                return System.Text.Encoding.UTF8.GetString(System.Security.Cryptography.ProtectedData.Unprotect(
                    bytes, null, System.Security.Cryptography.DataProtectionScope.CurrentUser));
            }
            catch { return ""; }
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
