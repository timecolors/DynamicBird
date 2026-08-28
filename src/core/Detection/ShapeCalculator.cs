using System;
using System.Windows;
using DynamicBird.Core.Services.Configuration;

namespace DynamicBird.Core.Detection
{
    /// <summary>
    /// 形状计算器（实例类）
    /// </summary>
    public class ShapeCalculator
    {
        private readonly ISettingsService _settings;

        public ShapeCalculator(ISettingsService settings)
        {
            _settings = settings;
        }

        public class ShapeResult
        {
            public double Width { get; set; }
            public double Height { get; set; }
            public bool IsSquare { get; set; }
            public bool IsHorizontal { get; set; }
            public string ShapeType { get; set; } = "Square";
            public string RegionType { get; set; } = "Taskbar";
        }

        public ShapeResult Calculate(string position, double mousePos, double screenLength, double taskbarHeight, bool isFixed = false, string fixedShape = "Square")
        {
            double stripLengthRatio = _settings.StripLengthRatio;
            double stripWidthMult = _settings.StripWidthMultiplier;
            double squareShortMult = _settings.SquareShortSideMultiplier;
            double golden = _settings.GoldenRatio;
            double regionRatio = _settings.TriggerRegionRatio;

            double baseWidth = taskbarHeight * stripWidthMult;
            bool isHorizontal = (position == "Top" || position == "Bottom");

            if (isFixed)
            {
                var result = CalculateFixedShape(fixedShape, baseWidth, golden, screenLength, isHorizontal);
                result.RegionType = isHorizontal ? "Taskbar" : "Widget";
                return result;
            }

            double halfRegion = regionRatio / 2.0;
            bool isCenter = (mousePos >= 0.5 - halfRegion) && (mousePos <= 0.5 + halfRegion);

            string region;
            if (isCenter) region = "Center";
            else if (mousePos < 0.5 - halfRegion) region = "Left";
            else region = "Right";

            string customShape = _settings.GetRegionShape(position, region);

            string shapeType;
            if (customShape != "Default" && !string.IsNullOrEmpty(customShape))
            {
                shapeType = customShape;
            }
            else
            {
                shapeType = isCenter ? "Square" : (isHorizontal ? "StripH" : "StripV");
            }

            var shapeResult = CalculateByShapeType(shapeType, baseWidth, golden, screenLength, isHorizontal);

            if (isCenter)
            {
                shapeResult.RegionType = "AppHelper";
            }
            else
            {
                bool isVerticalEdge = (position == "Left" || position == "Right");
                shapeResult.RegionType = isVerticalEdge ? "Widget" : "Taskbar";
            }

            return shapeResult;
        }

        private ShapeResult CalculateFixedShape(string shapeType, double baseWidth, double golden, double screenLength, bool isHorizontal)
        {
            return CalculateByShapeType(shapeType, baseWidth, golden, screenLength, isHorizontal);
        }

        private ShapeResult CalculateByShapeType(string shapeType, double baseWidth, double golden, double screenLength, bool isHorizontal)
        {
            double targetWidth, targetHeight;
            bool isSquare = false;

            switch (shapeType)
            {
                case "Square":
                default:
                    double shortSide = baseWidth * _settings.SquareShortSideMultiplier;
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
                    targetWidth = screenLength * _settings.StripLengthRatio;
                    targetHeight = baseWidth;
                    break;

                case "StripV":
                    targetWidth = baseWidth;
                    targetHeight = screenLength * _settings.StripLengthRatio;
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
                ShapeType = shapeType,
                RegionType = "Taskbar"
            };
        }

        public ShapeResult GetFixedShapeResult(string position, string shapeType, double taskbarHeight)
        {
            var wa = DynamicBird.Infrastructure.Utils.ScreenMetrics.GetCachedScreenForWindow(
                System.Windows.Application.Current?.MainWindow?.Left ?? 0,
                System.Windows.Application.Current?.MainWindow?.Top ?? 0,
                System.Windows.Application.Current?.MainWindow?.Width ?? 1920,
                System.Windows.Application.Current?.MainWindow?.Height ?? 1080);
            double screenLength = position == "Top" || position == "Bottom"
                ? wa.Width
                : wa.Height;

            bool isHorizontal = (position == "Top" || position == "Bottom");
            double baseWidth = taskbarHeight * _settings.StripWidthMultiplier;
            double golden = _settings.GoldenRatio;

            var result = CalculateFixedShape(shapeType, baseWidth, golden, screenLength, isHorizontal);
            result.RegionType = isHorizontal ? "Taskbar" : "Widget";
            return result;
        }
    }
}