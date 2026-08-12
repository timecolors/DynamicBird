using System;

namespace DynamicBird.Core.Detection
{
    public static class EdgeStateDetector
    {
        // ★★★ DIP 单位阈值 ★★★
        private const double EDGE_THRESHOLD = 12.0;
        private const double REGION_RATIO = 1.0 / 3.0;

        public static EdgeRegion DetectRegion(double mouseX, double mouseY, double screenWidth, double screenHeight)
        {
            if (screenWidth <= 0 || screenHeight <= 0)
                return EdgeRegion.Unknown;

            bool onTop = mouseY <= EDGE_THRESHOLD;
            bool onBottom = mouseY >= screenHeight - EDGE_THRESHOLD;
            bool onLeft = mouseX <= EDGE_THRESHOLD;
            bool onRight = mouseX >= screenWidth - EDGE_THRESHOLD;

            if (onTop && onLeft) return EdgeRegion.TopLeft;
            if (onTop && onRight) return EdgeRegion.TopRight;
            if (onBottom && onLeft) return EdgeRegion.BottomLeft;
            if (onBottom && onRight) return EdgeRegion.BottomRight;

            if (onTop) return GetHorizontalRegion(mouseX, screenWidth, EdgeRegion.Top_Left, EdgeRegion.Top_Center, EdgeRegion.Top_Right);
            if (onBottom) return GetHorizontalRegion(mouseX, screenWidth, EdgeRegion.Bottom_Left, EdgeRegion.Bottom_Center, EdgeRegion.Bottom_Right);
            if (onLeft) return GetVerticalRegion(mouseY, screenHeight, EdgeRegion.Left_Top, EdgeRegion.Left_Center, EdgeRegion.Left_Bottom);
            if (onRight) return GetVerticalRegion(mouseY, screenHeight, EdgeRegion.Right_Top, EdgeRegion.Right_Center, EdgeRegion.Right_Bottom);

            return EdgeRegion.Unknown;
        }

        private static EdgeRegion GetHorizontalRegion(double mouseX, double screenWidth, EdgeRegion left, EdgeRegion center, EdgeRegion right)
        {
            double pos = mouseX / screenWidth;
            double halfRegion = REGION_RATIO / 2.0;

            if (pos >= 0.5 - halfRegion && pos <= 0.5 + halfRegion)
                return center;
            else if (pos < 0.5 - halfRegion)
                return left;
            else
                return right;
        }

        private static EdgeRegion GetVerticalRegion(double mouseY, double screenHeight, EdgeRegion top, EdgeRegion center, EdgeRegion bottom)
        {
            double pos = mouseY / screenHeight;
            double halfRegion = REGION_RATIO / 2.0;

            if (pos >= 0.5 - halfRegion && pos <= 0.5 + halfRegion)
                return center;
            else if (pos < 0.5 - halfRegion)
                return top;
            else
                return bottom;
        }
    }
}