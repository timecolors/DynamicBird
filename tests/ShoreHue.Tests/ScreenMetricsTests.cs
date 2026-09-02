using System;
using System.Windows;
using ShoreHue.Infrastructure.Utils;
using Xunit;

namespace ShoreHue.Tests;

/// <summary>
/// 多显示器边界提供器测试：
/// ScreenMetrics 依赖真实桌面（Screen.FromPoint），CI/单屏环境只能验证行为不崩溃且返回合理值；
/// 多屏正确性靠"当前屏幕工作区"语义保证（每个点都查询其所在显示器，而非主屏常量）。
/// </summary>
public class ScreenMetricsTests
{
    [Fact]
    public void GetScreenForPoint_Returns_NonEmpty_Rect()
    {
        var wa = ScreenMetrics.GetScreenForPoint(100, 100);
        Assert.True(wa.Width > 0);
        Assert.True(wa.Height > 0);
        Assert.True(wa.Left >= 0);
        Assert.True(wa.Top >= 0);
    }

    [Fact]
    public void GetScreenForWindow_Returns_NonEmpty_Rect()
    {
        var wa = ScreenMetrics.GetScreenForWindow(0, 0, 800, 600);
        Assert.True(wa.Width > 0);
        Assert.True(wa.Height > 0);
    }

    [Fact]
    public void DipScale_Is_Positive()
    {
        Assert.True(ScreenMetrics.DipScale > 0);
        Assert.True(ScreenMetrics.DipScale <= 4.0); // 极端缩放也不应离谱
    }

    [Fact]
    public void Cached_Query_Matches_Uncached_Within_Tolerance()
    {
        var cached = ScreenMetrics.GetCachedScreenForPoint(500, 400);
        var direct = ScreenMetrics.GetScreenForPoint(500, 400);
        // 同一点应落在同一显示器（单屏时完全一致；多屏缓存边界最多相差一个屏宽）
        Assert.True(Math.Abs(cached.Width - direct.Width) < 1.0);
        Assert.True(Math.Abs(cached.Height - direct.Height) < 1.0);
    }

    /// <summary>
    /// ★ 关键回归护栏：单屏下 ScreenMetrics 必须与 WPF 主屏常量完全一致。
    /// 曾因 DipScale 错误（Graphics.FromHwnd 返回 96）导致所有尺寸被放大，
    /// 面板错位/圆角异常/跨边飞行失败均源于此。
    /// </summary>
    [Fact]
    public void SingleScreen_Matches_PrimaryScreenConstants()
    {
        // 屏幕中心与四角取样（屏幕外/边缘点可能落相邻屏，只在屏幕内取样）
        var wa = ScreenMetrics.GetCachedScreenForPoint(
            SystemParameters.PrimaryScreenWidth / 2,
            SystemParameters.PrimaryScreenHeight / 2);

        Assert.True(Math.Abs(wa.Width - SystemParameters.PrimaryScreenWidth) < 1.0,
            $"宽度不一致: Screen={wa.Width:F2} vs Primary={SystemParameters.PrimaryScreenWidth:F2}");
        Assert.True(Math.Abs(wa.Height - SystemParameters.PrimaryScreenHeight) < 1.0,
            $"高度不一致: Screen={wa.Height:F2} vs Primary={SystemParameters.PrimaryScreenHeight:F2}");
        Assert.True(Math.Abs(wa.Left) < 1.0, "左边界应为 0");
        Assert.True(Math.Abs(wa.Top) < 1.0, "上边界应为 0");

        // DPI 缩放必须 > 0 且合理（150% 缩放时 = 1.5）
        Assert.True(ScreenMetrics.DipScale >= 0.5 && ScreenMetrics.DipScale <= 4.0);
    }
}