using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DynamicBird.Core.Services.Ai
{
    public enum ChatRole
    {
        System,
        User,
        Assistant
    }

    public sealed class ChatMessage
    {
        public ChatRole Role { get; set; } = ChatRole.User;
        public string Content { get; set; } = "";

        /// <summary>可选：图片内容（Base64，无则纯文本）。</summary>
        public string? ImageBase64 { get; set; }

        /// <summary>图片 MIME 类型（如 image/png）。</summary>
        public string? ImageMime { get; set; }

        [JsonIgnore]
        public bool HasImage => !string.IsNullOrEmpty(ImageBase64);

        [JsonIgnore]
        public string RoleName => Role switch
        {
            ChatRole.System => "system",
            ChatRole.User => "user",
            _ => "assistant"
        };
    }

    /// <summary>
    /// AI 助手配置（独立存储于 ai.json，Key 仅保存在本机）。
    /// </summary>
    public sealed class AiSettings
    {
        public bool Enabled { get; set; } = false;
        public string BaseUrl { get; set; } = "https://api.deepseek.com/v1";
        /// <summary>内存中的明文 API Key（不直接序列化到 ai.json，见 AiSettingsStore 的 DPAPI 加密存储）。</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public string ApiKey { get; set; } = "";
        /// <summary>DPAPI 加密后的 API Key（持久化字段）。</summary>
        public string ApiKeyEncrypted { get; set; } = "";
        /// <summary>旧版明文 API Key（仅迁移用，读取后转加密并清空）。</summary>
        public string ApiKeyLegacy { get; set; } = "";
        public string Model { get; set; } = "deepseek-chat";
        public double Temperature { get; set; } = 0.7;

        /// <summary>模型上下文窗口（token），发送前按此裁剪历史；0 = 不裁剪。</summary>
        public int ContextWindowTokens { get; set; } = 32768;

        /// <summary>联网搜索（需服务商支持，如 DeepSeek 的 web_search 工具）。</summary>
        public bool EnableWebSearch { get; set; } = false;

        /// <summary>深度思考（需模型/服务商支持，如 reasoning_effort 或 reasoner 模型）。</summary>
        public bool EnableReasoning { get; set; } = false;

        public string SystemPrompt { get; set; } = "你是灵动鸟 AI 助手，一个运行在 Windows 桌面的智能助手。回答简洁、准确、友好。";

        /// <summary>服务商预设（name → 显示名）。</summary>
        public static readonly (string Name, string Display, string Url, string Model)[] Presets =
        {
            ("deepseek", "DeepSeek", "https://api.deepseek.com/v1", "deepseek-chat"),
            ("openai", "OpenAI", "https://api.openai.com/v1", "gpt-4o-mini"),
            ("siliconflow", "SiliconFlow（硅基流动）", "https://api.siliconflow.cn/v1", "Qwen/Qwen2.5-7B-Instruct"),
            ("ollama", "Ollama（本地）", "http://localhost:11434/v1", "llama3.2"),
            ("openrouter", "OpenRouter", "https://openrouter.ai/api/v1", "deepseek/deepseek-chat"),
            ("moonshot", "Moonshot（月之暗面）", "https://api.moonshot.cn/v1", "moonshot-v1-8k"),
            ("zhipu", "智谱 GLM", "https://open.bigmodel.cn/api/paas/v4", "glm-4-flash"),
            ("groq", "Groq", "https://api.groq.com/openai/v1", "llama-3.3-70b-versatile"),
        };
    }
}
