using System;

namespace DynamicBird.Core.Controllers
{
    public interface IEdgeTriggerController
    {
        EdgeRegion CurrentRegion { get; }
        string CurrentEdge { get; }
        bool IsFlying { get; }
        bool IsClinging { get; }
        bool IsSticking { get; }
        bool IsDragging { get; set; }
        bool IsPanelVisible { get; set; }

        void OnMouseMove(EdgeRegion region, double mouseX, double mouseY, bool isInsidePanel);
        void Reset();
        bool ShouldPreventAutoHide();
        void SetClingModeEnabled(bool enabled);
        void OnFlyCompleted();
        void OnStickToMouseSuccess();

        event Action<EdgeRegion, string> ModeSwitchRequested;
        event Action<double, double, bool> PositionUpdateRequested;
        event Action<double, double> JumpToPositionRequested;
        event Action<string> ShowPanelRequested;
        event Action HidePanelRequested;
        event Action StartHideDelayRequested;
        event Action CancelHideDelayRequested;
        event Action StartClingingRequested;
        event Action StopClingingRequested;
        event Action StickToMouseRequested;
        event Action FlyCompleted;
        event Action<EdgeRegion, double, double> FlyRequested;
    }
}