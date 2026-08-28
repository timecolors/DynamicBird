using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using DynamicBird.UI.Onboarding;
using Xunit;

namespace DynamicBird.Tests
{
    /// <summary>
    /// 引导窗口回归测试：OnboardingWindow 的 XAML 解析/构造/显示不得抛异常。
    /// 入口处 catch{} 会静默吞掉 XamlParseException（如非法颜色、坏绑定），
    /// 导致"引导页打不开"且无日志——此测试在 CI 中兜底拦截这类问题。
    /// OnboardingWindow 已自行合并 Theme.xaml，无需 Application 资源环境。
    /// </summary>
    public class OnboardingWindowLoadTests
    {
        [Fact]
        public void OnboardingWindow_ConstructsAndShows_WithoutError()
        {
            Exception? failure = null;

            var thread = new Thread(() =>
            {
                try
                {
                    var w = new OnboardingWindow();
                    w.Show();
                    w.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                    w.Close();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "OnboardingWindow 加载超时");

            if (failure != null)
            {
                throw new Xunit.Sdk.XunitException(
                    $"OnboardingWindow 加载失败: {failure.GetType().Name}: {failure.Message}");
            }
        }
    }
}
