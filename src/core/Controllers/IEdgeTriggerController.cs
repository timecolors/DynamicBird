using System;

namespace ShoreHue.Core.Controllers
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

        event Action<string, string>? SwitchStarted;

        void CompletePendingSwitch();

        event Action StartClingingRequested;
        event Action StopClingingRequested;
        event Action StickToMouseRequested;
        event Action FlyCompleted;
    }
}
