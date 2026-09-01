using DynamicBird.Core.Services.Configuration;

namespace DynamicBird.UI.Status
{
    /// <summary>
    /// 状态栏显示项接口：用户编写的自定义状态栏插件实现本接口，
    /// 放入 birdcage/状态栏/<名字>/main.cs（manifest kind = "StatusProvider"）后，
    /// watcher 识别 → 编译 → SystemStatusView 动态挂载（内置项之后）。
    /// </summary>
    public interface IStatusProvider
    {
        /// <summary>显示名（如 "CPU 温度"）。</summary>
        string Name { get; }

        /// <summary>图标（emoji 或文本，如 "🌡️"）。</summary>
        string IconText { get; }

        /// <summary>当前文本（每秒调用一次，UI 线程）。</summary>
        string GetText();

        /// <summary>挂载时调用（订阅资源 / 启动定时器）。</summary>
        void OnActivated();

        /// <summary>卸载时调用（释放资源 / 停止定时器）。</summary>
        void OnDeactivated();

        /// <summary>是否启用（可读 ISettingsService 的 StatusProviderEnabled 开关等）。</summary>
        bool IsEnabled(ISettingsService settings);
    }
}