using System;
using DynamicBird.Core.Infrastructure.Logging;
using DynamicBird.Core.Infrastructure.Service;

namespace DynamicBird.Core.Services.Configuration
{
    public class SettingsManager : ISettingsService, IService
    {
        private SettingsData _data;
        private readonly object _lock = new object();

        public event Action? SettingsChanged;

        public string Name => "SettingsManager";
        public bool IsInitialized { get; private set; } = false;

        public SettingsManager()
        {
            _data = SettingsFileManager.Load();
        }

        public void Initialize()
        {
            if (IsInitialized) return;
            Reload();
            IsInitialized = true;
            LogManager.Debug("SettingsManager 初始化完成");
        }

        public void Shutdown()
        {
            if (!IsInitialized) return;
            Save();
            IsInitialized = false;
            LogManager.Debug("SettingsManager 已关闭");
        }

        public void Reload()
        {
            lock (_lock)
            {
                _data = SettingsFileManager.Load();
                SettingsChanged?.Invoke();
            }
        }

        private void Save()
        {
            lock (_lock)
            {
                SettingsFileManager.Save(_data);
                SettingsChanged?.Invoke();
            }
        }

        public bool IsEdgeEnabled(string edge)
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

        public void SetEdgeEnabled(string edge, bool enabled)
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

        public bool IsCornerEnabled(string corner)
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

        public void SetCornerEnabled(string corner, bool enabled)
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

        // ========== 边行为模式 ==========
        public string GetEdgeMode(string edge)
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

        public void SetEdgeMode(string edge, string mode)
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

        // ========== 外观 ==========
        public string BackgroundColor
        {
            get => _data.BackgroundColor ?? "#2D2D2D";
            set { _data.BackgroundColor = value; Save(); }
        }

        public string TextColor
        {
            get => _data.TextColor ?? "#FFFFFF";
            set { _data.TextColor = value; Save(); }
        }

        public double Opacity
        {
            get => _data.Opacity;
            set { _data.Opacity = Math.Max(0, Math.Min(1, value)); Save(); }
        }

        public int CornerRadius
        {
            get => _data.CornerRadius;
            set { _data.CornerRadius = Math.Max(0, Math.Min(50, value)); Save(); }
        }

        public bool ShowSystemStatus
        {
            get => _data.ShowSystemStatus;
            set { _data.ShowSystemStatus = value; Save(); }
        }

        public string CustomIconPath
        {
            get => _data.CustomIconPath ?? "";
            set { _data.CustomIconPath = value; Save(); }
        }

        // ========== 形状参数 ==========
        public double StripLengthRatio
        {
            get => _data.StripLengthRatio;
            set { _data.StripLengthRatio = Math.Max(0.1, Math.Min(1.0, value)); Save(); }
        }

        public double StripWidthMultiplier
        {
            get => _data.StripWidthMultiplier;
            set { _data.StripWidthMultiplier = Math.Max(0.5, Math.Min(3.0, value)); Save(); }
        }

        public double SquareShortSideMultiplier
        {
            get => _data.SquareShortSideMultiplier;
            set { _data.SquareShortSideMultiplier = Math.Max(1.0, Math.Min(4.0, value)); Save(); }
        }

        public double GoldenRatio
        {
            get => _data.GoldenRatio;
            set { _data.GoldenRatio = Math.Max(1.0, Math.Min(3.0, value)); Save(); }
        }

        public double TriggerRegionRatio
        {
            get => _data.TriggerRegionRatio;
            set { _data.TriggerRegionRatio = Math.Max(0.1, Math.Min(0.5, value)); Save(); }
        }

        public double HorizontalLayoutThreshold
        {
            get => _data.HorizontalLayoutThreshold;
            set { _data.HorizontalLayoutThreshold = Math.Max(0.1, Math.Min(1.0, value)); Save(); }
        }

        public double TagWidth
        {
            get => _data.TagWidth;
            set { _data.TagWidth = Math.Max(40, Math.Min(400, value)); Save(); }
        }

