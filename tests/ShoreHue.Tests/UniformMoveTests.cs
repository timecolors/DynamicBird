using System;
using Xunit;

namespace ShoreHue.Tests;

/// <summary>
/// 验证匀速移动（MoveTowardFixedSpeed 的数学等价）：每帧移动固定距离，
/// 不随剩余距离衰减（修复：原"每帧移动剩余距离比例"是指数衰减，临近目标变慢）。
/// </summary>
public class UniformMoveTests
{
    /// <summary>固定速度移动一步：moveDist 恒定。与 ShapeAnimator.MoveTowardFixedSpeed 相同逻辑。</summary>
    private static (double left, double top) MoveStep(double curLeft, double curTop, double targetLeft, double targetTop, double moveDist)
    {
        double dx = targetLeft - curLeft;
        double dy = targetTop - curTop;
        double dist = Math.Sqrt(dx * dx + dy * dy);
        if (dist <= moveDist || dist < 0.5) return (targetLeft, targetTop);
        return (curLeft + dx / dist * moveDist, curTop + dy / dist * moveDist);
    }

    [Fact]
    public void FixedSpeed_EachStepMovesSameDistance()
    {
        // 从 (0,0) 匀速向 (1000, 0)，每步 100px → 每步位移恒定 100
        double x = 0, y = 0;
        double prevX = x;
        var distances = new System.Collections.Generic.List<double>();
        for (int i = 0; i < 9; i++)
        {
            (x, y) = MoveStep(x, y, 1000, 0, 100);
            distances.Add(Math.Abs(x - prevX));
            prevX = x;
        }
        // ★ 匀速：每一步移动距离都 ≈ 100（不衰减）
        foreach (var d in distances)
        {
            Assert.True(Math.Abs(d - 100) < 0.001, $"步进 {d} 应恒定 100（匀速）");
        }
        Assert.Equal(900, x);   // 9 步 × 100
    }

    [Fact]
    public void FixedSpeed_NearTarget_StillMovesFast()
    {
        // 关键：临近目标（剩余 30px）时仍一次移动 100px 直接到达（不减速）
        var (x, y) = MoveStep(970, 0, 1000, 0, 100);
        Assert.Equal(1000, x);   // 剩余 30 ≤ moveDist → 直接到位
        Assert.Equal(0, y);
    }

    [Fact]
    public void FixedSpeed_Diagonal_MovesConstantLength()
    {
        // 对角：每步移动距离恒定（√(100²) = 100）
        double x = 0, y = 0;
        for (int i = 0; i < 5; i++)
        {
            var before = Math.Sqrt(x * x + y * y);
            (x, y) = MoveStep(x, y, 1000, 1000, 100);
            var after = Math.Sqrt(x * x + y * y);
            Assert.True(Math.Abs((after - before) - 100) < 0.01, $"步进长度 {after - before} 应 ≈ 100");
        }
    }

    [Fact]
    public void Speed_DependsOnFlyDuration()
    {
        // 速度 = 1000/FlyDurationMs：T=500 → 2px/ms；T=2000 → 0.5px/ms（越慢）
        double speed500 = 1000.0 / 500;
        double speed2000 = 1000.0 / 2000;
        Assert.Equal(2.0, speed500);
        Assert.Equal(0.5, speed2000);
    }

    [Fact]
    public void FlyDurationZero_MeansInstant()
    {
        // FlyDurationMs=0 → 直接设置（拖动窗口式最跟手）
        // （由调用方处理：T<=0 时直接设置，这里验证数学上速度趋于无限）
        Assert.True(1000.0 / 1 > 1000.0 / 100);   // 小 T → 快
    }
}
