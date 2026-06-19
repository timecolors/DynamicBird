using System;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NAudio.CoreAudioApi;

namespace LingDongBird.Features.SystemStatus
{
    public partial class SystemStatusView : UserControl
    {
        private DispatcherTimer _timer;
        private PerformanceCounter? _cpuCounter;
        private PerformanceCounter? _memoryCounter;
        private int _frameCount = 0;
        private DateTime _fpsStartTime = DateTime.Now;
        private int _currentFps = 0;

        private MMDevice? _audioDevice;

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

            // 初始化音频设备
            InitAudioDevice();

            CompositionTarget.Rendering += OnRendering;

            UpdateStatus();

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += (s, e) => UpdateStatus();
            _timer.Start();

            Unloaded += (s, e) =>
            {
                _timer?.Stop();
                CompositionTarget.Rendering -= OnRendering;
                _audioDevice?.Dispose();
            };
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

        private void UpdateStatus()
        {
            UpdateTime();
            UpdateCpu();
            UpdateMemory();
            UpdateVolume();
            UpdateNetwork();
            UpdateBattery();
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
                    VolumeIcon.Text = vol > 70 ? "🔊" : vol > 30 ? "🔉" : vol > 0 ? "🔈" : "🔇";
                }
            }
            catch { }
        }

        // ---------- 鼠标滚轮调节音量 ----------
        private void VolumePanel_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            try
            {
                if (_audioDevice == null) return;

                int step = 2;
                int delta = e.Delta > 0 ? step : -step;

                float current = _audioDevice.AudioEndpointVolume.MasterVolumeLevelScalar;
                int newVol = (int)(current * 100) + delta;
                newVol = Math.Clamp(newVol, 0, 100);

                _audioDevice.AudioEndpointVolume.MasterVolumeLevelScalar = newVol / 100f;

                UpdateVolume(); // 立即刷新显示
                e.Handled = true;
            }
            catch { }
        }

        private void UpdateNetwork()
        {
            try
            {
                if (!NetworkInterface.GetIsNetworkAvailable())
                {
                    NetworkText.Text = "未连接";
                    NetworkIcon.Text = "❌";
                    return;
                }
                NetworkText.Text = "已连接";
                NetworkIcon.Text = "📶";
            }
            catch
            {
                NetworkText.Text = "未知";
                NetworkIcon.Text = "❓";
            }
        }

        private void UpdateBattery()
        {
            try
            {
                var powerStatus = System.Windows.Forms.SystemInformation.PowerStatus;
                if (powerStatus.BatteryChargeStatus == System.Windows.Forms.BatteryChargeStatus.NoSystemBattery)
                {
                    BatteryText.Text = "无电池";
                    BatteryIcon.Text = "⚡";
                    return;
                }
                int percent = (int)(powerStatus.BatteryLifePercent * 100);
                BatteryText.Text = $"{percent}%";
                BatteryIcon.Text = powerStatus.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Online ? "🔌" : "🔋";
            }
            catch
            {
                BatteryText.Text = "--";
                BatteryIcon.Text = "❓";
            }
        }
    }
}