using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using DynamicBird.Animation;
using DynamicBird.Core.Services.Configuration;
using Xunit;

namespace DynamicBird.Tests
{
    /// <summary>
    /// ShapeAnimator 烟雾测试：重构（WPF 动画/渲染帧跟随/切换）后所有公开 API 调用不得抛异常。
    /// 防止"面板移动 bug"回归（跟随/切换/滑入滑出/飞行之间的状态互斥）。
    /// </summary>
    public class ShapeAnimatorSmokeTests
    {
        [Fact]
        public void AllPublicMethods_NoThrow()
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var settings = new SettingsManager();
                    var w = new Window
                    {
                        Width = 100,
                        Height = 50,
                        WindowStyle = WindowStyle.None,
                        ShowInTaskbar = false,
                        Left = 0,
                        Top = 0
                    };
                    var panel = new Border();
                    w.Content = panel;

                    var anim = new ShapeAnimator(w, panel);
                    anim.SetSettings(settings);
                    anim.SetAnimationsEnabled(true);

                    anim.SetPositionAndSizeTarget(0, 0, 200, 100);   // 切换
                    anim.SetSizeTarget(150, 80);                     // 尺寸
                    anim.SetPositionTargetWithoutReset(10, 10);      // 跟随(动画重定向)
                    anim.StartFollowPosition();                      // 渲染帧跟随
                    anim.StopFollowPosition();
                    anim.SetShowHideTarget(-100, -100, 0, allowOffscreen: true); // 滑出
                    anim.SetShowHideTarget(50, 50, 0.85, allowOffscreen: true);  // 滑入
                    anim.SetOpacityTarget(0.5);
                    anim.SetFlyParameters(300);
                    anim.StartFly(300, 300);                         // 飞行
                    anim.SetSizeDirect(120, 60);
                    anim.JumpTo(0, 0, 100, 50);
                    anim.SetClingParameters();
                    anim.SetClingTarget(200, 200);
                    anim.ExitClingParameters();
                    anim.StopAll();
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
            Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "ShapeAnimator 冒烟测试超时");

            if (failure != null)
            {
                throw new Xunit.Sdk.XunitException(
                    $"ShapeAnimator 异常: {failure.GetType().Name}: {failure.Message}");
            }
        }
    }
}
