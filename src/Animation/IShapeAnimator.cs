using System;

namespace DynamicBird.Animation
{
    public interface IShapeAnimator : IDisposable
    {
        // ========== 当前状态属性（用于外部查询） ==========

        /// <summary>当前面板左边界位置</summary>
        double CurrentLeft { get; }

        /// <summary>当前面板上边界位置</summary>
        double CurrentTop { get; }

        /// <summary>当前面板宽度</summary>
        double CurrentWidth { get; }

        /// <summary>当前面板高度</summary>
        double CurrentHeight { get; }

        // ========== 核心方法 ==========

        /// <summary>
        /// 设置目标位置和尺寸（由弹簧-阻尼驱动）
        /// </summary>
        void SetTarget(double left, double top, double width, double height, bool resetVelocity = true);

        /// <summary>
        /// 只设置目标位置
        /// </summary>
        void SetTargetPosition(double left, double top, bool resetVelocity = false);

        /// <summary>
        /// 设置收敛参数
        /// </summary>

        /// <summary>
        /// 设置飞行参数（时长→刚度和阻尼映射）
        /// </summary>
        void SetFlyParameters(int durationMs);

        /// <summary>
        /// 立即跳转到目标（无动画）
        /// </summary>
        void JumpTo(double left, double top, double width, double height);

        /// <summary>仅位置动画（尺寸不动）：快速切换期间不形变，跟手。</summary>
        void SetPositionAnimate(double left, double top);

        /// <summary>仅尺寸形变动画（防抖稳定后）：平滑形变到目标尺寸，完成后回调。</summary>
        void AnimateSizeTo(double width, double height, Action? completed = null);

        /// <summary>位置+尺寸平滑目标（物理收敛，区域切换的平滑过渡）。</summary>
        void SetPositionAndSizeTarget(double left, double top, double width, double height);

        /// <summary>仅尺寸平滑目标（位置不动）。</summary>
        void SetSizeTarget(double width, double height);

        /// <summary>
        /// 停止所有动画
        /// </summary>
        void StopAll();

        // ===== 兼容旧接口 =====
        void AnimateTo(double width, double height, double left, double top, int durationMs);
        void SetImmediate(double width, double height, double left, double top);
        void FollowWithInertia(double targetLeft, double targetTop);

        /// <summary>
        /// 飞行完成回调
        /// </summary>
        event Action? FlyCompleted;
    }
}