using ShoreHue.Infrastructure.WinApi;
using System.Collections.Generic;
using Xunit;

namespace ShoreHue.Tests;

/// <summary>验证 Jump List 参数解析（命令转发/启动动作提取）。</summary>
public class JumpListManagerTests
{
    [Fact]
    public void ParseActions_EmptyArgs_ReturnsEmpty()
    {
        var actions = JumpListManager.ParseActions(System.Array.Empty<string>());
        Assert.Empty(actions);
    }

    [Fact]
    public void ParseActions_NullArgs_ReturnsEmpty()
    {
        var actions = JumpListManager.ParseActions(null!);
        Assert.Empty(actions);
    }

    [Fact]
    public void ParseActions_RecognizesAllActions()
    {
        var actions = JumpListManager.ParseActions(new[]
        {
            JumpListManager.ArgOpenSettings,
            JumpListManager.ArgToggleDnd,
            JumpListManager.ArgTogglePanel
        });
        Assert.Equal(3, actions.Count);
        Assert.Contains(JumpListManager.ArgOpenSettings, actions);
        Assert.Contains(JumpListManager.ArgToggleDnd, actions);
        Assert.Contains(JumpListManager.ArgTogglePanel, actions);
    }

    [Fact]
    public void ParseActions_IgnoresUnknownArgs()
    {
        var actions = JumpListManager.ParseActions(new[] { "--unknown", "foo", JumpListManager.ArgOpenSettings });
        Assert.Single(actions);
        Assert.Equal(JumpListManager.ArgOpenSettings, actions[0]);
    }

    [Fact]
    public void ParseActions_NoDuplicates()
    {
        var actions = JumpListManager.ParseActions(new[] { JumpListManager.ArgOpenSettings, JumpListManager.ArgOpenSettings });
        Assert.Single(actions);
    }

    [Fact]
    public void ParseActions_MixedWithAppArgs_FindsActions()
    {
        // 正常启动参数里夹带动作（如进程重启时残留参数）
        var actions = JumpListManager.ParseActions(new[] { "-single-instance", JumpListManager.ArgToggleDnd, "extra" });
        Assert.Single(actions);
        Assert.Equal(JumpListManager.ArgToggleDnd, actions[0]);
    }
}
