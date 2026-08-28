# 灵动鸟 C# 小组件开发指南

动态小组件用 **C# 代码**编写（完整自由度：任意 WPF UI 与逻辑）。应用内置 Roslyn 编译器，启动/安装时把源码编译成小组件运行。

**普通用户两条路：**
1. 设置 → 🧩 我的小组件 → **新建小组件** → 点"插入示例代码"起步，改代码 → 编译预览 → 保存
2. **把需求描述给 AI**（如：*写一个显示当前时间的时钟小组件*），要求 AI 按本文档输出完整 C# 类 → 粘贴到编辑器 → 编译预览 → 保存

---

## 1. 接口契约

小组件就是实现 **`DynamicBird.UI.Widgets.IWidget`** 的公开类，同时继承 `UserControl`：

```csharp
using System.Windows.Controls;
using DynamicBird.UI.Widgets;

public class MyWidget : UserControl, IWidget
{
    // 在构造函数里搭建你的 UI（任意 WPF 控件）
    public MyWidget()
    {
        Content = new System.Windows.Controls.TextBlock { Text = "你好" };
    }

    public string Name => "我的小组件";          // 面板标签显示名
    public UserControl CreateView() => this;     // 固定写法
    public void OnActivated() { }                // 面板激活时（可选）
    public void OnDeactivated() { }              // 面板切走时（可选）
}
```

需要的 using：`System`、`System.Windows`、`System.Windows.Controls`、`System.Windows.Threading`（定时器）、`DynamicBird.UI.Widgets`（IWidget）。框架里的类都能用（HttpClient、Json、Process 等）。

---

## 2. 常见功能示例（可直接复制修改）

### ⏰ 时钟（定时刷新）
```csharp
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DynamicBird.UI.Widgets;

public class ClockWidget : UserControl, IWidget
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly TextBlock _time = new() { FontSize = 32, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock _date = new() { FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center, Foreground = System.Windows.Media.Brushes.Gray };

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
        _time.Text = DateTime.Now.ToString("HH:mm:ss");
        _date.Text = DateTime.Now.ToString("yyyy-MM-dd dddd");
    }

    public string Name => "时钟";
    public UserControl CreateView() => this;
    public void OnActivated() { }
    public void OnDeactivated() { }
}
```

### 🔢 计数器（按钮交互）
```csharp
using System.Windows;
using System.Windows.Controls;
using DynamicBird.UI.Widgets;

public class CounterWidget : UserControl, IWidget
{
    private int _count;
    private readonly TextBlock _value = new() { FontSize = 30, HorizontalAlignment = HorizontalAlignment.Center };

    public CounterWidget()
    {
        var up = new Button { Content = "＋", Width = 48, Height = 26 };
        up.Click += (_, _) => { _count++; Refresh(); };
        var down = new Button { Content = "－", Width = 48, Height = 26, Margin = new Thickness(6, 0, 6, 0) };
        down.Click += (_, _) => { _count--; Refresh(); };

        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        row.Children.Add(up);
        row.Children.Add(down);
        var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(_value);
        panel.Children.Add(row);
        Content = panel;
        Refresh();
    }

    private void Refresh() => _value.Text = _count.ToString();

    public string Name => "计数器";
    public UserControl CreateView() => this;
    public void OnActivated() { }
    public void OnDeactivated() { }
}
```

### 📝 便签（静态内容 + 背景卡片）
```csharp
using System.Windows;
using System.Windows.Controls;
using DynamicBird.UI.Widgets;

public class NoteWidget : UserControl, IWidget
{
    public NoteWidget()
    {
        var panel = new StackPanel { Margin = new Thickness(10) };
        panel.Children.Add(new TextBlock { Text = "待办", FontWeight = FontWeights.SemiBold, Foreground = System.Windows.Media.Brushes.White });
        panel.Children.Add(new TextBlock { Text = "1. 写周报\n2. 买牛奶", TextWrapping = TextWrapping.Wrap, Foreground = System.Windows.Media.Brushes.LightGray });
        Content = new Border { Child = panel, Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x22, 0xFF, 0xFF, 0x99)), CornerRadius = new CornerRadius(8) };
    }

    public string Name => "便签";
    public UserControl CreateView() => this;
    public void OnActivated() { }
    public void OnDeactivated() { }
}
```

