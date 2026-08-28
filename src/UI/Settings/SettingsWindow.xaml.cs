using DynamicBird.Core.Services;
using DynamicBird.Core.Services.Ai;
using DynamicBird.Core.Services.Configuration;
using DynamicBird.Infrastructure.Utils;
using DynamicBird.Infrastructure.WinApi;
using DynamicBird.src.core.Services.Shortcuts;
using DynamicBird.UI.Widgets.Dynamic;
using DynamicBird.UI.Settings.Pages;
using DynamicBird.UI.Theme;
using DynamicBird.UI.Localization;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WinForms = System.Windows.Forms;

namespace DynamicBird.UI.Settings
{
    public partial class SettingsWindow : Window
    {
        private readonly ISettingsService _settings;
        private readonly IShortcutService _shortcutService;
        private SettingsData _settingsData = null!;

        // 区域面板自定义选项与区域键（Display 用本地化键，FillPanelCombo 时取当前语言）
        private static readonly (string Value, string LocKey)[] PanelOptions =
        {
            ("Default", "Panel_Default"),
            ("Taskbar", "Panel_Taskbar"),
            ("Widget", "Panel_Widget"),
            ("AppHelper", "Panel_AppHelper"),
            ("Notification", "Panel_Notification"),
            ("Recent", "Panel_Recent"),
            ("QuickSettings", "Panel_QuickSettings"),
            ("AI", "Panel_AI"),
            ("WindowControl", "Panel_WindowControl"),
        };

        private static readonly string[] RegionPanelKeys =
        {
            "Top_Left", "Top_Center", "Top_Right",
            "Bottom_Left", "Bottom_Center", "Bottom_Right",
            "Left_Top", "Left_Center", "Left_Bottom",
            "Right_Top", "Right_Center", "Right_Bottom",
            "TopLeft", "TopRight", "BottomLeft", "BottomRight"
        };

        // 逐区域触发/隐藏延时：区域键 + 显示名（复用区域面板标签的本地化键）
        private static readonly (string Key, string LocKey)[] RegionDelayConfig =
        {
            ("Top_Left", "UI_SettingsWindow_191"), ("Top_Center", "UI_SettingsWindow_192"), ("Top_Right", "UI_SettingsWindow_193"),
            ("Bottom_Left", "UI_SettingsWindow_194"), ("Bottom_Center", "UI_SettingsWindow_195"), ("Bottom_Right", "UI_SettingsWindow_196"),
            ("Left_Top", "UI_SettingsWindow_197"), ("Left_Center", "UI_SettingsWindow_198"), ("Left_Bottom", "UI_SettingsWindow_199"),
            ("Right_Top", "UI_SettingsWindow_200"), ("Right_Center", "UI_SettingsWindow_201"), ("Right_Bottom", "UI_SettingsWindow_202"),
            ("TopLeft", "UI_SettingsWindow_150"), ("TopRight", "UI_SettingsWindow_151"), ("BottomLeft", "UI_SettingsWindow_152"), ("BottomRight", "UI_SettingsWindow_153")
        };

        private sealed class RegionDelayControls
        {
            public Slider Trig = null!;
            public TextBlock TrigText = null!;
            public Slider Hide = null!;
            public TextBlock HideText = null!;
        }

        private readonly Dictionary<string, RegionDelayControls> _regionDelayControls = new();

