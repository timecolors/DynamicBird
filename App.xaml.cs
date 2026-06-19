using System;
using System.Windows;
using System.Windows.Threading;

namespace LingDongBird
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 全局异常捕获
            this.DispatcherUnhandledException += (s, args) =>
            {
                System.Windows.MessageBox.Show($"发生未处理异常:\n{args.Exception.Message}\n\n{args.Exception.StackTrace}", "灵动鸟错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                args.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as System.Exception;
                System.Windows.MessageBox.Show($"发生未处理异常:\n{ex?.Message}\n\n{ex?.StackTrace}", "灵动鸟错误", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            };
        }
    }
}