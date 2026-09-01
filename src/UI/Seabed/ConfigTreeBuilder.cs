using System.Collections.Generic;
using ShoreHue.Core.Models;

namespace ShoreHue.UI.Seabed
{
    /// <summary>
    /// 构建海床配置树：一级分类（面板设计/动画/外观/交互/状态栏）→ 二级 → 三级。
    /// 叶子节点绑定 SettingsData 字段名（编程框以 JSON 编辑这些字段）。
    /// </summary>
    public static class ConfigTreeBuilder
    {
        public static ConfigNode Build()
        {
            var root = new ConfigNode { Key = "root", Name = "海床" };

            root.Children.Add(BuildPanelDesign());
            root.Children.Add(BuildAnimation());
            root.Children.Add(BuildAppearance());
            root.Children.Add(BuildInteraction());
            root.Children.Add(BuildStatusBar());

            return root;
        }

        /// <summary>按 Key 查找节点（含子级递归）；未找到返回 null。用于预设覆盖 → 字段映射。</summary>
        public static ConfigNode? FindNodeByKey(string key)
        {
            var root = Build();
            return FindRecursive(root, key);
        }

        /// <summary>按 Key 返回节点路径的名称链（如 [面板设计, 面板尺寸]）；未找到返回空。用于树↔文件夹映射。</summary>
        public static System.Collections.Generic.List<string> FindPathNames(string key)
        {
            var result = new System.Collections.Generic.List<string>();
            if (string.IsNullOrEmpty(key)) return result;
            var root = Build();
            if (FindPathRecursive(root, key, result)) return result;
            return new System.Collections.Generic.List<string>();
        }

        private static bool FindPathRecursive(ConfigNode n, string key, System.Collections.Generic.List<string> path)
        {
            path.Add(n.Name);
            if (n.Key == key) return true;
            foreach (var c in n.Children)
            {
                if (FindPathRecursive(c, key, path)) return true;
            }
            path.RemoveAt(path.Count - 1);
            return false;
        }

        private static ConfigNode? FindRecursive(ConfigNode n, string key)
        {
            if (n.Key == key) return n;
            foreach (var c in n.Children)
            {
                var r = FindRecursive(c, key);
                if (r != null) return r;
            }
            return null;
        }

        /// <summary>收集节点自身 + 所有子级的字段名（用于预设覆盖 → 设置控件变灰）。</summary>
        public static void CollectFields(ConfigNode node, System.Collections.Generic.HashSet<string> into)
        {
            if (node.FieldNames != null)
            {
                foreach (var f in node.FieldNames) into.Add(f);
            }
            foreach (var c in node.Children) CollectFields(c, into);
        }

        /// <summary>根据字段名找到其所属节点链（叶子 → 二级 → 一级）。用于预设应用时定位冲突来源。</summary>
        public static System.Collections.Generic.List<ConfigNode> FindNodeChain(string fieldName)
        {
            var result = new System.Collections.Generic.List<ConfigNode>();
            var root = Build();
            foreach (var c1 in root.Children)
            {
                foreach (var c2 in c1.Children)
                {
                    if (c2.FieldNames.Contains(fieldName))
                    {
                        result.Add(c1);
                        result.Add(c2);
                        return result;
                    }
                    foreach (var c3 in c2.Children)
                    {
                        if (c3.FieldNames.Contains(fieldName))
                        {
                            result.Add(c1);
                            result.Add(c2);
                            result.Add(c3);
                            return result;
                        }
                    }
                }
            }
            return result;
        }

        private static ConfigNode Leaf(string key, string name, string category, params string[] fields)
        {
            var n = new ConfigNode { Key = key, Name = name, Category = category };
            n.FieldNames.AddRange(fields);
            return n;
        }

