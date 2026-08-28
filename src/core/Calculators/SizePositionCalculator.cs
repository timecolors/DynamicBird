using System;
using System.Windows;
using DynamicBird.Infrastructure.Utils;

namespace DynamicBird.Core.Calculators
{
    /// <summary>
    /// 位置计算器：根据边缘和窗口尺寸计算窗口位置
    /// </summary>
    public class SizePositionCalculator
    {
        private readonly Window _window;

        public SizePositionCalculator(Window window)
        {
            _window = window;
        }

        /// <summary>
        /// 计算窗口位置（根据边缘对齐）
        /// </summary>
        public (double left, double top) CalculatePosition(
            double targetWidth, double targetHeight,
            string currentEdge,
            double currentLeft, double currentTop,
            double currentWidth, double currentHeight)
        {
            var wa = ScreenMetrics.GetCachedScreenForWindow(
                _window.Left, _window.Top, _window.Width, _window.Height);
            double screenW = wa.Width;
            double screenH = wa.Height;

            double newLeft = currentLeft;
            double newTop = currentTop;

            if (!string.IsNullOrEmpty(currentEdge))
            {
                switch (currentEdge)
                {
                    case "Top":
                        newLeft = currentLeft - (targetWidth - currentWidth) / 2;
                        newLeft = Math.Max(0, Math.Min(newLeft, screenW - targetWidth));
                        newTop = 0;
                        break;
                    case "Bottom":
                        newLeft = currentLeft - (targetWidth - currentWidth) / 2;
                        newLeft = Math.Max(0, Math.Min(newLeft, screenW - targetWidth));
                        newTop = screenH - targetHeight;
                        break;
                    case "Left":
                        newLeft = 0;
                        newTop = currentTop - (targetHeight - currentHeight) / 2;
                        newTop = Math.Max(0, Math.Min(newTop, screenH - targetHeight));
                        break;
                    case "Right":
                        newLeft = screenW - targetWidth;
                        newTop = currentTop - (targetHeight - currentHeight) / 2;
                        newTop = Math.Max(0, Math.Min(newTop, screenH - targetHeight));
                        break;
                    default:
                        newLeft = Math.Max(0, Math.Min(currentLeft, screenW - targetWidth));
                        newTop = Math.Max(0, Math.Min(currentTop, screenH - targetHeight));
                        break;
                }
            }
            else
            {
                newLeft = Math.Max(0, Math.Min(currentLeft, screenW - targetWidth));
                newTop = Math.Max(0, Math.Min(currentTop, screenH - targetHeight));
            }

            return (newLeft, newTop);
        }

        /// <summary>
        /// 计算移动位置（仅位置，不改变尺寸）
        /// </summary>
        public (double left, double top) CalculateMovePosition(
            string edge,
            double mouseX, double mouseY,
            double currentWidth, double currentHeight)
        {
            var wa = ScreenMetrics.GetCachedScreenForWindow(
                _window.Left, _window.Top, _window.Width, _window.Height);
            double screenW = wa.Width;
            double screenH = wa.Height;

            double left = 0, top = 0;
            switch (edge)
            {
                case "Top":
                    left = mouseX - currentWidth / 2;
                    top = 0;
                    break;
                case "Bottom":
                    left = mouseX - currentWidth / 2;
                    top = screenH - currentHeight;
                    break;
                case "Left":
                    left = 0;
                    top = mouseY - currentHeight / 2;
                    break;
                case "Right":
                    left = screenW - currentWidth;
                    top = mouseY - currentHeight / 2;
                    break;
                default:
                    return (0, 0);
            }

            left = Math.Max(0, Math.Min(left, screenW - currentWidth));
            top = Math.Max(0, Math.Min(top, screenH - currentHeight));

            return (left, top);
        }
    }
}