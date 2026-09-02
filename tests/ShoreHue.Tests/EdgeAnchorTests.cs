using ShoreHue.Core;
using ShoreHue.Core.Controllers;
using System;
using Xunit;

namespace ShoreHue.Tests;

/// <summary>
/// 验证区域切换时的贴边锚定：尺寸变化后位置目标应保持"贴边边缘不动"。
/// （修复：任务栏→小组件切换原为"左上角固定缩放→完成才贴边"，现位置随尺寸同步锚定）
/// </summary>
public class EdgeAnchorTests
{
    // 模拟 settings 委托
    private static readonly Func<string, string> FollowMode = _ => "Follow";
    private static readonly Func<string, double> ZeroOffset = _ => 0;

    [Fact]
    public void TopEdge_Anchor_KeepsTopPinned_WhenSizeChanges()
    {
        // 上边缘任务栏（宽 1280 高 60）→ 小组件（方 400x400），鼠标在 x=500
        var (taskLeft, taskTop) = EdgeRegionMapping.CalculatePosition(
            EdgeRegion.Top_Left, 500, 50, 1920, 1080, 1280, 60, 1040, FollowMode, ZeroOffset);
        var (widgetLeft, widgetTop) = EdgeRegionMapping.CalculatePosition(
            EdgeRegion.Top_Left, 500, 50, 1920, 1080, 400, 400, 1040, FollowMode, ZeroOffset);

        // ★ 贴边核心：top 始终 0（贴顶），宽度变化只影响 left
        Assert.Equal(0, taskTop);
        Assert.Equal(0, widgetTop);
        // 任务栏宽 → left 更靠左；小组件窄 → left 居中（向鼠标靠拢）
        Assert.True(widgetLeft > taskLeft, $"小组件 left 应更靠右: widget={widgetLeft} task={taskLeft}");
        // 新尺寸锚点仍在屏幕内
        Assert.True(widgetLeft >= 0 && widgetLeft + 400 <= 1920);
    }

    [Fact]
    public void LeftEdge_Anchor_KeepsLeftPinned_WhenSizeChanges()
    {
        var (taskLeft, taskTop) = EdgeRegionMapping.CalculatePosition(
            EdgeRegion.Left_Center, 50, 500, 1920, 1080, 400, 300, 1040, FollowMode, ZeroOffset);
        var (squareLeft, squareTop) = EdgeRegionMapping.CalculatePosition(
            EdgeRegion.Left_Center, 50, 500, 1920, 1080, 400, 400, 1040, FollowMode, ZeroOffset);

        // ★ 贴边核心：left 始终 0（贴左），高度变化只影响 top
        Assert.Equal(0, taskLeft);
        Assert.Equal(0, squareLeft);
    }

    [Fact]
    public void Corner_Anchor_KeepsCornerFixed()
    {
        var (tl1, tt1) = EdgeRegionMapping.CalculatePosition(
            EdgeRegion.TopLeft, 3, 3, 1920, 1080, 300, 200, 1040, FollowMode, ZeroOffset);
        var (tl2, tt2) = EdgeRegionMapping.CalculatePosition(
            EdgeRegion.TopLeft, 3, 3, 1920, 1080, 400, 400, 1040, FollowMode, ZeroOffset);

        // 左上角：角点固定 (0,0)，尺寸变化不移动角点
        Assert.Equal(0, tl1);
        Assert.Equal(0, tt1);
        Assert.Equal(0, tl2);
        Assert.Equal(0, tt2);
    }

    [Fact]
    public void BottomEdge_Anchor_KeepsBottomPinned()
    {
        var (w1, h1) = EdgeRegionMapping.CalculatePosition(
            EdgeRegion.Bottom_Center, 960, 1000, 1920, 1080, 1280, 60, 1040, FollowMode, ZeroOffset);
        var (w2, h2) = EdgeRegionMapping.CalculatePosition(
            EdgeRegion.Bottom_Center, 960, 1000, 1920, 1080, 400, 400, 1040, FollowMode, ZeroOffset);

        // 底边贴边：top = bottomBoundary - height（始终贴任务栏顶部）
        Assert.Equal(1040 - 60, h1);
        Assert.Equal(1040 - 400, h2);
    }
}
