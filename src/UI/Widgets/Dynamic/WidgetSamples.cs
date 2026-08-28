namespace DynamicBird.UI.Widgets.Dynamic
{
    /// <summary>
    /// 内置示例插件源码（与 docs/WIDGET-SPEC.md 中的示例一致，可持续扩充）。
    /// </summary>
    public static class WidgetSamples
    {
        public const string Clock = @"using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DynamicBird.UI.Widgets;

public class ClockWidget : UserControl, IWidget
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly TextBlock _time = new()
    {
        FontSize = 32, FontWeight = FontWeights.SemiBold,
        HorizontalAlignment = HorizontalAlignment.Center,
        Foreground = System.Windows.Media.Brushes.White
    };
    private readonly TextBlock _date = new()
    {
        FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center,
        Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(138, 138, 138))
    };

    public ClockWidget()
    {
        var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(_time);
        panel.Children.Add(_date);
        Content = panel;
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
        Refresh();
    }

    private void Refresh()
    {
        _time.Text = DateTime.Now.ToString(""HH:mm:ss"");
        _date.Text = DateTime.Now.ToString(""yyyy-MM-dd dddd"");
    }

    public string Name => ""时钟"";
    public UserControl CreateView() => this;
    public void OnActivated() { }
    public void OnDeactivated() { }
}
";

        public const string Counter = @"using System.Windows;
using System.Windows.Controls;
using DynamicBird.UI.Widgets;

public class CounterWidget : UserControl, IWidget
{
    private int _count;
    private readonly TextBlock _value = new()
    {
        FontSize = 30, FontWeight = FontWeights.SemiBold,
        HorizontalAlignment = HorizontalAlignment.Center,
        Foreground = System.Windows.Media.Brushes.White
    };

    public CounterWidget()
    {
        var title = new TextBlock
        {
            Text = ""计数"", FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(170, 170, 170))
        };
        var up = new Button { Content = ""＋"", Width = 48, Height = 26 };
        up.Click += (_, _) => { _count++; Refresh(); };
        var down = new Button { Content = ""－"", Width = 48, Height = 26, Margin = new Thickness(6, 0, 6, 0) };
        down.Click += (_, _) => { _count--; Refresh(); };
        var reset = new Button { Content = ""归零"", Width = 48, Height = 26 };
        reset.Click += (_, _) => { _count = 0; Refresh(); };

        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        row.Children.Add(up);
        row.Children.Add(down);
        row.Children.Add(reset);

        var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(title);
        panel.Children.Add(_value);
        panel.Children.Add(row);
        Content = panel;
        Refresh();
    }

    private void Refresh() => _value.Text = _count.ToString();

    public string Name => ""计数器"";
    public UserControl CreateView() => this;
    public void OnActivated() { }
    public void OnDeactivated() { }
}
";

        public const string Note = @"using System.Windows;
using System.Windows.Controls;
using DynamicBird.UI.Widgets;

public class NoteWidget : UserControl, IWidget
{
    public NoteWidget()
    {
        var title = new TextBlock
        {
            Text = ""便签"", FontSize = 13, FontWeight = FontWeights.SemiBold,
            Foreground = System.Windows.Media.Brushes.White
        };
        var body = new TextBlock
        {
            Text = ""记得在周五前完成报告。\n双击这里没有反应，内容写死在代码里。"",
            FontSize = 12, TextWrapping = TextWrapping.Wrap,
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(221, 221, 221))
        };
        var panel = new StackPanel { Margin = new Thickness(10) };
        panel.Children.Add(title);
        panel.Children.Add(body);
        Content = new Border
        {
            Child = panel,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x22, 0xFF, 0xFF, 0x99)),
            CornerRadius = new CornerRadius(8)
        };
    }

    public string Name => ""便签"";
    public UserControl CreateView() => this;
    public void OnActivated() { }
    public void OnDeactivated() { }
}
";

        public const string Shortcut = @"using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using DynamicBird.UI.Widgets;

public class ShortcutWidget : UserControl, IWidget
{
    public ShortcutWidget()
    {
        var btn = new Button
        {
            Content = ""打开网站"",
            FontSize = 14,
            Padding = new Thickness(14, 6, 14, 6)
        };
        btn.Click += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo(""https://www.bing.com"") { UseShellExecute = true });
            }
            catch { }
        };
        Content = new StackPanel { Children = { btn }, VerticalAlignment = VerticalAlignment.Center };
    }

    public string Name => ""快捷打开"";
    public UserControl CreateView() => this;
    public void OnActivated() { }
    public void OnDeactivated() { }
}
";

        public const string Weather = @"using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DynamicBird.UI.Widgets;

// 需要联网权限（编辑器里勾选“联网”）
public class WeatherWidget : UserControl, IWidget
{
    private readonly TextBlock _text = new()
    {
        FontSize = 16, TextWrapping = TextWrapping.Wrap,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Foreground = System.Windows.Media.Brushes.White
    };
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };

    public WeatherWidget()
    {
        Content = _text;
        _text.Text = ""天气加载中…"";
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            // Open-Meteo 免费天气接口（按城市名定位）
            string city = ""北京"";
            string geo = await Http.GetStringAsync(
                ""https://geocoding-api.open-meteo.com/v1/search?name="" + Uri.EscapeDataString(city) + ""&count=1&language=zh"");
            using var g = JsonDocument.Parse(geo);
            var loc = g.RootElement.GetProperty(""results"")[0];
            double lat = loc.GetProperty(""latitude"").GetDouble();
            double lon = loc.GetProperty(""longitude"").GetDouble();

            string fc = await Http.GetStringAsync(
                $""https://api.open-meteo.com/v1/forecast?latitude={lat:F4}&longitude={lon:F4}&current=temperature_2m,weather_code&timezone=auto"");
            using var f = JsonDocument.Parse(fc);
            double temp = f.RootElement.GetProperty(""current"").GetProperty(""temperature_2m"").GetDouble();
            _text.Text = $""{city} · {temp:F0}°C"";
        }
        catch
        {
            _text.Text = ""天气获取失败"";
        }
    }

    public string Name => ""天气"";
    public UserControl CreateView() => this;
    public void OnActivated() { }
    public void OnDeactivated() { }
}
";
    }
}