        public SettingsWindow(ISettingsService settings, IShortcutService shortcutService)
        {
            _settings = settings;
            _shortcutService = shortcutService;
            Icon = AppIconHelper.LoadAppIcon();
            InitializeComponent();
            // ★ 不启用 Mica：Mica 跟随系统主题，深色主题下会把设置页背景变成黑色。
            //   固定用 XAML 浅色背景（#F9F9F9），Win10/Win11 观感一致。
            LoadSettings();
            LoadShortcutPage();
            // ★ 实时保存：所有设置控件变化自动保存（400ms 防抖）
            HookAutoSave();

            // ★ 插件安装/删除时实时刷新（本窗口非模态常驻，可能在别处保存插件）
            DynamicBird.UI.Widgets.Dynamic.WidgetPluginStore.Changed += () => Dispatcher.Invoke(() =>
            {
                RefreshWidgetMarket();
            });

            // AI 高级参数滑块
            sldAiTemperature.ValueChanged += (s, e) => txtAiTemperature.Text = sldAiTemperature.Value.ToString("F1");

            // 滑块事件绑定
            sldOpacity.ValueChanged += (s, e) => txtOpacityValue.Text = sldOpacity.Value.ToString("F2");
            sldCornerRadius.ValueChanged += (s, e) => txtCornerRadiusValue.Text = sldCornerRadius.Value.ToString("F0");
            sldHorizontalThreshold.ValueChanged += (s, e) => txtHorizontalThreshold.Text = (sldHorizontalThreshold.Value * 100).ToString("F0") + "%";
            sldTagWidth.ValueChanged += (s, e) => txtTagWidth.Text = sldTagWidth.Value.ToString("F0");

            sldIconSize.ValueChanged += (s, e) =>
            {
                double val = Math.Round(sldIconSize.Value);
                sldIconSize.Value = val;
                txtIconSize.Text = $"{val:F0}px";
            };

            sldClipboardMax.ValueChanged += (s, e) => txtClipboardMax.Text = sldClipboardMax.Value.ToString("F0");
            sldClipboardDisplay.ValueChanged += (s, e) => txtClipboardDisplay.Text = sldClipboardDisplay.Value.ToString("F0");
            sldClipImageMax.ValueChanged += (s, e) => txtClipImageMax.Text = ((int)sldClipImageMax.Value) + "px";
            sldClipImageCacheLimit.ValueChanged += (s, e) => txtClipImageCacheLimit.Text = ((int)sldClipImageCacheLimit.Value) + "MB";

            // 动画设置滑块（触发/隐藏分设）
            sldShowDuration.ValueChanged += (s, e) => txtShowDuration.Text = sldShowDuration.Value.ToString("F0") + "ms";
            sldHideDuration.ValueChanged += (s, e) => txtHideDuration.Text = sldHideDuration.Value.ToString("F0") + "ms";
            sldShowZoomFrom.ValueChanged += (s, e) => txtShowZoomFrom.Text = sldShowZoomFrom.Value.ToString("0.0#");
            sldHideZoomTo.ValueChanged += (s, e) => txtHideZoomTo.Text = sldHideZoomTo.Value.ToString("0.0#");
            sldShowOsc.ValueChanged += (s, e) => txtShowOsc.Text = sldShowOsc.Value.ToString("F0");
            sldHideOsc.ValueChanged += (s, e) => txtHideOsc.Text = sldHideOsc.Value.ToString("F0");
            sldShowSpring.ValueChanged += (s, e) => txtShowSpring.Text = sldShowSpring.Value.ToString("0.0#");
            sldHideSpring.ValueChanged += (s, e) => txtHideSpring.Text = sldHideSpring.Value.ToString("0.0#");
            sldTransformDuration.ValueChanged += (s, e) => txtTransformDuration.Text = sldTransformDuration.Value.ToString("F0") + "ms";
            sldHideDelay.ValueChanged += (s, e) => txtHideDelay.Text =
                sldHideDelay.Value <= 0 ? DynamicBird.UI.Localization.LocalizationManager.Instance["Set_Immediate"] : sldHideDelay.Value.ToString("F0") + "ms";
            sldFlyDuration.ValueChanged += (s, e) => txtFlyDuration.Text = sldFlyDuration.Value.ToString("F0") + "ms";
            sldContentStabilize.ValueChanged += (s, e) => txtContentStabilize.Text = sldContentStabilize.Value.ToString("F0") + "ms";
            sldSnapRange.ValueChanged += (s, e) => txtSnapRange.Text = ((int)sldSnapRange.Value) + "px";

            // ★★★ 区域防抖滑块 ★★★
            sldRegionDebounce.ValueChanged += (s, e) => txtRegionDebounce.Text = sldRegionDebounce.Value.ToString("F0") + "ms";

            // ★★★ 触发距离与延时滑块 ★★★
            sldTrigDist.ValueChanged += (s, e) => txtTrigDist.Text = ((int)sldTrigDist.Value) + "px";
            sldGlbTrig.ValueChanged += (s, e) => txtGlbTrig.Text = ((int)sldGlbTrig.Value) + "ms";
            // 全局隐藏延时与"动画"标签页的延时隐藏滑块双向联动（同一字段，改哪边都同步）
            sldGlbHide.ValueChanged += (s, e) =>
            {
                txtGlbHide.Text = ((int)sldGlbHide.Value) + "ms";
                if (Math.Abs(sldHideDelay.Value - sldGlbHide.Value) > 0.01)
                    sldHideDelay.Value = sldGlbHide.Value;
            };
            sldHideDelay.ValueChanged += (s, e) =>
            {
                if (Math.Abs(sldGlbHide.Value - sldHideDelay.Value) > 0.01)
                    sldGlbHide.Value = sldHideDelay.Value;
            };
        }

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private void TryApplyMicaBackdrop()
        {
            try
            {
                if (Environment.OSVersion.Version.Build < 22621) return;
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;

                const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
                const int DWMSBT_MAINWINDOW = 2;
                int value = DWMSBT_MAINWINDOW;
                if (DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref value, sizeof(int)) == 0)
                {
                    // Mica 生效后窗口背景接近透明，让毛玻璃材质透出
                    Background = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromArgb(2, 0xF9, 0xF9, 0xF9));
                }
            }
            catch { }
        }

        private void LoadShortcutPage()
        {
            var page = new ShortcutManagementPage(_shortcutService);
            ShortcutManagementFrame.Navigate(page);
        }

        private void LoadSettings()
        {
            _settingsData = SettingsDataManager.Load();

            // 触发位置
            chkTop.IsChecked = _settingsData.Edge_Top;
            chkBottom.IsChecked = _settingsData.Edge_Bottom;
            chkLeft.IsChecked = _settingsData.Edge_Left;
            chkRight.IsChecked = _settingsData.Edge_Right;
            chkTopLeft.IsChecked = _settingsData.Corner_TopLeft;
            chkTopRight.IsChecked = _settingsData.Corner_TopRight;
            chkBottomLeft.IsChecked = _settingsData.Corner_BottomLeft;
            chkBottomRight.IsChecked = _settingsData.Corner_BottomRight;

            // 边行为模式
            SettingsUIHelper.SetComboSelected(cmbTopMode, _settingsData.EdgeMode_Top ?? "Follow");
            SettingsUIHelper.SetComboSelected(cmbBottomMode, _settingsData.EdgeMode_Bottom ?? "Follow");
            SettingsUIHelper.SetComboSelected(cmbLeftMode, _settingsData.EdgeMode_Left ?? "Follow");
            SettingsUIHelper.SetComboSelected(cmbRightMode, _settingsData.EdgeMode_Right ?? "Follow");

            // 固定形状
            SettingsUIHelper.SetShapeComboSelected(cmbFixedShapeTop, _settingsData.FixedShape_Top ?? "Square");
            SettingsUIHelper.SetShapeComboSelected(cmbFixedShapeBottom, _settingsData.FixedShape_Bottom ?? "Square");
            SettingsUIHelper.SetShapeComboSelected(cmbFixedShapeLeft, _settingsData.FixedShape_Left ?? "Square");
            SettingsUIHelper.SetShapeComboSelected(cmbFixedShapeRight, _settingsData.FixedShape_Right ?? "Square");

            // 区域形状
            SettingsUIHelper.SetShapeComboSelected(cmbTopLeft, _settingsData.Region_Top_Left ?? "Default");
            SettingsUIHelper.SetShapeComboSelected(cmbTopCenter, _settingsData.Region_Top_Center ?? "Default");
            SettingsUIHelper.SetShapeComboSelected(cmbTopRight, _settingsData.Region_Top_Right ?? "Default");
            SettingsUIHelper.SetShapeComboSelected(cmbBottomLeft, _settingsData.Region_Bottom_Left ?? "Default");
            SettingsUIHelper.SetShapeComboSelected(cmbBottomCenter, _settingsData.Region_Bottom_Center ?? "Default");
            SettingsUIHelper.SetShapeComboSelected(cmbBottomRight, _settingsData.Region_Bottom_Right ?? "Default");
            SettingsUIHelper.SetShapeComboSelected(cmbLeftTop, _settingsData.Region_Left_Top ?? "Default");
            SettingsUIHelper.SetShapeComboSelected(cmbLeftCenter, _settingsData.Region_Left_Center ?? "Default");
            SettingsUIHelper.SetShapeComboSelected(cmbLeftBottom, _settingsData.Region_Left_Bottom ?? "Default");
            SettingsUIHelper.SetShapeComboSelected(cmbRightTop, _settingsData.Region_Right_Top ?? "Default");
            SettingsUIHelper.SetShapeComboSelected(cmbRightCenter, _settingsData.Region_Right_Center ?? "Default");
            SettingsUIHelper.SetShapeComboSelected(cmbRightBottom, _settingsData.Region_Right_Bottom ?? "Default");

            // 外观
            txtBgColor.Text = _settingsData.BackgroundColor ?? "#2D2D2D";
            txtTextColor.Text = _settingsData.TextColor ?? "#FFFFFF";
            sldOpacity.Value = _settingsData.Opacity;
            sldCornerRadius.Value = _settingsData.CornerRadius;
            chkShowSystemStatus.IsChecked = _settingsData.ShowSystemStatus;
            txtCustomIcon.Text = _settingsData.CustomIconPath ?? "";

            // 动画与布局
            sldHorizontalThreshold.Value = _settingsData.HorizontalLayoutThreshold;
            sldTagWidth.Value = _settingsData.TagWidth;

            // 任务栏
            sldIconSize.Value = _settingsData.TaskbarIconSize;
            txtIconSize.Text = $"{_settingsData.TaskbarIconSize:F0}px";

            // 剪贴板与便签
            sldClipboardMax.Value = _settingsData.ClipboardMaxCount;
            sldClipboardDisplay.Value = _settingsData.ClipboardDisplayLength;
            sldClipImageMax.Value = _settingsData.ClipboardImageMaxWidth;
            txtClipImageMax.Text = _settingsData.ClipboardImageMaxWidth + "px";
            sldClipImageCacheLimit.Value = _settingsData.ClipboardImageCacheLimitMB;
            txtClipImageCacheLimit.Text = _settingsData.ClipboardImageCacheLimitMB + "MB";
            txtDefaultNoteColor.Text = _settingsData.DefaultNoteColor ?? "#FFFF99";
            chkNoteShowTitle.IsChecked = _settingsData.NoteShowTitleByDefault;

            // 自适应
            chkAutoFitOnTrigger.IsChecked = _settingsData.AutoFitOnTrigger;

            // 勿扰
            chkRememberDndMode.IsChecked = _settingsData.RememberDndMode;

            // ★★★ 动画设置 ★★★
            chkAnimationsEnabled.IsChecked = _settingsData.AnimationsEnabled;

            cmbTransformEasing.SelectedItem = GetComboBoxItemByContent(cmbTransformEasing, SettingsUIHelper.GetEasingDisplayName(_settingsData.TransformEasingType ?? "CubicEase"));

            // ★ 触发/隐藏动画（类型 + 时长 + 特化参数）
            cmbShowAnimType.SelectedItem = GetComboBoxItemByContent(cmbShowAnimType, AnimTypeToLabel(_settingsData.ShowAnimationType, isHide: false));
            cmbHideAnimType.SelectedItem = GetComboBoxItemByContent(cmbHideAnimType, AnimTypeToLabel(_settingsData.HideAnimationType, isHide: true));
            sldShowDuration.Value = _settingsData.ShowAnimationDurationMs;
            txtShowDuration.Text = _settingsData.ShowAnimationDurationMs + "ms";
            sldHideDuration.Value = _settingsData.HideAnimationDurationMs;
            txtHideDuration.Text = _settingsData.HideAnimationDurationMs + "ms";
            sldShowZoomFrom.Value = _settingsData.ShowAnimationZoomFrom;
            txtShowZoomFrom.Text = _settingsData.ShowAnimationZoomFrom.ToString("0.0#");
            sldHideZoomTo.Value = _settingsData.HideAnimationZoomTo;
            txtHideZoomTo.Text = _settingsData.HideAnimationZoomTo.ToString("0.0#");
            sldShowOsc.Value = _settingsData.ShowAnimationOscillations;
            txtShowOsc.Text = _settingsData.ShowAnimationOscillations.ToString();
            sldHideOsc.Value = _settingsData.HideAnimationOscillations;
            txtHideOsc.Text = _settingsData.HideAnimationOscillations.ToString();
            sldShowSpring.Value = _settingsData.ShowAnimationSpringiness;
            txtShowSpring.Text = _settingsData.ShowAnimationSpringiness.ToString("0.0#");
            sldHideSpring.Value = _settingsData.HideAnimationSpringiness;
            txtHideSpring.Text = _settingsData.HideAnimationSpringiness.ToString("0.0#");
            UpdateShowAnimRows();
            UpdateHideAnimRows();

            sldTransformDuration.Value = _settingsData.TransformDurationMs;
            txtTransformDuration.Text = _settingsData.TransformDurationMs + "ms";
            sldContentStabilize.Value = _settingsData.ContentStabilizeMs;
            txtContentStabilize.Text = _settingsData.ContentStabilizeMs + "ms";
            sldSnapRange.Value = _settingsData.SnapRangePx;
            txtSnapRange.Text = _settingsData.SnapRangePx + "px";

            sldHideDelay.Value = _settingsData.HideDelayMs;
            txtHideDelay.Text = _settingsData.HideDelayMs <= 0
                ? DynamicBird.UI.Localization.LocalizationManager.Instance["Set_Immediate"]
                : _settingsData.HideDelayMs + "ms";

            sldFlyDuration.Value = _settingsData.FlyDurationMs;
            txtFlyDuration.Text = _settingsData.FlyDurationMs + "ms";

            // ★★★ 小鸟依人模式 ★★★
            chkClingMode.IsChecked = _settingsData.ClingModeEnabled;

            // ★ 面板点击穿透修饰键
            string passthrough = _settingsData.PassthroughModifier ?? "Ctrl";
            foreach (var item in cmbPassthrough.Items)
            {
                if (item is System.Windows.Controls.ComboBoxItem cbi && (cbi.Tag?.ToString() ?? "") == passthrough)
                {
                    cmbPassthrough.SelectedItem = item;
                    break;
                }
            }

            // ★★★ 区域防抖 ★★★
            sldRegionDebounce.Value = _settingsData.RegionDebounceMs;
            txtRegionDebounce.Text = _settingsData.RegionDebounceMs + "ms";

            // ★★★ 小工具市场列表 ★★★
            RefreshWidgetMarket();

            // ★★★ 触发距离与延时（全局默认 + 逐区域） ★★★
            sldTrigDist.Value = _settingsData.TriggerDistancePx;
            txtTrigDist.Text = _settingsData.TriggerDistancePx + "px";
            sldGlbTrig.Value = _settingsData.TriggerDelayMs;
            txtGlbTrig.Text = _settingsData.TriggerDelayMs + "ms";
            sldGlbHide.Value = _settingsData.HideDelayMs;
            txtGlbHide.Text = _settingsData.HideDelayMs + "ms";
            BuildRegionDelayRows();

            // ★★★ 区域面板自定义 ★★★
            foreach (string key in RegionPanelKeys)
            {
                var combo = (ComboBox?)FindName("cmbPanel_" + key);
                if (combo == null) continue;
                FillPanelCombo(combo);
                SelectPanelValue(combo, _settings.GetRegionPanel(key));
            }

            // ★★★ 语言（通用设置） ★★★
            SelectLanguage(_settingsData.Language ?? "");
            UpdateCurrentLanguageText();

            // ★★★ 面板小组件与划词翻译 快捷键 ★★★
            // ★ 小组件启停由小工具页签左侧勾选框即时生效（此处无需加载）
            txtTextAiHotkey.Text = _settingsData.TextAiHotkey ?? "";
            UpdateTextAiHotkeyHint();

            // ★★★ 状态栏显示项与天气 ★★★
            chkStatusTime.IsChecked = _settingsData.StatusShowTime;
            chkStatusCpu.IsChecked = _settingsData.StatusShowCpu;
            chkStatusMemory.IsChecked = _settingsData.StatusShowMemory;
            chkStatusFps.IsChecked = _settingsData.StatusShowFps;
            chkStatusVolume.IsChecked = _settingsData.StatusShowVolume;
            chkStatusNetwork.IsChecked = _settingsData.StatusShowNetwork;
            chkStatusBattery.IsChecked = _settingsData.StatusShowBattery;
            chkStatusWeather.IsChecked = _settingsData.StatusShowWeather;
            txtWeatherCity.Text = _settingsData.WeatherCity ?? "";

            // ★★★ 自动更新 ★★★
            chkAutoCheckUpdate.IsChecked = _settingsData.AutoCheckUpdate;
            UpdateUpdateStatus();

            // ★★★ 商店版（MSIX）不提供 GitHub 自更新 ★★★
            UpdateGroup.Visibility = AppPaths.IsPackaged ? Visibility.Collapsed : Visibility.Visible;

            // ★★★ 关于与隐私 ★★★
            var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            txtAbout.Text = $"灵动鸟 DynamicBird  v{ver?.ToString(3) ?? "1.0.0"}";

            // ★★★ AI 助手设置 ★★★
            LoadAiSettings();
        }

        private bool _loadingAi;

        private void LoadAiSettings()
        {
            var ai = AiSettingsStore.Load();
            _loadingAi = true;
            chkAiEnabled.IsChecked = ai.Enabled;
            txtAiBaseUrl.Text = ai.BaseUrl;
            pwdAiKey.Password = ai.ApiKey;
            txtAiModel.Text = ai.Model;
            txtAiSystemPrompt.Text = ai.SystemPrompt;
            sldAiTemperature.Value = Math.Clamp(ai.Temperature, 0, 2);
            txtAiTemperature.Text = ai.Temperature.ToString("F1");
            txtAiContextWindow.Text = ai.ContextWindowTokens.ToString();
            chkAiWebSearch.IsChecked = ai.EnableWebSearch;
            chkAiReasoning.IsChecked = ai.EnableReasoning;

            cmbAiProvider.Items.Clear();
            foreach (var (name, display, _, _) in AiSettings.Presets)
            {
                cmbAiProvider.Items.Add(new ComboBoxItem { Content = display, Tag = name });
            }
            int idx = -1;
            for (int i = 0; i < AiSettings.Presets.Length; i++)
            {
                if (string.Equals(ai.BaseUrl.TrimEnd('/'),
                        AiSettings.Presets[i].Url.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
                {
                    idx = i;
                    break;
                }
            }
            cmbAiProvider.SelectedIndex = idx;
            _loadingAi = false;
        }

        private void UpdateUpdateStatus()
        {
            string owner = DynamicBird.Infrastructure.WinApi.UpdateService.GitHubOwner;
            string repo = DynamicBird.Infrastructure.WinApi.UpdateService.GitHubRepo;
            if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo))
            {
                txtUpdateStatus.Text = DynamicBird.UI.Localization.LocalizationManager.Instance["Set_UpdateSourceMissing"];
            }
            else
            {
                txtUpdateStatus.Text = string.Format(DynamicBird.UI.Localization.LocalizationManager.Instance["Set_UpdateSource"], owner, repo);
            }
        }

        private async void BtnCheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(DynamicBird.Infrastructure.WinApi.UpdateService.GitHubOwner) ||
                string.IsNullOrWhiteSpace(DynamicBird.Infrastructure.WinApi.UpdateService.GitHubRepo))
            {
                txtUpdateStatus.Text = DynamicBird.UI.Localization.LocalizationManager.Instance["Set_UpdateSourceMissing2"];
                return;
            }

            btnCheckUpdate.IsEnabled = false;
            txtUpdateStatus.Text = DynamicBird.UI.Localization.LocalizationManager.Instance["Set_Checking"];
            try
            {
                var current = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version
                              ?? new Version(1, 0, 0);
                var info = await DynamicBird.Infrastructure.WinApi.UpdateService
                    .CheckForUpdateAsync(current);
                txtUpdateStatus.Text = info == null
                    ? DynamicBird.UI.Localization.LocalizationManager.Instance["Set_Latest"]
                    : string.Format(DynamicBird.UI.Localization.LocalizationManager.Instance["Set_NewVersion"], info.Version, info.Tag);
            }
            catch
            {
                txtUpdateStatus.Text = DynamicBird.UI.Localization.LocalizationManager.Instance["Set_CheckFailed"];
            }
            finally
            {
                btnCheckUpdate.IsEnabled = true;
            }
        }

        // ========== 天气城市选择 ==========

        private void BtnPickWeatherCity_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new WeatherCityPickerWindow(txtWeatherCity.Text);
                picker.Owner = this;
                if (picker.ShowDialog() == true && !string.IsNullOrEmpty(picker.SelectedCity))
                {
                    txtWeatherCity.Text = picker.SelectedCity;
                }
            }
            catch (Exception ex)
            {
                DynamicBird.Core.Infrastructure.Logging.LogManager.Error("打开城市选择器失败", ex);
            }
        }

        // ========== 划词翻译 快捷键捕获 ==========

        private const string PanelToggleHotkey = "Ctrl+Alt+B";

        /// <summary>热键捕获框：点击后按下组合键即显示；Backspace/Esc 清除。</summary>
        private void TxtTextAiHotkey_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true; // 只读框不参与输入

            if (e.Key == Key.Escape || (e.Key == Key.Back && Keyboard.Modifiers == ModifierKeys.None))
            {
                txtTextAiHotkey.Text = "";
                UpdateTextAiHotkeyHint();
                return;
            }

            string combo = HotkeyParser.Format(e.Key, Keyboard.Modifiers);
            if (combo.Length == 0) return; // 纯修饰键 / 不支持的键：继续等待组合完成

            txtTextAiHotkey.Text = combo;
            UpdateTextAiHotkeyHint();
        }

        private void BtnClearTextAiHotkey_Click(object sender, RoutedEventArgs e)
        {
            txtTextAiHotkey.Text = "";
            UpdateTextAiHotkeyHint();
        }

        private void UpdateTextAiHotkeyHint()
        {
            if (txtTextAiHotkey == null) return;
            string hotkey = txtTextAiHotkey.Text.Trim();
            if (hotkey.Length == 0)
            {
                txtTextAiHotkeyHint.Text = DynamicBird.UI.Localization.LocalizationManager.Instance["Set_HotkeyHintNotSet"];
                txtTextAiHotkeyHint.Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136));
            }
            else if (hotkey == PanelToggleHotkey)
            {
                txtTextAiHotkeyHint.Text = DynamicBird.UI.Localization.LocalizationManager.Instance["Set_HotkeyConflict"];
                txtTextAiHotkeyHint.Foreground = new SolidColorBrush(Color.FromRgb(200, 80, 70));
            }
            else
            {
                txtTextAiHotkeyHint.Text = DynamicBird.UI.Localization.LocalizationManager.Instance["Set_HotkeySet"];
                txtTextAiHotkeyHint.Foreground = new SolidColorBrush(Color.FromRgb(60, 170, 90));
            }
        }

        // ========== 语言（通用设置） ==========

        private void SelectLanguage(string value)
        {
            foreach (ComboBoxItem item in cmbLanguage.Items)
            {
                if (item.Tag?.ToString() == value)
                {
                    cmbLanguage.SelectedItem = item;
                    return;
                }
            }
            cmbLanguage.SelectedIndex = 0; // 跟随系统
        }

        private void UpdateCurrentLanguageText()
        {
            if (txtCurrentLanguage == null) return;
            var lm = DynamicBird.UI.Localization.LocalizationManager.Instance;
            string suffix = string.IsNullOrEmpty(_settingsData.Language)
                ? "（" + lm["Set_LangFollowSystem"] + "）"
                : "";
            txtCurrentLanguage.Text = lm["Set_LangPrefix"] + lm.CurrentCultureName + suffix;
        }

        // ========== AI 助手设置事件 ==========

        private void CmbAiProvider_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loadingAi) return;
            if (cmbAiProvider.SelectedItem is ComboBoxItem item && item.Tag is string name)
            {
                var preset = Array.Find(AiSettings.Presets, p => p.Name == name);
                if (preset.Name != null)
                {
                    txtAiBaseUrl.Text = preset.Url;
                    txtAiModel.Text = preset.Model;
                }
            }
        }

        private async void BtnAiTest_Click(object sender, RoutedEventArgs e)
        {
            var testSettings = new AiSettings
            {
                Enabled = true,
                BaseUrl = string.IsNullOrWhiteSpace(txtAiBaseUrl.Text) ? "https://api.deepseek.com/v1" : txtAiBaseUrl.Text.Trim(),
                ApiKey = pwdAiKey.Password ?? "",
                Model = string.IsNullOrWhiteSpace(txtAiModel.Text) ? "deepseek-chat" : txtAiModel.Text.Trim()
            };

            btnAiTest.IsEnabled = false;
            txtAiTestStatus.Text = DynamicBird.UI.Localization.LocalizationManager.Instance["Set_Testing"];
            txtAiTestStatus.Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 120));
            try
            {
                using var client = new AiChatClient();
                string? err = await client.TestConnectionAsync(testSettings);
                if (err == null)
                {
                    txtAiTestStatus.Text = DynamicBird.UI.Localization.LocalizationManager.Instance["Set_ConnOk"];
                    txtAiTestStatus.Foreground = new SolidColorBrush(Color.FromRgb(60, 170, 90));
                }
                else
                {
                    txtAiTestStatus.Text = "❌ " + err;
                    txtAiTestStatus.Foreground = new SolidColorBrush(Color.FromRgb(200, 80, 70));
                }
            }
            catch (Exception ex)
            {
                txtAiTestStatus.Text = "❌ " + ex.Message;
                txtAiTestStatus.Foreground = new SolidColorBrush(Color.FromRgb(200, 80, 70));
            }
            finally
            {
                btnAiTest.IsEnabled = true;
            }
        }

        private static void FillPanelCombo(ComboBox combo)
        {
            combo.Items.Clear();
            foreach (var (value, locKey) in PanelOptions)
            {
                combo.Items.Add(new ComboBoxItem
                {
                    Content = DynamicBird.UI.Localization.LocalizationManager.Instance[locKey],
                    Tag = value
                });
            }
        }

        private static void SelectPanelValue(ComboBox combo, string value)
        {
            foreach (ComboBoxItem item in combo.Items)
            {
                if (item.Tag?.ToString() == value)
                {
                    combo.SelectedItem = item;
                    return;
                }
            }
            combo.SelectedIndex = 0;
        }

        private static string GetSelectedPanelValue(ComboBox combo)
        {
            return (combo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Default";
        }

        private ComboBoxItem? GetComboBoxItemByContent(ComboBox combo, string content)
        {
            foreach (ComboBoxItem item in combo.Items)
            {
                if (item.Content?.ToString() == content)
                    return item;
            }
            return null;
        }

        // ========== 触发/隐藏动画类型 ==========

        private static string AnimTypeToLabel(string type, bool isHide) => type switch
        {
            "Fade" => isHide ? "淡出" : "淡入",
            "Zoom" => isHide ? "缩小" : "缩放",
            "Elastic" => "弹性",
            _ => isHide ? "滑出" : "滑入"
        };

        private static string LabelToAnimType(ComboBox cmb, bool isHide) =>
            (cmb.SelectedItem as ComboBoxItem)?.Content?.ToString() switch
            {
                "淡入" or "淡出" => "Fade",
                "缩放" or "缩小" => "Zoom",
                "弹性" => "Elastic",
                _ => "Slide"
            };

        private void UpdateShowAnimRows()
        {
            string label = (cmbShowAnimType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "滑入";
            bool zoom = label == "缩放";
            bool elastic = label == "弹性";
            SetAnimRow(lblShowZoom, sldShowZoomFrom, txtShowZoomFrom, zoom);
            SetAnimRow(lblShowOsc, sldShowOsc, txtShowOsc, elastic);
            SetAnimRow(lblShowSpring, sldShowSpring, txtShowSpring, elastic);
        }

        private void UpdateHideAnimRows()
        {
            string label = (cmbHideAnimType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "滑出";
            bool zoom = label == "缩小";
            bool elastic = label == "弹性";
            SetAnimRow(lblHideZoom, sldHideZoomTo, txtHideZoomTo, zoom);
            SetAnimRow(lblHideOsc, sldHideOsc, txtHideOsc, elastic);
            SetAnimRow(lblHideSpring, sldHideSpring, txtHideSpring, elastic);
        }

        private static void SetAnimRow(FrameworkElement lbl, FrameworkElement sld, FrameworkElement txt, bool visible)
        {
            var v = visible ? Visibility.Visible : Visibility.Collapsed;
            lbl.Visibility = v;
            sld.Visibility = v;
            txt.Visibility = v;
        }

        private void cmbShowAnimType_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateShowAnimRows();
        private void cmbHideAnimType_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateHideAnimRows();

        // ========== 颜色选择器 ==========

        private void BtnBgColorPicker_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new WinForms.ColorDialog();
            dialog.Color = SettingsUIHelper.HexToDrawingColor(txtBgColor.Text ?? "#2D2D2D");
            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                txtBgColor.Text = SettingsUIHelper.DrawingColorToHex(dialog.Color);
            }
        }

        private void BtnTextColorPicker_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new WinForms.ColorDialog();
            dialog.Color = SettingsUIHelper.HexToDrawingColor(txtTextColor.Text ?? "#FFFFFF");
            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                txtTextColor.Text = SettingsUIHelper.DrawingColorToHex(dialog.Color);
            }
        }

        private void BtnNoteColorPicker_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new WinForms.ColorDialog();
            dialog.Color = SettingsUIHelper.HexToDrawingColor(txtDefaultNoteColor.Text ?? "#FFFF99");
            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                txtDefaultNoteColor.Text = SettingsUIHelper.DrawingColorToHex(dialog.Color);
            }
        }

        private void BtnSelectIcon_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.ico|所有文件|*.*",
                Title = "选择自定义图标"
            };
            if (dialog.ShowDialog() == true)
            {
                txtCustomIcon.Text = dialog.FileName;
            }
        }

        // ========== 小工具市场（左侧竖排列表 + 右侧精调） ==========

        private static readonly Dictionary<string, string> _builtinLocKeys = new()
        {
            ["Clipboard"] = "UI_SettingsWindow_317",
            ["Note"] = "UI_SettingsWindow_318",
            ["Timer"] = "UI_SettingsWindow_319",
            ["Calculator"] = "UI_SettingsWindow_320",
            ["TextAi"] = "UI_SettingsWindow_321",
        };

        private string _selectedWidgetKey = "";

        /// <summary>刷新左侧小组件列表（内置 + 用户插件），保持当前选中项。</summary>
        private void RefreshWidgetMarket()
        {
            if (WidgetMarketList == null) return;
            WidgetPluginStore.Reload();
            WidgetMarketList.Children.Clear();

            foreach (var kv in _builtinLocKeys)
                AddMarketItem(kv.Key, LocalizationManager.Instance[kv.Value]);
            foreach (var plugin in WidgetPluginStore.Installed)
                AddPluginMarketItem(plugin);

            if (string.IsNullOrEmpty(_selectedWidgetKey) || !KeyExists(_selectedWidgetKey))
                _selectedWidgetKey = "Clipboard";
            SelectWidget(_selectedWidgetKey);
        }

        private bool KeyExists(string key)
        {
            if (_builtinLocKeys.ContainsKey(key)) return true;
            return WidgetPluginStore.Installed.Any(p => "Widget_" + p.Id == key);
        }
        private void AddMarketItem(string key, string name)
        {
            WidgetMarketList.Children.Add(BuildMarketRow(key, name, null));
        }

        private void AddPluginMarketItem(WidgetPlugin plugin)
        {
            WidgetMarketList.Children.Add(BuildMarketRow("Widget_" + plugin.Id, plugin.Name, plugin));
        }

        /// <summary>构建左侧列表项：勾选框（启用，即时生效）+ 名称按钮（左键选中精调，右键菜单）。</summary>
        private Grid BuildMarketRow(string key, string name, WidgetPlugin? plugin)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var chk = new CheckBox
            {
                IsChecked = _settings.IsWidgetEnabled(key),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0)
            };
            chk.Checked += (_, _) => _settings.SetWidgetEnabled(key, true);
            chk.Unchecked += (_, _) => _settings.SetWidgetEnabled(key, false);
            row.Children.Add(chk);

            var btn = new System.Windows.Controls.Button
            {
                Content = name,
                Tag = key,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(6, 5, 6, 5),
                FontSize = 12
            };
            btn.Click += (_, _) => SelectWidget(key);
            btn.ContextMenu = BuildMarketMenu(key, plugin);
            row.Children.Add(btn);
            System.Windows.Controls.Grid.SetColumn(btn, 1);
            return row;
        }

        /// <summary>右键菜单：仅用户插件提供编辑/删除（启停已由左侧勾选框承担）。</summary>
        private ContextMenu BuildMarketMenu(string key, WidgetPlugin? plugin)
        {
            var menu = new ContextMenu();
            if (plugin != null)
            {
                var miEdit = new MenuItem { Header = LocalizationManager.Instance["WidgetMkt_Edit"] };
                miEdit.Click += (_, _) => OpenWidgetEditor(plugin);
                menu.Items.Add(miEdit);
                var miDelete = new MenuItem { Header = LocalizationManager.Instance["WidgetMkt_Delete"] };
                miDelete.Click += (_, _) => DeletePlugin(plugin);
                menu.Items.Add(miDelete);
            }
            return menu;
        }

        /// <summary>左键选中：切换右侧精调面板 + 列表高亮。</summary>
        private void SelectWidget(string key)
        {
            _selectedWidgetKey = key;
            foreach (var row in WidgetMarketList.Children.OfType<Grid>())
            {
                var btn = row.Children.OfType<System.Windows.Controls.Button>().FirstOrDefault();
                if (btn == null) continue;
                btn.Background = (btn.Tag as string) == key
                    ? new SolidColorBrush(Color.FromRgb(229, 241, 255))
                    : System.Windows.Media.Brushes.Transparent;
            }
            DetailClipboard.Visibility = key == "Clipboard" ? Visibility.Visible : Visibility.Collapsed;
            DetailNote.Visibility = key == "Note" ? Visibility.Visible : Visibility.Collapsed;
            DetailTimer.Visibility = key == "Timer" ? Visibility.Visible : Visibility.Collapsed;
            DetailCalc.Visibility = key == "Calculator" ? Visibility.Visible : Visibility.Collapsed;
            DetailTextAi.Visibility = key == "TextAi" ? Visibility.Visible : Visibility.Collapsed;
            if (key.StartsWith("Widget_"))
            {
                DetailPlugin.Visibility = Visibility.Visible;
                FillPluginDetail(key.Substring("Widget_".Length));
            }
            else
            {
                DetailPlugin.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>精调区：用户插件的状态/权限/启用/编辑/删除。</summary>
        private void FillPluginDetail(string id)
        {
            DetailPlugin.Children.Clear();
            var plugin = WidgetPluginStore.GetById(id);
            if (plugin == null) return;
            string key = "Widget_" + plugin.Id;

            DetailPlugin.Children.Add(new TextBlock
            {
                Text = plugin.Name,
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 4)
            });

            bool compileOk = string.IsNullOrEmpty(WidgetCompiler.Validate(plugin.Id, plugin.Source));
            var permText = plugin.Permissions.Count == 0
                ? LocalizationManager.Instance["WidgetMkt_None"]
                : string.Join(" · ", plugin.Permissions.Select(WidgetPluginStore.PermissionLabel));
            DetailPlugin.Children.Add(new TextBlock
            {
                Text = (compileOk ? "✅  " : "⚠ 编译失败  ") + permText,
                FontSize = 10.5,
                Foreground = new SolidColorBrush(compileOk
                    ? (plugin.Permissions.Count > 0 ? Color.FromRgb(255, 170, 90) : Color.FromRgb(136, 136, 136))
                    : Color.FromRgb(200, 80, 70)),
                Margin = new Thickness(0, 0, 0, 10),
                TextWrapping = TextWrapping.Wrap
            });

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal };
            var btnEdit = new System.Windows.Controls.Button
            {
                Content = LocalizationManager.Instance["WidgetMkt_Edit"],
                Style = (Style)FindResource("Win11Button"),
                Width = 76,
                Height = 26,
                FontSize = 11
            };
            btnEdit.Click += (_, _) => OpenWidgetEditor(plugin);
            btnRow.Children.Add(btnEdit);
            var btnDel = new System.Windows.Controls.Button
            {
                Content = LocalizationManager.Instance["WidgetMkt_Delete"],
                Style = (Style)FindResource("Win11Button"),
                Width = 76,
                Height = 26,
                FontSize = 11,
                Margin = new Thickness(8, 0, 0, 0)
            };
            btnDel.Click += (_, _) => DeletePlugin(plugin);
            btnRow.Children.Add(btnDel);
            DetailPlugin.Children.Add(btnRow);
        }

        private void OpenWidgetEditor(WidgetPlugin plugin)
        {
            var b = new WidgetEditorWindow(plugin) { Owner = this };
            if (b.ShowDialog() == true) RefreshWidgetMarket();
        }

        private void DeletePlugin(WidgetPlugin plugin)
        {
            if (MessageBox.Show(string.Format(LocalizationManager.Instance["WidgetMkt_DeleteConfirm"], plugin.Name),
                    LocalizationManager.Instance["WidgetMkt_Confirm"],
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _settings.SetWidgetEnabled("Widget_" + plugin.Id, false);
                WidgetPluginStore.Delete(plugin.Id);
                RefreshWidgetMarket();
            }
        }

        private void BtnNewWidget_Click(object sender, RoutedEventArgs e)
        {
            var b = new WidgetEditorWindow { Owner = this };
            if (b.ShowDialog() == true) RefreshWidgetMarket();
        }



        private void BtnRefreshWidgets_Click(object sender, RoutedEventArgs e) => RefreshWidgetMarket();

        private void BtnOpenMarket_Click(object sender, RoutedEventArgs e)
        {
            var w = new DynamicBird.UI.Widgets.Dynamic.WidgetMarketWindow { Owner = this };
            w.ShowDialog();
        }


        // ========== 逐区域触发/隐藏延时 ==========

        /// <summary>动态生成 16 个区域的 触发延时/隐藏延时 行（排版：区域名 | 触发滑块 | 隐藏滑块）。</summary>
        private void BuildRegionDelayRows()
        {
            if (RegionDelayGrid == null) return;
            RegionDelayGrid.RowDefinitions.Clear();
            for (int i = 0; i < RegionDelayConfig.Length; i++)
            {
                RegionDelayGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
            }

            _regionDelayControls.Clear();
            for (int i = 0; i < RegionDelayConfig.Length; i++)
            {
                var (key, locKey) = RegionDelayConfig[i];

                var name = new TextBlock
                {
                    Text = LocalizationManager.Instance[locKey],
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(0, 0, 4, 0)
                };
                Grid.SetRow(name, i);
                Grid.SetColumn(name, 0);

                var trig = MakeDelaySlider();
                Grid.SetRow(trig, i);
                Grid.SetColumn(trig, 1);
                var trigText = MakeDelayText();
                Grid.SetRow(trigText, i);
                Grid.SetColumn(trigText, 2);

                var hide = MakeDelaySlider();
                Grid.SetRow(hide, i);
                Grid.SetColumn(hide, 3);
                var hideText = MakeDelayText();
                Grid.SetRow(hideText, i);
                Grid.SetColumn(hideText, 4);

                int trigMs = _settingsData.RegionTriggerDelay != null && _settingsData.RegionTriggerDelay.TryGetValue(key, out int tv)
                    ? tv : _settingsData.TriggerDelayMs;
                int hideMs = _settingsData.RegionHideDelay != null && _settingsData.RegionHideDelay.TryGetValue(key, out int hv)
                    ? hv : _settingsData.HideDelayMs;
                trig.Value = trigMs;
                trigText.Text = trigMs + "ms";
                hide.Value = hideMs;
                hideText.Text = hideMs + "ms";

                trig.ValueChanged += (_, _) => trigText.Text = ((int)trig.Value) + "ms";
                hide.ValueChanged += (_, _) => hideText.Text = ((int)hide.Value) + "ms";

                RegionDelayGrid.Children.Add(name);
                RegionDelayGrid.Children.Add(trig);
                RegionDelayGrid.Children.Add(trigText);
                RegionDelayGrid.Children.Add(hide);
                RegionDelayGrid.Children.Add(hideText);

                _regionDelayControls[key] = new RegionDelayControls { Trig = trig, TrigText = trigText, Hide = hide, HideText = hideText };
            }
        }

        private static Slider MakeDelaySlider()
        {
            return new Slider
            {
                Minimum = 0,
                Maximum = 1000,
                TickFrequency = 25,
                IsSnapToTickEnabled = true,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
        }

        private static TextBlock MakeDelayText()
        {
            return new TextBlock
            {
                FontSize = 11,
                TextAlignment = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        // ========== 自动保存（实时生效）/ 刷新 ==========

        /// <summary>把所有控件值写入设置数据（不含保存/应用，供实时保存与刷新调用）。</summary>
        private void ApplyControlsToData()
        {
            // 触发位置
            _settingsData.Edge_Top = chkTop.IsChecked ?? true;
            _settingsData.Edge_Bottom = chkBottom.IsChecked ?? true;
            _settingsData.Edge_Left = chkLeft.IsChecked ?? true;
            _settingsData.Edge_Right = chkRight.IsChecked ?? true;
            _settingsData.Corner_TopLeft = chkTopLeft.IsChecked ?? true;
            _settingsData.Corner_TopRight = chkTopRight.IsChecked ?? true;
            _settingsData.Corner_BottomLeft = chkBottomLeft.IsChecked ?? true;
            _settingsData.Corner_BottomRight = chkBottomRight.IsChecked ?? true;

            // 边行为模式
            _settingsData.EdgeMode_Top = SettingsUIHelper.GetComboMode(cmbTopMode);
            _settingsData.EdgeMode_Bottom = SettingsUIHelper.GetComboMode(cmbBottomMode);
            _settingsData.EdgeMode_Left = SettingsUIHelper.GetComboMode(cmbLeftMode);
            _settingsData.EdgeMode_Right = SettingsUIHelper.GetComboMode(cmbRightMode);

            // 固定形状
            _settingsData.FixedShape_Top = SettingsUIHelper.GetShapeValue(cmbFixedShapeTop);
            _settingsData.FixedShape_Bottom = SettingsUIHelper.GetShapeValue(cmbFixedShapeBottom);
            _settingsData.FixedShape_Left = SettingsUIHelper.GetShapeValue(cmbFixedShapeLeft);
            _settingsData.FixedShape_Right = SettingsUIHelper.GetShapeValue(cmbFixedShapeRight);

            // 区域形状
            _settingsData.Region_Top_Left = SettingsUIHelper.GetShapeValue(cmbTopLeft);
            _settingsData.Region_Top_Center = SettingsUIHelper.GetShapeValue(cmbTopCenter);
            _settingsData.Region_Top_Right = SettingsUIHelper.GetShapeValue(cmbTopRight);
            _settingsData.Region_Bottom_Left = SettingsUIHelper.GetShapeValue(cmbBottomLeft);
            _settingsData.Region_Bottom_Center = SettingsUIHelper.GetShapeValue(cmbBottomCenter);
            _settingsData.Region_Bottom_Right = SettingsUIHelper.GetShapeValue(cmbBottomRight);
            _settingsData.Region_Left_Top = SettingsUIHelper.GetShapeValue(cmbLeftTop);
            _settingsData.Region_Left_Center = SettingsUIHelper.GetShapeValue(cmbLeftCenter);
            _settingsData.Region_Left_Bottom = SettingsUIHelper.GetShapeValue(cmbLeftBottom);
            _settingsData.Region_Right_Top = SettingsUIHelper.GetShapeValue(cmbRightTop);
            _settingsData.Region_Right_Center = SettingsUIHelper.GetShapeValue(cmbRightCenter);
            _settingsData.Region_Right_Bottom = SettingsUIHelper.GetShapeValue(cmbRightBottom);

            // 外观
            _settingsData.BackgroundColor = txtBgColor.Text;
            _settingsData.TextColor = txtTextColor.Text;
            _settingsData.Opacity = sldOpacity.Value;
            _settingsData.CornerRadius = (int)sldCornerRadius.Value;
            _settingsData.ShowSystemStatus = chkShowSystemStatus.IsChecked ?? true;
            _settingsData.CustomIconPath = txtCustomIcon.Text;

            // 动画与布局
            _settingsData.HorizontalLayoutThreshold = sldHorizontalThreshold.Value;
            _settingsData.TagWidth = sldTagWidth.Value;

            // 任务栏
            _settingsData.TaskbarIconSize = Math.Round(sldIconSize.Value);

            // 剪贴板与便签
            _settingsData.ClipboardMaxCount = (int)sldClipboardMax.Value;
            _settingsData.ClipboardDisplayLength = (int)sldClipboardDisplay.Value;
            _settingsData.ClipboardImageMaxWidth = (int)sldClipImageMax.Value;
            _settingsData.ClipboardImageCacheLimitMB = (int)sldClipImageCacheLimit.Value;
            _settingsData.DefaultNoteColor = txtDefaultNoteColor.Text;
            _settingsData.NoteShowTitleByDefault = chkNoteShowTitle.IsChecked ?? true;

            // 自适应
            _settingsData.AutoFitOnTrigger = chkAutoFitOnTrigger.IsChecked ?? true;

            // 勿扰
            _settingsData.RememberDndMode = chkRememberDndMode.IsChecked ?? false;

            // ★★★ 动画设置 ★★★
            _settingsData.AnimationsEnabled = chkAnimationsEnabled.IsChecked ?? true;

            string transformEasing = SettingsUIHelper.GetEasingValue(
                (cmbTransformEasing.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "立方缓动");
            _settingsData.TransformEasingType = transformEasing;

            // ★ 触发/隐藏动画保存
            _settingsData.ShowAnimationType = LabelToAnimType(cmbShowAnimType, isHide: false);
            _settingsData.HideAnimationType = LabelToAnimType(cmbHideAnimType, isHide: true);
            _settingsData.ShowAnimationDurationMs = (int)sldShowDuration.Value;
            _settingsData.HideAnimationDurationMs = (int)sldHideDuration.Value;
            _settingsData.ShowAnimationZoomFrom = sldShowZoomFrom.Value;
            _settingsData.HideAnimationZoomTo = sldHideZoomTo.Value;
            _settingsData.ShowAnimationOscillations = (int)sldShowOsc.Value;
            _settingsData.HideAnimationOscillations = (int)sldHideOsc.Value;
            _settingsData.ShowAnimationSpringiness = sldShowSpring.Value;
            _settingsData.HideAnimationSpringiness = sldHideSpring.Value;
            _settingsData.TransformDurationMs = (int)sldTransformDuration.Value;
            _settingsData.ContentStabilizeMs = (int)sldContentStabilize.Value;
            _settingsData.SnapRangePx = (int)sldSnapRange.Value;
            _settingsData.HideDelayMs = (int)sldHideDelay.Value;
            _settingsData.FlyDurationMs = (int)sldFlyDuration.Value;

            // ★★★ 小鸟依人模式 ★★★
            _settingsData.ClingModeEnabled = chkClingMode.IsChecked ?? false;

            // ★ 面板点击穿透修饰键（双写：本地副本 + 设置服务）
            string passthroughSel = "Ctrl";
            if (cmbPassthrough.SelectedItem is System.Windows.Controls.ComboBoxItem pasCbi)
                passthroughSel = pasCbi.Tag?.ToString() ?? "Ctrl";
            _settingsData.PassthroughModifier = passthroughSel;
            _settings.PassthroughModifier = passthroughSel;

            // ★★★ 区域防抖 ★★★
            _settingsData.RegionDebounceMs = (int)sldRegionDebounce.Value;

            // ★★★ 触发距离与延时（全局默认 + 逐区域） ★★★
            _settingsData.TriggerDistancePx = (int)sldTrigDist.Value;
            _settingsData.TriggerDelayMs = (int)sldGlbTrig.Value;
            // HideDelayMs 由"动画"标签页的 sldHideDelay 写入（sldGlbHide 已与之联动）
            _settingsData.RegionTriggerDelay = new System.Collections.Generic.Dictionary<string, int>();
            _settingsData.RegionHideDelay = new System.Collections.Generic.Dictionary<string, int>();
            foreach (var (key, c) in _regionDelayControls)
            {
                // 仅记录与全局不同的值；与全局相同 → 不写字典 → 始终跟随全局（后续改全局仍生效）
                if ((int)c.Trig.Value != (int)sldGlbTrig.Value)
                    _settingsData.RegionTriggerDelay[key] = (int)c.Trig.Value;
                if ((int)c.Hide.Value != (int)sldGlbHide.Value)
                    _settingsData.RegionHideDelay[key] = (int)c.Hide.Value;
            }

            // ★★★ 区域面板自定义 ★★★
            foreach (string key in RegionPanelKeys)
            {
                var combo = (ComboBox?)FindName("cmbPanel_" + key);
                if (combo == null) continue;
                string panelValue = GetSelectedPanelValue(combo);
                _settings.SetRegionPanel(key, panelValue);
                // ★ 双写：同步进 _settingsData，否则后续 _settings.Apply(_settingsData)
                //   会用旧副本把这里刚设置的区域面板覆盖丢失。
                switch (key)
                {
                    case "Top_Left": _settingsData.RegionPanel_Top_Left = panelValue; break;
                    case "Top_Center": _settingsData.RegionPanel_Top_Center = panelValue; break;
                    case "Top_Right": _settingsData.RegionPanel_Top_Right = panelValue; break;
                    case "Bottom_Left": _settingsData.RegionPanel_Bottom_Left = panelValue; break;
                    case "Bottom_Center": _settingsData.RegionPanel_Bottom_Center = panelValue; break;
                    case "Bottom_Right": _settingsData.RegionPanel_Bottom_Right = panelValue; break;
                    case "Left_Top": _settingsData.RegionPanel_Left_Top = panelValue; break;
                    case "Left_Center": _settingsData.RegionPanel_Left_Center = panelValue; break;
                    case "Left_Bottom": _settingsData.RegionPanel_Left_Bottom = panelValue; break;
                    case "Right_Top": _settingsData.RegionPanel_Right_Top = panelValue; break;
                    case "Right_Center": _settingsData.RegionPanel_Right_Center = panelValue; break;
                    case "Right_Bottom": _settingsData.RegionPanel_Right_Bottom = panelValue; break;
                    case "TopLeft": _settingsData.RegionPanel_TopLeft = panelValue; break;
                    case "TopRight": _settingsData.RegionPanel_TopRight = panelValue; break;
                    case "BottomLeft": _settingsData.RegionPanel_BottomLeft = panelValue; break;
                    case "BottomRight": _settingsData.RegionPanel_BottomRight = panelValue; break;
                }
            }

            // ★★★ 语言（通用设置） ★★★
            _settingsData.Language = (cmbLanguage.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";

            // ★★★ 划词翻译 热键（静默校验：冲突/无效则不写入，提示由输入框下方文字展示） ★★★
            string textAiHotkey = txtTextAiHotkey.Text.Trim();
            if (textAiHotkey.Length == 0 ||
                (textAiHotkey != PanelToggleHotkey && HotkeyParser.TryParse(textAiHotkey, out _, out _)))
            {
                _settingsData.TextAiHotkey = textAiHotkey;
            }

            // ★★★ 天气城市（选择器只改文本框，这里统一写入并即时保存） ★★★
            _settings.WeatherCity = txtWeatherCity.Text;
            _settingsData.WeatherCity = txtWeatherCity.Text; // ★ 双写，避免 Apply 覆盖

            // ★★★ 性能模式 ★★★
            string perfMode = _settingsData.PerformanceMode ?? PerformancePresets.Normal;
            if (!PerformancePresets.Matches(_settingsData, perfMode))
            {
                _settingsData.PerformanceMode = PerformancePresets.Custom;
            }
        }

        private System.Windows.Threading.DispatcherTimer? _saveTimer;

        /// <summary>防抖自动保存：设置控件变化后 400ms 内未再变化则写入并应用。</summary>
        private void ScheduleSave()
        {
            if (_saveTimer == null)
            {
                _saveTimer = new System.Windows.Threading.DispatcherTimer();
                _saveTimer.Interval = TimeSpan.FromMilliseconds(400);
                _saveTimer.Tick += (s, e) =>
                {
                    _saveTimer.Stop();
                    SaveSettingsNow();
                };
            }
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        /// <summary>立即把所有控件值写入设置并保存应用（含语言切换与 AI 配置）。</summary>
        private void SaveSettingsNow()
        {
            try
            {
                ApplyControlsToData();
                // ★ 关键修复：把设置副本整体同步进 SettingsManager 再落盘。
                //   之前 ApplyControlsToData 只写本地 _settingsData，_settings.SaveSettings()
                //   保存的是 SettingsManager 内部从未更新的旧数据 → 设置改动全部丢失，
                //   刷新/重启后还原（曾导致"关掉小鸟依人刷新又开"、面板一直跟随鼠标）。
                _settings.Apply(_settingsData);
                SaveAiSettings();
                DynamicBird.Infrastructure.WinApi.WeatherService.ClearCache();
                DynamicBird.UI.Localization.LocalizationManager.Instance.SetCulture(_settingsData.Language);
                _settings.SaveSettings();
            }
            catch (Exception ex)
            {
                DynamicBird.Core.Infrastructure.Logging.LogManager.Error("自动保存设置失败", ex);
            }
        }

        /// <summary>AI 助手配置（独立存储，随实时保存一起写入）。</summary>
        private void SaveAiSettings()
        {
            try
            {
                var ai = new AiSettings
                {
                    Enabled = chkAiEnabled.IsChecked ?? false,
                    BaseUrl = txtAiBaseUrl.Text.Trim(),
                    ApiKey = pwdAiKey.Password ?? "",
                    Model = txtAiModel.Text.Trim(),
                    SystemPrompt = txtAiSystemPrompt.Text,
                    Temperature = sldAiTemperature.Value,
                    ContextWindowTokens = int.TryParse(txtAiContextWindow.Text, out var cw) ? cw : 8000,
                    EnableWebSearch = chkAiWebSearch.IsChecked ?? false,
                    EnableReasoning = chkAiReasoning.IsChecked ?? false
                };
                DynamicBird.Core.Services.Ai.AiSettingsStore.Save(ai);
            }
            catch { }
        }

        /// <summary>所有设置控件值变化 → 实时自动保存（滑块/勾选/下拉/文本，防抖）。</summary>
        private void HookAutoSave()
        {
            foreach (var s in FindVisualChildren<Slider>(this))
                s.ValueChanged += (_, _) => ScheduleSave();
            foreach (var c in FindVisualChildren<CheckBox>(this))
            {
                c.Checked += (_, _) => ScheduleSave();
                c.Unchecked += (_, _) => ScheduleSave();
            }
            foreach (var c in FindVisualChildren<ComboBox>(this))
                c.SelectionChanged += (_, _) => ScheduleSave();
            foreach (var t in FindVisualChildren<TextBox>(this))
                t.TextChanged += (_, _) => ScheduleSave();

            // 语言：选中立即切换界面语言（不等防抖）
            cmbLanguage.SelectionChanged += (_, _) =>
            {
                _settingsData.Language = (cmbLanguage.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
                DynamicBird.UI.Localization.LocalizationManager.Instance.SetCulture(_settingsData.Language);
                ScheduleSave();
            };
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t) yield return t;
                foreach (var sub in FindVisualChildren<T>(child)) yield return sub;
            }
        }

        /// <summary>刷新：重新从磁盘加载设置并刷新所有控件显示（面板已通过 SettingsChanged 同步应用）。</summary>
        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveSettingsNow();
                LoadSettings();
                UpdateCurrentLanguageText();
            }
            catch (Exception ex)
            {
                DynamicBird.Core.Infrastructure.Logging.LogManager.Error("刷新设置失败", ex);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            // 关闭前立即保存挂起的变化（防抖未触发时兜底）
            if (_saveTimer != null && _saveTimer.IsEnabled)
            {
                _saveTimer.Stop();
                SaveSettingsNow();
            }
            base.OnClosed(e);
        }

        private void BtnOnboarding_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var onboarding = new DynamicBird.UI.Onboarding.OnboardingWindow(
                    noMore => _settings.OnboardingCompleted = noMore,
                    _settings);
                onboarding.Owner = this;
                onboarding.ShowDialog();
            }
            catch
            {
            }
        }
    }
}