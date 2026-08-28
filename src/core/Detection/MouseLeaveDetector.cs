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
                var wa = DynamicBird.Infrastructure.Utils.ScreenMetrics.GetCachedScreenForWindow(
                    _window.Left, _window.Top, _window.Width, _window.Height);
                bool isOnBottom = Math.Abs(_window.Top + _window.Height - wa.Height + _taskbarHeight) < 50;
                double bottomExtension = isOnBottom ? _taskbarHeight + SafetyMargin : 0;

                // ★ 严格面板边界：鼠标离开面板即视为"已离开"开始隐藏计时。
                //   仅保留极小防抖余量（4 物理像素），避免边缘像素抖动反复计时/取消。
                double threshold = 4.0;

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