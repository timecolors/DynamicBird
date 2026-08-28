using DynamicBird.Core.Detection;
using System;
using System.Windows;
using System.Windows.Interop;

namespace DynamicBird.Core.Controllers
{
    /// <summary>
    /// 拖拽/调整大小手柄在“有效屏幕边缘触发带”内的让位判定。
    /// 坐标体系与主窗口边缘 tick（StartEdgeTimer）完全一致：物理像素 ÷ DPI → DIP，屏幕尺寸用 SystemParameters。
    /// 避免“想切边缘内容却变成拖拽面板”。
    /// </summary>
    internal static class EdgeBandHelper
    {
        public static bool IsInEdgeTriggerBand(Window window, Point panelPos,
            int edgeThreshold, Func<EdgeRegion, bool>? enabledCheck = null)
        {
            try
            {
                // PointToScreen 返回物理像素（device units），需除以 DPI 得到与主窗口 tick 一致的 DIP
                var screenPt = window.PointToScreen(panelPos);
                double dpi = 1.0;
                var ps = PresentationSource.FromVisual(window);
                if (ps?.CompositionTarget != null)
                {
                    dpi = ps.CompositionTarget.TransformToDevice.M11;
                }
                if (dpi <= 0 || double.IsNaN(dpi) || double.IsInfinity(dpi)) dpi = 1.0;

                double mouseX = screenPt.X / dpi;
                double mouseY = screenPt.Y / dpi;
                var wa = DynamicBird.Infrastructure.Utils.ScreenMetrics.GetCachedScreenForPoint(mouseX, mouseY);
                double screenW = wa.Width;
                double screenH = wa.Height;

                // 右上角配置"窗口操作中心"时放行（与主窗口 tick 判定一致）
                bool allowTopRight = enabledCheck == null || enabledCheck(EdgeRegion.TopRight);
                EdgeRegion region = EdgeStateDetector.DetectRegion(mouseX, mouseY, screenW, screenH, edgeThreshold, allowTopRight);
                if (region == EdgeRegion.Unknown) return false;
                return enabledCheck == null || enabledCheck(region);
            }
            catch
            {
                // 判定失败时保守返回 false（不阻塞正常拖拽）
                return false;
            }
        }
    }
}