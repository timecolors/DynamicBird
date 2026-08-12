using System;

namespace DynamicBird.Core.Controllers
{
    public interface IPanelVisibilityController
    {
        bool IsLocked { get; }
        bool IsVisible { get; }
        double Opacity { get; set; }

        /// <summary>
        /// ★★★ 是否正在延时隐藏计时中 ★★★
        /// </summary>
        bool IsInHideDelay { get; }

        void SetPanelLock(bool locked);
        void Show(string edge = "");
        void Show();
        void Hide();
        void ForceHide();
        void CancelHide();
        void HideWithDelay();
        bool IsMouseNearPanel();
        void UpdateEdge(string edge);

        event Action? PanelHidden;
        event Action? PanelShown;
    }
}