### 🔗 快捷打开网址（需勾选"联网"权限）
```csharp
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using DynamicBird.UI.Widgets;

public class ShortcutWidget : UserControl, IWidget
{
    public ShortcutWidget()
    {
        var btn = new Button { Content = "打开网站", FontSize = 14, Padding = new Thickness(14, 6, 14, 6) };
        btn.Click += (_, _) => Process.Start(new ProcessStartInfo("https://www.bing.com") { UseShellExecute = true });
        Content = new StackPanel { Children = { btn }, VerticalAlignment = VerticalAlignment.Center };
    }

    public string Name => "快捷打开";
    public UserControl CreateView() => this;
    public void OnActivated() { }
    public void OnDeactivated() { }
}
```

### 🌤 天气（联网获取 JSON，需"联网"权限）
```csharp
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DynamicBird.UI.Widgets;

public class WeatherWidget : UserControl, IWidget
{
    private readonly TextBlock _text = new() { FontSize = 16, TextWrapping = TextWrapping.Wrap, Foreground = System.Windows.Media.Brushes.White };
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };

    public WeatherWidget()
    {
        Content = _text;
        _text.Text = "加载中…";
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            string city = "北京";
            string geo = await Http.GetStringAsync("https://geocoding-api.open-meteo.com/v1/search?name=" + Uri.EscapeDataString(city) + "&count=1&language=zh");
            var loc = JsonDocument.Parse(geo).RootElement.GetProperty("results")[0];
            double lat = loc.GetProperty("latitude").GetDouble();
            double lon = loc.GetProperty("longitude").GetDouble();

            string fc = await Http.GetStringAsync($"https://api.open-meteo.com/v1/forecast?latitude={lat:F4}&longitude={lon:F4}&current=temperature_2m,weather_code&timezone=auto");
            double temp = JsonDocument.Parse(fc).RootElement.GetProperty("current").GetProperty("temperature_2m").GetDouble();
            _text.Text = $"{city} · {temp:F0}°C";
        }
        catch { _text.Text = "获取失败"; }
    }

    public string Name => "天气";
    public UserControl CreateView() => this;
    public void OnActivated() { }
    public void OnDeactivated() { }
}
```

### 📋 剪贴板（注意：必须在 UI 线程调用）
```csharp
private void Copy_Click(object sender, RoutedEventArgs e)
{
    // Clipboard.SetText 必须 STA/UI 线程；小组件代码运行在 UI 线程，直接调用即可
    System.Windows.Clipboard.SetText("要复制的内容");
}
```

### 💻 系统信息（CPU/内存）
```csharp
// 用 System.Management（已内置）读取，或 System.Diagnostics.PerformanceCounter
// 示例：内存占用百分比
var counter = new System.Diagnostics.PerformanceCounter("Memory", "Available MBytes");
float availableMB = counter.NextValue();
```

---

## 3. 注意事项（必读）

- **线程**：所有 UI 更新必须在 UI 线程（构造函数/事件回调天然在 UI 线程；`async` 里 `await` 后回到 UI 线程用 `Dispatcher`）
- **剪贴板**：`Clipboard.SetText` 必须 STA 线程（小组件在 UI 线程运行，正常使用即可）
- **定时器**：用 `DispatcherTimer`（自动回 UI 线程），不要用 `System.Threading.Timer`
- **资源清理**：`DispatcherTimer` 记得 `Stop()`（小组件卸载时 `OnDeactivated` 里可处理）
- **编译失败**：编辑器会显示错误行号与信息，修复后重新"编译预览"
- **本地自用模型**：你运行的是自己（或 AI 帮你生成）的代码——请只从可信来源粘贴代码；未来市场分发会引入沙箱与风险标记

---

## 4. 给 AI 的生成提示（模板）

> 你是灵动鸟小组件生成器。按 docs/WIDGET-SPEC.md 的规范，用 C# 编写一个小组件：
> 1. 类名随意，**必须**继承 `UserControl` 并实现 `DynamicBird.UI.Widgets.IWidget`（Name/CreateView/OnActivated/OnDeactivated）
> 2. 功能：<在这里描述你的需求，如"显示当前时间的时钟"或"每秒更新的倒计时"或"点击打开某网站的按钮">
> 3. 只输出完整可编译的 C# 代码（含所有 using），不要解释
> 4. 需要联网就注明"需要联网权限"

---

## 5. 未来规划

- 小组件包（源码 + 清单）可导出/分享
- 市场托管（GitHub 等）：含代码插件将带**风险标记**与分类（联网/文件/剪贴板）
- 沙箱/权限隔离（若市场开放第三方上传）
- 本文档会持续补充常见功能示例（欢迎贡献）
