using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using DynamicBird.Infrastructure.WinApi;
using NAudio.CoreAudioApi;
using Windows.Devices.Radios;

namespace DynamicBird.UI.Panels
{
    /// <summary>
    /// 左上角“快捷开关”：音量 / 亮度 / 蓝牙 / Wi-Fi / 移动热点 / 系统设置入口。
    /// </summary>
    public partial class QuickSettingsView : UserControl
    {
        private readonly DispatcherTimer _stateTimer;
        private MMDevice? _audioDevice;
        private bool _brightnessChanging;
        private bool _volumeChanging;
        private bool _hotspotSupported;

        public QuickSettingsView()
        {
            InitializeComponent();

            try
            {
                _audioDevice = new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            }
            catch { _audioDevice = null; }

            _stateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _stateTimer.Tick += async (_, _) => await RefreshStatesAsync();

            Loaded += async (_, _) =>
            {
                await InitBrightnessAsync();
                await RefreshStatesAsync();
                _stateTimer.Start();
            };
            Unloaded += (_, _) => _stateTimer.Stop();

            Loaded += (_, _) => RefreshVolume();
        }

        // ================= 音量 =================

        private void RefreshVolume()
        {
            try
            {
                if (_audioDevice == null) return;
                float vol = _audioDevice.AudioEndpointVolume.MasterVolumeLevelScalar;
                _volumeChanging = true;
                VolumeSlider.Value = vol;
                VolumeText.Text = $"{vol * 100:F0}%";
                BtnMute.Content = _audioDevice.AudioEndpointVolume.Mute ? "🔇" : "🔊";
                _volumeChanging = false;
            }
            catch { }
        }

        private void Volume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_volumeChanging || _audioDevice == null) return;
            try
            {
                _audioDevice.AudioEndpointVolume.MasterVolumeLevelScalar = (float)VolumeSlider.Value;
                VolumeText.Text = $"{VolumeSlider.Value * 100:F0}%";
            }
            catch { }
        }

        private void Mute_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_audioDevice == null) return;
                _audioDevice.AudioEndpointVolume.Mute = !_audioDevice.AudioEndpointVolume.Mute;
                BtnMute.Content = _audioDevice.AudioEndpointVolume.Mute ? "🔇" : "🔊";
            }
            catch { }
        }

        // ================= 亮度 =================

        private async Task InitBrightnessAsync()
        {
            // WMI 查询可能耗时数百毫秒，放到后台线程，避免阻塞面板滑入动画
            var state = await Task.Run(() =>
            {
                return DisplayBrightness.TryGetState(out int min, out int current, out int max)
                    ? (Ok: true, min, current, max)
                    : (Ok: false, min: 0, current: 0, max: 0);
            });

            if (!state.Ok)
            {
                BrightnessRow.Visibility = Visibility.Collapsed;
                return;
            }

            BrightnessRow.Visibility = Visibility.Visible;
            BrightnessSlider.Minimum = state.min;
            BrightnessSlider.Maximum = state.max;
            _brightnessChanging = true;
            BrightnessSlider.Value = state.current;
            BrightnessText.Text = $"{state.current}";
            _brightnessChanging = false;
        }

        private void Brightness_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_brightnessChanging) return;
            DisplayBrightness.Set((int)BrightnessSlider.Value);
            BrightnessText.Text = $"{BrightnessSlider.Value:F0}";
        }

        // ================= 无线与热点 =================

        private async Task RefreshStatesAsync()
        {
            // 蓝牙
            var bt = await SystemRadios.GetStateAsync(RadioKind.Bluetooth);
            if (bt.HasValue)
            {
                bool on = bt.Value == RadioState.On;
                BluetoothStateText.Text = on ? "已开启" : "已关闭";
                BtnBluetooth.Content = on ? "开" : "关";
                BtnBluetooth.Style = (Style)FindResource(on ? "AccentButton" : "FlatButton");
            }
            else
            {
                BluetoothStateText.Text = "不可用";
                BtnBluetooth.Content = "—";
                BtnBluetooth.IsEnabled = false;
            }

            // Wi-Fi
            var wifi = await SystemRadios.GetStateAsync(RadioKind.WiFi);
            if (wifi.HasValue)
            {
                bool on = wifi.Value == RadioState.On;
                WifiStateText.Text = on ? "已开启" : "已关闭";
                BtnWifi.Content = on ? "开" : "关";
                BtnWifi.Style = (Style)FindResource(on ? "AccentButton" : "FlatButton");
            }
            else
            {
                WifiStateText.Text = "不可用";
                BtnWifi.Content = "—";
                BtnWifi.IsEnabled = false;
            }

            // 热点
            var hotspot = await HotspotControl.GetStateAsync();
            _hotspotSupported = hotspot.Supported;
            if (hotspot.Supported)
            {
                HotspotRow.Visibility = Visibility.Visible;
                HotspotStateText.Text = hotspot.Enabled ? "已开启" : "已关闭";
                BtnHotspot.Content = hotspot.Enabled ? "开" : "关";
                BtnHotspot.Style = (Style)FindResource(hotspot.Enabled ? "AccentButton" : "FlatButton");
                BtnHotspot.IsEnabled = true;
            }
            else
            {
                HotspotRow.Visibility = Visibility.Collapsed;
            }
        }

        private async void Bluetooth_Click(object sender, RoutedEventArgs e)
        {
            bool current = await SystemRadios.GetStateAsync(RadioKind.Bluetooth) == RadioState.On;
            await SystemRadios.SetStateAsync(RadioKind.Bluetooth, !current);
            await RefreshStatesAsync();
        }

        private async void Wifi_Click(object sender, RoutedEventArgs e)
        {
            bool current = await SystemRadios.GetStateAsync(RadioKind.WiFi) == RadioState.On;
            await SystemRadios.SetStateAsync(RadioKind.WiFi, !current);
            await RefreshStatesAsync();
        }

        private async void Hotspot_Click(object sender, RoutedEventArgs e)
        {
            var state = await HotspotControl.GetStateAsync();
            await HotspotControl.SetAsync(!state.Enabled);
            await RefreshStatesAsync();
        }

        // ================= 系统入口 =================

        private void BatterySaver_Click(object sender, RoutedEventArgs e)
        {
            SystemLauncher.OpenBatterySaverSettings();
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            SystemLauncher.OpenWindowsSettings();
        }
    }
}
