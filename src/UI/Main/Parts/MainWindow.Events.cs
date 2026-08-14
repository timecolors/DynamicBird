using DynamicBird.Core.Infrastructure.Logging;
using DynamicBird.UI.Settings;
using System;
using System.Windows;

namespace DynamicBird.UI.Main
{
    public partial class MainWindow
    {
        private void OpenSettings()
        {
            try
            {
                var settingsWindow = new SettingsWindow(_settingsService, _shortcutService);
                settingsWindow.Owner = this;
                settingsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                LogManager.Error("打开设置窗口失败", ex);
                MessageBox.Show($"打开设置失败:\n{ex.Message}", "灵动鸟", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void ToggleWindow()
        {
            // ★ 切换勿扰模式（与点击🐦图标行为一致）
            bool newState = !_modeService.IsDoNotDisturb;
            _modeService.IsDoNotDisturb = newState;

            if (newState)
            {
                _visibilityController.ForceHide();
            }
            // 退出勿扰时，面板由边缘触发自动显示，不需要手动 Show

            UpdateIconText();
        }

        /// <summary>全局热键 Ctrl+Alt+B：切换面板显示/隐藏。</summary>
        private void HotkeyTogglePanel()
        {
            try
            {
                if (_visibilityController == null) return;

                if (_visibilityController.IsVisible)
                {
                    _visibilityController.Hide();
                }
                else
                {
                    // 在最后锚定的位置呼出面板（若在勿扰模式则先退出勿扰）
                    if (_modeService.IsDoNotDisturb)
                    {
                        _modeService.IsDoNotDisturb = false;
                        UpdateIconText();
                    }
                    _edgeController.ShowPanelAtAnchor();
                }
            }
            catch (Exception ex)
            {
                LogManager.Error("热键切换面板失败", ex);
            }
        }

        private void ExitApp()
        {
            LogManager.Info("用户退出应用");
            _trayManager?.Dispose();
            Application.Current.Shutdown();
        }

        // ========== 面板鼠标事件（UI 绑定） ==========

        // 注意：MainPanel_MouseEnter 和 MainPanel_MouseLeave 在 XAML 中已绑定
        // 这些方法在 MainWindow.UI.cs 中有完整实现

        // ========== 托盘相关 ==========

        internal void UpdateIconText()
        {
            UpdateIconTextInternal();
        }
    }
}
