using System;
using System.Windows;
using System.Windows.Controls;

namespace DynamicBird.Core.Calculators
{
    /// <summary>
    /// 尺寸计算器：计算目标尺寸、最小尺寸、内容尺寸
    /// </summary>
    public class SizeCalculator
    {
        private readonly Window _window;
        private readonly ContentControl _contentContainer;

        private double _cachedMinWidth = 0;
        private double _cachedMinHeight = 0;
        private bool _hasCachedMinSize = false;

        public SizeCalculator(Window window, ContentControl contentContainer)
        {
            _window = window;
            _contentContainer = contentContainer;
        }

        public void RefreshCache()
        {
            _hasCachedMinSize = false;
            _cachedMinWidth = 0;
            _cachedMinHeight = 0;
        }

        /// <summary>
        /// 测量内容尺寸
        /// </summary>
        public (double width, double height) MeasureContent()
        {
            // 强制更新布局
            _contentContainer.UpdateLayout();

            double contentWidth = _contentContainer.ActualWidth;
            double contentHeight = _contentContainer.ActualHeight;

            // 如果 ActualWidth 无效，尝试测量
            if (contentWidth < 10 || contentHeight < 10)
            {
                _contentContainer.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                contentWidth = _contentContainer.DesiredSize.Width;
                contentHeight = _contentContainer.DesiredSize.Height;
            }

            // 如果还是无效，尝试从子元素获取
            if (contentWidth < 10 || contentHeight < 10)
            {
                if (_contentContainer.Content is FrameworkElement child)
                {
                    child.UpdateLayout();
                    child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    if (child.DesiredSize.Width > 10) contentWidth = child.DesiredSize.Width;
                    if (child.DesiredSize.Height > 10) contentHeight = child.DesiredSize.Height;
                }
            }

            // ★★★ 如果仍然无效，返回保底值（而非让后续计算崩溃） ★★★
            if (contentWidth < 10) contentWidth = 280;
            if (contentHeight < 10) contentHeight = 160;

            return (contentWidth, contentHeight);
        }

        /// <summary>
        /// 计算目标尺寸（含最小值和模式限幅）
        /// </summary>
        public (double width, double height) CalculateTargetSize(
            double contentWidth, double contentHeight, string mode)
        {
            // ★★★ 确保传入的内容尺寸有效 ★★★
            if (contentWidth < 10) contentWidth = 280;
            if (contentHeight < 10) contentHeight = 160;

            const double paddingX = 40;
            const double paddingY = 30;
            const double minWidth = 340;
            const double minHeight = 220;

            double rawWidth = contentWidth + paddingX;
            double rawHeight = contentHeight + paddingY;

            double targetWidth = Math.Max(minWidth, rawWidth);
            double targetHeight = Math.Max(minHeight, rawHeight);

            double screenW = SystemParameters.PrimaryScreenWidth;
            double screenH = SystemParameters.PrimaryScreenHeight;

            if (mode == "Widget")
            {
                double maxW = screenW * 2.0 / 5.0;
                double maxH = screenH * 2.0 / 3.0;
                targetWidth = Math.Min(targetWidth, maxW);
                targetHeight = Math.Min(targetHeight, maxH);
            }
            else if (mode == "AppHelper")
            {
                targetWidth = Math.Min(targetWidth, screenW * 0.8);
                targetHeight = Math.Min(targetHeight, screenH * 0.8);
            }

            targetWidth = Math.Min(targetWidth, screenW);
            targetHeight = Math.Min(targetHeight, screenH);

            return (targetWidth, targetHeight);
        }

        /// <summary>
        /// 计算最小尺寸（使用缓存）
        /// </summary>
        public (double width, double height) CalculateMinSize()
        {
            if (_hasCachedMinSize && _cachedMinWidth > 0 && _cachedMinHeight > 0)
            {
                return (_cachedMinWidth, _cachedMinHeight);
            }

            _contentContainer.UpdateLayout();

            double contentWidth = _contentContainer.ActualWidth;
            double contentHeight = _contentContainer.ActualHeight;

            if (contentWidth < 10)
            {
                _contentContainer.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                contentWidth = _contentContainer.DesiredSize.Width;
            }
            if (contentHeight < 10)
            {
                _contentContainer.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                contentHeight = _contentContainer.DesiredSize.Height;
            }

            if (contentWidth < 10 || contentHeight < 10)
            {
                _contentContainer.Arrange(new Rect(0, 0, double.PositiveInfinity, double.PositiveInfinity));
                if (contentWidth < 10) contentWidth = _contentContainer.RenderSize.Width;
                if (contentHeight < 10) contentHeight = _contentContainer.RenderSize.Height;
            }

            if (contentWidth < 10 || contentHeight < 10)
            {
                if (_contentContainer.Content is FrameworkElement child)
                {
                    child.UpdateLayout();
                    child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    if (contentWidth < 10) contentWidth = child.DesiredSize.Width;
                    if (contentHeight < 10) contentHeight = child.DesiredSize.Height;
                }
            }

            // ★★★ 保底值 ★★★
            if (contentWidth < 10) contentWidth = 280;
            if (contentHeight < 10) contentHeight = 160;

            const double minWidth = 340;
            const double minHeightForButtons = 300;

            if (contentWidth < minWidth) contentWidth = minWidth;
            if (contentHeight < minHeightForButtons) contentHeight = minHeightForButtons;

            const double paddingX = 30;
            const double paddingY = 20;

            double targetWidth = contentWidth + paddingX;
            double targetHeight = contentHeight + paddingY;

            double screenW = SystemParameters.PrimaryScreenWidth;
            double screenH = SystemParameters.PrimaryScreenHeight;
            double maxW = screenW * 2.0 / 5.0;
            double maxH = screenH * 2.0 / 3.0;

            targetWidth = Math.Min(targetWidth, maxW);
            targetHeight = Math.Min(targetHeight, maxH);

            _cachedMinWidth = targetWidth;
            _cachedMinHeight = targetHeight;
            _hasCachedMinSize = true;

            return (targetWidth, targetHeight);
        }
    }
}