using System;

namespace ShoreHue.Core.Infrastructure.Service
{
    /// <summary>
    /// 服务接口，所有服务必须实现
    /// </summary>
    public interface IService
    {
        /// <summary>
        /// 服务名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 初始化服务
        /// </summary>
        void Initialize();

        /// <summary>
        /// 关闭服务（释放资源）
        /// </summary>
        void Shutdown();

        /// <summary>
        /// 服务是否已初始化
        /// </summary>
        bool IsInitialized { get; }
    }
}