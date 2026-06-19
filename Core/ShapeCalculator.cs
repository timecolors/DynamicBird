using System;
using System.Windows;

namespace LingDongBird.Core
{
    /// <summary>
    /// 计算面板目标形状和尺寸
    /// </summary>
    public static class ShapeCalculator
    {
        public class ShapeResult
        {
            public double Width { get; set; }
            public double Height { get; set; }
            public bool IsSquare { get; set; }
            public bool IsHorizontal { get; set; }
            public string ShapeType { get; set; } = "Square"; // "Square", "StripH", "StripV"
        }

        /// <summary>
        /// 计算目标形状（支持自定义区域配置）
        /// </summary>
        public static ShapeResult Calculate(string position, double mousePos, double screenLength, double taskbarHeight, bool isFixed = false, string fixedShape = "Square")
        {
            // 读取全局配置
            double stripLengthRatio = SettingsManager.StripLengthRatio;
            double stripWidthMult = SettingsManager.StripWidthMultiplier;
            double squareShortMult = SettingsManager.SquareShortSideMultiplier;
            double golden = SettingsManager.GoldenRatio;
            double regionRatio = SettingsManager.TriggerRegionRatio;

            // 基础短边宽度
            double baseWidth = taskbarHeight * stripWidthMult;

            bool isHorizontal = (position == "Top" || position == "Bottom");

            // 固定模式：使用指定的固定形状，不随鼠标位置变化
            if (isFixed)
            {
                return CalculateFixedShape(fixedShape, baseWidth, golden, screenLength, isHorizontal);
            }

            // 动态模式：根据鼠标位置决定形状
            double halfRegion = regionRatio / 2.0;
            bool isCenter = (mousePos >= 0.5 - halfRegion) && (mousePos <= 0.5 + halfRegion);

            // 确定区域
            string region;
            if (isCenter) region = "Center";
            else if (mousePos < 0.5 - halfRegion) region = "Left";
            else region = "Right";

            // 读取该区域的自定义形状设置
            string customShape = SettingsManager.GetRegionShape(position, region);

            // 确定最终形状类型
            string shapeType;
            if (customShape != "Default" && !string.IsNullOrEmpty(customShape))
            {
                shapeType = customShape;
            }
            else
            {
                // 默认逻辑：中间方形，两侧长条
                shapeType = isCenter ? "Square" : (isHorizontal ? "StripH" : "StripV");
            }

            return CalculateByShapeType(shapeType, baseWidth, golden, screenLength, isHorizontal);
        }

        private static ShapeResult CalculateFixedShape(string shapeType, double baseWidth, double golden, double screenLength, bool isHorizontal)
        {
            return CalculateByShapeType(shapeType, baseWidth, golden, screenLength, isHorizontal);
        }

        private static ShapeResult CalculateByShapeType(string shapeType, double baseWidth, double golden, double screenLength, bool isHorizontal)
        {
            double targetWidth, targetHeight;
            bool isSquare = false;

            switch (shapeType)
            {
                case "Square":
                default:
                    // 方形：短边 = baseWidth * squareShortMult，长边 = 短边 * golden
                    double shortSide = baseWidth * SettingsManager.SquareShortSideMultiplier;
                    double longSide = shortSide * golden;
                    if (isHorizontal)
                    {
                        targetWidth = longSide;
                        targetHeight = shortSide;
                    }
                    else
                    {
                        targetWidth = shortSide;
                        targetHeight = longSide;
                    }
                    isSquare = true;
                    break;

                case "StripH":
                    // 横向长条
                    targetWidth = screenLength * SettingsManager.StripLengthRatio;
                    targetHeight = baseWidth;
                    break;

                case "StripV":
                    // 纵向长条
                    targetWidth = baseWidth;
                    targetHeight = screenLength * SettingsManager.StripLengthRatio;
                    break;
            }

            targetWidth = Math.Max(20, targetWidth);
            targetHeight = Math.Max(20, targetHeight);

            return new ShapeResult
            {
                Width = targetWidth,
                Height = targetHeight,
                IsSquare = isSquare,
                IsHorizontal = isHorizontal,
                ShapeType = shapeType
            };
        }

        /// <summary>
        /// 获取固定位置模式下，某个形状对应的尺寸
        /// </summary>
        public static ShapeResult GetFixedShapeResult(string position, string shapeType, double taskbarHeight)
        {
            double screenLength = position == "Top" || position == "Bottom"
                ? SystemParameters.PrimaryScreenWidth
                : SystemParameters.PrimaryScreenHeight;

            bool isHorizontal = (position == "Top" || position == "Bottom");
            double baseWidth = taskbarHeight * SettingsManager.StripWidthMultiplier;
            double golden = SettingsManager.GoldenRatio;

            return CalculateFixedShape(shapeType, baseWidth, golden, screenLength, isHorizontal);
        }
    }
}