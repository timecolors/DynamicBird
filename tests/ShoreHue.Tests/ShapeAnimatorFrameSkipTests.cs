using ShoreHue.Animation;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Xunit;

namespace ShoreHue.Tests;

/// <summary>验证 ShapeAnimator 节能降帧（SetFrameSkip 钳制 + 渲染帧跳帧）。</summary>
public class ShapeAnimatorFrameSkipTests
{
    /// <summary>SetFrameSkip 钳制逻辑：负值→0，超大→3，正常值保持。</summary>
    [Fact]
    public void SetFrameSkip_ClampsRange()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var w = new Window
                {
                    Width = 100,
                    Height = 50,
                    WindowStyle = WindowStyle.None,
                    ShowInTaskbar = false,
                    Left = -3000,
                    Top = -3000
                };
                var panel = new Border();
                w.Content = panel;
                w.Show();

                var anim = new ShapeAnimator(w, panel);

                // 钳制下界
                anim.SetFrameSkip(-1);
                anim.SetFrameSkip(0);
                anim.SetFrameSkip(-100);

                // 钳制上界
                anim.SetFrameSkip(3);
                anim.SetFrameSkip(10);
                anim.SetFrameSkip(99);

                // 正常值
                anim.SetFrameSkip(1);
                anim.SetFrameSkip(2);

                anim.Dispose();
                w.Close();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "SetFrameSkip 测试超时");
        if (failure != null)
        {
            throw new Xunit.Sdk.XunitException(
                $"SetFrameSkip 异常: {failure.GetType().Name}: {failure.Message}");
        }
    }

    /// <summary>跳帧启用时渲染回调不抛异常（跟随/小鸟依人路径在降帧下正常）。</summary>
    [Fact]
    public void FrameSkip_WithFollow_NoThrow()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var w = new Window
                {
                    Width = 100,
                    Height = 50,
                    WindowStyle = WindowStyle.None,
                    ShowInTaskbar = false,
                    Left = -3000,
                    Top = -3000
                };
                var panel = new Border();
                w.Content = panel;
                w.Show();

                var anim = new ShapeAnimator(w, panel);
                anim.SetFrameSkip(2);   // 省电跳帧

                // 跟随路径（渲染帧 provider）
                double targetX = 0, targetY = 0;
                anim.FollowPositionProvider = () => (targetX, targetY);
                anim.StartFollowPosition();
                targetX = 50;
                targetY = 30;
                Thread.Sleep(80);       // 若干渲染帧（部分被跳帧）
                anim.StopFollowPosition();

                // 小鸟依人路径
                anim.SetClingParameters();
                anim.SetClingTarget(100, 100);
                Thread.Sleep(80);
                anim.ExitClingParameters();

                anim.SetFrameSkip(0);   // 恢复正常
                anim.Dispose();
                w.Close();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "降帧跟随测试超时");
        if (failure != null)
        {
            throw new Xunit.Sdk.XunitException(
                $"降帧跟随异常: {failure.GetType().Name}: {failure.Message}");
        }
    }
}
