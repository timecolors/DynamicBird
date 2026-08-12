using System;

namespace DynamicBird.src.core.Services.System
{
    /// <summary>
    /// 勿扰模式服务接口
    /// </summary>
    public interface IModeService
    {
        /// <summary>
        /// 当前是否为勿扰模式
        /// </summary>
        bool IsDoNotDisturb { get; set; }

        /// <summary>
        /// 切换勿扰模式
        /// </summary>
        void Toggle();

        /// <summary>
        /// 初始化（从设置恢复状态）
        /// </summary>
        void Initialize();

        /// <summary>
        /// 模式变化事件
        /// </summary>
        event Action<bool>? ModeChanged;
    }
}