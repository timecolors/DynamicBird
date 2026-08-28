using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DynamicBird.Infrastructure.Utils;

namespace DynamicBird.Core.Services.Ai
{
    public sealed class AiSession
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = "新对话";
        public DateTime Created { get; set; } = DateTime.Now;
        public List<ChatMessage> Messages { get; set; } = new();
    }

    public sealed class AiSessionData
    {
        public string CurrentId { get; set; } = "";
        public List<AiSession> Sessions { get; set; } = new();
    }

    /// <summary>
    /// AI 多会话存储（ai_sessions.json）与旧版单会话历史（ai_history.json）的迁移。
    /// </summary>
    public static class AiSessionStore
    {
        private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

        public static AiSessionData Load()
        {
            try
            {
                if (File.Exists(AppPaths.AiSessionsPath))
                {
                    var data = JsonSerializer.Deserialize<AiSessionData>(File.ReadAllText(AppPaths.AiSessionsPath));
                    if (data != null && data.Sessions.Count > 0) return data;
                }
            }
            catch { }

            // 迁移旧版单会话历史
            try
            {
                if (File.Exists(AppPaths.AiHistoryPath))
                {
                    var old = JsonSerializer.Deserialize<List<ChatMessage>>(File.ReadAllText(AppPaths.AiHistoryPath));
                    if (old != null && old.Count > 0)
                    {
                        var session = new AiSession
                        {
                            Title = BuildTitle(old),
                            Messages = old
                        };
                        var data = new AiSessionData { CurrentId = session.Id, Sessions = { session } };
                        Save(data);
                        return data;
                    }
                }
            }
            catch { }

            var fresh = new AiSessionData();
            var first = new AiSession();
            fresh.Sessions.Add(first);
            fresh.CurrentId = first.Id;
            return fresh;
        }

        public static void Save(AiSessionData data)
        {
            try
            {
                // 对话历史完整保留：每会话仅设极端兜底（5 万条 ≈ 数十 MB），正常使用永不触发。
                // 发送给模型时的上下文裁剪在 AiChatView 发送阶段进行，与存储无关。
                foreach (var s in data.Sessions)
                {
                    if (s.Messages.Count > 50000)
                        s.Messages.RemoveRange(0, s.Messages.Count - 50000);
                }
                // 会话数量上限放宽到 100（每个会话容量小，可自行删除）
                if (data.Sessions.Count > 100)
                    data.Sessions.RemoveRange(0, data.Sessions.Count - 100);

                string? dir = Path.GetDirectoryName(AppPaths.AiSessionsPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(AppPaths.AiSessionsPath, JsonSerializer.Serialize(data, Options));
            }
            catch { }
        }

        private static string BuildTitle(List<ChatMessage> messages)
        {
            var firstUser = messages.FirstOrDefault(m => m.Role == ChatRole.User);
            if (firstUser == null) return DynamicBird.UI.Localization.LocalizationManager.Instance["Session_Old"];
            string t = firstUser.Content.Trim();
            return t.Length > 20 ? t[..20] + "…" : t;
        }
    }
}