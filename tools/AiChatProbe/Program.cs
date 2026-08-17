using System;
using System.Windows;
using DynamicBird.UI.AI;
using DynamicBird.UI.Theme;

namespace AiChatProbe
{
    /// <summary>开发探针：AI 面板（多会话/回车发送/文件上传/输出到光标）。</summary>
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            var app = new Application();
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/DynamicBird;component/src/UI/Theme/Theme.xaml") });
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/DynamicBird;component/src/UI/Theme/AppIcons.xaml") });
            var view = new AiChatView();
            var win = new Window { Title = "AiChatProbe", Width = 420, Height = 560, WindowStartupLocation = WindowStartupLocation.CenterScreen, Content = view, Icon = AppIconHelper.LoadAppIcon() };
            win.Show();
            var close = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(25) };
            close.Tick += (_, _) => { close.Stop(); app.Shutdown(); };
            close.Start();
            app.Run();
        }
    }
}