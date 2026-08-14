using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace DynamicBird.UI.Main
{
    public partial class MainWindow
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, ref RECT rect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref RECT pvParam, uint fWinIni);

        [DllImport("shell32.dll")]
        private static extern IntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
        private const int DWMSBT_MAINWINDOW = 2;
        private const int WM_HOTKEY = 0x0312;
        private const int HotkeyId = 0x5A11;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_ALT = 0x0001;
        private const uint VK_B = 0x42; // B

        [DllImport("user32.dll")]
        private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);

        [DllImport("user32.dll")]
        private static extern bool SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int width, int height);

        [DllImport("gdi32.dll")]
        private static extern int CombineRgn(IntPtr dest, IntPtr src1, IntPtr src2, int mode);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        private const int RGN_AND = 1;

        private const uint SPI_GETWORKAREA = 0x0030;
        private const uint ABM_GETTASKBARPOS = 0x00000005;
        private const uint ABE_BOTTOM = 3;

        private double _lastTaskbarBoundary = -1;
        private string _lastRegionSignature = "";

        // 面板贴屏幕底边时，最底部 3 物理像素让给自动隐藏任务栏的呼出条
        private const int BottomStripClickThroughPx = 3;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct APPBARDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uCallbackMessage;
            public uint uEdge;
            public RECT rc;
            public int lParam;
        }

        private double GetDpiScale()
        {
            try
            {
                return VisualTreeHelper.GetDpi(this).DpiScaleX;
            }
            catch { }
            try
            {
                double scale = PresentationSource.FromVisual(this)?.CompositionTarget.TransformToDevice.M11 ?? 0;
                if (scale > 0) return scale;
            }
            catch { }
            return 1.0;
        }

        /// <summary>
        /// 尝试启用 Win11 Mica 背景（22H2+）。成功返回 true；Win10/失败返回 false。
        /// </summary>
        private bool TryApplyMicaBackdrop()
        {
            try
            {
                if (Environment.OSVersion.Version.Build < 22621) return false;
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return false;

                int value = DWMSBT_MAINWINDOW;
                return DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref value, sizeof(int)) == 0;
            }
            catch { return false; }
        }

        /// <summary>注册全局热键 Ctrl+Alt+B：切换面板显示/隐藏。</summary>
        private void RegisterGlobalHotkey(IntPtr hwnd)
        {
            try
            {
                RegisterHotKey(hwnd, HotkeyId, MOD_CONTROL | MOD_ALT, VK_B);
            }
            catch { }
        }

        private void UnregisterGlobalHotkey(IntPtr hwnd)
        {
            try
            {
                if (hwnd != IntPtr.Zero) UnregisterHotKey(hwnd, HotkeyId);
            }
            catch { }
        }

        private IntPtr HotkeyWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
            {
                HotkeyTogglePanel();
                handled = true;
            }
            return IntPtr.Zero;
        }

        private double GetTaskbarHeight()
        {
            try
            {
                IntPtr taskbarHandle = FindWindow("Shell_TrayWnd", null);
                if (taskbarHandle != IntPtr.Zero)
                {
                    RECT rect = new RECT();
                    GetWindowRect(taskbarHandle, ref rect);
                    return rect.Bottom - rect.Top;
                }
            }
            catch { }
            return 40;
        }

        /// <summary>
        /// ★★★ 获取任务栏顶部坐标（DIP 单位） ★★★
        /// 通过 SHAppBarMessage(ABM_GETTASKBARPOS) 实时查询任务栏矩形：
        ///  - 任务栏显示时 → 返回任务栏上边缘（面板贴任务栏顶）
        ///  - 自动隐藏且未呼出时 → 任务栏矩形在屏幕外 → 返回屏幕底边（面板贴屏幕底）
        /// 相比 SPI_GETWORKAREA 更精确，且不依赖 WPF 缓存的工作区。
        /// </summary>
        private double GetTaskbarTopInDips()
        {
            try
            {
                var abd = new APPBARDATA { cbSize = (uint)Marshal.SizeOf<APPBARDATA>() };
                if (SHAppBarMessage(ABM_GETTASKBARPOS, ref abd) != IntPtr.Zero &&
                    abd.uEdge == ABE_BOTTOM && abd.rc.Bottom > abd.rc.Top)
                {
                    double dpiScale = GetDpiScale();
                    if (dpiScale <= 0 || double.IsNaN(dpiScale) || double.IsInfinity(dpiScale))
                    {
                        dpiScale = 1.0;
                    }

                    double topDips = abd.rc.Top / dpiScale;
                    double screenHeightDips = SystemParameters.PrimaryScreenHeight;

                    // 自动隐藏且未呼出：任务栏顶边已超出/等于屏幕底边 → 面板贴屏幕底
                    if (topDips >= screenHeightDips - 1)
                    {
                        return screenHeightDips;
                    }
                    if (topDips > 0)
                    {
                        return topDips;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取任务栏位置失败: {ex.Message}");
            }

            // ★ 备用：Win32 实时工作区（任务栏隐藏时等于屏幕底边，升起时等于任务栏顶边）
            try
            {
                RECT workArea = new RECT();
                if (SystemParametersInfo(SPI_GETWORKAREA, 0, ref workArea, 0))
                {
                    double dpiScale = GetDpiScale();
                    return workArea.Bottom / dpiScale;
                }
            }
            catch { }

            return SystemParameters.WorkArea.Bottom;
        }

        /// <summary>
        /// ★★★ 动态刷新底部边界并让面板跟随 ★★★
        /// 每 ~150ms 由边缘定时器调用：任务栏隐藏/升起时更新边界，
        /// 若边界变化且面板正贴着底边，立即重新锚定，避免面板停留在旧位置。
        /// </summary>
        private void RefreshTaskbarBoundary()
        {
            try
            {
                double screenH = SystemParameters.PrimaryScreenHeight;
                double boundary = GetTaskbarTopInDips();

                // 容错：边界必须在屏幕范围内
                if (boundary <= 0 || double.IsNaN(boundary) || double.IsInfinity(boundary))
                {
                    boundary = screenH;
                }
                boundary = Math.Max(0, Math.Min(boundary, screenH));

                bool boundaryChanged = Math.Abs(boundary - _lastTaskbarBoundary) > 0.5;
                if (boundaryChanged)
                {
                    _lastTaskbarBoundary = boundary;
                }

                _edgeController?.UpdateBottomBoundary(boundary);
                _sizeController?.UpdateBottomBoundary(boundary);

                // 边界变化且面板可见贴底 → 立即重锚，跟随任务栏
                if (boundaryChanged)
                {
                    _edgeController?.ReanchorBottomPanel();
                }

                // 面板贴屏幕底边时挖掉底部呼出条，保证自动隐藏任务栏能正常呼出；
                // ★ 拖拽调整大小时跳过，避免区域反复切换导致抖动
                if (_edgeController != null && !_edgeController.IsDragging)
                {
                    ApplyBottomStripClickThrough();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"刷新任务栏边界失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 面板底部贴住屏幕底边时，将最底部 3 物理像素设为点击穿透，
        /// 让 Windows 自动隐藏任务栏的呼出条可以接收鼠标；任务栏升起后恢复。
        /// </summary>
        private void ApplyBottomStripClickThrough()
        {
            bool atBottom = Math.Abs((Top + Height) - SystemParameters.PrimaryScreenHeight) < 1.0;
            ApplyWindowRegion(atBottom && Height > BottomStripClickThroughPx + 2);
        }

        /// <summary>
        /// 窗口区域：圆角 + 底部点击穿透条。
        /// 非透明窗口（AllowsTransparency=false）下 WPF 走硬件渲染，
        /// 用窗口区域实现圆角，避免透明窗口强制软件渲染导致的视频/镜像卡顿。
        /// </summary>
        private void ApplyWindowRegion(bool carveBottom)
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;

                double scale = GetDpiScale();
                if (scale <= 0 || double.IsNaN(scale) || double.IsInfinity(scale)) scale = 1.0;
                int w = Math.Max(1, (int)Math.Round(ActualWidth * scale));
                int h = Math.Max(1, (int)Math.Round(ActualHeight * scale));
                int radius = Math.Max(2, (int)(16 * scale * 2)); // 16 DIP 圆角 → 椭圆直径

                string sig = $"{w}x{h}|{carveBottom}";
                if (sig == _lastRegionSignature) return;

                IntPtr roundRgn = CreateRoundRectRgn(0, 0, w + 1, h + 1, radius, radius);
                if (roundRgn == IntPtr.Zero) return;

                if (carveBottom && h > BottomStripClickThroughPx + 2)
                {
                    IntPtr bottomRgn = CreateRectRgn(0, 0, w, h - BottomStripClickThroughPx);
                    IntPtr combined = CreateRectRgn(0, 0, 0, 0);
                    if (bottomRgn != IntPtr.Zero && combined != IntPtr.Zero)
                    {
                        CombineRgn(combined, roundRgn, bottomRgn, RGN_AND);
                        DeleteObject(roundRgn);
                        DeleteObject(bottomRgn);
                        SetWindowRgn(hwnd, combined, true);
                        _lastRegionSignature = sig;
                        return;
                    }
                    if (bottomRgn != IntPtr.Zero) DeleteObject(bottomRgn);
                    if (combined != IntPtr.Zero) DeleteObject(combined);
                }

                SetWindowRgn(hwnd, roundRgn, true);
                _lastRegionSignature = sig;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"设置窗口区域失败: {ex.Message}");
            }
        }
    }
}
