using System.Windows;
using System.Windows.Controls;

namespace DynamicBird.UI.Settings.Pages
{
    /// <summary>
    /// 简单输入对话框（鸟笼新增功能命名用）。避免 WinForms InputBox 在 WPF 环境弹多次的问题。
    /// </summary>
    public sealed class InputDialog : Window
    {
        private readonly TextBox _box;

        public string ResultText => _box.Text?.Trim() ?? "";

        public InputDialog(string title, string prompt, string defaultText = "")
        {
            Title = title;
            Width = 360;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;

            var panel = new StackPanel { Margin = new Thickness(16) };
            panel.Children.Add(new TextBlock { Text = prompt, FontSize = 12, Margin = new Thickness(0, 0, 0, 8), TextWrapping = TextWrapping.Wrap });
            _box = new TextBox { FontSize = 13, Padding = new Thickness(4, 3, 4, 3), Text = defaultText };
            panel.Children.Add(_box);

            var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            var ok = new Button { Content = "确定", Width = 72, Height = 28, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
            ok.Click += (_, _) => { DialogResult = true; };
            var cancel = new Button { Content = "取消", Width = 72, Height = 28, IsCancel = true };
            cancel.Click += (_, _) => { DialogResult = false; };
            btns.Children.Add(ok);
            btns.Children.Add(cancel);
            panel.Children.Add(btns);

            Content = panel;
            Loaded += (_, _) => _box.Focus();
        }
    }
}
