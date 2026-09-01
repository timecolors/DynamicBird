using System;
using System.Threading;
using DynamicBird.Core.Infrastructure.Logging;
using DynamicBird.Core.Infrastructure.Service;

namespace DynamicBird.Core.Services.Configuration
{
    public class SettingsManager : ISettingsService, IService
    {
        private SettingsData _data;
        private readonly object _lock = new object();
        private bool _applyingPreset; // 应用性能预设期间不触发"自定义"检测

        // ★ 防抖落盘：拖动滑块等高频 set 时，内存即时更新，但落盘与 SettingsChanged
        //   合并为 300ms 一次，避免写盘风暴与 UI 全量刷新风暴。
        private Timer? _saveTimer;
        private bool _saveDirty;
        private const int SaveDebounceMs = 300;

        public event Action? SettingsChanged;

        public string Name => "SettingsManager";
        public bool IsInitialized { get; private set; } = false;

        public SettingsManager()
        {
            _data = SettingsFileManager.Load();
            NormalizePanelKinds();
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
            FlushSaveNow(); // 关闭前强制落盘（防抖未触发时兜底）
            IsInitialized = false;
            LogManager.Debug("SettingsManager 已关闭");
        }

        public void Reload()
        {
            // ★ 刷新前先强制落盘内存中的待保存改动（防抖 300ms 内点刷新会丢改动：
            //   例如刚关掉小鸟依人未落盘，Reload 读到旧值又恢复开启）。
            FlushSaveNow();
            lock (_lock)
            {
                _data = SettingsFileManager.Load();
                NormalizePanelKinds();
                SettingsChanged?.Invoke();
            }
        }

        /// <summary>
        /// 统一自定义面板种类：BaseType=Widget → Kind=Widget（小组件变体，进小组件标签）；
        /// BaseType 为面板类型 → Kind=Panel（区域面板，进区域面板下拉）；其余不变。
        /// 修复旧版本（Kind 为空/Panel 混存）导致的位置错乱。
        /// </summary>
        private void NormalizePanelKinds()
        {
            if (_data.CustomPanels == null || _data.CustomPanels.Count == 0) return;
            bool changed = false;
            foreach (var p in _data.CustomPanels)
            {
                if (p.Kind == "Config" || p.Kind == "Category") continue;   // 配置代码项/新分类保持
                string bt = p.BaseType ?? "";
                if (bt == "Widget")
                {
                    if (p.Kind != "Widget") { p.Kind = "Widget"; changed = true; }
                }
                else if (!string.IsNullOrEmpty(bt) && bt != "Category")
                {
                    if (p.Kind != "Panel") { p.Kind = "Panel"; changed = true; }
                }
            }
            if (changed)
            {
                try { SettingsFileManager.Save(_data); } catch { }
            }
        }

        /// <summary>立即写入磁盘并通知设置变化（设置页实时保存入口）。</summary>
        public void SaveSettings()
        {
            Save();
        }

        /// <summary>
        /// 用一份完整的 SettingsData 替换内部数据并落盘。
        /// 设置窗口的 ApplyControlsToData 把控件值写入本地副本后调用此方法，
        /// 把副本整体同步进 SettingsManager（否则设置改动只改副本、不落盘，
        /// 刷新/重启后全部还原——曾导致"关掉小鸟依人刷新又开"）。
        /// </summary>
        public void Apply(SettingsData data)
        {
            // ★ 设置窗口保存入口：整体替换数据并立即落盘 + 立即通知。
            //   调用方（SaveSettingsNow）已自带防抖，这里不再叠加 300ms 延迟，
            //   保证"保存即生效"且 SettingsChanged 同步触发。
            lock (_lock)
            {
                _data = data;
                _saveDirty = true;
                _saveTimer?.Dispose();
                _saveTimer = null;
            }
            try
            {
                lock (_lock)
                {
                    SettingsFileManager.Save(_data);
                }
                NotifySettingsChanged();
            }
            catch (Exception ex)
            {
                LogManager.Error("设置落盘失败", ex);
            }
        }

        private void Save()
        {
            lock (_lock)
            {
                _saveDirty = true;
                if (_saveTimer == null)
                {
                    _saveTimer = new Timer(_ => FlushSaveNow(), null, SaveDebounceMs, Timeout.Infinite);
                }
                else
                {
                    _saveTimer.Change(SaveDebounceMs, Timeout.Infinite);
                }
            }
        }

        // ========== 属性样板瘦身辅助 ==========