        // ========== 自适应行为 ==========
        public bool AutoFitOnTrigger
        {
            get => _data.AutoFitOnTrigger;
            set { _data.AutoFitOnTrigger = value; Save(); }
        }

        // ========== 固定位置 ==========
        public string GetFixedShape(string edge)
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

        public void SetFixedShape(string edge, string shape)
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

        public double GetFixedOffset(string edge)
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

        public void SetFixedOffset(string edge, double offset)
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

        // ========== 区域形状 ==========
        public string GetRegionShape(string edge, string region)
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

        public void SetRegionShape(string edge, string region, string shape)
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

        // ========== 剪贴板与便签 ==========
        public int ClipboardMaxCount
        {
            get => _data.ClipboardMaxCount;
            set { _data.ClipboardMaxCount = Math.Max(1, Math.Min(50, value)); Save(); }
        }

        public int ClipboardDisplayLength
        {
            get => _data.ClipboardDisplayLength;
            set { _data.ClipboardDisplayLength = Math.Max(10, Math.Min(500, value)); Save(); }
        }

        public string LastWidgetTab
        {
            get => _data.LastWidgetTab ?? "Clipboard";
            set { _data.LastWidgetTab = value; Save(); }
        }

        public string DefaultNoteColor
        {
            get => _data.DefaultNoteColor ?? "#FFFF99";
            set { _data.DefaultNoteColor = value; Save(); }
        }

        public bool NoteShowTitleByDefault
        {
            get => _data.NoteShowTitleByDefault;
            set { _data.NoteShowTitleByDefault = value; Save(); }
        }

        public bool UseAutoSize
        {
            get => _data.UseAutoSize;
            set { _data.UseAutoSize = value; Save(); }
        }

        // ========== 自动更新（GitHub Releases） ==========
        public bool AutoCheckUpdate
        {
            get => _data.AutoCheckUpdate;
            set { _data.AutoCheckUpdate = value; Save(); }
        }

        public bool OnboardingCompleted
        {
            get => _data.OnboardingCompleted;
            set { _data.OnboardingCompleted = value; Save(); }
        }

        // ========== 勿扰模式 ==========
        public bool RememberDndMode
        {
            get => _data.RememberDndMode;
            set { _data.RememberDndMode = value; Save(); }
        }

        public bool DndModeEnabled
        {
            get => _data.DndModeEnabled;
            set { _data.DndModeEnabled = value; Save(); }
        }

        // ========== 任务栏 ==========
        public double TaskbarIconSize
        {
            get => _data.TaskbarIconSize;
            set
            {
                _data.TaskbarIconSize = Math.Max(16, Math.Min(48, value));
                Save();
            }
        }

        public double DividerOffset
        {
            get => _data.DividerOffset;
            set
            {
                _data.DividerOffset = Math.Max(0.1, Math.Min(0.9, value));
                Save();
            }
        }

        // ========== 16个独立区域尺寸（含四角） ==========
        public (double width, double height) GetUserSize(string regionKey)
        {
            return regionKey switch
            {
                // 12个边缘区域
                "Top_Left" => (_data.UserWidth_Top_Left, _data.UserHeight_Top_Left),
                "Top_Center" => (_data.UserWidth_Top_Center, _data.UserHeight_Top_Center),
                "Top_Right" => (_data.UserWidth_Top_Right, _data.UserHeight_Top_Right),
                "Bottom_Left" => (_data.UserWidth_Bottom_Left, _data.UserHeight_Bottom_Left),
                "Bottom_Center" => (_data.UserWidth_Bottom_Center, _data.UserHeight_Bottom_Center),
                "Bottom_Right" => (_data.UserWidth_Bottom_Right, _data.UserHeight_Bottom_Right),
                "Left_Top" => (_data.UserWidth_Left_Top, _data.UserHeight_Left_Top),
                "Left_Center" => (_data.UserWidth_Left_Center, _data.UserHeight_Left_Center),
                "Left_Bottom" => (_data.UserWidth_Left_Bottom, _data.UserHeight_Left_Bottom),
                "Right_Top" => (_data.UserWidth_Right_Top, _data.UserHeight_Right_Top),
                "Right_Center" => (_data.UserWidth_Right_Center, _data.UserHeight_Right_Center),
                "Right_Bottom" => (_data.UserWidth_Right_Bottom, _data.UserHeight_Right_Bottom),
                // ★★★ 4个角落区域 ★★★
                "TopLeft" => (_data.UserWidth_Corner_TopLeft, _data.UserHeight_Corner_TopLeft),
                "TopRight" => (_data.UserWidth_Corner_TopRight, _data.UserHeight_Corner_TopRight),
                "BottomLeft" => (_data.UserWidth_Corner_BottomLeft, _data.UserHeight_Corner_BottomLeft),
                "BottomRight" => (_data.UserWidth_Corner_BottomRight, _data.UserHeight_Corner_BottomRight),
                _ => (0, 0)
            };
        }

