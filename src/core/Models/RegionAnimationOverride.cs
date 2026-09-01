namespace ShoreHue.Core.Models
{
    /// <summary>
    /// 逐区域动画覆盖（动画页签「动画应用于」）：只覆盖 触发/隐藏动画的 类型+时长，
    /// 其余参数（缩放/振荡/弹性/形变/稳定/飞行/延时）仍用全局值。
    /// 字段为空（null/空串）= 继承全局；整个条目不存在 = 完全跟随全局。
    /// </summary>
    public class RegionAnimationOverride
    {
        public string? ShowAnimationType { get; set; }
        public int? ShowAnimationDurationMs { get; set; }
        public string? HideAnimationType { get; set; }
        public int? HideAnimationDurationMs { get; set; }
    }
}
