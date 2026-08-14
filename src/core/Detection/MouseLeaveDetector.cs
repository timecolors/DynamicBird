using System;
using System.Windows;
using System.Windows.Media;
using DynamicBird.Core.Services.Configuration;

namespace DynamicBird.Core.Detection
{
    /// <summary>
    /// 检测鼠标是否离开面板区域（实例类）
    /// </summary>
    public class MouseLeaveDetector
    {
        private readonly Window _window;
        private readonly FrameworkElement _mainPanel;
        private readonly ISettingsService _settings;
        private readonly double _taskbarHeight;

        private const double SafetyMargin = 10;

        public MouseLeaveDetector(Window window, FrameworkElement mainPanel, ISettingsService settings, double taskbarHeight = 40)
        {
            _window = window;
            _mainPanel = mainPanel;
            _settings = settings;
            _taskbarHeight = taskbarHeight;
        }

        /// <summary>
        /// 判断鼠标是否在面板附近（包含任务栏区域补偿）
        /// </summary>
        public bool IsMouseNearPanel()
        {
            try
            {
                var mousePos = System.Windows.Forms.Cursor.Position;
                var mousePoint = new Point(mousePos.X, mousePos.Y);

                var topLeft = _mainPanel.PointToScreen(new Point(0, 0));
                var bottomRight = _mainPanel.PointToScreen(new Point(
                    _mainPanel.ActualWidth,
                    _mainPanel.ActualHeight));

                if (bottomRight.X - topLeft.X < 1 || bottomRight.Y - topLeft.Y < 1)
                {
                    topLeft = _mainPanel.PointToScreen(new Point(0, 0));
                    bottomRight = _mainPanel.PointToScreen(new Point(
                        _mainPanel.RenderSize.Width,
                        _mainPanel.RenderSize.Height));
                }

                if (bottomRight.X - topLeft.X < 1 || bottomRight.Y - topLeft.Y < 1)
                {
                    topLeft = new Point(_window.Left, _window.Top);
                    bottomRight = new Point(_window.Left + _window.Width, _window.Top + _window.Height);
                }

                double panelLeft = topLeft.X;
                double panelTop = topLeft.Y;
                double panelRight = bottomRight.X;
                double panelBottom = bottomRight.Y;

                // ★★★ 底部额外扩展任务栏高度 ★★★
                // 当面板在底部边缘时，任务栏区域也应该算作"附近"
                bool isOnBottom = Math.Abs(_window.Top + _window.Height - SystemParameters.PrimaryScreenHeight + _taskbarHeight) < 50;
                double bottomExtension = isOnBottom ? _taskbarHeight + SafetyMargin : 0;

                // ★ 阈值按系统 DPI 动态计算（基础 20px × 缩放），不再依赖分辨率预设
                double dpiScale = 1.0;
                try { dpiScale = VisualTreeHelper.GetDpi(_window).DpiScaleX; } catch { }
                if (dpiScale <= 0 || double.IsNaN(dpiScale) || double.IsInfinity(dpiScale)) dpiScale = 1.0;
                double threshold = 20 * dpiScale + SafetyMargin;

                bool inX = mousePoint.X >= panelLeft - threshold && mousePoint.X <= panelRight + threshold;
                bool inY = mousePoint.Y >= panelTop - threshold && mousePoint.Y <= panelBottom + threshold + bottomExtension;

                return inX && inY;
            }
            catch { return false; }
        }

        /// <summary>
        /// 判断鼠标是否在面板内部
        /// </summary>
        public bool IsMouseInsidePanel()
        {
            try
            {
                var mousePos = System.Windows.Forms.Cursor.Position;
                var mousePoint = new Point(mousePos.X, mousePos.Y);

                var topLeft = _mainPanel.PointToScreen(new Point(0, 0));
                var bottomRight = _mainPanel.PointToScreen(new Point(
                    _mainPanel.ActualWidth,
                    _mainPanel.ActualHeight));

                if (bottomRight.X - topLeft.X < 1 || bottomRight.Y - topLeft.Y < 1)
                {
                    topLeft = _mainPanel.PointToScreen(new Point(0, 0));
                    bottomRight = _mainPanel.PointToScreen(new Point(
                        _mainPanel.RenderSize.Width,
                        _mainPanel.RenderSize.Height));
                }

                if (bottomRight.X - topLeft.X < 1 || bottomRight.Y - topLeft.Y < 1)
                {
                    topLeft = new Point(_window.Left, _window.Top);
                    bottomRight = new Point(_window.Left + _window.Width, _window.Top + _window.Height);
                }

                return mousePoint.X >= topLeft.X && mousePoint.X <= bottomRight.X &&
                       mousePoint.Y >= topLeft.Y && mousePoint.Y <= bottomRight.Y;
            }
            catch { return false; }
        }
    }
}