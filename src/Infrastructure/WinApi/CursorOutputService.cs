using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using System.Windows.Threading;

namespace DynamicBird.Infrastructure.WinApi
{
    /// <summary>
    /// 把文本输出到用户指定的光标位置（如直接写进 Word / 记事本）：
    ///  1. 锁定目标：记住当前前台窗口与其中的文本光标位置（UIA TextPattern）
    ///  2. 输出时重新激活目标窗口、把光标恢复到锁定位置，再粘贴文本
    ///  3. 中途焦点切换不影响输出位置；Unlock 前一直输出到原位置
    /// </summary>
    public sealed class CursorOutputService : IDisposable
    {
        private IntPtr _targetHwnd;
        private AutomationElement? _targetElement;
        private TextPatternRange? _lockedRange;
        private bool _locked;
        private bool _firstOutput = true;

        public bool IsLocked => _locked;
        public IntPtr TargetHwnd => _targetHwnd;

        /// <summary>
        /// 锁定当前光标位置所在窗口与插入点（瞄准模式：调用前用户已点击目标窗口，前台即目标）。
        /// </summary>
        public bool TryLockTarget(out string error)
        {
            error = "";
            try
            {
                IntPtr hwnd = GetForegroundWindow();
                Log($"TryLockTarget fg={hwnd} own={IsOwnWindow(hwnd)}");
                if (hwnd == IntPtr.Zero || IsOwnWindow(hwnd))
                {
                    error = "请先点击要输出的目标窗口（如 Word / 记事本）";
                    return false;
                }

                // 尝试用 UIA 找到可编辑元素并记录光标位置（Word / 旧记事本等支持）；
                // 找不到（如 Win11 新记事本）时仍锁定窗口，输出退化为“粘贴到当前光标”。
                _targetElement = null;
                _lockedRange = null;
                try
                {
                    var root = AutomationElement.FromHandle(hwnd);
                    if (root != null)
                    {
                        var tpCond = new PropertyCondition(AutomationElement.IsTextPatternAvailableProperty, true);
                        var controlCond = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit);
                        var docCond = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document);
                        var editEl = root.FindFirst(TreeScope.Descendants, tpCond);
                        if (editEl == null)
                        {
                            var edit = root.FindFirst(TreeScope.Descendants, controlCond);
                            editEl = edit ?? root.FindFirst(TreeScope.Descendants, docCond);
                        }
                        if (editEl != null)
                        {
                            var tp = (TextPattern)editEl.GetCurrentPattern(TextPattern.Pattern);
                            var selection = tp.GetSelection();
                            if (selection != null && selection.Length > 0)
                            {
                                _lockedRange = selection[0];
                            }
                            _targetElement = editEl;
                        }
                    }
                }
                catch { }

                _targetHwnd = hwnd;
                _locked = true;
                Log($"locked hwnd={hwnd} hasRange={_lockedRange != null}");
                return true;
            }
            catch (Exception ex)
            {
                error = "锁定失败：" + ex.Message;
                Log("lock fail: " + ex.Message);
                return false;
            }
        }

        private static void Log(string msg)
        {
            try
            {
                DynamicBird.Core.Infrastructure.Logging.LogManager.Debug("[CursorOutput] " + msg);
            }
            catch { }
        }

        /// <summary>把文本输出到锁定的光标位置（重新激活窗口 + 恢复光标 + 粘贴）。</summary>
        public void OutputText(string text)
        {
            Log($"OutputText called locked={_locked} textLen={text?.Length} hwnd={_targetHwnd}");
            if (!_locked || string.IsNullOrEmpty(text) || _targetHwnd == IntPtr.Zero) return;
            try
            {
                ActivateWindow(_targetHwnd);
                Log("activated window");
                System.Threading.Thread.Sleep(80); // 等待窗口激活

                // 只第一次输出时恢复到锁定位置；之后沿用当前光标（粘贴后光标自然在末尾，
                // 保证流式连续输出顺序正确；用户中途手动移动光标则尊重新位置）
                if (_firstOutput)
                {
                    _firstOutput = false;
                    RestoreCaret();
                }

                // 剪贴板粘贴
                try
                {
                    System.Windows.Clipboard.SetText(text);
                    Log("clipboard set");
                }
                catch (Exception cex)
                {
                    Log("clipboard fail: " + cex.Message);
                    return;
                }
                System.Threading.Thread.Sleep(40);
                SendCtrlV();
                Log("ctrl+v sent");


            }
            catch (Exception ex)
            {
                Log("output error: " + ex.Message);
            }
        }

        /// <summary>把光标恢复到锁定位置（尽力而为，失败则粘贴到当前光标）。</summary>
        private void RestoreCaret()
        {
            try
            {
                if (_targetElement != null)
                {
                    if (_lockedRange != null)
                    {
                        var tp = (TextPattern)_targetElement.GetCurrentPattern(TextPattern.Pattern);
                        var current = tp.GetSelection();
                        if (current != null && current.Length > 0) current[0].Select(); // 先清理选择
                        _lockedRange.Select();
                    }
                    else
                    {
                        _targetElement.SetFocus();
                    }
                }
            }
            catch { }
        }

        public void Unlock()
        {
            _locked = false;
            _firstOutput = true;
            _targetHwnd = IntPtr.Zero;
            _targetElement = null;
            _lockedRange = null;
        }

        public void Dispose() => Unlock();

        // ============ 窗口查找 ============

        private static bool IsOwnWindow(IntPtr hwnd)
        {
            GetWindowThreadProcessId(hwnd, out uint pid);
            return pid == Environment.ProcessId;
        }

        // ============ Win32 ============

        private static void ActivateWindow(IntPtr hwnd)
        {
            try
            {
                uint thisThread = GetCurrentThreadId();
                uint fgThread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
                uint targetThread = GetWindowThreadProcessId(hwnd, out _);
                if (fgThread != thisThread) AttachThreadInput(fgThread, thisThread, true);
                if (targetThread != thisThread) AttachThreadInput(targetThread, thisThread, true);
                ShowWindow(hwnd, 9); // SW_RESTORE
                SetForegroundWindow(hwnd);
                SetFocus(hwnd);
                if (fgThread != thisThread) AttachThreadInput(fgThread, thisThread, false);
                if (targetThread != thisThread) AttachThreadInput(targetThread, thisThread, false);
            }
            catch { }
        }

        private static void SendCtrlV()
        {
            keybd_event(0x11, 0, 0, UIntPtr.Zero); // Ctrl down
            keybd_event(0x56, 0, 0, UIntPtr.Zero); // V down
            keybd_event(0x56, 0, 2, UIntPtr.Zero); // V up
            keybd_event(0x11, 0, 2, UIntPtr.Zero); // Ctrl up
        }

        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern IntPtr SetFocus(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        [DllImport("user32.dll")] private static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
    }
}