        /// <summary>简单赋值 + 自动保存（替代 4 行样板 setter）。</summary>
        private void SetField<T>(Action<T> setter, T value)
        {
            setter(value);
            Save();
        }

        /// <summary>钳制赋值 + 自动保存（替代"Math.Max/Min + Save"样板）。</summary>
        private void SetField(Action<int> setter, int value, int min, int max)
        {
            setter(Math.Max(min, Math.Min(max, value)));
            Save();
        }

        /// <summary>钳制赋值 + 自动保存（double 版）。</summary>
        private void SetField(Action<double> setter, double value, double min, double max)
        {
            setter(Math.Max(min, Math.Min(max, value)));
            Save();
        }

        /// <summary>防抖到期/关闭时：落盘一次并触发一次 SettingsChanged。</summary>
        private void FlushSaveNow()
        {
            bool shouldSave;
            lock (_lock)
            {
                _saveTimer?.Dispose();
                _saveTimer = null;
                shouldSave = _saveDirty;
                _saveDirty = false;
            }
            if (!shouldSave) return;
            try
            {
                lock (_lock)
                {
                    SettingsFileManager.Save(_data);
                }
                NotifySettingsChanged();
            }
            catch (Exception ex)
            {
                LogManager.Error("设置落盘失败", ex);
            }
        }

