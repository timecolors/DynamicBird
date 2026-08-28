using System;

namespace DynamicBird.Core.Services.Configuration
{
    /// <summary>
    /// 灵动鸟性能模式预设：
    ///  - Smooth / Normal / PowerSaver 是预设档（一键应用一组动画/触发参数）
    ///  - Custom 不是档位：用户手动修改任一相关参数后自动进入（SettingsManager 属性 / 设置保存时检测）
    /// </summary>
    public static class PerformancePresets
    {
        public const string Smooth = "Smooth";
        public const string Normal = "Normal";
        public const string PowerSaver = "PowerSaver";
        public const string Custom = "Custom";

        /// <summary>点击按钮时的循环顺序（Custom 为游离态，点击后回 Normal）。</summary>
        public static readonly string[] CycleOrder = { Smooth, Normal, PowerSaver };

        /// <summary>把预设应用到设置服务（会触发 SettingsChanged，主窗口即时生效）。</summary>
        public static void Apply(ISettingsService s, string mode)
        {
            switch (mode)
            {
                case Smooth:
                    s.AnimationsEnabled = true;
                    s.ShowHideDurationMs = 250;
                    s.TransformDurationMs = 350;
                    s.FlyDurationMs = 700;
                    s.HideDelayMs = 200;
                    s.TriggerDistancePx = 6;
                    s.TriggerDelayMs = 80;
                    s.RegionDebounceMs = 80;
                    break;
                case PowerSaver:
                    s.AnimationsEnabled = false;
                    s.ShowHideDurationMs = 150;
                    s.TransformDurationMs = 250;
                    s.FlyDurationMs = 200;
                    s.HideDelayMs = 100;
                    s.TriggerDistancePx = 6;
                    s.TriggerDelayMs = 0;   // 即时触发，减少等待
                    s.RegionDebounceMs = 80;
                    break;
                case Normal:
                default:
                    s.AnimationsEnabled = true;
                    s.ShowHideDurationMs = 150;
                    s.TransformDurationMs = 250;
                    s.FlyDurationMs = 500;
                    s.HideDelayMs = 200;
                    s.TriggerDistancePx = 6;
                    s.TriggerDelayMs = 150;
                    s.RegionDebounceMs = 80;
                    break;
            }
        }

        /// <summary>判断一组设置是否与该预设的标准参数完全一致（用于"自定义模式"检测）。</summary>
        public static bool Matches(SettingsData d, string mode)
        {
            switch (mode)
            {
                case Smooth:
                    return d.AnimationsEnabled == true &&
                           d.ShowHideDurationMs == 250 && d.TransformDurationMs == 350 &&
                           d.FlyDurationMs == 700 && d.HideDelayMs == 200 &&
                           d.TriggerDistancePx == 6 && d.TriggerDelayMs == 80 &&
                           d.RegionDebounceMs == 80;
                case PowerSaver:
                    return d.AnimationsEnabled == false &&
                           d.FlyDurationMs == 200 && d.HideDelayMs == 100 &&
                           d.TriggerDelayMs == 0 && d.TriggerDistancePx == 6 &&
                           d.RegionDebounceMs == 80;
                case Normal:
                default:
                    return d.AnimationsEnabled == true &&
                           d.ShowHideDurationMs == 150 && d.TransformDurationMs == 250 &&
                           d.FlyDurationMs == 500 && d.HideDelayMs == 200 &&
                           d.TriggerDistancePx == 6 && d.TriggerDelayMs == 150 &&
                           d.RegionDebounceMs == 80;
            }
        }
    }
}
