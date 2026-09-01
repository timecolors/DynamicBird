using System;
using System.Runtime.InteropServices;

namespace ShoreHue.Infrastructure.WinApi
{
    /// <summary>
    /// 系统媒体键（对前台应用生效，作为 SMTC 会话不可用时的兜底方案）。
    /// </summary>
    public static class MediaKeyHelper
    {
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const uint KEYEVENTF_KEYUP = 0x0002;

        public const byte PrevTrack = 0xB1;
        public const byte NextTrack = 0xB0;
        public const byte PlayPause = 0xB3;
        public const byte VolumeUp = 0xAF;
        public const byte VolumeDown = 0xAE;
        public const byte VolumeMute = 0xAD;

        public static void Press(byte virtualKey)
        {
            try
            {
                keybd_event(virtualKey, 0, 0, UIntPtr.Zero);
                keybd_event(virtualKey, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            catch { }
        }
    }
}
