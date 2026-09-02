using System;
using Xunit;

namespace ShoreHue.Tests;

/// <summary>
/// 验证小鸟依人目标钳制数学（与 UpdateClinging 相同逻辑）：
/// 鼠标在屏幕任意位置（含四边/四角）→ 面板目标左上角始终在屏幕内。
/// </summary>
public class ClingClampTests
{
    /// <summary>与 UpdateClinging 相同的钳制公式。</summary>
    private static (double left, double top) ClampTarget(double mouseX, double mouseY, double halfW, double halfH, double screenW, double screenH, double panelW, double panelH)
    {
        double clingLeft = mouseX - halfW;
        double clingTop = mouseY - halfH;
        clingLeft = Math.Max(0, Math.Min(screenW - panelW, clingLeft));
        clingTop = Math.Max(0, Math.Min(screenH - panelH, clingTop));
        return (clingLeft, clingTop);
    }

    [Theory]
    [InlineData(3, 500, 1920, 1080)]        // 左边缘
    [InlineData(1917, 500, 1920, 1080)]     // 右边缘
    [InlineData(500, 3, 1920, 1080)]        // 上边缘
    [InlineData(500, 1077, 1920, 1080)]     // 下边缘
    [InlineData(0, 0, 1920, 1080)]          // 左上角
    [InlineData(1920, 1080, 1920, 1080)]    // 右下角
    [InlineData(-10, -10, 1920, 1080)]      // 屏幕外左上（钩子可报屏外坐标）
    [InlineData(2000, 1200, 1920, 1080)]    // 屏幕外右下
    public void Clamp_AlwaysInside(double mx, double my, double sw, double sh)
    {
        double panelW = 200, panelH = 60;
        var (l, t) = ClampTarget(mx, my, panelW / 2, panelH / 2, sw, sh, panelW, panelH);

        Assert.True(l >= 0 && l <= sw - panelW, $"left={l} 应在 [0, {sw - panelW}]");
        Assert.True(t >= 0 && t <= sh - panelH, $"top={t} 应在 [0, {sh - panelH}]");
        Assert.True(l + panelW <= sw, $"right={l + panelW} 不应越界 {sw}");
        Assert.True(t + panelH <= sh, $"bottom={t + panelH} 不应越界 {sh}");
    }

    [Fact]
    public void Clamp_CenterMouse_CentersPanel()
    {
        var (l, t) = ClampTarget(960, 540, 100, 30, 1920, 1080, 200, 60);
        Assert.Equal(860, l);   // 960 - 100
        Assert.Equal(510, t);   // 540 - 30
    }

    [Fact]
    public void Clamp_EdgeMouse_SticksToEdge()
    {
        // 鼠标在左缘 x=3 → 面板贴左（left=0）
        var (l, _) = ClampTarget(3, 540, 100, 30, 1920, 1080, 200, 60);
        Assert.Equal(0, l);
        // 鼠标在右缘 x=1917 → 面板贴右
        var (l2, _) = ClampTarget(1917, 540, 100, 30, 1920, 1080, 200, 60);
        Assert.Equal(1720, l2);   // 1920 - 200
    }
}
