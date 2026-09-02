using ShoreHue.UI.Widgets.Dynamic;
using ShoreHue.UI.Widgets;
using Xunit;
using System;
using System.Threading;
using System.Windows;
using System.IO;

namespace ShoreHue.Tests
{
    [Collection("WidgetStore")]
    public class FullProgrammingSaveTest
    {
        const string Xaml = @"<UserControl xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""><StackPanel><Button Content=""Hi""/></StackPanel></UserControl>";
        const string XamlCs = @"using System.Windows;
using System.Windows.Controls;
using ShoreHue.UI.Widgets;
public partial class TestXamlWidget : UserControl, IWidget
{
    public TestXamlWidget() { InitializeComponent(); }
    public string Name => ""完全编程测试"";
    public UserControl CreateView() => this;
    public void OnActivated() { }
    public void OnDeactivated() { }
}";

        private static (IWidget? w, string err) RunSta()
        {
            (IWidget? w, string err) result = (null, "");
            Exception? error = null;
            var t = new Thread(() =>
            {
                try
                {
                    var app = Application.Current ?? new Application();
                    if (app.Resources.MergedDictionaries.Count == 0)
                        app.Resources.MergedDictionaries.Add(new ResourceDictionary
                        { Source = new Uri("pack://application:,,,/ShoreHue;component/src/UI/Theme/Theme.xaml") });
                    _ = typeof(ShoreHue.UI.Localization.LocalizationManager).Assembly;
                    var (widget, err) = WidgetCompiler.CompileXaml("test-fullprog", Xaml, XamlCs);
                    result = (widget, err);
                }
                catch (Exception ex)
                {
                    var sb = new System.Text.StringBuilder();
                    for (var e = ex; e != null; e = e.InnerException) sb.AppendLine(e.GetType().Name + ": " + e.Message);
                    result = (null, sb.ToString());
                }
            });
            t.SetApartmentState(ApartmentState.STA); t.Start(); t.Join();
            if (error != null) result = (null, error.Message);
            return result;
        }

        [Fact]
        public void FullProgramming_XamlCs_Compiles()
        {
            var (w, err) = RunSta();
            Assert.True(w != null, "完全编程（XAML+CS）编译失败: " + err);
            Assert.Equal("完全编程测试", w!.Name);
        }
    }
}
