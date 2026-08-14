using System;

namespace DynamicBird.Core.Detection
{
    public static class EdgeStateDetector
    {
        // ★★★ DIP 单位阈值 ★★★
        private const double EDGE_THRESHOLD = 12.0;
        // 四角触发区（比普通边缘更宽容，避免“触发后挪向面板就滑出角区”）
        private const double CORNER_THRESHOLD = 24.0;
        private const double REGION_RATIO = 1.0 / 3.0;

        // ★★★ 右上角安全区 ★★★
        // 右上角用于关闭窗口，不应呼出任何面板：右边缘 20px × 标题栏高度 48px 的矩形内全部屏蔽。
        private const double TOP_RIGHT_SAFE_ZONE_X = 20.0;
        private const double TOP_RIGHT_SAFE_ZONE_Y = 48.0;

        public static EdgeRegion DetectRegion(double mouseX, double mouseY, double screenWidth, double screenHeight)
        {
            if (screenWidth <= 0 || screenHeight <= 0)
                return EdgeRegion.Unknown;

            // 右上角安全区：不触发任何面板，也不进入“角/边”逻辑
            if (mouseX >= screenWidth - TOP_RIGHT_SAFE_ZONE_X && mouseY <= TOP_RIGHT_SAFE_ZONE_Y)
                return EdgeRegion.Unknown;

            bool nearTop = mouseY <= EDGE_THRESHOLD;
            bool nearBottom = mouseY >= screenHeight - EDGE_THRESHOLD;
            bool nearLeft = mouseX <= EDGE_THRESHOLD;
            bool nearRight = mouseX >= screenWidth - EDGE_THRESHOLD;

            // 四角用更宽的阈值，且需要鼠标真正贴近角点（x/y 同时满足）
            if (mouseX <= CORNER_THRESHOLD && mouseY <= CORNER_THRESHOLD) return EdgeRegion.TopLeft;
            if (mouseX >= screenWidth - CORNER_THRESHOLD && mouseY <= CORNER_THRESHOLD) return EdgeRegion.TopRight;
            if (mouseX <= CORNER_THRESHOLD && mouseY >= screenHeight - CORNER_THRESHOLD) return EdgeRegion.BottomLeft;
            if (mouseX >= screenWidth - CORNER_THRESHOLD && mouseY >= screenHeight - CORNER_THRESHOLD) return EdgeRegion.BottomRight;

            if (nearTop) return GetHorizontalRegion(mouseX, screenWidth, EdgeRegion.Top_Left, EdgeRegion.Top_Center, EdgeRegion.Top_Right);
            if (nearBottom) return GetHorizontalRegion(mouseX, screenWidth, EdgeRegion.Bottom_Left, EdgeRegion.Bottom_Center, EdgeRegion.Bottom_Right);
            if (nearLeft) return GetVerticalRegion(mouseY, screenHeight, EdgeRegion.Left_Top, EdgeRegion.Left_Center, EdgeRegion.Left_Bottom);
            if (nearRight) return GetVerticalRegion(mouseY, screenHeight, EdgeRegion.Right_Top, EdgeRegion.Right_Center, EdgeRegion.Right_Bottom);

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