        // ========== 面板设计 ==========
        private static ConfigNode BuildPanelDesign()
        {
            var c = new ConfigNode { Key = "panel", Name = "面板设计", Category = "面板设计" };
            c.Children.Add(Leaf("panel-regions", "区域面板类型", "面板设计",
                "Region_Top_Left", "Region_Top_Center", "Region_Top_Right",
                "Region_Bottom_Left", "Region_Bottom_Center", "Region_Bottom_Right",
                "Region_Left_Top", "Region_Left_Center", "Region_Left_Bottom",
                "Region_Right_Top", "Region_Right_Center", "Region_Right_Bottom",
                "RegionPanel_Top_Left", "RegionPanel_Top_Center", "RegionPanel_Top_Right",
                "RegionPanel_Bottom_Left", "RegionPanel_Bottom_Center", "RegionPanel_Bottom_Right",
                "RegionPanel_Left_Top", "RegionPanel_Left_Center", "RegionPanel_Left_Bottom",
                "RegionPanel_Right_Top", "RegionPanel_Right_Center", "RegionPanel_Right_Bottom",
                "RegionPanel_TopLeft", "RegionPanel_TopRight", "RegionPanel_BottomLeft", "RegionPanel_BottomRight"));
            c.Children.Add(Leaf("panel-sizes", "面板尺寸（各区域）", "面板设计",
                "UserWidth_Top_Left", "UserHeight_Top_Left", "UserWidth_Top_Center", "UserHeight_Top_Center",
                "UserWidth_Top_Right", "UserHeight_Top_Right", "UserWidth_Bottom_Left", "UserHeight_Bottom_Left",
                "UserWidth_Bottom_Center", "UserHeight_Bottom_Center", "UserWidth_Bottom_Right", "UserHeight_Bottom_Right",
                "UserWidth_Left_Top", "UserHeight_Left_Top", "UserWidth_Left_Center", "UserHeight_Left_Center",
                "UserWidth_Left_Bottom", "UserHeight_Left_Bottom", "UserWidth_Right_Top", "UserHeight_Right_Top",
                "UserWidth_Right_Center", "UserHeight_Right_Center", "UserWidth_Right_Bottom", "UserHeight_Right_Bottom",
                "UserWidth_Corner_TopLeft", "UserHeight_Corner_TopLeft", "UserWidth_Corner_TopRight", "UserHeight_Corner_TopRight",
                "UserWidth_Corner_BottomLeft", "UserHeight_Corner_BottomLeft", "UserWidth_Corner_BottomRight", "UserHeight_Corner_BottomRight"));
            c.Children.Add(Leaf("panel-auto", "自适应与固定位置", "面板设计",
                "AutoFitOnTrigger", "UseAutoSize", "HorizontalLayoutThreshold", "TagWidth",
                "EdgeMode_Top", "EdgeMode_Bottom", "EdgeMode_Left", "EdgeMode_Right",
                "FixedShape_Top", "FixedShape_Bottom", "FixedShape_Left", "FixedShape_Right",
                "FixedOffset_Top", "FixedOffset_Bottom", "FixedOffset_Left", "FixedOffset_Right"));
            c.Children.Add(Leaf("panel-taskbar", "任务栏", "面板设计",
                "TaskbarIconSize", "DividerOffset"));
            c.Children.Add(Leaf("panel-dnd", "勿扰模式", "面板设计",
                "DndModeEnabled", "RememberDndMode"));
            c.Children.Add(Leaf("panel-edges", "边缘与角落开关", "面板设计",
                "Edge_Top", "Edge_Bottom", "Edge_Left", "Edge_Right",
                "Corner_TopLeft", "Corner_TopRight", "Corner_BottomLeft", "Corner_BottomRight"));

            // 小组件（三级：具体小组件）
            var widget = new ConfigNode { Key = "panel-widgets", Name = "小组件", Category = "面板设计" };
            widget.Children.Add(Leaf("widget-clipboard", "剪贴板", "面板设计",
                "WidgetEnabled_Clipboard", "ClipboardMaxCount", "ClipboardDisplayLength",
                "ClipboardImageMaxWidth", "ClipboardImageCacheLimitMB"));
            widget.Children.Add(Leaf("widget-note", "便签", "面板设计",
                "WidgetEnabled_Note", "DefaultNoteColor", "NoteShowTitleByDefault"));
            widget.Children.Add(Leaf("widget-timer", "计时器", "面板设计", "WidgetEnabled_Timer"));
            widget.Children.Add(Leaf("widget-calculator", "计算器", "面板设计", "WidgetEnabled_Calculator"));
            widget.Children.Add(Leaf("widget-textai", "划词翻译", "面板设计",
                "WidgetEnabled_TextAi", "TextAiHotkey"));
            c.Children.Add(widget);

            // 面板功能（任务栏/通知坞/最近/快捷设置/AI/窗口控制 → 可源码化，Kind=Panel 进区域面板）
            var features = new ConfigNode { Key = "panel-features", Name = "面板功能", Category = "面板设计" };
            features.Children.Add(Leaf("panel-notification", "通知坞", "面板设计"));
            features.Children.Add(Leaf("panel-recent", "最近使用", "面板设计"));
            features.Children.Add(Leaf("panel-quicksettings", "快捷设置", "面板设计"));
            features.Children.Add(Leaf("panel-taskbar-feature", "任务栏", "面板设计"));
            features.Children.Add(Leaf("panel-ai", "AI 助手", "面板设计"));
            features.Children.Add(Leaf("panel-windowcontrol", "窗口控制", "面板设计"));
            c.Children.Add(features);
            return c;
        }

