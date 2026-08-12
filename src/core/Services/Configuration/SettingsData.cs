using System;

namespace DynamicBird.Core.Services.Configuration
{
    public class SettingsData
    {
        // ========== 触发位置 ==========
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

        // ========== 边行为模式 ==========
        public string? EdgeMode_Top { get; set; } = "Follow";
        public string? EdgeMode_Bottom { get; set; } = "Follow";
        public string? EdgeMode_Left { get; set; } = "Follow";
        public string? EdgeMode_Right { get; set; } = "Follow";

        // ========== 外观 ==========
        public string? BackgroundColor { get; set; } = "#2D2D2D";
        public string? TextColor { get; set; } = "#FFFFFF";
        public double Opacity { get; set; } = 0.85;
        public int CornerRadius { get; set; } = 16;
        public bool ShowSystemStatus { get; set; } = true;
        public string? CustomIconPath { get; set; } = "";

        // ========== 形状参数 ==========
        public double StripLengthRatio { get; set; } = 0.6;
        public double StripWidthMultiplier { get; set; } = 1.5;
        public double SquareShortSideMultiplier { get; set; } = 1.8;
        public double GoldenRatio { get; set; } = 1.618;
        public double TriggerRegionRatio { get; set; } = 1.0 / 3.0;

        // ========== 动画与布局 ==========
        public int AnimationDurationMs { get; set; } = 120;
        public double HorizontalLayoutThreshold { get; set; } = 3.0 / 7.0;
        public double TagWidth { get; set; } = 120;

        // ========== 自适应行为 ==========
        public bool AutoFitOnTrigger { get; set; } = true;

        // ========== 固定位置 ==========
        public string? FixedShape_Top { get; set; } = "Square";
        public string? FixedShape_Bottom { get; set; } = "Square";
        public string? FixedShape_Left { get; set; } = "Square";
        public string? FixedShape_Right { get; set; } = "Square";
        public double FixedOffset_Top { get; set; } = 0;
        public double FixedOffset_Bottom { get; set; } = 0;
        public double FixedOffset_Left { get; set; } = 0;
        public double FixedOffset_Right { get; set; } = 0;

        // ========== 区域形状 ==========
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

        // ========== 模式切换 ==========
        public string? CurrentMode { get; set; } = "Taskbar";

        // ========== 剪贴板与便签 ==========
        public int ClipboardMaxCount { get; set; } = 10;
        public int ClipboardDisplayLength { get; set; } = 100;
        public string? LastWidgetTab { get; set; } = "Clipboard";
        public string? DefaultNoteColor { get; set; } = "#FFFF99";
        public bool NoteShowTitleByDefault { get; set; } = true;

        // ========== 区域模式配置 ==========
        public string? TaskbarRegionMode { get; set; } = "Taskbar";
        public string? WidgetRegionMode { get; set; } = "Widget";
        public string? CenterRegionMode { get; set; } = "AppHelper";
        public string? CornerRegionMode { get; set; } = "Taskbar";

        // ========== 面板尺寸 ==========
        public double PanelWidth { get; set; } = 120;
        public double PanelHeight { get; set; } = 350;
        public bool UseAutoSize { get; set; } = true;

        // ========== 鼠标离开判定 ==========
        public string? ResolutionPreset { get; set; } = "1080p";
        public string? DpiScalePreset { get; set; } = "150%";

        // ========== 勿扰模式 ==========
        public bool RememberDndMode { get; set; } = false;
        public bool DndModeEnabled { get; set; } = false;

        // ========== 任务栏 ==========
        public double TaskbarIconSize { get; set; } = 28.0;
        public double DividerOffset { get; set; } = 0.4;

        // ========== 12个独立区域尺寸 ==========
        public double UserWidth_Top_Left { get; set; } = 0;
        public double UserHeight_Top_Left { get; set; } = 0;
        public double UserWidth_Top_Center { get; set; } = 0;
        public double UserHeight_Top_Center { get; set; } = 0;
        public double UserWidth_Top_Right { get; set; } = 0;
        public double UserHeight_Top_Right { get; set; } = 0;
        public double UserWidth_Bottom_Left { get; set; } = 0;
        public double UserHeight_Bottom_Left { get; set; } = 0;
        public double UserWidth_Bottom_Center { get; set; } = 0;
        public double UserHeight_Bottom_Center { get; set; } = 0;
        public double UserWidth_Bottom_Right { get; set; } = 0;
        public double UserHeight_Bottom_Right { get; set; } = 0;
        public double UserWidth_Left_Top { get; set; } = 0;
        public double UserHeight_Left_Top { get; set; } = 0;
        public double UserWidth_Left_Center { get; set; } = 0;
        public double UserHeight_Left_Center { get; set; } = 0;
        public double UserWidth_Left_Bottom { get; set; } = 0;
        public double UserHeight_Left_Bottom { get; set; } = 0;
        public double UserWidth_Right_Top { get; set; } = 0;
        public double UserHeight_Right_Top { get; set; } = 0;
        public double UserWidth_Right_Center { get; set; } = 0;
        public double UserHeight_Right_Center { get; set; } = 0;
        public double UserWidth_Right_Bottom { get; set; } = 0;
        public double UserHeight_Right_Bottom { get; set; } = 0;

        // ========== 四角区域尺寸 ==========
        public double UserWidth_Corner_TopLeft { get; set; } = 0;
        public double UserHeight_Corner_TopLeft { get; set; } = 0;
        public double UserWidth_Corner_TopRight { get; set; } = 0;
        public double UserHeight_Corner_TopRight { get; set; } = 0;
        public double UserWidth_Corner_BottomLeft { get; set; } = 0;
        public double UserHeight_Corner_BottomLeft { get; set; } = 0;
        public double UserWidth_Corner_BottomRight { get; set; } = 0;
        public double UserHeight_Corner_BottomRight { get; set; } = 0;

        // ========== 动画设置 ==========
        public bool AnimationsEnabled { get; set; } = true;
        public string ShowHideEasingType { get; set; } = "CubicEase";
        public int ShowHideDurationMs { get; set; } = 150;      // ★ 从 300 改为 150
        public string TransformEasingType { get; set; } = "CubicEase";
        public int TransformDurationMs { get; set; } = 250;
        public int HideDelayMs { get; set; } = 200;            // ★ 从 300 改为 200
        public int FlyDurationMs { get; set; } = 500;

        // ========== 小鸟依人模式 ==========
        public bool ClingModeEnabled { get; set; } = false;

        // ========== 区域防抖延迟 ==========
        public int RegionDebounceMs { get; set; } = 80;
    }
}