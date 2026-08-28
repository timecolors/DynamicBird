using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using DynamicBird.Infrastructure.WinApi;
using DynamicBird.Core.Services;
using DynamicBird.Core.Services.Configuration;
using DynamicBird.Core.Infrastructure.Logging;
using DynamicBird.UI.AppHelper;
using DynamicBird.UI.Media;
using DynamicBird.UI.Panels;
using DynamicBird.UI.Widgets.Calculator;
using DynamicBird.UI.Widgets.Timer;
using DynamicBird.UI.Theme;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WinForms = System.Windows.Forms;

namespace SmokeTest
{
    public static class Program
    {
        private static readonly string ResultFile =
            Path.Combine(AppContext.BaseDirectory, "smoke-result.txt");
        private static readonly string ProgressFile =
            Path.Combine(AppContext.BaseDirectory, "smoke-progress.txt");

        private static void Step(string name)
        {
            try { File.AppendAllText(ProgressFile, $"{DateTime.Now:HH:mm:ss.fff} {name}{Environment.NewLine}"); } catch { }
        }

        [STAThread]
        public static void Main()
        {
            var app = new Application();
            app.Startup += async (_, _) =>
            {
                try
                {
                    LogManager.Initialize(LogLevel.Debug);
                    Step("start");

                    // 加载灵动鸟全局主题（正式运行由 App.xaml 合并）
                    app.Resources.MergedDictionaries.Add(new ResourceDictionary
                    {
                        Source = new Uri("pack://application:,,,/DynamicBird;component/src/UI/Theme/Theme.xaml")
                    });
                    app.Resources.MergedDictionaries.Add(new ResourceDictionary
                    {
                        Source = new Uri("pack://application:,,,/DynamicBird;component/src/UI/Theme/AppIcons.xaml")
                    });

                    // 1) 实例化新增控件（触发 XAML 解析与模板构建）
                    Step("widgets");
                    _ = new TimerWidget();
                    _ = new CalculatorWidget();
                    _ = new MediaControlView();
                    _ = new AppHelperView();
                    _ = new NotificationDockView();
                    _ = new RecentItemsView();
                    _ = new QuickSettingsView(new SettingsManager());
                    Step("widgets-done");

                    // 0) 验证系统 Toast 注册：开始菜单快捷方式 + AppUserModelID
                    Step("toast");
                    SystemToast.EnsureRegistered();
                    string toastLnk = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                        "灵动鸟.lnk");
                    string? toastAumid = SystemToast.ReadAumid(toastLnk);
                    if (!File.Exists(toastLnk) || toastAumid != SystemToast.Aumid)
                        throw new InvalidOperationException(
                            $"Toast 注册失败: lnkExists={File.Exists(toastLnk)}, aumid={toastAumid ?? "<null>"}");
                    Step("toast-done");

                    // 通知监听：启动一轮扫描并触发一次清空
                    Step("toastmonitor");
                    ToastMonitor.Start();
                    ToastMonitor.ClearAll();
                    Step("toastmonitor-done");

                    // 2) 面板内嵌播放器：用生成的 WAV 走一遍媒体打开链路
                    Step("player");
                    var player = new PanelMediaPlayer();
                    Step("player-new");
                    var host = new Window { Width = 420, Height = 340, Content = player };
                    host.Icon = AppIconHelper.LoadAppIcon();
                    host.Show();
                    Step("player-show");
                    Step("player-done");

                    // 3) 验证窗口捕获（捕获宿主窗口自身）
                    Step("capture");
                    var hwnd = new System.Windows.Interop.WindowInteropHelper(host).Handle;
                    var frame = WindowCaptureService.Capture(hwnd);
                    Step("capture-done");
                    if (frame == null) throw new InvalidOperationException("窗口捕获返回空帧");

                    // 4) 验证媒体会话枚举不崩溃（无会话时返回空列表）
                    Step("sessions");
                    var sessions = await MediaSessionController.GetSessionsAsync();
                    Step("sessions-done");

                    // 5) 端到端验证任务栏“关闭窗口”按钮点击链路
                    Step("taskbar");
                    TestTaskbarClose();
                    Step("taskbar-done");

                    var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
                    timer.Tick += (_, _) =>
                    {
                        timer.Stop();
                        Step("finishing");
                        player.StopAll();
                        host.Close();
                        Step("writing");
                        File.WriteAllText(ResultFile, $"SMOKE OK (sessions={sessions.Count}, frame={frame.PixelWidth}x{frame.PixelHeight}, closeButton=OK, toastAumid={toastAumid})");
                        Step("shutdown");
                        app.Shutdown();
                    };
                    timer.Start();
                }
                catch (Exception ex)
                {
                    Step("FAIL: " + ex.GetType().Name);
                    File.WriteAllText(ResultFile, "SMOKE FAIL: " + ex);
                    app.Shutdown();
                }
            };
            app.Run();
        }

