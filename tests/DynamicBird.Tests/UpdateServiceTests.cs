using System;
using DynamicBird.Infrastructure.WinApi;
using Xunit;

namespace DynamicBird.Tests;

public class UpdateServiceTests
{
    // ================= tag → Version 解析 =================

    [Theory]
    [InlineData("v1.0.1", 1, 0, 1)]
    [InlineData("v1.0.1-beta.1", 1, 0, 1)]   // 预发布后缀应被剥离
    [InlineData("V1.2.3", 1, 2, 3)]          // 大写 V 也接受
    [InlineData("1.0.0", 1, 0, 0)]           // 无 v 前缀
    public void ParseVersion_Strips_Prefix_And_Prerelease(string tag, int major, int minor, int build)
    {
        var v = UpdateService.ParseVersion(tag);
        Assert.NotNull(v);
        Assert.Equal(new Version(major, minor, build), v);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-version")]
    [InlineData("v")]
    public void ParseVersion_Invalid_Returns_Null(string tag)
    {
        Assert.Null(UpdateService.ParseVersion(tag));
    }

    // ================= Release body → SHA256 =================

    [Fact]
    public void ParseSha256_Extracts_Hex_From_Body()
    {
        string body = """
        ## 更新内容
        - 修复了若干问题
        SHA256: 0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef
        """;

        Assert.Equal(
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            UpdateService.ParseSha256(body));
    }

    [Fact]
    public void ParseSha256_No_Hash_Returns_Empty()
    {
        Assert.Equal("", UpdateService.ParseSha256("没有 SHA256 的正文"));
        Assert.Equal("", UpdateService.ParseSha256(null));
        Assert.Equal("", UpdateService.ParseSha256(""));
    }

    [Fact]
    public void ParseSha256_Ignores_Invalid_Length()
    {
        string body = "SHA256: abc123"; // 太短，不是合法哈希
        Assert.Equal("", UpdateService.ParseSha256(body));
    }

    [Fact]
    public void ParseSha256_Lowercases_Result()
    {
        string body = "SHA256=ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789";
        Assert.Equal(
            "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789",
            UpdateService.ParseSha256(body));
    }
}
