using System;
using System.IO;
using System.Text.Json;

namespace LingDongBird.Core
{
    /// <summary>
    /// 配置数据结构
    /// </summary>
    public class SettingsData
    {
        public string? TriggerMode { get; set; } = "EdgeFollow";
        public string? TriggerPosition { get; set; } = "BottomRight";
        public bool Edge_Top { get; set; } = true;
        public bool Edge_Bottom { get; set; } = true;
        public bool Edge_Left { get; set; } = true;
        public bool Edge_Right { get; set; } = true;
        public bool Corner_TopLeft { get; set; } = true;
        public bool Corner_TopRight { get; set; } = true;
        public bool Corner_BottomLeft { get; set; } = true;
        public bool Corner_BottomRight { get; set; } = true;
        public string? EdgeMode_Top { get; set; } = "Follow";
        public string? EdgeMode_Bottom { get; set; } = "Follow";
        public string? EdgeMode_Left { get; set; } = "Follow";
        public string? EdgeMode_Right { get; set; } = "Follow";
        public string? BackgroundColor { get; set; } = "#2D2D2D";
        public string? TextColor { get; set; } = "#FFFFFF";
        public double Opacity { get; set; } = 0.85;
        public int CornerRadius { get; set; } = 16;
        public bool ShowSystemStatus { get; set; } = true;
        public double StripLengthRatio { get; set; } = 0.6;
        public double StripWidthMultiplier { get; set; } = 1.5;
        public double SquareShortSideMultiplier { get; set; } = 1.8;
        public double GoldenRatio { get; set; } = 1.618;
        public int AnimationDurationMs { get; set; } = 50;
        public double TriggerRegionRatio { get; set; } = 1.0 / 3.0;
        public string? FixedShape_Top { get; set; } = "Square";
        public string? FixedShape_Bottom { get; set; } = "Square";
        public string? FixedShape_Left { get; set; } = "Square";
        public string? FixedShape_Right { get; set; } = "Square";
        public double FixedOffset_Top { get; set; } = 0;
        public double FixedOffset_Bottom { get; set; } = 0;
        public double FixedOffset_Left { get; set; } = 0;
        public double FixedOffset_Right { get; set; } = 0;
        public string? Region_Top_Left { get; set; } = "Default";
        public string? Region_Top_Center { get; set; } = "Default";
        public string? Region_Top_Right { get; set; } = "Default";
        public string? Region_Bottom_Left { get; set; } = "Default";
        public string? Region_Bottom_Center { get; set; } = "Default";
        public string? Region_Bottom_Right { get; set; } = "Default";
        public string? Region_Left_Top { get; set; } = "Default";
        public string? Region_Left_Center { get; set; } = "Default";
        public string? Region_Left_Bottom { get; set; } = "Default";
        public string? Region_Right_Top { get; set; } = "Default";
        public string? Region_Right_Center { get; set; } = "Default";
        public string? Region_Right_Bottom { get; set; } = "Default";
        public double HorizontalLayoutThreshold { get; set; } = 3.0 / 7.0;
        public double TagWidth { get; set; } = 120;
        public string? CustomIconPath { get; set; } = "";
        public string? CurrentMode { get; set; } = "Taskbar";
    }

    public static class SettingsManager
    {
        private static readonly string ConfigPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "config.json"
        );

        private static SettingsData _data = new SettingsData();
        private static bool _isLoaded = false;

        static SettingsManager()
        {
            Load();
        }

        private static void Load()
        {
            if (File.Exists(ConfigPath))
            {
                try
                {
                    string json = File.ReadAllText(ConfigPath);
                    var data = JsonSerializer.Deserialize<SettingsData>(json);
                    if (data != null)
                    {
                        _data = data;
                        _isLoaded = true;
                        return;
                    }
                }
                catch { }
            }
            _data = new SettingsData();
            _isLoaded = true;
        }

