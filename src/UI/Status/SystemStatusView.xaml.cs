using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ShoreHue.Core.Services.Configuration;
using ShoreHue.UI.Localization;
using ShoreHue.Infrastructure.WinApi;
using ShoreHue.UI.Widgets.Dynamic;
using NAudio.CoreAudioApi;

namespace ShoreHue.UI.Status
{
    public partial class SystemStatusView : UserControl
    {
        private DispatcherTimer _timer;
        private DispatcherTimer? _weatherTimer;
        private PerformanceCounter? _cpuCounter;
        private PerformanceCounter? _memoryCounter;
        private int _frameCount = 0;
        private DateTime _fpsStartTime = DateTime.Now;
        private int _currentFps = 0;

        private MMDevice? _audioDevice;
        private ISettingsService? _settings;
        private bool _weatherEnabled;
        private string _weatherCity = "";
        private DateTime _weatherLastClick = DateTime.MinValue;   // 双击判定：与系统双击时间比较，不依赖 ClickCount
        private DispatcherTimer? _weatherClickTimer;              // 单击延迟刷新（给双击留判定窗口）

        // ===== 自定义状态栏显示项（IStatusProvider 动态挂载） =====
        private sealed class CustomStatusItem
        {
            public string Key = "";
            public IStatusProvider Provider = null!;
            public TextBlock Text = null!;
            public StackPanel Panel = null!;
        }

        private readonly List<CustomStatusItem> _customItems = new();
        private readonly System.Action _pluginChangedHandler;   // 保存引用以便 Unloaded 解绑（防视图重建累积订阅）

        public SystemStatusView()
        {
            InitializeComponent();

            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            }
            catch { }

            try
            {
                _memoryCounter = new PerformanceCounter("Memory", "Available MBytes");
            }
            catch { }

            InitAudioDevice();

            CompositionTarget.Rendering += OnRendering;

            UpdateStatus();

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += (s, e) => UpdateStatus();
            _timer.Start();

