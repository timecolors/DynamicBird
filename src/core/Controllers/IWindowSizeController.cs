using System;
using System.Windows.Input;

namespace ShoreHue.Core.Controllers
{
    public interface IWindowSizeController
    {
        string CurrentMode { get; }

        /// <summary>
        /// 是否正在飞行中（用于禁止尺寸修改）
        /// </summary>
        bool IsFlying { get; set; }

        void SetMode(string mode, string edge = "", string region = "");

        void ApplySizeForCurrentMode();

        void ApplySizeStrategyForWidget();

        void ApplyTaskbarPresetSize();

        void SaveCurrentSize();

        void SaveCurrentSizeWithDelay();

        bool HandleMouseDown(object sender, MouseButtonEventArgs e);

        void HandleMouseMove(object sender, MouseEventArgs e);

        void HandleMouseUp(object sender, MouseButtonEventArgs e);

        void UpdateHandlePosition(string edge);

        void RestoreAutoSize();

        void RefreshMinSizeCache();

        (double width, double height) GetTargetSizeForCurrentMode();

        event Action<bool>? UserResizeStarted;
        event Action? SizeChanged;
        event Action? ResizeEnded;
        event Action<bool>? LockRequest;
    }
}