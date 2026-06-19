using System;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Input;
using LingDongBird.Core;
using LingDongBird.Features.SystemStatus;
using LingDongBird.Features.WindowSwitcher;
using LingDongBird.Features.ShapeAnimator;

namespace LingDongBird
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer _edgeTimer;
        private System.Windows.Forms.NotifyIcon _notifyIcon;
        private bool _panelLock = false;
        private ShapeAnimator _shapeAnimator;
        private double _currentTaskbarHeight = 40;
        private bool _isDraggingPosition = false;
        private Point _dragStartPoint;
        private double _dragStartLeft;
        private double _dragStartTop;
        private string _currentEdge = "";

        // 模式切换
        private string _currentMode = "Taskbar"; // "Taskbar" | "AppHelper"

        public MainWindow()
        {
            try
            {
                this.AllowsTransparency = true;
                InitializeComponent();

                ContentContainer.Content = new WindowSwitcherView();

                Left = SystemParameters.WorkArea.Width - Width - 10;
                Top = SystemParameters.WorkArea.Height - Height - 10;
                ApplyAppearance();
                MainPanel.Opacity = 0;

                _shapeAnimator = new ShapeAnimator(this);

                CreateTrayIcon();
                RefreshSystemStatus();

                _edgeTimer = new DispatcherTimer();
                _edgeTimer.Interval = TimeSpan.FromMilliseconds(16); // 约60fps
                _edgeTimer.Tick += CheckEdge!;
                _edgeTimer.Start();

                _currentTaskbarHeight = GetTaskbarHeight();

                // 加载当前模式
                _currentMode = SettingsManager.CurrentMode;

                // 为图标添加点击切换模式事件
                IconText.MouseDown += IconText_MouseDown;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"启动失败:\n{ex.Message}", "灵动鸟错误", MessageBoxButton.OK, MessageBoxImage.Error);
                System.Windows.Application.Current.Shutdown();
            }
        }

        public void SetPanelLock(bool locked)
        {
            _panelLock = locked;
        }

        private void RefreshSystemStatus()
        {
            try
            {
                if (SettingsManager.ShowSystemStatus)
                {
                    if (SystemStatusContainer.Content == null)
                    {
                        SystemStatusContainer.Content = new SystemStatusView();
                    }
                    SystemStatusContainer.Visibility = Visibility.Visible;
                }
                else
                {
                    SystemStatusContainer.Visibility = Visibility.Collapsed;
                    SystemStatusContainer.Content = null;
                }
            }
            catch { }
        }

        private void ApplyAppearance()
        {
            try
            {
                var bgColor = HexToMediaColor(SettingsManager.BackgroundColor);
                MainPanel.Background = new SolidColorBrush(bgColor);
            }
            catch { MainPanel.Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)); }

            try
            {
                var textColor = HexToMediaColor(SettingsManager.TextColor);
                IconText.Foreground = new SolidColorBrush(textColor);
            }
            catch { }

            MainPanel.Opacity = SettingsManager.Opacity;
            MainPanel.CornerRadius = new CornerRadius(SettingsManager.CornerRadius);

            ApplyCustomIcon();
        }

        private void ApplyCustomIcon()
        {
            string iconPath = SettingsManager.CustomIconPath;
            if (!string.IsNullOrEmpty(iconPath) && System.IO.File.Exists(iconPath))
            {
                try
                {
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(iconPath, UriKind.Absolute);
                    bitmap.EndInit();
                    IconText.Text = "🖼️"; // 占位
                }
                catch { IconText.Text = "🐦"; }
            }
            else
            {
                IconText.Text = "🐦";
            }
        }

        private void IconText_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_currentMode == "Taskbar")
            {
                _currentMode = "AppHelper";
                SettingsManager.CurrentMode = "AppHelper";
                System.Windows.MessageBox.Show("切换到应用辅助模式（功能开发中）", "灵动鸟", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                _currentMode = "Taskbar";
                SettingsManager.CurrentMode = "Taskbar";
                System.Windows.MessageBox.Show("切换到任务栏模式", "灵动鸟", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            // 刷新界面
            RefreshSystemStatus();
            ApplyAppearance();
        }

        private System.Windows.Media.Color HexToMediaColor(string hex)
        {
            try
            {
                if (string.IsNullOrEmpty(hex)) hex = "#2D2D2D";
                if (hex.StartsWith("#")) hex = hex.Substring(1);
                byte a = 255, r = 0, g = 0, b = 0;
                if (hex.Length == 6)
                {
                    r = Convert.ToByte(hex.Substring(0, 2), 16);
                    g = Convert.ToByte(hex.Substring(2, 2), 16);
                    b = Convert.ToByte(hex.Substring(4, 2), 16);
                }
                else if (hex.Length == 8)
                {
                    a = Convert.ToByte(hex.Substring(0, 2), 16);
                    r = Convert.ToByte(hex.Substring(2, 2), 16);
                    g = Convert.ToByte(hex.Substring(4, 2), 16);
                    b = Convert.ToByte(hex.Substring(6, 2), 16);
                }
                else return System.Windows.Media.Color.FromRgb(45, 45, 45);
                return System.Windows.Media.Color.FromArgb(a, r, g, b);
            }
            catch { return System.Windows.Media.Color.FromRgb(45, 45, 45); }
        }

        private void CreateTrayIcon()
        {
            _notifyIcon = new System.Windows.Forms.NotifyIcon();
            _notifyIcon.Text = "🐦 灵动鸟";
            try
            {
                if (System.IO.File.Exists("Resources/icon.ico"))
                    _notifyIcon.Icon = new System.Drawing.Icon("Resources/icon.ico");
                else
                    _notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetEntryAssembly().Location);
            }
            catch { _notifyIcon.Icon = System.Drawing.SystemIcons.Application; }
            _notifyIcon.Visible = true;

            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("设置", null, (s, e) => OpenSettings());
            menu.Items.Add("-");
            menu.Items.Add("显示 / 隐藏", null, (s, e) => ToggleWindow());
            menu.Items.Add("-");
            var autoStartItem = new System.Windows.Forms.ToolStripMenuItem("开机自启")
            {
                CheckOnClick = true,
                Checked = IsAutoStartEnabled()
            };
            autoStartItem.Click += (s, e) => ToggleAutoStart(autoStartItem.Checked);
            menu.Items.Add(autoStartItem);
            menu.Items.Add("-");
            menu.Items.Add("退出", null, (s, e) => ExitApp());

            _notifyIcon.ContextMenuStrip = menu;
            _notifyIcon.DoubleClick += (s, e) => ToggleWindow();
        }

        private void OpenSettings()
        {
            try
            {
                var settingsWindow = new SettingsWindow();
                settingsWindow.Owner = this;
                settingsWindow.ShowDialog();
                ApplyAppearance();
                RefreshSystemStatus();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"打开设置失败:\n{ex.Message}", "灵动鸟", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ---------- 边缘检测 ----------
        private void CheckEdge(object? sender, EventArgs e)
        {
            try
            {
                if (_panelLock) return;

                double screenWidth = SystemParameters.PrimaryScreenWidth;
                double screenHeight = SystemParameters.PrimaryScreenHeight;
                double dpiScale = DpiHelper.GetDpiScale(this);

                var result = EdgeDetector.Detect(screenWidth, screenHeight, dpiScale);

                if (result.IsTriggered)
                {
                    string pos = result.Position;
                    bool isEnabled = false;

                    if (pos == "TopLeft" || pos == "TopRight" || pos == "BottomLeft" || pos == "BottomRight")
                        isEnabled = SettingsManager.IsCornerEnabled(pos);
                    else
                        isEnabled = SettingsManager.IsEdgeEnabled(pos);

                    if (isEnabled)
                    {
                        if (pos == "TopLeft" || pos == "TopRight" || pos == "BottomLeft" || pos == "BottomRight")
                        {
                            MainPanel.Opacity = SettingsManager.Opacity;
                            PositionPanelCorner(pos, screenWidth, screenHeight);
                            return;
                        }

                        string edgeMode = SettingsManager.GetEdgeMode(pos);

                        if (edgeMode == "Fixed")
                        {
                            ProcessFixedEdge(pos, result, screenWidth, screenHeight);
                        }
                        else
                        {
                            ProcessFollowEdge(pos, result, screenWidth, screenHeight);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CheckEdge error: {ex.Message}");
            }
        }

        private void ProcessFixedEdge(string pos, EdgeDetector.EdgeResult result, double screenWidth, double screenHeight)
        {
            bool isHorizontal = (pos == "Top" || pos == "Bottom");
            double mousePosOnEdge = isHorizontal ? result.MouseX / screenWidth : result.MouseY / screenHeight;
            double screenLength = isHorizontal ? screenWidth : screenHeight;

            string fixedShape = SettingsManager.GetFixedShape(pos);
            double offset = SettingsManager.GetFixedOffset(pos);

            var shapeResult = ShapeCalculator.GetFixedShapeResult(pos, fixedShape, _currentTaskbarHeight);

            double left = 0, top = 0;
            switch (pos)
            {
                case "Top":
                    left = screenWidth / 2 - shapeResult.Width / 2 + offset;
                    top = 0;
                    break;
                case "Bottom":
                    left = screenWidth / 2 - shapeResult.Width / 2 + offset;
                    top = screenHeight - shapeResult.Height;
                    break;
                case "Left":
                    left = 0;
                    top = screenHeight / 2 - shapeResult.Height / 2 + offset;
                    break;
                case "Right":
                    left = screenWidth - shapeResult.Width;
                    top = screenHeight / 2 - shapeResult.Height / 2 + offset;
                    break;
            }

            left = Math.Max(0, Math.Min(left, screenWidth - shapeResult.Width));
            top = Math.Max(0, Math.Min(top, screenHeight - shapeResult.Height));

            _currentEdge = pos;

            MainPanel.Opacity = SettingsManager.Opacity;
            int animDuration = SettingsManager.AnimationDurationMs;
            _shapeAnimator.AnimateTo(shapeResult.Width, shapeResult.Height, left, top, animDuration);
        }

        private void ProcessFollowEdge(string pos, EdgeDetector.EdgeResult result, double screenWidth, double screenHeight)
        {
            bool isHorizontal = (pos == "Top" || pos == "Bottom");
            double mousePosOnEdge = isHorizontal ? result.MouseX / screenWidth : result.MouseY / screenHeight;
            double screenLength = isHorizontal ? screenWidth : screenHeight;

            var shapeResult = ShapeCalculator.Calculate(pos, mousePosOnEdge, screenLength, _currentTaskbarHeight);

            double left = 0, top = 0;
            switch (pos)
            {
                case "Top": left = result.MouseX - shapeResult.Width / 2; top = 0; break;
                case "Bottom": left = result.MouseX - shapeResult.Width / 2; top = screenHeight - shapeResult.Height; break;
                case "Left": left = 0; top = result.MouseY - shapeResult.Height / 2; break;
                case "Right": left = screenWidth - shapeResult.Width; top = result.MouseY - shapeResult.Height / 2; break;
            }

            left = Math.Max(0, Math.Min(left, screenWidth - shapeResult.Width));
            top = Math.Max(0, Math.Min(top, screenHeight - shapeResult.Height));

            MainPanel.Opacity = SettingsManager.Opacity;
            int animDuration = SettingsManager.AnimationDurationMs;
            _shapeAnimator.AnimateTo(shapeResult.Width, shapeResult.Height, left, top, animDuration);
        }

        private void PositionPanelCorner(string position, double screenWidth, double screenHeight)
        {
            double width = Width;
            double height = Height;
            double left = 0, top = 0;

            switch (position)
            {
                case "TopLeft": left = 0; top = 0; break;
                case "TopRight": left = screenWidth - width; top = 0; break;
                case "BottomLeft": left = 0; top = screenHeight - height; break;
                case "BottomRight": left = screenWidth - width; top = screenHeight - height; break;
            }

            _shapeAnimator.SetImmediate(width, height, left, top);
        }

        // ---------- 面板拖动调整固定位置 ----------
        private void MainPanel_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_currentEdge == "") return;
            string edgeMode = SettingsManager.GetEdgeMode(_currentEdge);
            if (edgeMode != "Fixed") return;

            _isDraggingPosition = true;
            _dragStartPoint = e.GetPosition(this);
            _dragStartLeft = this.Left;
            _dragStartTop = this.Top;
            MainPanel.CaptureMouse();
            SetPanelLock(true);
        }

        private void MainPanel_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDraggingPosition) return;

            var currentPos = e.GetPosition(this);
            double deltaX = currentPos.X - _dragStartPoint.X;
            double deltaY = currentPos.Y - _dragStartPoint.Y;

            double newLeft = _dragStartLeft + deltaX;
            double newTop = _dragStartTop + deltaY;

            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            newLeft = Math.Max(0, Math.Min(newLeft, screenWidth - Width));
            newTop = Math.Max(0, Math.Min(newTop, screenHeight - Height));

            double offset = 0;
            switch (_currentEdge)
            {
                case "Top": offset = newLeft - (screenWidth / 2 - Width / 2); break;
                case "Bottom": offset = newLeft - (screenWidth / 2 - Width / 2); break;
                case "Left": offset = newTop - (screenHeight / 2 - Height / 2); break;
                case "Right": offset = newTop - (screenHeight / 2 - Height / 2); break;
            }

            this.Left = newLeft;
            this.Top = newTop;
        }

        private void MainPanel_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingPosition)
            {
                _isDraggingPosition = false;
                MainPanel.ReleaseMouseCapture();
                SetPanelLock(false);

                double screenWidth = SystemParameters.PrimaryScreenWidth;
                double screenHeight = SystemParameters.PrimaryScreenHeight;
                double offset = 0;
                switch (_currentEdge)
                {
                    case "Top": offset = this.Left - (screenWidth / 2 - Width / 2); break;
                    case "Bottom": offset = this.Left - (screenWidth / 2 - Width / 2); break;
                    case "Left": offset = this.Top - (screenHeight / 2 - Height / 2); break;
                    case "Right": offset = this.Top - (screenHeight / 2 - Height / 2); break;
                }
                SettingsManager.SetFixedOffset(_currentEdge, offset);
            }
        }

        private void MainPanel_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            MainPanel.Opacity = SettingsManager.Opacity;
        }

        private void MainPanel_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_panelLock) return;
            MainPanel.Opacity = 0;
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

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hwnd, ref RECT rect);

        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private bool IsAutoStartEnabled()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", false);
                return key?.GetValue("LingDongBird") != null;
            }
            catch { return false; }
        }

        private void ToggleAutoStart(bool enable)
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run");
                if (enable)
                    key?.SetValue("LingDongBird", Assembly.GetEntryAssembly().Location);
                else
                    key?.DeleteValue("LingDongBird", false);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"设置开机自启失败: {ex.Message}", "灵动鸟", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ToggleWindow()
        {
            if (IsVisible) Hide();
            else { Show(); Activate(); }
        }

        private void ExitApp()
        {
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            System.Windows.Application.Current.Shutdown();
        }
    }
}