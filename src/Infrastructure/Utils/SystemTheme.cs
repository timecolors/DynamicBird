using Microsoft.Win32;

namespace DynamicBird.Infrastructure.Utils
{
    /// <summary>系统浅/深色主题读取（Windows 10/11 应用模式）。</summary>
    public static class SystemTheme
    {
        /// <summary>当前是否为浅色应用模式（HKCU 应用主题设置；读取失败按浅色）。</summary>
        public static bool IsLightTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                return key?.GetValue("AppsUseLightTheme") is int v && v == 1;
            }
            catch { return true; }
        }
    }
}
