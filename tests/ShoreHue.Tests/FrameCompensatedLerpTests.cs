using System;
using Xunit;

namespace ShoreHue.Tests;

/// <summary>
/// 验证帧率补偿 lerp（FrameCompensatedLerp 的数学等价）：
/// 跳帧后每秒收敛量应与 60fps 基准一致——否则改帧率会改变追随/小鸟依人速度。
/// 测试用与 ShapeAnimator 相同的公式：effectiveK = 1 - (1 - k)^(frameSkip + 1)。
/// </summary>
public class FrameCompensatedLerpTests
{
    private static double Compensate(double baseK, int frameSkip)
    {
        if (frameSkip <= 0 || baseK >= 0.999) return baseK;
        return Math.Min(0.999, Math.Max(0, 1.0 - Math.Pow(1.0 - baseK, frameSkip + 1)));
    }

    /// <summary>模拟从 start 到 target 的 lerp 收敛，1 秒（60 帧）后位置。</summary>
    private static double SimulateSecond(double start, double target, double baseK, int frameSkip)
    {
        double pos = start;
        int frames = 60 / (frameSkip + 1);   // 每秒实际处理帧数
        double k = Compensate(baseK, frameSkip);
        for (int i = 0; i < frames; i++)
        {
            pos += (target - pos) * k;
        }
        return pos;
    }

    [Fact]
    public void OneSecondConvergence_SameAcrossFrameSkips()
    {
        // 小鸟依人基准：ClingLerp = 0.18
        double baseK = 0.18;
        double noSkip = SimulateSecond(0, 100, baseK, 0);      // 60fps
        double skip1 = SimulateSecond(0, 100, baseK, 1);       // ~30fps
        double skip2 = SimulateSecond(0, 100, baseK, 2);       // ~20fps

        // ★ 1 秒后位置应基本一致（帧率无关）
        Assert.True(Math.Abs(noSkip - skip1) < 0.5, $"60fps={noSkip:F2} 30fps={skip1:F2}");
        Assert.True(Math.Abs(noSkip - skip2) < 1.0, $"60fps={noSkip:F2} 20fps={skip2:F2}");
    }

    [Fact]
    public void OneSecondConvergence_FollowLerp_SameAcrossFrameSkips()
    {
        // 跟随松紧：FlyDurationMs=500 → lerp 约 0.22（模拟典型值）
        double baseK = 0.22;
        double noSkip = SimulateSecond(0, 100, baseK, 0);
        double skip1 = SimulateSecond(0, 100, baseK, 1);
        double skip2 = SimulateSecond(0, 100, baseK, 2);

        Assert.True(Math.Abs(noSkip - skip1) < 0.5, $"60fps={noSkip:F2} 30fps={skip1:F2}");
        Assert.True(Math.Abs(noSkip - skip2) < 1.0, $"60fps={noSkip:F2} 20fps={skip2:F2}");
    }

    [Fact]
    public void NoSkip_ReturnsBaseK()
    {
        Assert.Equal(0.18, Compensate(0.18, 0));
    }

    [Fact]
    public void AbsoluteFollow_Unaffected()
    {
        // k >= 0.999（绝对跟手）不补偿
        Assert.Equal(1.0, Compensate(1.0, 2));
    }

    [Fact]
    public void Compensated_IsAlwaysHigherThanBase()
    {
        // 跳帧后每帧系数应提高（补偿减少的处理次数）
        double compensated = Compensate(0.18, 1);
        Assert.True(compensated > 0.18, $"compensated={compensated:F4}");
        Assert.True(compensated < 1.0);
    }
}