        public void SetUserSize(string regionKey, double width, double height)
        {
            switch (regionKey)
            {
                // 12个边缘区域
                case "Top_Left": _data.UserWidth_Top_Left = width; _data.UserHeight_Top_Left = height; break;
                case "Top_Center": _data.UserWidth_Top_Center = width; _data.UserHeight_Top_Center = height; break;
                case "Top_Right": _data.UserWidth_Top_Right = width; _data.UserHeight_Top_Right = height; break;
                case "Bottom_Left": _data.UserWidth_Bottom_Left = width; _data.UserHeight_Bottom_Left = height; break;
                case "Bottom_Center": _data.UserWidth_Bottom_Center = width; _data.UserHeight_Bottom_Center = height; break;
                case "Bottom_Right": _data.UserWidth_Bottom_Right = width; _data.UserHeight_Bottom_Right = height; break;
                case "Left_Top": _data.UserWidth_Left_Top = width; _data.UserHeight_Left_Top = height; break;
                case "Left_Center": _data.UserWidth_Left_Center = width; _data.UserHeight_Left_Center = height; break;
                case "Left_Bottom": _data.UserWidth_Left_Bottom = width; _data.UserHeight_Left_Bottom = height; break;
                case "Right_Top": _data.UserWidth_Right_Top = width; _data.UserHeight_Right_Top = height; break;
                case "Right_Center": _data.UserWidth_Right_Center = width; _data.UserHeight_Right_Center = height; break;
                case "Right_Bottom": _data.UserWidth_Right_Bottom = width; _data.UserHeight_Right_Bottom = height; break;
                // ★★★ 4个角落区域 ★★★
                case "TopLeft": _data.UserWidth_Corner_TopLeft = width; _data.UserHeight_Corner_TopLeft = height; break;
                case "TopRight": _data.UserWidth_Corner_TopRight = width; _data.UserHeight_Corner_TopRight = height; break;
                case "BottomLeft": _data.UserWidth_Corner_BottomLeft = width; _data.UserHeight_Corner_BottomLeft = height; break;
                case "BottomRight": _data.UserWidth_Corner_BottomRight = width; _data.UserHeight_Corner_BottomRight = height; break;
                default: return;
            }
            Save();
        }

        // ========== 动画设置 ==========
        public bool AnimationsEnabled
        {
            get => _data.AnimationsEnabled;
            set { _data.AnimationsEnabled = value; Save(); }
        }

        public string ShowHideEasingType
        {
            get => _data.ShowHideEasingType ?? "CubicEase";
            set { _data.ShowHideEasingType = value; Save(); }
        }

        public int ShowHideDurationMs
        {
            get => _data.ShowHideDurationMs;
            set { _data.ShowHideDurationMs = Math.Max(100, Math.Min(800, value)); Save(); }
        }

        public string TransformEasingType
        {
            get => _data.TransformEasingType ?? "CubicEase";
            set { _data.TransformEasingType = value; Save(); }
        }

