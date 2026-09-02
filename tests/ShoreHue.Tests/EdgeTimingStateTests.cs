using ShoreHue.Core;
using ShoreHue.Core.Controllers;
using System;
using Xunit;

namespace ShoreHue.Tests;

/// <summary>
/// 验证 EdgeTimingState 时序状态机：防抖 / 触发延时 / 快速切换计数。
/// 注入假时钟精确控制时间流逝，验证边界行为。
/// </summary>
public class EdgeTimingStateTests
{
    /// <summary>假时钟：测试代码手动推进时间。</summary>
    private sealed class FakeClock : EdgeTimingState.IClock
    {
        public DateTime Now { get; private set; } = new DateTime(2026, 1, 1, 0, 0, 0);
        public void Advance(TimeSpan span) => Now += span;
        public void AdvanceMs(double ms) => Now += TimeSpan.FromMilliseconds(ms);
    }

    private static EdgeTimingState Create(FakeClock clock, Func<string, int>? delay = null)
        => new(clock, delay ?? (_ => 0));

    // ========== 防抖 ==========

    [Fact]
    public void Debounce_FirstVisit_Allowed()
    {
        var clock = new FakeClock();
        var timing = Create(clock);

        Assert.False(timing.ShouldDebounce(EdgeRegion.Top_Left, 80));   // 首次：放行
    }

    [Fact]
    public void Debounce_SameRegionWithinWindow_Dropped()
    {
        var clock = new FakeClock();
        var timing = Create(clock);

        timing.ShouldDebounce(EdgeRegion.Top_Left, 80);
        clock.AdvanceMs(30);
        Assert.True(timing.ShouldDebounce(EdgeRegion.Top_Left, 80));    // 30ms < 80ms：丢弃
    }

    [Fact]
    public void Debounce_SameRegionAfterWindow_Allowed()
    {
        var clock = new FakeClock();
        var timing = Create(clock);

        timing.ShouldDebounce(EdgeRegion.Top_Left, 80);
        clock.AdvanceMs(100);
        Assert.False(timing.ShouldDebounce(EdgeRegion.Top_Left, 80));   // 100ms ≥ 80ms：放行
    }

    [Fact]
    public void Debounce_DifferentRegion_ResetsTimer()
    {
        var clock = new FakeClock();
        var timing = Create(clock);

        timing.ShouldDebounce(EdgeRegion.Top_Left, 80);
        clock.AdvanceMs(30);
        Assert.False(timing.ShouldDebounce(EdgeRegion.Top_Right, 80));  // 不同区域：重置计时并放行
        clock.AdvanceMs(30);
        Assert.True(timing.ShouldDebounce(EdgeRegion.Top_Right, 80));   // 新区域 30ms 后：丢弃
    }

    [Fact]
    public void Debounce_ProcessRegionSemantics_NoRefreshOnPass()
    {
        var clock = new FakeClock();
        var timing = Create(clock);

        // ProcessRegion 语义：同区域超时放行但不刷新时间戳
        timing.ShouldDebounce(EdgeRegion.Top_Left, 80);
        clock.AdvanceMs(100);                                  // 超过防抖窗口
        Assert.False(timing.ShouldDebounce(EdgeRegion.Top_Left, 80));   // 放行
        clock.AdvanceMs(30);
        // 时间戳未刷新 → 距首次进入 130ms，仍 ≥ 80ms → 仍放行
        Assert.False(timing.ShouldDebounce(EdgeRegion.Top_Left, 80));
    }

    [Fact]
    public void Debounce_FollowMouseSemantics_RefreshesOnPass()
    {
        var clock = new FakeClock();
        var timing = Create(clock);

        // FollowMouseInPanel 语义：同区域超时放行且刷新时间戳
        timing.ShouldDebounceAndRefresh(EdgeRegion.Top_Left, 80);
        clock.AdvanceMs(100);                                  // 超过防抖窗口
        Assert.False(timing.ShouldDebounceAndRefresh(EdgeRegion.Top_Left, 80));   // 放行（时间戳已刷新）
        clock.AdvanceMs(30);
        // 时间戳已刷新到 100ms 时刻 → 距上次 30ms < 80ms → 丢弃
        Assert.True(timing.ShouldDebounceAndRefresh(EdgeRegion.Top_Left, 80));
    }

    // ========== 触发延时 ==========

    [Fact]
    public void TriggerDelay_Zero_ImmediatePass()
    {
        var clock = new FakeClock();
        var timing = Create(clock, delay: _ => 0);

        Assert.True(timing.TriggerDelayPassed(EdgeRegion.Top_Left));
        Assert.False(timing.IsTriggerDelaying);
    }

