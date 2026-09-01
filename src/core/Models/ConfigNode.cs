using System.Collections.Generic;

namespace DynamicBird.Core.Models
{
    /// <summary>
    /// 鸟笼配置树节点：一级（面板设计/动画/外观/交互/状态栏）→ 二级 → 三级。
    /// 每个节点绑定一组 SettingsData 字段名，选中后在编程框里以 JSON 编辑。
    /// 每一级都可新增选项（新增节点 = 新增可编程单元）。
    /// </summary>
    public class ConfigNode
    {
        /// <summary>唯一标识（如 "anim-show"）。</summary>
        public string Key { get; set; } = "";

        /// <summary>显示名。</summary>
        public string Name { get; set; } = "";

        /// <summary>所属一级分类。</summary>
        public string Category { get; set; } = "";

        /// <summary>该节点绑定的 SettingsData 字段名列表（叶子节点使用）。</summary>
        public List<string> FieldNames { get; set; } = new();

        /// <summary>子节点（非叶子节点有）。</summary>
        public List<ConfigNode> Children { get; set; } = new();

        /// <summary>父节点。</summary>
        public ConfigNode? Parent { get; set; }

        /// <summary>自定义面板 Id（非空 = 用户新增的自定义功能，编辑其 CustomPanels 配置）。</summary>
        public string? CustomId { get; set; }

        /// <summary>自定义项的种类（Widget/Panel/Config/StatusProvider/Animation；由 CustomPanels.Kind 带出，
        /// PromptGenerator 据此分发生成不同提示词）。</summary>
        public string? Kind { get; set; }

        public bool IsLeaf => Children.Count == 0;
    }
}