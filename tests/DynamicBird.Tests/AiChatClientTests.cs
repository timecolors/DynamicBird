using System.Collections.Generic;
using DynamicBird.Core.Services.Ai;
using Xunit;

namespace DynamicBird.Tests;

public class AiChatClientTests
{
    // ============ SSE delta 解析（OpenAI 兼容格式，覆盖多服务商变体） ============

    [Fact]
    public void Delta_With_Content_Is_Parsed()
    {
        const string payload = "{\"id\":\"x\",\"choices\":[{\"index\":0,\"delta\":{\"content\":\"你好\"}}]}";
        Assert.Equal("你好", AiChatClient.TryParseDelta(payload));
    }

    [Fact]
    public void Delta_With_Role_Only_Returns_Null()
    {
        // 首包常只含 role，无 content
        const string payload = "{\"choices\":[{\"index\":0,\"delta\":{\"role\":\"assistant\"}}]}";
        Assert.Null(AiChatClient.TryParseDelta(payload));
    }

    [Fact]
    public void Delta_With_Empty_Content_Returns_Null()
    {
        const string payload = "{\"choices\":[{\"index\":0,\"delta\":{\"content\":\"\"}}]}";
        Assert.Null(AiChatClient.TryParseDelta(payload));
    }

    [Fact]
    public void Delta_With_Reasoning_Content_Is_Skipped()
    {
        // 深度思考模型：delta 里可能只有 reasoning_content，应跳过（content 缺失）
        const string payload = "{\"choices\":[{\"index\":0,\"delta\":{\"reasoning_content\":\"思考中\"}}]}";
        Assert.Null(AiChatClient.TryParseDelta(payload));
    }

    [Fact]
    public void Delta_With_Finish_Reason_Returns_Null()
    {
        const string payload = "{\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}";
        Assert.Null(AiChatClient.TryParseDelta(payload));
    }

    [Fact]
    public void Malformed_Payload_Returns_Null_Not_Throw()
    {
        Assert.Null(AiChatClient.TryParseDelta("not json"));
        Assert.Null(AiChatClient.TryParseDelta("{broken"));
        Assert.Null(AiChatClient.TryParseDelta(""));
    }

    [Fact]
    public void Delta_Content_With_Markdown_Is_Preserved()
    {
        // 内容含代码块/换行（JSON 转义）应原样解析
        const string payload = "{\"choices\":[{\"index\":0,\"delta\":{\"content\":\"```csharp\\nvar x = 1;\\n```\"}}]}";
        Assert.Equal("```csharp\nvar x = 1;\n```", AiChatClient.TryParseDelta(payload));
    }

    // ============ token 估算 ============

    [Fact]
    public void Token_Estimate_Is_Nonzero_And_Roughly_Linear()
    {
        Assert.True(AiChatClient.EstimateTokens("") == 0);
        Assert.True(AiChatClient.EstimateTokens("hello") >= 1);
        Assert.True(AiChatClient.EstimateTokens("short") < AiChatClient.EstimateTokens("a much longer piece of text here"));
    }

    [Fact]
    public void Messages_Tokens_Includes_Image_Overhead()
    {
        var plain = new List<ChatMessage> { new() { Content = "hi" } };
        var withImage = new List<ChatMessage> { new() { Content = "hi", ImageBase64 = "AAAA", ImageMime = "image/png" } };
        Assert.True(AiChatClient.EstimateMessagesTokens(withImage) > AiChatClient.EstimateMessagesTokens(plain));
    }
}