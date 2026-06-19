using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace LingDongBird.Features.FocusAware
{
    public partial class FocusAwareView : UserControl
    {
        private Timer? _timer;

        public FocusAwareView()
        {
            InitializeComponent();
            Loaded += FocusAwareView_Loaded;
            Unloaded += FocusAwareView_Unloaded;
        }

        private void FocusAwareView_Loaded(object sender, RoutedEventArgs e)
        {
            _timer = new Timer(CheckActiveWindow, null, 0, 500);
        }

        private void FocusAwareView_Unloaded(object sender, RoutedEventArgs e)
        {
            _timer?.Dispose();
        }

        private void CheckActiveWindow(object? state)
        {
            IntPtr hwnd = GetForegroundWindow();
            string title = GetWindowText(hwnd);

            if (string.IsNullOrWhiteSpace(title))
                title = "桌面";

            Application.Current.Dispatcher.Invoke(() =>
            {
                AppNameText.Text = title.Length > 20 ? title.Substring(0, 20) + "..." : title;
            });
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);

        private static string GetWindowText(IntPtr hWnd)
        {
            const int nChars = 256;
            var buff = new System.Text.StringBuilder(nChars);
            return GetWindowText(hWnd, buff, nChars) > 0 ? buff.ToString() : "";
        }
    }
}