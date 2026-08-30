using System.Windows;
using System.Windows.Controls;
using DynamicBird.UI.Widgets;

namespace SampleMarketTimer
{
    /// <summary>市场示例包：安装后出现在小组件区，显示一个提示。</summary>
    public class MyTimerWidget : IWidget
    {
        public string Name => "市场示例计时器";

        public UserControl CreateView()
        {
            var root = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            root.Children.Add(new TextBlock
            {
                Text = "来自在线市场的示例包",
                FontSize = 18,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            root.Children.Add(new TextBlock
            {
                Text = "安装成功：权限检测 + 沙箱编译已通过",
                FontSize = 12,
                Foreground = System.Windows.Media.Brushes.Gray,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0)
            });
            return new UserControl { Content = root };
        }

        public void OnActivated() { }
        public void OnDeactivated() { }
    }
}