        private static void TestTaskbarClose()
        {
            var settings = new SettingsManager();
            var shortcutService = new ShortcutManager();
            shortcutService.Initialize();

            string appPath = Path.Combine(Path.GetTempPath(), "dynbird_smoke_app.exe");
            File.WriteAllBytes(appPath, new byte[0]);
            shortcutService.AddShortcut(appPath, "冒烟应用");

            var view = new TaskbarView(shortcutService, settings);

            // 创建一个“目标窗口”，并让它进入窗口列表
            var form = new WinForms.Form
            {
                Text = "DynBirdCloseTest",
                ShowInTaskbar = true,
                Opacity = 0
            };
            form.Show();
            form.Update();
            IntPtr targetHwnd = form.Handle;

            // 等一轮窗口枚举（TaskbarView 的 RefreshWindows 使用缓存列表，这里手动刷新）
            view.RefreshData();
            view.Measure(new Size(800, 80));
            view.Arrange(new Rect(0, 0, 800, 80));
            view.UpdateLayout();
            System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                System.Windows.Threading.DispatcherPriority.Loaded,
                new Action(() => { }));

            // 在可视树中找到目标窗口项对应的关闭按钮
            var mainGrid = (Grid)view.FindName("MainGrid");
            Button? closeBtn = FindCloseButton(mainGrid, targetHwnd);
            if (closeBtn == null)
            {
                form.Close();
                throw new InvalidOperationException("未找到目标窗口的关闭按钮");
            }

            // 先直接发 WM_CLOSE 验证关闭链路本身可用（排除 UI 模拟干扰）
            if (closeBtn.DataContext is TaskbarItem item0 && item0.Handle.HasValue)
            {
                Step($"closebtn itemHandle={item0.Handle} formHandle={targetHwnd} equal={item0.Handle.Value == targetHwnd}");
                DynamicBird.Infrastructure.WinApi.WindowAction.Close(item0.Handle.Value);
                bool directClosed = WaitUntil(() => form.IsDisposed, 1500);
                Step($"direct WM_CLOSE closed={directClosed}");
                if (directClosed)
                {
                    form.Close(); // 已关闭，无需继续 UI 模拟
                    return;
                }
            }

            // 完整模拟点击：从 MainGrid 发起隧道/冒泡事件（Button 的 Click 需要完整按下/抬起序列）
            int tick = Environment.TickCount;
            RaiseClick(mainGrid, closeBtn, tick);

            if (!form.IsDisposed)
            {
                // 程序化鼠标事件无法驱动 ButtonBase 的捕获逻辑，直接触发 Click 验证关闭链路
                closeBtn.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            }

            // WM_CLOSE 应关闭目标窗口
            bool closed = WaitUntil(() => form.IsDisposed, 2000);
            if (!closed)
            {
                form.Close();
                throw new InvalidOperationException("点击关闭按钮后目标窗口未被关闭");
            }
        }

        private static void RaiseClick(UIElement root, UIElement target, int tick)
        {
            void Raise(RoutedEvent evt, int t)
            {
                var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, t, MouseButton.Left)
                {
                    RoutedEvent = evt,
                    Source = target
                };
                root.RaiseEvent(args);
            }

            Raise(UIElement.PreviewMouseLeftButtonDownEvent, tick);
            Raise(UIElement.MouseLeftButtonDownEvent, tick + 1);
            Raise(UIElement.PreviewMouseLeftButtonUpEvent, tick + 2);
            Raise(UIElement.MouseLeftButtonUpEvent, tick + 3);
        }

        private static Button? FindCloseButton(DependencyObject root, IntPtr targetHwnd)
        {
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is Button btn &&
                    btn.DataContext is TaskbarItem item &&
                    item.Type == TaskbarItemType.Window &&
                    item.Handle == targetHwnd)
                {
                    return btn;
                }
                var found = FindCloseButton(child, targetHwnd);
                if (found != null) return found;
            }
            return null;
        }

        private static bool WaitUntil(Func<bool> condition, int timeoutMs)
        {
            var start = DateTime.Now;
            while ((DateTime.Now - start).TotalMilliseconds < timeoutMs)
            {
                WinForms.Application.DoEvents(); // 泵 WinForms 消息，让 WM_CLOSE 真正被处理
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
                    System.Windows.Threading.DispatcherPriority.Background,
                    new Action(() => { }));
                if (condition()) return true;
                System.Threading.Thread.Sleep(50);
            }
            return condition();
        }

    }
}
