using DynamicBird.Core;
using DynamicBird.Core.Detection;
using Xunit;

namespace DynamicBird.Tests;

public class EdgeStateDetectorTests
{
    private const double W = 1280;
    private const double H = 720;

    // ================= 四角 =================

    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, 5)]
    [InlineData(11, 11)]
    public void TopLeft_Corner_Detected(double x, double y)
    {
        Assert.Equal(EdgeRegion.TopLeft, EdgeStateDetector.DetectRegion(x, y, W, H));
    }

    [Theory]
    [InlineData(1275, 5)]
    [InlineData(1279, 23)]
    [InlineData(1260, 20)]
    public void TopRight_Is_SafeZone(double x, double y)
    {
        // 右上角安全区：不触发任何面板（避免影响关闭窗口）
        Assert.Equal(EdgeRegion.Unknown, EdgeStateDetector.DetectRegion(x, y, W, H));
    }

    [Theory]
    [InlineData(5, 715)]
    [InlineData(11, 709)]
    public void BottomLeft_Corner_Detected(double x, double y)
    {
        Assert.Equal(EdgeRegion.BottomLeft, EdgeStateDetector.DetectRegion(x, y, W, H));
    }

    [Theory]
    [InlineData(1275, 715)]
    [InlineData(1275, 710)]
    public void BottomRight_Corner_Detected(double x, double y)
    {
        Assert.Equal(EdgeRegion.BottomRight, EdgeStateDetector.DetectRegion(x, y, W, H));
    }

    // ================= 上下边（左/中/右三段） =================

    [Theory]
    [InlineData(100, 5, EdgeRegion.Top_Left)]
    [InlineData(640, 0, EdgeRegion.Top_Center)]
    [InlineData(1100, 5, EdgeRegion.Top_Right)]
    [InlineData(100, 715, EdgeRegion.Bottom_Left)]
    [InlineData(640, 719, EdgeRegion.Bottom_Center)]
    [InlineData(1100, 715, EdgeRegion.Bottom_Right)]
    public void Horizontal_Edge_Regions(double x, double y, EdgeRegion expected)
    {
        Assert.Equal(expected, EdgeStateDetector.DetectRegion(x, y, W, H));
    }

    // ================= 左右边（上/中/下三段） =================

    [Theory]
    [InlineData(5, 100, EdgeRegion.Left_Top)]
    [InlineData(0, 360, EdgeRegion.Left_Center)]
    [InlineData(5, 650, EdgeRegion.Left_Bottom)]
    [InlineData(1275, 100, EdgeRegion.Right_Top)]
    [InlineData(1280, 360, EdgeRegion.Right_Center)]
    [InlineData(1275, 650, EdgeRegion.Right_Bottom)]
    public void Vertical_Edge_Regions(double x, double y, EdgeRegion expected)
    {
        Assert.Equal(expected, EdgeStateDetector.DetectRegion(x, y, W, H));
    }

    // ================= 屏幕中间 =================

    [Theory]
    [InlineData(640, 360)]
    [InlineData(100, 100)]
    [InlineData(1180, 600)]
    public void Center_Is_Unknown(double x, double y)
    {
        Assert.Equal(EdgeRegion.Unknown, EdgeStateDetector.DetectRegion(x, y, W, H));
    }

    // ================= 边界与角区的界线 =================

    [Fact]
    public void Corner_Threshold_Wider_Than_Edge()
    {
        // 角区 = 边缘阈值×2（默认 12px），边缘 = 6px；在角区外、边区内的点应判定为边
        var nearCornerButEdge = EdgeStateDetector.DetectRegion(30, 5, W, H);
        Assert.Equal(EdgeRegion.Top_Left, nearCornerButEdge);
    }

    [Fact]
    public void Invalid_Screen_Returns_Unknown()
    {
        Assert.Equal(EdgeRegion.Unknown, EdgeStateDetector.DetectRegion(5, 5, 0, 0));
        Assert.Equal(EdgeRegion.Unknown, EdgeStateDetector.DetectRegion(5, 5, -100, 720));
    }

    // ================= 1/3 区域分界 =================

    [Fact]
    public void Bottom_Edge_Thirds_Boundaries()
    {
        // 1/3 中心区：426.6 ~ 853.3；中心两侧为左右区
        Assert.Equal(EdgeRegion.Bottom_Left, EdgeStateDetector.DetectRegion(300, 715, W, H));
        Assert.Equal(EdgeRegion.Bottom_Center, EdgeStateDetector.DetectRegion(640, 715, W, H));
        Assert.Equal(EdgeRegion.Bottom_Right, EdgeStateDetector.DetectRegion(1000, 715, W, H));
    }
}
