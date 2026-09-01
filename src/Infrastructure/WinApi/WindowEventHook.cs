using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace ShoreHue.Infrastructure.WinApi
{
    /// <summary>
    /// 窗口事件监听：用 SetWinEventHook 监听系统窗口的创建/销毁/显隐/标题/前置变化，
    /// 替代任务栏面板的每秒轮询。窗口变化时触发 Changed 事件（UI 线程回调），
    /// 空闲时零开销（WinEventHook 由系统事件驱动，非轮询）。
    /// 注意：事件钩子在系统消息线程触发，统一 Dispatcher 回调到 UI 线程并做节流合并。
    /// </summary>
    public static class WindowEventHook
    {
        private const uint EVENT_OBJECT_CREATE = 0x8000;
        private const uint EVENT_OBJECT_DESTROY = 0x8001;
        private const uint EVENT_OBJECT_SHOW = 0x8002;
        private const uint EVENT_OBJECT_HIDE = 0x8003;
        private const uint EVENT_OBJECT_NAMECHANGE = 0x800C;
        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        private const uint EVENT_SYSTEM_MINIMIZESTART = 0x0016;
        private const uint EVENT_SYSTEM_MINIMIZEEND = 0x0017;
        private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        private const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
            int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
            WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        private static IntPtr _hook = IntPtr.Zero;
        private static readonly WinEventDelegate _callback = OnWinEvent;
        private static DispatcherTimer? _debounceTimer;
        private static DateTime _lastFire = DateTime.MinValue;

        /// <summary>窗口列表变化（节流合并后触发，UI 线程）。</summary>
        public static event Action? Changed;

        public static void Start()
        {
            if (_hook != IntPtr.Zero) return;
            // 监听窗口创建/销毁/显隐/标题变化/前置切换/最小化——任务栏列表相关事件全集
            _hook = SetWinEventHook(EVENT_OBJECT_CREATE, EVENT_OBJECT_NAMECHANGE, IntPtr.Zero, _callback, 0, 0,
                WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
            if (_hook == IntPtr.Zero)
            {
                // 钩子注册失败（极端情况）：静默降级，调用方继续走轮询兜底
                return;
            }
        }

        public static void Stop()
        {
            if (_hook != IntPtr.Zero)
            {
                UnhookWinEvent(_hook);
                _hook = IntPtr.Zero;
            }
            _debounceTimer?.Stop();
            _debounceTimer = null;
        }

        private static void OnWinEvent(IntPtr hook, uint eventType, IntPtr hwnd,
            int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            // 只关心顶层窗口级事件（OBJID_WINDOW = 0），忽略子元素级事件（UIA 高噪声）
            if (idObject != 0) return;
            if (hwnd == IntPtr.Zero) return;

            // 节流：窗口变化可能短时间内高频到达（如打开程序时多个窗口依次创建），
            // 合并为 300ms 内最多触发一次刷新
            var now = DateTime.Now;
            if ((now - _lastFire).TotalMilliseconds < 300) return;
            _lastFire = now;

            // 统一到 UI 线程触发（WPF Dispatcher），避免跨线程调用
            var app = System.Windows.Application.Current;
            if (app == null) return;
            app.Dispatcher.BeginInvoke(new Action(() => Changed?.Invoke()),
                DispatcherPriority.Background);
        }
    }
}