        private static void Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigPath, json);
            }
            catch { }
        }

        // ----- 所有属性 -----
        public static string TriggerMode
        {
            get => _data.TriggerMode ?? "EdgeFollow";
            set { _data.TriggerMode = value; Save(); }
        }

        public static string TriggerPosition
        {
            get => _data.TriggerPosition ?? "BottomRight";
            set { _data.TriggerPosition = value; Save(); }
        }

        public static bool IsEdgeEnabled(string edge)
        {
            return edge switch
            {
                "Top" => _data.Edge_Top,
                "Bottom" => _data.Edge_Bottom,
                "Left" => _data.Edge_Left,
                "Right" => _data.Edge_Right,
                _ => true
            };
        }

        public static void SetEdgeEnabled(string edge, bool enabled)
        {
            switch (edge)
            {
                case "Top": _data.Edge_Top = enabled; break;
                case "Bottom": _data.Edge_Bottom = enabled; break;
                case "Left": _data.Edge_Left = enabled; break;
                case "Right": _data.Edge_Right = enabled; break;
                default: return;
            }
            Save();
        }

        public static bool IsCornerEnabled(string corner)
        {
            return corner switch
            {
                "TopLeft" => _data.Corner_TopLeft,
                "TopRight" => _data.Corner_TopRight,
                "BottomLeft" => _data.Corner_BottomLeft,
                "BottomRight" => _data.Corner_BottomRight,
                _ => true
            };
        }

        public static void SetCornerEnabled(string corner, bool enabled)
        {
            switch (corner)
            {
                case "TopLeft": _data.Corner_TopLeft = enabled; break;
                case "TopRight": _data.Corner_TopRight = enabled; break;
                case "BottomLeft": _data.Corner_BottomLeft = enabled; break;
                case "BottomRight": _data.Corner_BottomRight = enabled; break;
                default: return;
            }
            Save();
        }

        public static string GetEdgeMode(string edge)
        {
            return edge switch
            {
                "Top" => _data.EdgeMode_Top ?? "Follow",
                "Bottom" => _data.EdgeMode_Bottom ?? "Follow",
                "Left" => _data.EdgeMode_Left ?? "Follow",
                "Right" => _data.EdgeMode_Right ?? "Follow",
                _ => "Follow"
            };
        }

        public static void SetEdgeMode(string edge, string mode)
        {
            switch (edge)
            {
                case "Top": _data.EdgeMode_Top = mode; break;
                case "Bottom": _data.EdgeMode_Bottom = mode; break;
                case "Left": _data.EdgeMode_Left = mode; break;
                case "Right": _data.EdgeMode_Right = mode; break;
                default: return;
            }
            Save();
        }

        public static string BackgroundColor
        {
            get => _data.BackgroundColor ?? "#2D2D2D";
            set { _data.BackgroundColor = value; Save(); }
        }

        public static string TextColor
        {
            get => _data.TextColor ?? "#FFFFFF";
            set { _data.TextColor = value; Save(); }
        }

        public static double Opacity
        {
            get => _data.Opacity;
            set { _data.Opacity = Math.Max(0, Math.Min(1, value)); Save(); }
        }

        public static int CornerRadius
        {
            get => _data.CornerRadius;
            set { _data.CornerRadius = Math.Max(0, Math.Min(50, value)); Save(); }
        }

        public static bool ShowSystemStatus
        {
            get => _data.ShowSystemStatus;
            set { _data.ShowSystemStatus = value; Save(); }
        }

        public static double StripLengthRatio
        {
            get => _data.StripLengthRatio;
            set { _data.StripLengthRatio = Math.Max(0.1, Math.Min(1.0, value)); Save(); }
        }

        public static double StripWidthMultiplier
        {
            get => _data.StripWidthMultiplier;
            set { _data.StripWidthMultiplier = Math.Max(0.5, Math.Min(3.0, value)); Save(); }
        }

        public static double SquareShortSideMultiplier
        {
            get => _data.SquareShortSideMultiplier;
            set { _data.SquareShortSideMultiplier = Math.Max(1.0, Math.Min(4.0, value)); Save(); }
        }

        public static double GoldenRatio
        {
            get => _data.GoldenRatio;
            set { _data.GoldenRatio = Math.Max(1.0, Math.Min(3.0, value)); Save(); }
        }

        public static int AnimationDurationMs
        {
            get => _data.AnimationDurationMs;
            set { _data.AnimationDurationMs = Math.Max(10, Math.Min(500, value)); Save(); }
        }

        public static double TriggerRegionRatio
        {
            get => _data.TriggerRegionRatio;
            set { _data.TriggerRegionRatio = Math.Max(0.1, Math.Min(0.9, value)); Save(); }
        }

        public static string GetFixedShape(string edge)
        {
            return edge switch
            {
                "Top" => _data.FixedShape_Top ?? "Square",
                "Bottom" => _data.FixedShape_Bottom ?? "Square",
                "Left" => _data.FixedShape_Left ?? "Square",
                "Right" => _data.FixedShape_Right ?? "Square",
                _ => "Square"
            };
        }

        public static void SetFixedShape(string edge, string shape)
        {
            switch (edge)
            {
                case "Top": _data.FixedShape_Top = shape; break;
                case "Bottom": _data.FixedShape_Bottom = shape; break;
                case "Left": _data.FixedShape_Left = shape; break;
                case "Right": _data.FixedShape_Right = shape; break;
                default: return;
            }
            Save();
        }

        public static double GetFixedOffset(string edge)
        {
            return edge switch
            {
                "Top" => _data.FixedOffset_Top,
                "Bottom" => _data.FixedOffset_Bottom,
                "Left" => _data.FixedOffset_Left,
                "Right" => _data.FixedOffset_Right,
                _ => 0
            };
        }

        public static void SetFixedOffset(string edge, double offset)
        {
            switch (edge)
            {
                case "Top": _data.FixedOffset_Top = Math.Max(0, offset); break;
                case "Bottom": _data.FixedOffset_Bottom = Math.Max(0, offset); break;
                case "Left": _data.FixedOffset_Left = Math.Max(0, offset); break;
                case "Right": _data.FixedOffset_Right = Math.Max(0, offset); break;
                default: return;
            }
            Save();
        }

        public static string GetRegionShape(string edge, string region)
        {
            return region switch
            {
                "Left" when edge == "Top" => _data.Region_Top_Left ?? "Default",
                "Center" when edge == "Top" => _data.Region_Top_Center ?? "Default",
                "Right" when edge == "Top" => _data.Region_Top_Right ?? "Default",
                "Left" when edge == "Bottom" => _data.Region_Bottom_Left ?? "Default",
                "Center" when edge == "Bottom" => _data.Region_Bottom_Center ?? "Default",
                "Right" when edge == "Bottom" => _data.Region_Bottom_Right ?? "Default",
                "Top" when edge == "Left" => _data.Region_Left_Top ?? "Default",
                "Center" when edge == "Left" => _data.Region_Left_Center ?? "Default",
                "Bottom" when edge == "Left" => _data.Region_Left_Bottom ?? "Default",
                "Top" when edge == "Right" => _data.Region_Right_Top ?? "Default",
                "Center" when edge == "Right" => _data.Region_Right_Center ?? "Default",
                "Bottom" when edge == "Right" => _data.Region_Right_Bottom ?? "Default",
                _ => "Default"
            };
        }

        public static void SetRegionShape(string edge, string region, string shape)
        {
            switch (region)
            {
                case "Left" when edge == "Top": _data.Region_Top_Left = shape; break;
                case "Center" when edge == "Top": _data.Region_Top_Center = shape; break;
                case "Right" when edge == "Top": _data.Region_Top_Right = shape; break;
                case "Left" when edge == "Bottom": _data.Region_Bottom_Left = shape; break;
                case "Center" when edge == "Bottom": _data.Region_Bottom_Center = shape; break;
                case "Right" when edge == "Bottom": _data.Region_Bottom_Right = shape; break;
                case "Top" when edge == "Left": _data.Region_Left_Top = shape; break;
                case "Center" when edge == "Left": _data.Region_Left_Center = shape; break;
                case "Bottom" when edge == "Left": _data.Region_Left_Bottom = shape; break;
                case "Top" when edge == "Right": _data.Region_Right_Top = shape; break;
                case "Center" when edge == "Right": _data.Region_Right_Center = shape; break;
                case "Bottom" when edge == "Right": _data.Region_Right_Bottom = shape; break;
                default: return;
            }
            Save();
        }

        public static double HorizontalLayoutThreshold
        {
            get => _data.HorizontalLayoutThreshold;
            set { _data.HorizontalLayoutThreshold = Math.Max(0.1, Math.Min(1.0, value)); Save(); }
        }

        public static double TagWidth
        {
            get => _data.TagWidth;
            set { _data.TagWidth = Math.Max(40, Math.Min(400, value)); Save(); }
        }

        public static string CustomIconPath
        {
            get => _data.CustomIconPath ?? "";
            set { _data.CustomIconPath = value; Save(); }
        }

        public static string CurrentMode
        {
            get => _data.CurrentMode ?? "Taskbar";
            set
            {
                if (_data.CurrentMode == value) return;
                _data.CurrentMode = value;
                Save();
            }
        }
    }
}