        // ========== 动画 ==========
        private static ConfigNode BuildAnimation()
        {
            var c = new ConfigNode { Key = "anim", Name = "动画", Category = "动画" };
            c.Children.Add(Leaf("anim-show", "触发动画", "动画",
                "ShowAnimationType", "ShowAnimationDurationMs", "ShowAnimationZoomFrom",
                "ShowAnimationOscillations", "ShowAnimationSpringiness"));
            c.Children.Add(Leaf("anim-hide", "隐藏动画", "动画",
                "HideAnimationType", "HideAnimationDurationMs", "HideAnimationZoomTo",
                "HideAnimationOscillations", "HideAnimationSpringiness"));
            c.Children.Add(Leaf("anim-transform", "尺寸形变", "动画",
                "TransformEasingType", "TransformDurationMs"));
            c.Children.Add(Leaf("anim-fly", "跟随 / 飞行", "动画", "FlyDurationMs"));
            c.Children.Add(Leaf("anim-master", "总开关与旧兼容", "动画",
                "AnimationsEnabled", "ShowHideEasingType", "ShowHideDurationMs"));
            return c;
        }

        // ========== 外观 ==========
        private static ConfigNode BuildAppearance()
        {
            var c = new ConfigNode { Key = "appr", Name = "外观", Category = "外观" };
            c.Children.Add(Leaf("appr-theme", "颜色主题", "外观",
                "BackgroundColor", "TextColor", "Opacity", "CornerRadius", "UiFontScale"));
            c.Children.Add(Leaf("appr-shape", "形状参数", "外观",
                "StripLengthRatio", "StripWidthMultiplier", "SquareShortSideMultiplier",
                "GoldenRatio", "TriggerRegionRatio"));

            return c;
        }

        // ========== 交互 ==========
        private static ConfigNode BuildInteraction()
        {
            var c = new ConfigNode { Key = "inter", Name = "交互", Category = "交互" };
            c.Children.Add(Leaf("inter-trigger", "触发行为", "交互",
                "TriggerDistancePx", "TriggerDelayMs", "RegionDebounceMs"));
            c.Children.Add(Leaf("inter-hide", "隐藏行为", "交互", "HideDelayMs"));
            c.Children.Add(Leaf("inter-cling", "小鸟依人", "交互",
                "ClingModeEnabled", "SnapRangePx", "ContentStabilizeMs"));
            c.Children.Add(Leaf("inter-passthrough", "穿透", "交互", "PassthroughModifier"));
            c.Children.Add(Leaf("inter-perf", "性能模式", "交互", "PerformanceMode", "PanelFrameRate"));
            return c;
        }

        // ========== 状态栏 ==========
        private static ConfigNode BuildStatusBar()
        {
            var c = new ConfigNode { Key = "status", Name = "状态栏", Category = "状态栏" };
            c.Children.Add(Leaf("status-items", "显示项", "状态栏",
                "ShowSystemStatus", "StatusShowTime", "StatusShowCpu", "StatusShowMemory",
                "StatusShowFps", "StatusShowVolume", "StatusShowNetwork", "StatusShowBattery", "StatusShowWeather"));
            c.Children.Add(Leaf("status-weather", "天气", "状态栏",
                "WeatherEnabled", "WeatherCity"));
            c.Children.Add(Leaf("status-clipboard", "剪贴板", "状态栏",
                "ClipboardMaxCount", "ClipboardDisplayLength", "ClipboardImageMaxWidth", "ClipboardImageCacheLimitMB"));
            c.Children.Add(Leaf("status-note", "便签", "状态栏",
                "DefaultNoteColor", "NoteShowTitleByDefault", "LastWidgetTab"));
            c.Children.Add(Leaf("status-widgets", "小组件开关", "状态栏",
                "WidgetEnabled_Clipboard", "WidgetEnabled_Note", "WidgetEnabled_Timer",
                "WidgetEnabled_Calculator", "WidgetEnabled_TextAi", "TextAiHotkey"));
            return c;
        }
    }
}