            // ★ 海床文件夹增删（用户放/删状态栏插件）：自动重新挂载自定义项
            _pluginChangedHandler = () =>
            {
                if (_settings == null || !IsLoaded) return;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try { ApplySettings(_settings); } catch { }
                }), System.Windows.Threading.DispatcherPriority.Background);
            };
            WidgetPluginStore.Changed += _pluginChangedHandler;

            Unloaded += (s, e) =>
            {
                _timer?.Stop();
                _weatherTimer?.Stop();
                CompositionTarget.Rendering -= OnRendering;
                _audioDevice?.Dispose();
                DeactivateCustomItems();
                WidgetPluginStore.Changed -= _pluginChangedHandler;
            };
        }

        /// <summary>应用设置：控制各状态项显隐与天气开关。</summary>
        public void ApplySettings(ISettingsService settings)
        {
            _settings = settings;
            // ★ 天气启用 = "状态栏显示天气" 勾选（WeatherEnabled 字段无独立入口，不再作为门槛）
            _weatherEnabled = settings.StatusShowWeather;
            _weatherCity = settings.WeatherCity ?? "";

            SetVisible(TimePanel, settings.StatusShowTime);
            SetVisible(CpuPanel, settings.StatusShowCpu);
            SetVisible(MemoryPanel, settings.StatusShowMemory);
            SetVisible(FpsPanel, settings.StatusShowFps);
            SetVisible(VolumePanel, settings.StatusShowVolume);
            SetVisible(NetworkPanel, settings.StatusShowNetwork);
            SetVisible(BatteryPanel, settings.StatusShowBattery);
            SetVisible(WeatherPanel, settings.StatusShowWeather && _weatherEnabled);

            // ★ 自定义状态栏显示项：先卸载旧项再按当前启用状态重新挂载（内置项之后）
            RebuildCustomItems(settings);

            if (_weatherEnabled && settings.StatusShowWeather)
            {
                WeatherText.Text = LocalizationManager.Instance["Status_WeatherLoading"];
                _ = RefreshWeatherAsync();
                _weatherTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMinutes(15) };
                // ★ 修复：每次 ApplySettings 都 += 会累积多个 Tick 订阅（重复刷新天气）；
                //   先 -= 再 += 保证只挂一个
                _weatherTimer.Tick -= OnWeatherTimerTick;
                _weatherTimer.Tick += OnWeatherTimerTick;
                _weatherTimer.Start();
            }
            else
            {
                _weatherTimer?.Stop();
            }
        }

        /// <summary>重新挂载自定义状态栏显示项：卸载旧项 → 编译缓存中取启用项 → Children.Add 到容器尾。</summary>
        private void RebuildCustomItems(ISettingsService settings)
        {
            DeactivateCustomItems();

            foreach (var kvp in WidgetPluginStore.StatusProviders)
            {
                try
                {
                    var provider = kvp.Value;
                    // ★ 启用判定：设置开关（StatusProviderEnabled，缺省启用）+ 插件自决 IsEnabled
                    if (!settings.IsStatusProviderEnabled(kvp.Key) || !provider.IsEnabled(settings)) continue;

                    var icon = new TextBlock
                    {
                        Text = provider.IconText ?? "",
                        FontSize = 13,
                        Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    var text = new TextBlock
                    {
                        Text = "",
                        FontSize = 13,
                        Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xAA)),
                        Margin = new Thickness(4, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    var panel = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Margin = new Thickness(0, 0, 15, 0)
                    };
                    panel.Children.Add(icon);
                    panel.Children.Add(text);
                    StatusContainer.Children.Add(panel);

                    provider.OnActivated();
                    _customItems.Add(new CustomStatusItem
                    {
                        Key = kvp.Key,
                        Provider = provider,
                        Text = text,
                        Panel = panel
                    });
                }
                catch { /* 单个插件异常不影响其他项 */ }
            }

            UpdateCustomItems();
        }

        /// <summary>每秒刷新自定义状态栏项的文本（provider.GetText()）。</summary>
        private void UpdateCustomItems()
        {
            foreach (var item in _customItems)
            {
                try { item.Text.Text = item.Provider.GetText() ?? ""; }
                catch { }
            }
        }

        /// <summary>卸载全部自定义项：OnDeactivated + 从容器移除。</summary>
        private void DeactivateCustomItems()
        {
            foreach (var item in _customItems)
            {
                try { item.Provider.OnDeactivated(); } catch { }
                StatusContainer.Children.Remove(item.Panel);
            }
            _customItems.Clear();
        }

        private static void SetVisible(FrameworkElement el, bool visible)
        {
            el.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }

        private async System.Threading.Tasks.Task RefreshWeatherAsync()
        {
            var w = await WeatherService.GetWeatherWithCityAsync(_weatherCity);

            // 确保回到 UI 线程更新（await 可能在无 SynchronizationContext 时落到线程池）
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (w.HasValue)
                {
                    // 显示生效城市名 + 天气，如 保定 · ☀25° 晴；IP 定位无城市名时只显示天气
                    WeatherText.Text = string.IsNullOrEmpty(w.Value.City)
                        ? w.Value.Text
                        : w.Value.City + " · " + w.Value.Text;
                }
                else
                {
                    WeatherText.Text = LocalizationManager.Instance["Status_WeatherUnavailable"];
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        /// <summary>天气交互：单击 → 刷新；双击（系统双击时间内两次点击）→ 默认浏览器搜索"城市 + 天气"。
        /// ★ 不用 WPF ClickCount（曾出现双击判不出/单击被双击第一击打断），用时间差自判：
        ///   单击只延迟执行刷新（给双击留判定窗口），双击到达即取消刷新、直接开浏览器。</summary>
        private async void WeatherPanel_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                var now = DateTime.Now;
                bool isDouble = (now - _weatherLastClick).TotalMilliseconds
                    <= System.Windows.Forms.SystemInformation.DoubleClickTime;
                _weatherLastClick = now;

                if (isDouble)
                {
                    // 双击：取消待执行的单击刷新 → 开浏览器搜索天气
                    if (_weatherClickTimer != null) { _weatherClickTimer.Stop(); _weatherClickTimer = null; }
                    _weatherLastClick = DateTime.MinValue;   // 防三连击再次触发
                    await WeatherService.OpenForecastPageAsync(_weatherCity);
                    return;
                }

                // 单击：延迟 300ms 刷新（若紧随其后有第二击 → isDouble 分支接管并取消）
                if (_weatherClickTimer == null)
                {
                    _weatherClickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
                    _weatherClickTimer.Tick += async (_, _) =>
                    {
                        _weatherClickTimer?.Stop();
                        _weatherClickTimer = null;
                        try
                        {
                            WeatherText.Text = LocalizationManager.Instance["Status_WeatherLoading"];
                            await RefreshWeatherAsync();
                        }
                        catch { }
                    };
                }
                _weatherClickTimer.Stop();
                _weatherClickTimer.Start();
            }
            catch { }
        }

        private void InitAudioDevice()
        {
            try
            {
                var enumerator = new MMDeviceEnumerator();
                _audioDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            }
            catch
            {
                _audioDevice = null;
            }
        }

        private void OnRendering(object? sender, EventArgs e)
        {
            _frameCount++;
            var now = DateTime.Now;
            if ((now - _fpsStartTime).TotalSeconds >= 1)
            {
                _currentFps = _frameCount;
                _frameCount = 0;
                _fpsStartTime = now;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    FpsText.Text = $"{_currentFps}fps";
                });
            }
        }

        /// <summary>天气定时器回调（15 分钟刷新一次；-= / += 防重复订阅）。</summary>
        private void OnWeatherTimerTick(object? sender, EventArgs e) => _ = RefreshWeatherAsync();

        private void UpdateStatus()
        {
            UpdateTime();
            UpdateCpu();
            UpdateMemory();
            UpdateVolume();
            UpdateNetwork();
            UpdateBattery();
            UpdateCustomItems();
        }

        private void UpdateTime()
        {
            TimeText.Text = DateTime.Now.ToString("HH:mm:ss");
        }

        private void UpdateCpu()
        {
            try
            {
                if (_cpuCounter != null)
                {
                    float val = _cpuCounter.NextValue();
                    if (val > 0 && val < 100)
                        CpuText.Text = $"{val:F0}%";
                }
            }
            catch { }
        }

        private void UpdateMemory()
        {
            try
            {
                if (_memoryCounter != null)
                {
                    float availableMB = _memoryCounter.NextValue();
                    float totalMB = GetTotalMemoryMB();
                    if (totalMB > 0)
                    {
                        float usedPercent = (1 - availableMB / totalMB) * 100;
                        MemoryText.Text = $"{usedPercent:F0}%";
                    }
                }
            }
            catch { }
        }

        private float GetTotalMemoryMB()
        {
            try
            {
                var gcMemoryInfo = GC.GetGCMemoryInfo();
                return gcMemoryInfo.TotalAvailableMemoryBytes / 1024f / 1024f;
            }
            catch { return 0; }
        }

        private void UpdateVolume()
        {
            try
            {
                if (_audioDevice != null)
                {
                    float volume = _audioDevice.AudioEndpointVolume.MasterVolumeLevelScalar;
                    int vol = (int)(volume * 100);
                    VolumeText.Text = $"{vol}%";
                    VolumeIcon.SetResourceReference(
                        System.Windows.Shapes.Path.StrokeProperty,
                        vol <= 0 ? "DangerBrush" : "TextSecondaryBrush");
                }
            }
            catch { }
        }

        private void VolumePanel_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            HandleVolumeWheel(e.Delta);
        }

        public void HandleVolumeWheel(int delta)
        {
            try
            {
                if (_audioDevice == null) return;

                int step = 2;
                int deltaValue = delta > 0 ? step : -step;

                float current = _audioDevice.AudioEndpointVolume.MasterVolumeLevelScalar;
                int newVol = (int)(current * 100) + deltaValue;
                newVol = Math.Clamp(newVol, 0, 100);

                _audioDevice.AudioEndpointVolume.MasterVolumeLevelScalar = newVol / 100f;

                UpdateVolume();
            }
            catch { }
        }

        private void UpdateNetwork()
        {
            try
            {
                if (!NetworkInterface.GetIsNetworkAvailable())
                {
                    NetworkText.Text = LocalizationManager.Instance["Status_NetDisconnected"];
                    NetworkIcon.SetResourceReference(
                        System.Windows.Shapes.Path.StrokeProperty, "DangerBrush");
                    return;
                }
                NetworkText.Text = LocalizationManager.Instance["UI_SystemStatusView_401"];
                NetworkIcon.SetResourceReference(
                    System.Windows.Shapes.Path.StrokeProperty, "TextSecondaryBrush");
            }
            catch
            {
                NetworkText.Text = LocalizationManager.Instance["Status_NetUnknown"];
                NetworkIcon.SetResourceReference(
                    System.Windows.Shapes.Path.StrokeProperty, "TextSecondaryBrush");
            }
        }

        private void UpdateBattery()
        {
            try
            {
                var powerStatus = System.Windows.Forms.SystemInformation.PowerStatus;
                if (powerStatus.BatteryChargeStatus == System.Windows.Forms.BatteryChargeStatus.NoSystemBattery)
                {
                    BatteryText.Text = LocalizationManager.Instance["Status_NoBattery"];
                    BatteryIcon.SetResourceReference(
                        System.Windows.Shapes.Path.StrokeProperty, "TextSecondaryBrush");
                    return;
                }
                int percent = (int)(powerStatus.BatteryLifePercent * 100);
                BatteryText.Text = $"{percent}%";
                bool charging = powerStatus.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Online;
                BatteryIcon.SetResourceReference(
                    System.Windows.Shapes.Path.StrokeProperty,
                    charging ? "AccentBrush" : "TextSecondaryBrush");
            }
            catch
            {
                BatteryText.Text = "--";
                BatteryIcon.SetResourceReference(
                    System.Windows.Shapes.Path.StrokeProperty, "TextSecondaryBrush");
            }
        }
    }
}