        /// <summary>
        /// 触发 SettingsChanged（订阅者含 UI 刷新逻辑，必须在 UI 线程执行）。
        /// Timer 线程调用时封送回 WPF Dispatcher；无 Dispatcher 环境（单元测试）直接调用。
        /// </summary>
        private void NotifySettingsChanged()
        {
            var app = System.Windows.Application.Current;
            if (app != null &&
                app.Dispatcher != null &&
                !app.Dispatcher.CheckAccess())
            {
                app.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try { SettingsChanged?.Invoke(); } catch (Exception ex) { LogManager.Error("设置变更通知失败", ex); }
                }));
                return;
            }
            try { SettingsChanged?.Invoke(); } catch (Exception ex) { LogManager.Error("设置变更通知失败", ex); }
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
            set => SetField(v => _data.BackgroundColor = v, value);
        }

        public string TextColor
        {
            get => _data.TextColor ?? "#FFFFFF";
            set => SetField(v => _data.TextColor = v, value);
        }

        public double Opacity
        {
            get => _data.Opacity;
            set => SetField(v => _data.Opacity = v, value, 0, 1);
        }

        public int CornerRadius
        {
            get => _data.CornerRadius;
            set => SetField(v => _data.CornerRadius = v, value, 0, 50);
        }

        public bool ShowSystemStatus
        {
            get => _data.ShowSystemStatus;
            set => SetField(v => _data.ShowSystemStatus = v, value);
        }

        public string WebWidgetUrl
        {
            get => _data.WebWidgetUrl;
            set => SetField(v => _data.WebWidgetUrl = v, value);
        }

        public System.Collections.Generic.List<DynamicBird.Core.Services.Configuration.WebBookmark> WebBookmarks
        {
            get => _data.WebBookmarks;
            set => SetField(v => _data.WebBookmarks = v, value);
        }




        // ========== 形状参数 ==========
        public double StripLengthRatio
        {
            get => _data.StripLengthRatio;
            set => SetField(v => _data.StripLengthRatio = v, value, 0.1, 1.0);
        }

        public double StripWidthMultiplier
        {
            get => _data.StripWidthMultiplier;
            set => SetField(v => _data.StripWidthMultiplier = v, value, 0.5, 3.0);
        }

        public double SquareShortSideMultiplier
        {
            get => _data.SquareShortSideMultiplier;
            set => SetField(v => _data.SquareShortSideMultiplier = v, value, 1.0, 4.0);
        }

        public double GoldenRatio
        {
            get => _data.GoldenRatio;
            set => SetField(v => _data.GoldenRatio = v, value, 1.0, 3.0);
        }

        public double TriggerRegionRatio
        {
            get => _data.TriggerRegionRatio;
            set => SetField(v => _data.TriggerRegionRatio = v, value, 0.1, 0.5);
        }

        public double HorizontalLayoutThreshold
        {
            get => _data.HorizontalLayoutThreshold;
            set => SetField(v => _data.HorizontalLayoutThreshold = v, value, 0.1, 1.0);
        }

        public double TagWidth
        {
            get => _data.TagWidth;
            set => SetField(v => _data.TagWidth = v, value, 40, 400);
        }

        // ========== 自适应行为 ==========
        public bool AutoFitOnTrigger
        {
            get => _data.AutoFitOnTrigger;
            set => SetField(v => _data.AutoFitOnTrigger = v, value);
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
            set => SetField(v => _data.ClipboardMaxCount = v, value, 1, 50);
        }

        public int ClipboardDisplayLength
        {
            get => _data.ClipboardDisplayLength;
            set => SetField(v => _data.ClipboardDisplayLength = v, value, 10, 500);
        }

        public int ClipboardImageMaxWidth
        {
            get => _data.ClipboardImageMaxWidth;
            set => SetField(v => _data.ClipboardImageMaxWidth = v, value, 0, 4096);
        }

        public int ClipboardImageCacheLimitMB
        {
            get => _data.ClipboardImageCacheLimitMB;
            set => SetField(v => _data.ClipboardImageCacheLimitMB = v, value, 5, 1024);
        }

        public string LastWidgetTab
        {
            get => _data.LastWidgetTab ?? "Clipboard";
            set => SetField(v => _data.LastWidgetTab = v, value);
        }

        public string DefaultNoteColor
        {
            get => _data.DefaultNoteColor ?? "#FFFF99";
            set => SetField(v => _data.DefaultNoteColor = v, value);
        }

        public bool NoteShowTitleByDefault
        {
            get => _data.NoteShowTitleByDefault;
            set => SetField(v => _data.NoteShowTitleByDefault = v, value);
        }

        public bool UseAutoSize
        {
            get => _data.UseAutoSize;
            set => SetField(v => _data.UseAutoSize = v, value);
        }

        // ========== 自动更新（GitHub Releases） ==========
        public bool AutoCheckUpdate
        {
            get => _data.AutoCheckUpdate;
            set => SetField(v => _data.AutoCheckUpdate = v, value);
        }

        public bool OnboardingCompleted
        {
            get => _data.OnboardingCompleted;
            set => SetField(v => _data.OnboardingCompleted = v, value);
        }

        // ========== 状态栏显示项 ==========
        public bool StatusShowTime { get => _data.StatusShowTime; set => SetField(v => _data.StatusShowTime = v, value); }
        public bool StatusShowCpu { get => _data.StatusShowCpu; set => SetField(v => _data.StatusShowCpu = v, value); }
        public bool StatusShowMemory { get => _data.StatusShowMemory; set => SetField(v => _data.StatusShowMemory = v, value); }
        public bool StatusShowFps { get => _data.StatusShowFps; set => SetField(v => _data.StatusShowFps = v, value); }
        public bool StatusShowVolume { get => _data.StatusShowVolume; set => SetField(v => _data.StatusShowVolume = v, value); }
        public bool StatusShowNetwork { get => _data.StatusShowNetwork; set => SetField(v => _data.StatusShowNetwork = v, value); }
        public bool StatusShowBattery { get => _data.StatusShowBattery; set => SetField(v => _data.StatusShowBattery = v, value); }
        public bool StatusShowWeather { get => _data.StatusShowWeather; set => SetField(v => _data.StatusShowWeather = v, value); }

        // ========== 天气 ==========
        public bool WeatherEnabled { get => _data.WeatherEnabled; set => SetField(v => _data.WeatherEnabled = v, value); }
        public string? WeatherCity { get => _data.WeatherCity; set => SetField(v => _data.WeatherCity = v, value); }

        // ========== 灵动鸟性能模式 ==========
        public string PerformanceMode
        {
            get => _data.PerformanceMode ?? "Normal";
            set => SetField(v => _data.PerformanceMode = v, value);
        }

        // ========== 面板运行帧率（fps，0=自动满帧） ==========
        public int PanelFrameRate
        {
            get => _data.PanelFrameRate;
            set => SetField(v => _data.PanelFrameRate = v, value);
        }

        // ========== 全局界面字号缩放（0.75~1.5） ==========
        public double UiFontScale
        {
            get => _data.UiFontScale;
            set => SetField(v => _data.UiFontScale = Math.Max(0.75, Math.Min(1.5, value)), value);
        }

        /// <summary>应用性能预设（内部标志保护：不触发自定义检测）。</summary>
        public void SetPerformanceMode(string mode)
        {
            _applyingPreset = true;
            try
            {
                PerformancePresets.Apply(this, mode);
                _data.PerformanceMode = mode;
            }
            finally
            {
                _applyingPreset = false;
            }
            Save();
        }

        /// <summary>非预设应用路径修改相关参数 → 自动进入自定义模式。</summary>
        private void MarkCustomIfPreset()
        {
            if (_applyingPreset) return;
            if (_data.PerformanceMode != PerformancePresets.Custom)
            {
                _data.PerformanceMode = PerformancePresets.Custom;
            }
        }

        // ========== 边缘触发距离与延时 ==========
        public int TriggerDistancePx
        {
            get => _data.TriggerDistancePx;
            set { _data.TriggerDistancePx = Math.Max(2, Math.Min(20, value)); MarkCustomIfPreset(); Save(); }
        }

        public int TriggerDelayMs
        {
            get => _data.TriggerDelayMs;
            set { _data.TriggerDelayMs = Math.Max(0, Math.Min(1000, value)); MarkCustomIfPreset(); Save(); }
        }

        public int GetTriggerDelay(string regionKey)
        {
            if (_data.RegionTriggerDelay != null &&
                _data.RegionTriggerDelay.TryGetValue(regionKey, out int v))
                return Math.Max(0, Math.Min(1000, v));
            return TriggerDelayMs;
        }

        public void SetTriggerDelay(string regionKey, int ms)
        {
            _data.RegionTriggerDelay ??= new System.Collections.Generic.Dictionary<string, int>();
            _data.RegionTriggerDelay[regionKey] = Math.Max(0, Math.Min(1000, ms));
            Save();
        }

        public int GetHideDelay(string regionKey)
        {
            if (_data.RegionHideDelay != null &&
                _data.RegionHideDelay.TryGetValue(regionKey, out int v))
                return Math.Max(0, Math.Min(1000, v));
            return HideDelayMs;
        }

        public void SetHideDelay(string regionKey, int ms)
        {
            _data.RegionHideDelay ??= new System.Collections.Generic.Dictionary<string, int>();
            _data.RegionHideDelay[regionKey] = Math.Max(0, Math.Min(1000, ms));
            Save();
        }

        // ========== 小组件显示开关 ==========
        public bool IsWidgetEnabled(string widgetKey)
        {
            // ★ 用户 C# 插件小组件：启用状态存 WidgetPluginOverrides（缺省启用）
            if (widgetKey.StartsWith("Widget_", StringComparison.Ordinal))
                return _data.WidgetPluginOverrides.TryGetValue(widgetKey, out var v) ? v : true;

            return widgetKey switch
            {
                "Clipboard" => _data.WidgetEnabled_Clipboard,
                "Note" => _data.WidgetEnabled_Note,
                "Timer" => _data.WidgetEnabled_Timer,
                "Calculator" => _data.WidgetEnabled_Calculator,
                "TextAi" => _data.WidgetEnabled_TextAi,
                "Web" => _data.WidgetEnabled_Web,
                _ => true
            };
        }

        public void SetWidgetEnabled(string widgetKey, bool enabled)
        {
            // ★ 用户 C# 插件小组件
            if (widgetKey.StartsWith("Widget_", StringComparison.Ordinal))
            {
                _data.WidgetPluginOverrides[widgetKey] = enabled;
                Save();
                return;
            }

            switch (widgetKey)
            {
                case "Clipboard": _data.WidgetEnabled_Clipboard = enabled; break;
                case "Note": _data.WidgetEnabled_Note = enabled; break;
                case "Timer": _data.WidgetEnabled_Timer = enabled; break;
                case "Calculator": _data.WidgetEnabled_Calculator = enabled; break;
                case "TextAi": _data.WidgetEnabled_TextAi = enabled; break;
                case "Web": _data.WidgetEnabled_Web = enabled; break;
                default: return;
            }
            Save();
        }

        // ========== 自定义状态栏显示项开关 ==========
        /// <summary>自定义状态栏插件（status_&lt;id&gt;）是否启用；缺省视为启用。</summary>
        public bool IsStatusProviderEnabled(string providerId)
        {
            if (string.IsNullOrEmpty(providerId)) return false;
            return _data.StatusProviderEnabled.TryGetValue(providerId, out var v) ? v : true;
        }

        /// <summary>设置自定义状态栏插件开关（true=显示）。</summary>
        public void SetStatusProviderEnabled(string providerId, bool enabled)
        {
            if (string.IsNullOrEmpty(providerId)) return;
            _data.StatusProviderEnabled[providerId] = enabled;
            Save();
        }

        // ========== 划词翻译 热键 ==========
        public string TextAiHotkey
        {
            get => _data.TextAiHotkey ?? "";
            set => SetField(v => _data.TextAiHotkey = v, value);
        }

        // ========== 勿扰模式 ==========
        public bool RememberDndMode
        {
            get => _data.RememberDndMode;
            set => SetField(v => _data.RememberDndMode = v, value);
        }

        public bool DndModeEnabled
        {
            get => _data.DndModeEnabled;
            set => SetField(v => _data.DndModeEnabled = v, value);
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
            set { _data.AnimationsEnabled = value; MarkCustomIfPreset(); Save(); }
        }

        public string ShowHideEasingType
        {
            get => _data.ShowHideEasingType ?? "CubicEase";
            set => SetField(v => _data.ShowHideEasingType = v, value);
        }

        public int ShowHideDurationMs
        {
            get => _data.ShowHideDurationMs;
            set { _data.ShowHideDurationMs = Math.Max(100, Math.Min(800, value)); MarkCustomIfPreset(); Save(); }
        }

        public string TransformEasingType
        {
            get => _data.TransformEasingType ?? "CubicEase";
            set => SetField(v => _data.TransformEasingType = v, value);
        }

        // ========== 触发/隐藏动画（类型 + 时长 + 特化参数） ==========
        public string ShowAnimationType
        {
            get => string.IsNullOrEmpty(_data.ShowAnimationType) ? "Slide" : _data.ShowAnimationType;
            set { _data.ShowAnimationType = value; MarkCustomIfPreset(); Save(); }
        }

        public int ShowAnimationDurationMs
        {
            get => _data.ShowAnimationDurationMs > 0 ? _data.ShowAnimationDurationMs : _data.ShowHideDurationMs;
            set { _data.ShowAnimationDurationMs = Math.Max(30, Math.Min(2000, value)); MarkCustomIfPreset(); Save(); }
        }

        public double ShowAnimationZoomFrom
        {
            get => _data.ShowAnimationZoomFrom;
            set { _data.ShowAnimationZoomFrom = Math.Max(0.05, Math.Min(0.95, value)); MarkCustomIfPreset(); Save(); }
        }

        public int ShowAnimationOscillations
        {
            get => _data.ShowAnimationOscillations;
            set { _data.ShowAnimationOscillations = Math.Max(1, Math.Min(10, value)); MarkCustomIfPreset(); Save(); }
        }

        public double ShowAnimationSpringiness
        {
            get => _data.ShowAnimationSpringiness;
            set { _data.ShowAnimationSpringiness = Math.Max(1, Math.Min(10, value)); MarkCustomIfPreset(); Save(); }
        }

        public string HideAnimationType
        {
            get => string.IsNullOrEmpty(_data.HideAnimationType) ? _data.ShowAnimationType : _data.HideAnimationType;
            set { _data.HideAnimationType = value; MarkCustomIfPreset(); Save(); }
        }

        public int HideAnimationDurationMs
        {
            get => _data.HideAnimationDurationMs > 0 ? _data.HideAnimationDurationMs : _data.ShowAnimationDurationMs;
            set { _data.HideAnimationDurationMs = Math.Max(30, Math.Min(2000, value)); MarkCustomIfPreset(); Save(); }
        }

        public double HideAnimationZoomTo
        {
            get => _data.HideAnimationZoomTo;
            set { _data.HideAnimationZoomTo = Math.Max(0.05, Math.Min(0.95, value)); MarkCustomIfPreset(); Save(); }
        }

        public int HideAnimationOscillations
        {
            get => _data.HideAnimationOscillations;
            set { _data.HideAnimationOscillations = Math.Max(1, Math.Min(10, value)); MarkCustomIfPreset(); Save(); }
        }

        public double HideAnimationSpringiness
        {
            get => _data.HideAnimationSpringiness;
            set { _data.HideAnimationSpringiness = Math.Max(1, Math.Min(10, value)); MarkCustomIfPreset(); Save(); }
        }

        public int TransformDurationMs
        {
            get => _data.TransformDurationMs;
            set { _data.TransformDurationMs = Math.Max(100, Math.Min(600, value)); MarkCustomIfPreset(); Save(); }
        }

        public int HideDelayMs
        {
            get => _data.HideDelayMs;
            // ★ 0 = 取消延时隐藏（鼠标一离开立即隐藏）
            set { _data.HideDelayMs = Math.Max(0, Math.Min(1000, value)); MarkCustomIfPreset(); Save(); }
        }

        public int FlyDurationMs
        {
            get => _data.FlyDurationMs;
            set { _data.FlyDurationMs = Math.Max(0, Math.Min(2000, value)); MarkCustomIfPreset(); Save(); }
        }

        // ========== 逐区域动画覆盖（动画页签「动画应用于」） ==========
        public DynamicBird.Core.Models.RegionAnimationOverride? GetRegionAnimation(string regionKey)
        {
            if (_data.RegionAnimationOverrides != null &&
                _data.RegionAnimationOverrides.TryGetValue(regionKey, out var ov))
                return ov;
            return null;
        }

        public void SetRegionAnimation(string regionKey, DynamicBird.Core.Models.RegionAnimationOverride? ov)
        {
            _data.RegionAnimationOverrides ??= new System.Collections.Generic.Dictionary<string, DynamicBird.Core.Models.RegionAnimationOverride>();
            if (ov == null || (string.IsNullOrEmpty(ov.ShowAnimationType) && !ov.ShowAnimationDurationMs.HasValue &&
                               string.IsNullOrEmpty(ov.HideAnimationType) && !ov.HideAnimationDurationMs.HasValue))
            {
                _data.RegionAnimationOverrides.Remove(regionKey);
            }
            else
            {
                _data.RegionAnimationOverrides[regionKey] = ov;
            }
            Save();
        }

        public string GetResolvedShowAnimationType(string regionKey)
        {
            var ov = GetRegionAnimation(regionKey);
            return !string.IsNullOrEmpty(ov?.ShowAnimationType) ? ov!.ShowAnimationType! : ShowAnimationType;
        }

        public int GetResolvedShowAnimationDurationMs(string regionKey)
        {
            var ov = GetRegionAnimation(regionKey);
            return ov?.ShowAnimationDurationMs.HasValue == true ? ov.ShowAnimationDurationMs.Value : ShowAnimationDurationMs;
        }

        public string GetResolvedHideAnimationType(string regionKey)
        {
            var ov = GetRegionAnimation(regionKey);
            return !string.IsNullOrEmpty(ov?.HideAnimationType) ? ov!.HideAnimationType! : HideAnimationType;
        }

        public int GetResolvedHideAnimationDurationMs(string regionKey)
        {
            var ov = GetRegionAnimation(regionKey);
            return ov?.HideAnimationDurationMs.HasValue == true ? ov.HideAnimationDurationMs.Value : HideAnimationDurationMs;
        }

        // ========== 编程模式（鸟笼） ==========
        public bool ProgrammingModeEnabled
        {
            get => _data.ProgrammingModeEnabled;
            set { _data.ProgrammingModeEnabled = value; Save(); }
        }

        public System.Collections.Generic.List<DynamicBird.Core.Models.CustomPanelDefinition> CustomPanels
        {
            get => _data.CustomPanels ??= new System.Collections.Generic.List<DynamicBird.Core.Models.CustomPanelDefinition>();
            set { _data.CustomPanels = value; Save(); }
        }

        public System.Collections.Generic.Dictionary<string, string> AppliedPresets
        {
            get => _data.AppliedPresets ??= new System.Collections.Generic.Dictionary<string, string>();
            set { _data.AppliedPresets = value; Save(); }
        }

        // ========== 小鸟依人模式 ==========
        public bool ClingModeEnabled
        {
            get => _data.ClingModeEnabled;
            set => SetField(v => _data.ClingModeEnabled = v, value);
        }

        public int SnapRangePx
        {
            get => _data.SnapRangePx;
            set => SetField(v => _data.SnapRangePx = Math.Max(0, Math.Min(100, value)), value);
        }

        public int ContentStabilizeMs
        {
            get => _data.ContentStabilizeMs;
            set => SetField(v => _data.ContentStabilizeMs = Math.Max(200, Math.Min(800, value)), value);
        }

        public string? PassthroughModifier
        {
            get => _data.PassthroughModifier ?? "Ctrl";
            set => SetField(v => _data.PassthroughModifier = v, string.IsNullOrWhiteSpace(value) ? "Ctrl" : value);
        }

        // ★★★ 新增：区域防抖延迟 ★★★
        public int RegionDebounceMs
        {
            get => _data.RegionDebounceMs;
            set { _data.RegionDebounceMs = Math.Max(30, Math.Min(300, value)); MarkCustomIfPreset(); Save(); }
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