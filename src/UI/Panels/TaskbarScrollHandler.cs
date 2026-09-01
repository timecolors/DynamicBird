using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ShoreHue.UI.Panels
{
    public class TaskbarScrollHandler
    {
        private ScrollViewer? _scrollViewer;
        private readonly DispatcherTimer _scrollTimer;
        private bool _isEnabled = true;
        private int _scrollDirection = 0; // -1 左/上, 1 右/下
        private readonly double _scrollStep = 30;
        private bool _isMouseOver = false;
        private readonly string _regionName;
        private bool _isHorizontal = true;

        private const double EDGE_THRESHOLD = 50;

        public TaskbarScrollHandler(ScrollViewer scrollViewer, string regionName = "未知区域", bool isHorizontal = true)
        {
            _regionName = regionName;
            _isHorizontal = isHorizontal;
            Reattach(scrollViewer, isHorizontal);

            _scrollTimer = new DispatcherTimer();
            _scrollTimer.Interval = TimeSpan.FromMilliseconds(30);
            _scrollTimer.Tick += OnScrollTimerTick;

            System.Diagnostics.Debug.WriteLine($"[TaskbarScrollHandler] 创建: {_regionName}, 方向={(isHorizontal ? "水平" : "垂直")}");
        }

        public void Reattach(ScrollViewer newScrollViewer, bool isHorizontal = true)
        {
            if (_scrollViewer != null)
            {
                _scrollViewer.MouseMove -= OnMouseMove;
                _scrollViewer.MouseEnter -= OnMouseEnter;
                _scrollViewer.MouseLeave -= OnMouseLeave;
            }

            _scrollViewer = newScrollViewer;
            _isHorizontal = isHorizontal;

            if (_scrollViewer != null)
            {
                _scrollViewer.MouseMove += OnMouseMove;
                _scrollViewer.MouseEnter += OnMouseEnter;
                _scrollViewer.MouseLeave += OnMouseLeave;
                _scrollViewer.HorizontalScrollBarVisibility = isHorizontal ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Disabled;
                _scrollViewer.VerticalScrollBarVisibility = isHorizontal ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Hidden;
                _scrollViewer.CanContentScroll = false;

                System.Diagnostics.Debug.WriteLine($"[{_regionName}] Reattach: 方向={(isHorizontal ? "水平" : "垂直")}");
            }
        }

        public void Enable() => _isEnabled = true;
        public void Disable() => _isEnabled = false;

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            _isMouseOver = true;
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            _isMouseOver = false;
            StopScrolling();
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isEnabled || !_isMouseOver || _scrollViewer == null) return;

            var position = e.GetPosition(_scrollViewer);
            double size = _isHorizontal ? _scrollViewer.ActualWidth : _scrollViewer.ActualHeight;

            bool canScroll = _isHorizontal
                ? _scrollViewer.ExtentWidth > _scrollViewer.ViewportWidth + 1
                : _scrollViewer.ExtentHeight > _scrollViewer.ViewportHeight + 1;

            if (!canScroll)
            {
                if (_scrollDirection != 0) StopScrolling();
                return;
            }

            if (_isHorizontal)
            {
                // 水平滚动：左边缘和右边缘
                if (position.X < EDGE_THRESHOLD)
                {
                    if (_scrollDirection != -1) { _scrollDirection = -1; _scrollTimer.Start(); }
                }
                else if (position.X > size - EDGE_THRESHOLD)
                {
                    if (_scrollDirection != 1) { _scrollDirection = 1; _scrollTimer.Start(); }
                }
                else
                {
                    StopScrolling();
                }
            }
            else
            {
                // 垂直滚动：上边缘和下边缘
                if (position.Y < EDGE_THRESHOLD)
                {
                    if (_scrollDirection != -1) { _scrollDirection = -1; _scrollTimer.Start(); }
                }
                else if (position.Y > size - EDGE_THRESHOLD)
                {
                    if (_scrollDirection != 1) { _scrollDirection = 1; _scrollTimer.Start(); }
                }
                else
                {
                    StopScrolling();
                }
            }
        }

        private void OnScrollTimerTick(object? sender, EventArgs e)
        {
            if (_scrollViewer == null) return;

            if (_scrollDirection == -1)
            {
                if (_isHorizontal)
                {
                    double newOffset = Math.Max(0, _scrollViewer.HorizontalOffset - _scrollStep);
                    _scrollViewer.ScrollToHorizontalOffset(newOffset);
                }
                else
                {
                    double newOffset = Math.Max(0, _scrollViewer.VerticalOffset - _scrollStep);
                    _scrollViewer.ScrollToVerticalOffset(newOffset);
                }
            }
            else if (_scrollDirection == 1)
            {
                if (_isHorizontal)
                {
                    double maxOffset = Math.Max(0, _scrollViewer.ExtentWidth - _scrollViewer.ViewportWidth);
                    double newOffset = Math.Min(maxOffset, _scrollViewer.HorizontalOffset + _scrollStep);
                    _scrollViewer.ScrollToHorizontalOffset(newOffset);
                }
                else
                {
                    double maxOffset = Math.Max(0, _scrollViewer.ExtentHeight - _scrollViewer.ViewportHeight);
                    double newOffset = Math.Min(maxOffset, _scrollViewer.VerticalOffset + _scrollStep);
                    _scrollViewer.ScrollToVerticalOffset(newOffset);
                }
            }
        }

        private void StopScrolling()
        {
            _scrollDirection = 0;
            _scrollTimer.Stop();
        }

        public void Detach()
        {
            if (_scrollViewer != null)
            {
                _scrollViewer.MouseMove -= OnMouseMove;
                _scrollViewer.MouseEnter -= OnMouseEnter;
                _scrollViewer.MouseLeave -= OnMouseLeave;
            }
            _scrollTimer.Stop();
        }
    }
}