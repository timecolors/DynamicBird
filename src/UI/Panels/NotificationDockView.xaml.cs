using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ShoreHue.Infrastructure.WinApi;
using ShoreHue.UI.Localization;

namespace ShoreHue.UI.Panels
{
    /// <summary>
    /// 右下角通知坞：被动展示捕获到的消息弹窗/系统通知，点击打开对应应用。
    /// 功能专一，不再混杂蓝牙/手机连接入口。
    /// </summary>
    public partial class NotificationDockView : UserControl
    {
        public NotificationDockView()
        {
            InitializeComponent();
            NotificationList.ItemsSource = ToastMonitor.Notifications;
            ToastMonitor.Changed += OnNotificationsChanged;
            UpdateHeader();
        }

        private void OnNotificationsChanged()
        {
            Dispatcher.BeginInvoke(new Action(UpdateHeader));
        }

        private void UpdateHeader()
        {
            int count = ToastMonitor.Notifications.Count;
            TitleText.Text = count > 0
                    ? string.Format(LocalizationManager.Instance["Notify_Title"], count)
                    : LocalizationManager.Instance["Notify_TitleEmpty"];
        }

        private void Item_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is ToastNotificationItem item)
            {
                ToastMonitor.OpenApp(item);
                e.Handled = true;
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            ToastMonitor.ClearAll();
        }
    }
}