    [Fact]
    public void TriggerDelay_FirstEntry_StartsTimer()
    {
        var clock = new FakeClock();
        var timing = Create(clock, delay: key => key == "Top_Left" ? 150 : 0);

        Assert.False(timing.TriggerDelayPassed(EdgeRegion.Top_Left));   // 首次进入：计时开始
        Assert.True(timing.IsTriggerDelaying);
    }

    [Fact]
    public void TriggerDelay_AfterDelay_Passes()
    {
        var clock = new FakeClock();
        var timing = Create(clock, delay: _ => 150);

        timing.TriggerDelayPassed(EdgeRegion.Top_Left);
        clock.AdvanceMs(149);
        Assert.False(timing.TriggerDelayPassed(EdgeRegion.Top_Left));   // 未到
        clock.AdvanceMs(2);
        Assert.True(timing.TriggerDelayPassed(EdgeRegion.Top_Left));    // ≥150ms：放行
        Assert.False(timing.IsTriggerDelaying);
    }

    [Fact]
    public void TriggerDelay_RegionChange_Restarts()
    {
        var clock = new FakeClock();
        var timing = Create(clock, delay: _ => 150);

        timing.TriggerDelayPassed(EdgeRegion.Top_Left);
        clock.AdvanceMs(100);
        Assert.False(timing.TriggerDelayPassed(EdgeRegion.Top_Right));  // 换区域：重新计时
        clock.AdvanceMs(100);
        Assert.False(timing.TriggerDelayPassed(EdgeRegion.Top_Right));  // 新区域 100ms：仍未到 150
        clock.AdvanceMs(60);
        Assert.True(timing.TriggerDelayPassed(EdgeRegion.Top_Right));   // 新区域 ≥150ms：放行
    }

    [Fact]
    public void ResetTriggerDelay_ClearsState()
    {
        var clock = new FakeClock();
        var timing = Create(clock, delay: _ => 150);

        timing.TriggerDelayPassed(EdgeRegion.Top_Left);
        Assert.True(timing.IsTriggerDelaying);
        timing.ResetTriggerDelay();
        Assert.False(timing.IsTriggerDelaying);
        // 重置后重新进入需要重新停留
        clock.AdvanceMs(200);
        Assert.False(timing.TriggerDelayPassed(EdgeRegion.Top_Left));
    }

    // ========== 快速切换计数 ==========

    [Fact]
    public void RapidSwitch_UnderThree_NotRapid()
    {
        var clock = new FakeClock();
        var timing = Create(clock);

        Assert.False(timing.IsRapidSwitching());   // 1
        clock.AdvanceMs(200);
        Assert.False(timing.IsRapidSwitching());   // 2
    }

    [Fact]
    public void RapidSwitch_ThreeWithinWindow_Rapid()
    {
        var clock = new FakeClock();
        var timing = Create(clock);

        timing.IsRapidSwitching();                 // 1
        clock.AdvanceMs(200);
        timing.IsRapidSwitching();                 // 2
        clock.AdvanceMs(200);
        Assert.True(timing.IsRapidSwitching());    // 3 → 图标模态
    }

    [Fact]
    public void RapidSwitch_AfterWindow_Restarts()
    {
        var clock = new FakeClock();
        var timing = Create(clock);

        timing.IsRapidSwitching();                 // 1
        clock.AdvanceMs(200);
        timing.IsRapidSwitching();                 // 2
        clock.AdvanceMs(1100);                     // 超过 1s 窗口
        Assert.False(timing.IsRapidSwitching());   // 重新起算：1
    }

    [Fact]
    public void ResetSwitchCount_Clears()
    {
        var clock = new FakeClock();
        var timing = Create(clock);

        timing.IsRapidSwitching();
        clock.AdvanceMs(100);
        timing.IsRapidSwitching();
        clock.AdvanceMs(100);
        Assert.True(timing.IsRapidSwitching());
        timing.ResetSwitchCount();
        clock.AdvanceMs(100);
        Assert.False(timing.IsRapidSwitching());   // 重置后从 1 开始
    }

    // ========== 综合重置 ==========

    [Fact]
    public void ResetAll_ClearsEverything()
    {
        var clock = new FakeClock();
        var timing = Create(clock, delay: _ => 150);

        timing.ShouldDebounce(EdgeRegion.Top_Left, 80);
        timing.TriggerDelayPassed(EdgeRegion.Top_Left);
        timing.IsRapidSwitching();
        timing.IsRapidSwitching();
        timing.IsRapidSwitching();
        Assert.True(timing.IsTriggerDelaying);

        timing.ResetAll();
        Assert.False(timing.IsTriggerDelaying);
        Assert.False(timing.ShouldDebounce(EdgeRegion.Top_Left, 80));    // 防抖已清
        Assert.False(timing.IsRapidSwitching());                         // 计数已清
    }
}