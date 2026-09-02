using ShoreHue.Core;
using ShoreHue.Core.Controllers;
using System;
using Xunit;

namespace ShoreHue.Tests;

/// <summary>验证 EdgeRegionMapping 纯函数：区域映射/面板类型/位置计算。</summary>
public class EdgeRegionMappingTests
{
    [Fact]
    public void GetEdgeName_MapsAllEdges()
    {
        Assert.Equal("Top", EdgeRegionMapping.GetEdgeName(EdgeRegion.Top_Left));
        Assert.Equal("Top", EdgeRegionMapping.GetEdgeName(EdgeRegion.Top_Center));
        Assert.Equal("Bottom", EdgeRegionMapping.GetEdgeName(EdgeRegion.Bottom_Right));
        Assert.Equal("Left", EdgeRegionMapping.GetEdgeName(EdgeRegion.Left_Top));
        Assert.Equal("Right", EdgeRegionMapping.GetEdgeName(EdgeRegion.Right_Center));
        Assert.Equal("", EdgeRegionMapping.GetEdgeName(EdgeRegion.TopLeft));
        Assert.Equal("", EdgeRegionMapping.GetEdgeName(EdgeRegion.Unknown));
    }

    [Fact]
    public void GetRegionKey_MapsSubRegions()
    {
        Assert.Equal("Top_Left", EdgeRegionMapping.GetRegionKey(EdgeRegion.Top_Left));
        Assert.Equal("Bottom_Center", EdgeRegionMapping.GetRegionKey(EdgeRegion.Bottom_Center));
        Assert.Equal("Left_Top", EdgeRegionMapping.GetRegionKey(EdgeRegion.Left_Top));
        Assert.Equal("Right_Bottom", EdgeRegionMapping.GetRegionKey(EdgeRegion.Right_Bottom));
        Assert.Equal("TopLeft", EdgeRegionMapping.GetRegionKey(EdgeRegion.TopLeft));   // 角落返回枚举名
        Assert.Equal("Unknown", EdgeRegionMapping.GetRegionKey(EdgeRegion.Unknown));
    }

    [Fact]
    public void GetEdgeFromKey_SplitsPrefix()
    {
        Assert.Equal("Top", EdgeRegionMapping.GetEdgeFromKey("Top_Left"));
        Assert.Equal("", EdgeRegionMapping.GetEdgeFromKey("TopLeft"));
        Assert.Equal("", EdgeRegionMapping.GetEdgeFromKey(""));
    }

    [Fact]
    public void GetRegionTypeFromEnum_DefaultLayout()
    {
        // 左边缘中心 → AI；其他中心 → AppHelper
        Assert.Equal("AI", EdgeRegionMapping.GetRegionTypeFromEnum(EdgeRegion.Left_Center, _ => "Default", EdgeRegionMapping.IsValidPanelType));
        Assert.Equal("AppHelper", EdgeRegionMapping.GetRegionTypeFromEnum(EdgeRegion.Top_Center, _ => "Default", EdgeRegionMapping.IsValidPanelType));
        // 竖边缘 → Widget；横边缘 → Taskbar
        Assert.Equal("Widget", EdgeRegionMapping.GetRegionTypeFromEnum(EdgeRegion.Left_Top, _ => "Default", EdgeRegionMapping.IsValidPanelType));
        Assert.Equal("Taskbar", EdgeRegionMapping.GetRegionTypeFromEnum(EdgeRegion.Top_Left, _ => "Default", EdgeRegionMapping.IsValidPanelType));
    }

    [Fact]
    public void GetRegionTypeFromEnum_CustomOverride()
    {
        // 自定义面板覆盖默认布局
        Assert.Equal("Notification",
            EdgeRegionMapping.GetRegionTypeFromEnum(EdgeRegion.Top_Left,
                key => key == "Top_Left" ? "Notification" : "Default",
                EdgeRegionMapping.IsValidPanelType));
        // 非法类型忽略（回退默认）
        Assert.Equal("Taskbar",
            EdgeRegionMapping.GetRegionTypeFromEnum(EdgeRegion.Top_Left,
                key => key == "Top_Left" ? "Bogus" : "Default",
                EdgeRegionMapping.IsValidPanelType));
    }

    [Fact]
    public void IsValidPanelType_AcceptsBuiltinsAndCustom()
    {
        Assert.True(EdgeRegionMapping.IsValidPanelType("Taskbar"));
        Assert.True(EdgeRegionMapping.IsValidPanelType("Widget"));
        Assert.True(EdgeRegionMapping.IsValidPanelType("Custom:abc123"));
        Assert.False(EdgeRegionMapping.IsValidPanelType("Bogus"));
        Assert.False(EdgeRegionMapping.IsValidPanelType(""));
    }

    [Fact]
    public void CalculatePosition_FollowMode_CentersOnMouse()
    {
        // 上边缘跟随：left 居中鼠标，top=0
        var (l, t) = EdgeRegionMapping.CalculatePosition(EdgeRegion.Top_Center,
            500, 100, 1920, 1080, 300, 50, 1040,
            _ => "Follow", _ => 0);
        Assert.Equal(350, l);   // 500 - 300/2
        Assert.Equal(0, t);
    }

    [Fact]
    public void CalculatePosition_ClampsToScreen()
    {
        // 鼠标靠近右缘 → left 钳制到 sw-w
        var (l, _) = EdgeRegionMapping.CalculatePosition(EdgeRegion.Top_Right,
            1900, 100, 1920, 1080, 300, 50, 1040,
            _ => "Follow", _ => 0);
        Assert.Equal(1620, l);   // 1920 - 300

        // 鼠标靠近左缘 → left 钳制到 0
        var (l2, _) = EdgeRegionMapping.CalculatePosition(EdgeRegion.Top_Left,
            10, 100, 1920, 1080, 300, 50, 1040,
            _ => "Follow", _ => 0);
        Assert.Equal(0, l2);
    }

    [Fact]
    public void CalculatePosition_FixedMode_UsesOffset()
    {
        var (l, t) = EdgeRegionMapping.CalculatePosition(EdgeRegion.Bottom_Center,
            500, 100, 1920, 1080, 300, 50, 1040,
            _ => "Fixed", _ => 80);
        // 固定：水平居中 + 偏移 80
        Assert.Equal(960 - 150 + 80, l);
        Assert.Equal(1040 - 50, t);   // bottomBoundary - h
    }

    [Fact]
    public void CalculatePosition_Unknown_ReturnsZero()
    {
        var (l, t) = EdgeRegionMapping.CalculatePosition(EdgeRegion.Unknown,
            500, 100, 1920, 1080, 300, 50, 1040,
            _ => "Follow", _ => 0);
        Assert.Equal(0, l);
        Assert.Equal(0, t);
    }
}
