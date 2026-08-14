using System;
using System.Diagnostics;

namespace DynamicBird.Infrastructure.WinApi
{
    /// <summary>
    /// 打开 Windows 系统功能入口（手机连接 / 蓝牙设置）。
    /// 投屏与蓝牙协议层不对第三方开放，统一走系统入口最稳妥。
    /// </summary>
    public static class SystemLauncher
    {
        public static void OpenPhoneLink()
        {
            // 优先打开“手机连接”(Phone Link)，失败则退回手机设备设置
            if (!TryStart("ms-phone-link:"))
            {
                TryStart("ms-settings:mobile-devices");
            }
        }

        public static void OpenBluetoothSettings()
        {
            TryStart("ms-settings:bluetooth");
        }

        public static void OpenBatterySaverSettings()
        {
            TryStart("ms-settings:batterysaver");
        }

        public static void OpenWindowsSettings()
        {
            TryStart("ms-settings:");
        }

        private static bool TryStart(string uri)
        {
            try
            {
                Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
