using System;
using System.Windows;

namespace ShoreHue.Animation
{
    /// <summary>
    /// 自定义动画接口：用户编写的动画插件实现本接口，
    /// 放入 seabed/动画/&lt;名字&gt;/main.cs（manifest kind = "Animation"）后，
    /// watcher 识别 → 编译 → 注册进 AnimationRegistry → 设置页动画类型可选、
    /// ShapeAnimator 按类型 Id 分发执行。
    /// </summary>
    /// <remarks>
    /// ★ 性能与安全（渲染帧热路径）：动画可能每帧驱动面板/窗口属性，
    ///   必须遵守——
    ///   1) 用 DispatcherTimer 驱动（不要直接挂 CompositionTarget.Rendering，
    ///      防止无限自激循环导致 100% CPU，项目历史教训）；
    ///   2) 动画时长内完成，onCompleted 只调一次（调用方有超时兜底强制完成）；
    ///   3) 任何异常都要 catch（调用方 try-catch 会回退内置动画）；
    ///   4) 不要引用被沙箱拦截的危险 API（市场来源编译前会被拦截）。
    /// </remarks>
    public interface IAnimation
    {
        /// <summary>显示名（如 "弹跳"，设置页动画类型下拉展示）。</summary>
        string Name { get; }

        /// <summary>唯一标识（设置里存这个值，ShapeAnimator 据此查注册表）。</summary>
        string Id { get; }

        /// <summary>呼出动画：把 panel/window 从当前状态动画到显示态，完成后调 onCompleted。</summary>
        void AnimateShow(FrameworkElement panel, Window window, double ms, Action onCompleted);

        /// <summary>隐藏动画：把 panel/window 动画到隐藏态（透明度→0），完成后调 onCompleted。</summary>
        void AnimateHide(FrameworkElement panel, Window window, double ms, Action onCompleted);
    }
}