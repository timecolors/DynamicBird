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
        void SetParameters(double posStiffness, double posDamping, double sizeStiffness, double sizeDamping);

        /// <summary>
        /// 设置飞行参数（时长→刚度和阻尼映射）
        /// </summary>
        void SetFlyParameters(int durationMs);

        /// <summary>
        /// 立即跳转到目标（无动画）
        /// </summary>
        void JumpTo(double left, double top, double width, double height);

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