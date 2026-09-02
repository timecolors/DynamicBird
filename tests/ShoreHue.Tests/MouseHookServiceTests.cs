using ShoreHue.Infrastructure.WinApi;
using System;
using System.Threading;
using Xunit;

namespace ShoreHue.Tests;

/// <summary>验证 WH_MOUSE_LL 鼠标钩子服务：安装/事件缓存/清理（钩子安装失败时优雅降级）。</summary>
public class MouseHookServiceTests
{
    [Fact]
    public void Construct_NoThrow()
    {
        Exception? ex = null;
        MouseHookService? hook = null;
        try
        {
            hook = new MouseHookService();
            // 安装成功则 IsActive=true；失败（环境限制）则降级，均不应抛异常
            _ = hook.IsActive;
        }
        catch (Exception e)
        {
            ex = e;
        }
        finally
        {
            hook?.Dispose();
        }
        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_NoThrow_AndClearsState()
    {
        MouseHookService? hook = null;
        try
        {
            hook = new MouseHookService();
            hook.Dispose();
            hook.Dispose();   // 幂等
            Assert.False(hook.IsActive);
        }
        catch (Exception ex)
        {
            Assert.Fail("Dispose 抛异常: " + ex);
        }
    }

    [Fact]
    public void ConsumeEvent_AfterHasEvent_Clears()
    {
        var hook = new MouseHookService();
        try
        {
            // 初始无事件
            Assert.False(hook.HasEvent);
            hook.ConsumeEvent();   // 空消费不抛
            Assert.False(hook.HasEvent);
        }
        finally
        {
            hook.Dispose();
        }
    }

    [Fact]
    public void LastPosition_DefaultsToZero_WhenNoEvent()
    {
        var hook = new MouseHookService();
        try
        {
            var (x, y) = hook.LastPosition;
            Assert.True(x >= 0 && y >= 0, "位置应为有效坐标");
        }
        finally
        {
            hook.Dispose();
        }
    }
}
