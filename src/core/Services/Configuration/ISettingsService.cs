using System;

namespace ShoreHue.Core.Services.Configuration
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
        string WebWidgetUrl { get; set; }
        System.Collections.Generic.List<ShoreHue.Core.Services.Configuration.WebBookmark> WebBookmarks { get; set; }

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
        int ClipboardImageMaxWidth { get; set; }
        int ClipboardImageCacheLimitMB { get; set; }
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
        // ★ 触发/隐藏动画（类型 + 时长 + 特化参数）
        string ShowAnimationType { get; set; }
        int ShowAnimationDurationMs { get; set; }
        double ShowAnimationZoomFrom { get; set; }
        int ShowAnimationOscillations { get; set; }
        double ShowAnimationSpringiness { get; set; }
        string HideAnimationType { get; set; }
        int HideAnimationDurationMs { get; set; }
        double HideAnimationZoomTo { get; set; }
        int HideAnimationOscillations { get; set; }
        double HideAnimationSpringiness { get; set; }
        string TransformEasingType { get; set; }
        int TransformDurationMs { get; set; }
        int HideDelayMs { get; set; }
        int FlyDurationMs { get; set; }
        // ========== 逐区域动画覆盖（动画页签「动画应用于」） ==========
        /// <summary>取某区域的动画覆盖（null = 该区域无覆盖，完全跟随全局）。</summary>
        ShoreHue.Core.Models.RegionAnimationOverride? GetRegionAnimation(string regionKey);

        /// <summary>设置某区域的动画覆盖（null = 清除覆盖，恢复继承全局）。</summary>
        void SetRegionAnimation(string regionKey, ShoreHue.Core.Models.RegionAnimationOverride? ov);

        /// <summary>解析后的触发动画类型：区域覆盖优先，缺省用全局。</summary>
        string GetResolvedShowAnimationType(string regionKey);

        /// <summary>解析后的触发动画时长（ms）：区域覆盖优先，缺省用全局。</summary>
        int GetResolvedShowAnimationDurationMs(string regionKey);

        /// <summary>解析后的隐藏动画类型：区域覆盖优先，缺省用全局。</summary>
        string GetResolvedHideAnimationType(string regionKey);

        /// <summary>解析后的隐藏动画时长（ms）：区域覆盖优先，缺省用全局。</summary>
        int GetResolvedHideAnimationDurationMs(string regionKey);

        // ========== 编程模式（海床） ==========
        bool ProgrammingModeEnabled { get; set; }
        System.Collections.Generic.List<ShoreHue.Core.Models.CustomPanelDefinition> CustomPanels { get; set; }
        System.Collections.Generic.Dictionary<string, string> AppliedPresets { get; set; }

        /// <summary>
        /// 小鸟依人模式开关
        /// </summary>
        bool ClingModeEnabled { get; set; }

        /// <summary>贴边吸附范围（px）：面板边缘距屏幕边小于该值时磁铁吸附贴边（0=关闭）。</summary>
        int SnapRangePx { get; set; }

        /// <summary>内容切换稳定防抖（ms）：图标中置期间，无新切换保持该时长后内容归位显示。</summary>
        int ContentStabilizeMs { get; set; }

        /// <summary>面板点击穿透修饰键（None / Ctrl / Alt / Shift）：按住该键时点击可穿透面板。</summary>
        string? PassthroughModifier { get; set; }

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

        // ========== ShoreHue 性能模式 ==========
        string PerformanceMode { get; set; }
        void SetPerformanceMode(string mode);

        // ========== 面板运行帧率（fps，0=自动满帧） ==========
        int PanelFrameRate { get; set; }

        // ========== 全局界面字号缩放（0.75~1.5） ==========
        double UiFontScale { get; set; }

        // ========== 边缘触发距离与延时 ==========
        int TriggerDistancePx { get; set; }
        int TriggerDelayMs { get; set; }
        int GetTriggerDelay(string regionKey);
        void SetTriggerDelay(string regionKey, int ms);
        int GetHideDelay(string regionKey);
        void SetHideDelay(string regionKey, int ms);

        // ========== 小组件显示开关 ==========
        bool IsWidgetEnabled(string widgetKey);
        void SetWidgetEnabled(string widgetKey, bool enabled);

        // ========== 自定义状态栏显示项开关 ==========
        bool IsStatusProviderEnabled(string providerId);
        void SetStatusProviderEnabled(string providerId, bool enabled);

        // ========== 划词翻译 热键 ==========
        string TextAiHotkey { get; set; }

        // ========== 重新加载 / 保存 ==========
        void Reload();

        /// <summary>
        /// 立即保存设置并通知变化（实时保存入口）
        /// </summary>
        void SaveSettings();

        /// <summary>用一份完整 SettingsData 替换内部数据并落盘（设置窗口保存入口）。</summary>
        void Apply(SettingsData data);

        // ========== 事件 ==========
        event Action? SettingsChanged;
    }
}