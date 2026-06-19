using System;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace LingDongBird.Features.ShapeAnimator
{
    /// <summary>
    /// 负责窗口尺寸和位置的平滑动画
    /// </summary>
    public class ShapeAnimator
    {
        private readonly Window _window;
        private DoubleAnimation? _widthAnim;
        private DoubleAnimation? _heightAnim;
        private DoubleAnimation? _leftAnim;
        private DoubleAnimation? _topAnim;
        private readonly DispatcherTimer _cleanupTimer;

        public ShapeAnimator(Window window)
        {
            _window = window;
            _cleanupTimer = new DispatcherTimer();
            _cleanupTimer.Interval = TimeSpan.FromMilliseconds(100);
            _cleanupTimer.Tick += (s, e) =>
            {
                _cleanupTimer.Stop();
                _widthAnim = null;
                _heightAnim = null;
                _leftAnim = null;
                _topAnim = null;
            };
        }

        /// <summary>
        /// 平滑过渡到目标尺寸和位置
        /// </summary>
        public void AnimateTo(double width, double height, double left, double top, int durationMs)
        {
            // 取消已有动画
            _window.BeginAnimation(Window.WidthProperty, null);
            _window.BeginAnimation(Window.HeightProperty, null);
            _window.BeginAnimation(Window.LeftProperty, null);
            _window.BeginAnimation(Window.TopProperty, null);

            var duration = TimeSpan.FromMilliseconds(durationMs);

            // 创建动画
            _widthAnim = new DoubleAnimation(width, duration)
            {
                EasingFunction = new QuadraticEase() { EasingMode = EasingMode.EaseInOut }
            };
            _heightAnim = new DoubleAnimation(height, duration)
            {
                EasingFunction = new QuadraticEase() { EasingMode = EasingMode.EaseInOut }
            };
            _leftAnim = new DoubleAnimation(left, duration)
            {
                EasingFunction = new QuadraticEase() { EasingMode = EasingMode.EaseInOut }
            };
            _topAnim = new DoubleAnimation(top, duration)
            {
                EasingFunction = new QuadraticEase() { EasingMode = EasingMode.EaseInOut }
            };

            // 启动动画
            _window.BeginAnimation(Window.WidthProperty, _widthAnim);
            _window.BeginAnimation(Window.HeightProperty, _heightAnim);
            _window.BeginAnimation(Window.LeftProperty, _leftAnim);
            _window.BeginAnimation(Window.TopProperty, _topAnim);

            // 设置清理计时器
            _cleanupTimer.Interval = TimeSpan.FromMilliseconds(durationMs + 50);
            _cleanupTimer.Start();
        }

        /// <summary>
        /// 立即设置尺寸和位置（无动画）
        /// </summary>
        public void SetImmediate(double width, double height, double left, double top)
        {
            _window.BeginAnimation(Window.WidthProperty, null);
            _window.BeginAnimation(Window.HeightProperty, null);
            _window.BeginAnimation(Window.LeftProperty, null);
            _window.BeginAnimation(Window.TopProperty, null);

            _window.Width = width;
            _window.Height = height;
            _window.Left = left;
            _window.Top = top;
        }
    }
}