        public int TransformDurationMs
        {
            get => _data.TransformDurationMs;
            set { _data.TransformDurationMs = Math.Max(100, Math.Min(600, value)); Save(); }
        }

        public int HideDelayMs
        {
            get => _data.HideDelayMs;
            // ★ 0 = 取消延时隐藏（鼠标一离开立即隐藏）
            set { _data.HideDelayMs = Math.Max(0, Math.Min(1000, value)); Save(); }
        }

        public int FlyDurationMs
        {
            get => _data.FlyDurationMs;
            set { _data.FlyDurationMs = Math.Max(100, Math.Min(2000, value)); Save(); }
        }

        // ========== 小鸟依人模式 ==========
        public bool ClingModeEnabled
        {
            get => _data.ClingModeEnabled;
            set { _data.ClingModeEnabled = value; Save(); }
        }

        // ★★★ 新增：区域防抖延迟 ★★★
        public int RegionDebounceMs
        {
            get => _data.RegionDebounceMs;
            set { _data.RegionDebounceMs = Math.Max(30, Math.Min(300, value)); Save(); }
        }

        public string GetRegionPanel(string regionKey)
        {
            return regionKey switch
            {
                "Top_Left" => _data.RegionPanel_Top_Left ?? "Default",
                "Top_Center" => _data.RegionPanel_Top_Center ?? "Default",
                "Top_Right" => _data.RegionPanel_Top_Right ?? "Default",
                "Bottom_Left" => _data.RegionPanel_Bottom_Left ?? "Default",
                "Bottom_Center" => _data.RegionPanel_Bottom_Center ?? "Default",
                "Bottom_Right" => _data.RegionPanel_Bottom_Right ?? "Default",
                "Left_Top" => _data.RegionPanel_Left_Top ?? "Default",
                "Left_Center" => _data.RegionPanel_Left_Center ?? "Default",
                "Left_Bottom" => _data.RegionPanel_Left_Bottom ?? "Default",
                "Right_Top" => _data.RegionPanel_Right_Top ?? "Default",
                "Right_Center" => _data.RegionPanel_Right_Center ?? "Default",
                "Right_Bottom" => _data.RegionPanel_Right_Bottom ?? "Default",
                "TopLeft" => _data.RegionPanel_TopLeft ?? "Default",
                "TopRight" => _data.RegionPanel_TopRight ?? "Default",
                "BottomLeft" => _data.RegionPanel_BottomLeft ?? "Default",
                "BottomRight" => _data.RegionPanel_BottomRight ?? "Default",
                _ => "Default"
            };
        }

        public void SetRegionPanel(string regionKey, string panelType)
        {
            switch (regionKey)
            {
                case "Top_Left": _data.RegionPanel_Top_Left = panelType; break;
                case "Top_Center": _data.RegionPanel_Top_Center = panelType; break;
                case "Top_Right": _data.RegionPanel_Top_Right = panelType; break;
                case "Bottom_Left": _data.RegionPanel_Bottom_Left = panelType; break;
                case "Bottom_Center": _data.RegionPanel_Bottom_Center = panelType; break;
                case "Bottom_Right": _data.RegionPanel_Bottom_Right = panelType; break;
                case "Left_Top": _data.RegionPanel_Left_Top = panelType; break;
                case "Left_Center": _data.RegionPanel_Left_Center = panelType; break;
                case "Left_Bottom": _data.RegionPanel_Left_Bottom = panelType; break;
                case "Right_Top": _data.RegionPanel_Right_Top = panelType; break;
                case "Right_Center": _data.RegionPanel_Right_Center = panelType; break;
                case "Right_Bottom": _data.RegionPanel_Right_Bottom = panelType; break;
                case "TopLeft": _data.RegionPanel_TopLeft = panelType; break;
                case "TopRight": _data.RegionPanel_TopRight = panelType; break;
                case "BottomLeft": _data.RegionPanel_BottomLeft = panelType; break;
                case "BottomRight": _data.RegionPanel_BottomRight = panelType; break;
                default: return;
            }
            Save();
        }
    }
}
