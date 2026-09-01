using System;

namespace ShoreHue.Core.Detection
{
    public static class EdgeStateDetector
    {
        // ★★★ DIP 单位阈值 ★★★
        // 边缘阈值由设置动态传入（TriggerDistancePx，默认 6；原固定 12 偏宽导致误触）
        // 四角触发区 = 边缘阈值的 2 倍（比普通边缘更宽容，避免“触发后挪向面板就滑出角区”）
        private const double REGION_RATIO = 1.0 / 3.0;

        // ★★★ 右上角安全区 ★★★
        // 右上角用于关闭窗口，不应呼出任何面板：右边缘 20px × 标题栏高度 48px 的矩形内全部屏蔽。
        // ★★★ 右上角安全区 ★★★（MainWindow tick 复用：已显示的面板滑过安全区时不隐藏）
        public const double TOP_RIGHT_SAFE_ZONE_X = 20.0;
        public const double TOP_RIGHT_SAFE_ZONE_Y = 48.0;

        public static EdgeRegion DetectRegion(double mouseX, double mouseY, double screenWidth, double screenHeight,
            double edgeThreshold = 6.0, bool allowTopRightPanel = false)
        {
            if (screenWidth <= 0 || screenHeight <= 0)
                return EdgeRegion.Unknown;

            double edge = Math.Max(2, edgeThreshold);
            double corner = Math.Max(8, edge * 2.0);

            // 右上角安全区：默认不触发任何面板（避免影响关闭窗口）。
            // 仅当用户把右上角显式配置为"窗口操作中心"时放行（allowTopRightPanel）。
            if (!allowTopRightPanel &&
                mouseX >= screenWidth - TOP_RIGHT_SAFE_ZONE_X && mouseY <= TOP_RIGHT_SAFE_ZONE_Y)
                return EdgeRegion.Unknown;

            bool nearTop = mouseY <= edge;
            bool nearBottom = mouseY >= screenHeight - edge;
            bool nearLeft = mouseX <= edge;
            bool nearRight = mouseX >= screenWidth - edge;

            // 四角用更宽的阈值，且需要鼠标真正贴近角点（x/y 同时满足）
            if (mouseX <= corner && mouseY <= corner) return EdgeRegion.TopLeft;
            if (mouseX >= screenWidth - corner && mouseY <= corner) return EdgeRegion.TopRight;
            if (mouseX <= corner && mouseY >= screenHeight - corner) return EdgeRegion.BottomLeft;
            if (mouseX >= screenWidth - corner && mouseY >= screenHeight - corner) return EdgeRegion.BottomRight;

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