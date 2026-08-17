using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DynamicBird.Core.Services;
using DynamicBird.UI.Status;

namespace DynamicBird.UI.Main
{
    public partial class MainWindow
    {
        private Point _lastMousePosition = new Point(-1, -1);
        private bool _hasLastMousePosition = false;

        // ========== 面板鼠标事件 ==========

        private void MainPanel_MouseEnter(object sender, MouseEventArgs e)
        {
            if (_modeService.IsDoNotDisturb) return;

            _visibilityController.CancelHide();
            // 通过锚点滑入/恢复显示（含滑出途中重新进入的情况）
            _edgeController.ShowPanelAtAnchor();
            _lastMousePosition = e.GetPosition(this);
            _hasLastMousePosition = true;
        }

        private void MainPanel_MouseLeave(object sender, MouseEventArgs e)
        {
            // ★ 尺寸调整中（_edgeController.IsDragging）不因鼠标暂时离开而隐藏
            if (_dragController.IsDragging || _dragController.IsRecentlyDragged ||
                _edgeController.IsDragging || _edgeController.IsRecentlyDragged) return;
            if (_visibilityController.IsLocked || PresentationSource.FromVisual(this) == null) return;

            var currentMousePos = e.GetPosition(this);

            if (_hasLastMousePosition)
            {
                double dx = currentMousePos.X - _lastMousePosition.X;
                double dy = currentMousePos.Y - _lastMousePosition.Y;
                if (Math.Sqrt(dx * dx + dy * dy) < 1.0) return;
            }

            _lastMousePosition = currentMousePos;
            _hasLastMousePosition = true;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_dragController.IsDragging || _dragController.IsRecentlyDragged) return;
                if (_visibilityController.IsLocked) return;
                if (_visibilityController.IsMouseNearPanel()) return;
                _visibilityController.HideWithDelay();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        // ========== 外观应用 ==========

        private void ApplyAppearance()
        {
            try
            {
                var bgColor = HexToMediaColor(_settingsService.BackgroundColor);
                MainPanel.Background = new SolidColorBrush(bgColor);
            }
            catch { MainPanel.Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)); }

            try
            {
                var textColor = HexToMediaColor(_settingsService.TextColor);
                IconPath.Stroke = new SolidColorBrush(textColor);
            }
            catch { }

            _visibilityController.Opacity = _settingsService.Opacity;
            MainPanel.CornerRadius = new CornerRadius(_settingsService.CornerRadius);
            UpdateIconTextInternal();
        }

        // ========== 系统状态刷新 ==========

        private void RefreshSystemStatus()
        {
            try
            {
                if (_settingsService.ShowSystemStatus)
                {
                    if (SystemStatusContainer.Content is not DynamicBird.UI.Status.SystemStatusView statusView)
                    {
                        statusView = new DynamicBird.UI.Status.SystemStatusView();
                        SystemStatusContainer.Content = statusView;
                    }
                    statusView.ApplySettings(_settingsService);
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

        // ========== 颜色工具 ==========

        private Color HexToMediaColor(string hex)
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
                else return Color.FromRgb(45, 45, 45);
                return Color.FromArgb(a, r, g, b);
            }
            catch { return Color.FromRgb(45, 45, 45); }
        }
    }
}
