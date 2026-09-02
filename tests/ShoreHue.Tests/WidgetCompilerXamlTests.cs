using ShoreHue.UI.Widgets.Dynamic;
using Xunit;
using System;
using System.Threading;

namespace ShoreHue.Tests
{
    /// <summary>完全编程：XAML + 代码后置 动态编译验证。</summary>
    [Collection("WidgetStore")]
    public class WidgetCompilerXamlTests
    {
        const string Xaml = @"<UserControl xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">
<StackPanel>
  <TextBlock x:Name=""Lbl"" Text=""Hello""/>
  <Button x:Name=""Btn"" Content=""点我"" Click=""OnClick""/>
</StackPanel>
</UserControl>";

        const string Cs = @"using System.Windows;
using System.Windows.Controls;
public partial class MyXamlWidget : System.Windows.Controls.UserControl, ShoreHue.UI.Widgets.IWidget
{
    public MyXamlWidget() { InitializeComponent(); }
    public string Name => ""XAML测试"";
    public UserControl CreateView() => this;
    public void OnClick(object sender, RoutedEventArgs e) { Lbl.Text = ""Clicked!""; }
    public void OnActivated() { }
    public void OnDeactivated() { }
}";

        private static T RunSta<T>(Func<T> action)
        {
            T result = default!;
            Exception? error = null;
            var t = new Thread(() =>
            {
                try { result = action(); }
                catch (Exception ex) { error = ex; }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();
            if (error != null) throw error;
            return result;
        }

        [Fact]
        public void CompileXaml_LoadsAndBindsEvents()
        {
            var result = RunSta(() =>
            {
                var (widget, err) = WidgetCompiler.CompileXaml("xaml-test", Xaml, Cs);
                return (widget, err);
            });
            Assert.True(result.widget != null, "编译失败: " + result.err);
            Assert.Equal("XAML测试", result.widget!.Name);
        }

        [Fact]
        public void CompileXaml_InvalidXaml_ReturnsError()
        {
            var result = RunSta(() =>
            {
                var (widget, err) = WidgetCompiler.CompileXaml("xaml-bad", "<UserControl>不完整",
                    "public partial class X : System.Windows.Controls.UserControl { public X(){InitializeComponent();} }");
                return (widget, err);
            });
            Assert.Null(result.widget);
        }
    }
}
