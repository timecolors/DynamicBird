using DynamicBird.Core.Infrastructure.Logging;
using DynamicBird.UI.Settings;
using System;
using System.Windows;

namespace DynamicBird.UI.Main
{
    public partial class MainWindow
    {
        // ★ 非模态单实例：设置窗口打开时主面板仍正常工作（可实时看面板效果）
        private SettingsWindow? _settingsWindow;

        private void OpenSettings()
        {
            try
            {
                if (_settingsWindow == null || !_settingsWindow.IsLoaded)
                {
                    _settingsWindow = new SettingsWindow(_settingsService, _shortcutService);
                    _settingsWindow.Closed += (_, _) => _settingsWindow = null;
                }
                if (!_settingsWindow.IsVisible)
                {
                    _settingsWindow.Show();
                }
                _settingsWindow.Activate();
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

        /// <summary>
        /// 划词翻译 全局热键：呼出小组件面板并翻译当前前台窗口的选中文本。
        /// 热键未设置时由 TextAiWidget 内部提示去设置；捕获失败也会在面板内给出原因。
        /// </summary>
        private async void OnTextAiHotkey()
        {
            try
            {
                if (_modeService.IsDoNotDisturb)
                {
                    _modeService.IsDoNotDisturb = false;
                    UpdateIconText();
                }

                // 先切到小组件内容，再显示面板（避免先显示旧内容闪一下）
                _contentController.ShowWidgetTab("TextAi");
                var widget = _contentController.WidgetSwitcher?.TextAiWidget;
                if (widget == null) return;

                // 钉住：避免边缘定时器在鼠标不在边缘/面板上时立刻隐藏
                _visibilityController.SetHotkeyPinned(true);
                _edgeController.ShowPanelAtAnchor();

                await widget.CaptureAndTranslateAsync();
            }
            catch (Exception ex)
            {
                LogManager.Error("划词翻译 热键处理失败", ex);
            }
        }

        /// <summary>全局热键 Ctrl+Alt+B：切换面板显示/隐藏。</summary>
        private void HotkeyTogglePanel()
        {
            try
            {
                if (_visibilityController == null) return;

                if (_visibilityController.IsVisible)
                {
                    // ★ 解除钉住并隐藏；若面板处于“跟随边缘”状态则直接隐藏
                    _visibilityController.SetHotkeyPinned(false);
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
                    // ★ 钉住：避免 30ms 边缘定时器因鼠标不在边缘/面板上而立刻隐藏
                    _visibilityController.SetHotkeyPinned(true);
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
