using System;

namespace DynamicBird.Core.Services.Configuration
{
    public interface ISettingsService
    {
        bool IsEdgeEnabled(string edge);
        void SetEdgeEnabled(string edge, bool enabled);
        bool IsCornerEnabled(string corner);
        void SetCornerEnabled(string corner, bool enabled);

        // ========== 边行为模式 ==========
        string GetEdgeMode(string edge);
        void SetEdgeMode(string edge, string mode);

        // ========== 外观 ==========
        string BackgroundColor { get; set; }
        string TextColor { get; set; }
        double Opacity { get; set; }
        int CornerRadius { get; set; }
        bool ShowSystemStatus { get; set; }
        string CustomIconPath { get; set; }

        // ========== 形状参数 ==========
        double StripLengthRatio { get; set; }
        double StripWidthMultiplier { get; set; }
        double SquareShortSideMultiplier { get; set; }
        double GoldenRatio { get; set; }
        double TriggerRegionRatio { get; set; }

        // ========== 动画与布局 ==========
        double HorizontalLayoutThreshold { get; set; }
        double TagWidth { get; set; }

        // ========== 自适应行为 ==========
        bool AutoFitOnTrigger { get; set; }

        // ========== 固定位置 ==========
        string GetFixedShape(string edge);
        void SetFixedShape(string edge, string shape);
        double GetFixedOffset(string edge);
        void SetFixedOffset(string edge, double offset);

        // ========== 区域形状 ==========
        string GetRegionShape(string edge, string region);
        void SetRegionShape(string edge, string region, string shape);

        // ========== 剪贴板与便签 ==========
        int ClipboardMaxCount { get; set; }
        int ClipboardDisplayLength { get; set; }
        string LastWidgetTab { get; set; }
        string DefaultNoteColor { get; set; }
        bool NoteShowTitleByDefault { get; set; }

        // ========== 面板尺寸 ==========
        bool UseAutoSize { get; set; }

        // ========== 勿扰模式 ==========
        bool RememberDndMode { get; set; }
        bool DndModeEnabled { get; set; }

        // ========== 任务栏 ==========
        double TaskbarIconSize { get; set; }
        double DividerOffset { get; set; }

        // ========== 16个独立区域尺寸（含四角） ==========
        (double width, double height) GetUserSize(string regionKey);
        void SetUserSize(string regionKey, double width, double height);

        // ========== 动画设置 ==========
        bool AnimationsEnabled { get; set; }
        string ShowHideEasingType { get; set; }
        int ShowHideDurationMs { get; set; }
        string TransformEasingType { get; set; }
        int TransformDurationMs { get; set; }
        int HideDelayMs { get; set; }
        int FlyDurationMs { get; set; }

        /// <summary>
        /// 小鸟依人模式开关
        /// </summary>
        bool ClingModeEnabled { get; set; }

        /// <summary>
        /// ★★★ 区域防抖延迟（毫秒） ★★★
        /// </summary>
        int RegionDebounceMs { get; set; }

        /// <summary>
        /// 获取指定区域的自定义面板类型（"Default" 表示跟随默认布局）。
        /// </summary>
        string GetRegionPanel(string regionKey);

        /// <summary>
        /// 设置指定区域的自定义面板类型。
        /// </summary>
        void SetRegionPanel(string regionKey, string panelType);

        // ========== 自动更新 ==========
        bool AutoCheckUpdate { get; set; }

        /// <summary>是否已完成首次使用引导。</summary>
        bool OnboardingCompleted { get; set; }

        // ========== 状态栏显示项 ==========
        bool StatusShowTime { get; set; }
        bool StatusShowCpu { get; set; }
        bool StatusShowMemory { get; set; }
        bool StatusShowFps { get; set; }
        bool StatusShowVolume { get; set; }
        bool StatusShowNetwork { get; set; }
        bool StatusShowBattery { get; set; }
        bool StatusShowWeather { get; set; }

        // ========== 天气 ==========
        bool WeatherEnabled { get; set; }
        string? WeatherCity { get; set; }

        // ========== 重新加载 ==========
        void Reload();

        // ========== 事件 ==========
        event Action? SettingsChanged;
    }
}
