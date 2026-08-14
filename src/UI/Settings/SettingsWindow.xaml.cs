using DynamicBird.Core.Services;
using DynamicBird.Core.Services.Configuration;
using DynamicBird.src.core.Services.Shortcuts;
using DynamicBird.UI.Settings.Pages;
using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WinForms = System.Windows.Forms;

namespace DynamicBird.UI.Settings
{
    public partial class SettingsWindow : Window
    {
        private readonly ISettingsService _settings;
        private readonly IShortcutService _shortcutService;
        private SettingsData _settingsData = null!;

        // 区域面板自定义选项与区域键
        private static readonly (string Value, string Display)[] PanelOptions =
        {
            ("Default", "默认布局"),
            ("Taskbar", "任务栏"),
            ("Widget", "小组件"),
            ("AppHelper", "应用辅助"),
            ("Notification", "通知坞"),
            ("Recent", "最近使用"),
            ("QuickSettings", "快捷设置"),
        };

        private static readonly string[] RegionPanelKeys =
        {
            "Top_Left", "Top_Center", "Top_Right",
            "Bottom_Left", "Bottom_Center", "Bottom_Right",
            "Left_Top", "Left_Center", "Left_Bottom",
            "Right_Top", "Right_Center", "Right_Bottom",
            "TopLeft", "TopRight", "BottomLeft", "BottomRight"
        };

        public SettingsWindow(ISettingsService settings, IShortcutService shortcutService)
        {
            _settings = settings;
            _shortcutService = shortcutService;
            InitializeComponent();
            // Win11 22H2+ 启用 Mica 浅色背景，与系统设置应用观感一致
            SourceInitialized += (_, _) => TryApplyMicaBackdrop();
            LoadSettings();
            LoadShortcutPage();

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

            // 动画设置滑块
            sldShowHideDuration.ValueChanged += (s, e) => txtShowHideDuration.Text = sldShowHideDuration.Value.ToString("F0") + "ms";
            sldTransformDuration.ValueChanged += (s, e) => txtTransformDuration.Text = sldTransformDuration.Value.ToString("F0") + "ms";
            sldHideDelay.ValueChanged += (s, e) => txtHideDelay.Text =
                sldHideDelay.Value <= 0 ? "0（立即）" : sldHideDelay.Value.ToString("F0") + "ms";
            sldFlyDuration.ValueChanged += (s, e) => txtFlyDuration.Text = sldFlyDuration.Value.ToString("F0") + "ms";

            // ★★★ 区域防抖滑块 ★★★
            sldRegionDebounce.ValueChanged += (s, e) => txtRegionDebounce.Text = sldRegionDebounce.Value.ToString("F0") + "ms";
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
            txtDefaultNoteColor.Text = _settingsData.DefaultNoteColor ?? "#FFFF99";
            chkNoteShowTitle.IsChecked = _settingsData.NoteShowTitleByDefault;

            // 自适应
            chkAutoFitOnTrigger.IsChecked = _settingsData.AutoFitOnTrigger;

            // 勿扰
            chkRememberDndMode.IsChecked = _settingsData.RememberDndMode;

            // ★★★ 动画设置 ★★★
            chkAnimationsEnabled.IsChecked = _settingsData.AnimationsEnabled;

            cmbShowHideEasing.SelectedItem = GetComboBoxItemByContent(cmbShowHideEasing, SettingsUIHelper.GetEasingDisplayName(_settingsData.ShowHideEasingType ?? "CubicEase"));
            cmbTransformEasing.SelectedItem = GetComboBoxItemByContent(cmbTransformEasing, SettingsUIHelper.GetEasingDisplayName(_settingsData.TransformEasingType ?? "CubicEase"));

            sldShowHideDuration.Value = _settingsData.ShowHideDurationMs;
            txtShowHideDuration.Text = _settingsData.ShowHideDurationMs + "ms";

            sldTransformDuration.Value = _settingsData.TransformDurationMs;
            txtTransformDuration.Text = _settingsData.TransformDurationMs + "ms";

            sldHideDelay.Value = _settingsData.HideDelayMs;
            txtHideDelay.Text = _settingsData.HideDelayMs <= 0
                ? "0（立即）"
                : _settingsData.HideDelayMs + "ms";

            sldFlyDuration.Value = _settingsData.FlyDurationMs;
            txtFlyDuration.Text = _settingsData.FlyDurationMs + "ms";

            // ★★★ 小鸟依人模式 ★★★
            chkClingMode.IsChecked = _settingsData.ClingModeEnabled;

            // ★★★ 区域防抖 ★★★
            sldRegionDebounce.Value = _settingsData.RegionDebounceMs;
            txtRegionDebounce.Text = _settingsData.RegionDebounceMs + "ms";

            // ★★★ 区域面板自定义 ★★★
            foreach (string key in RegionPanelKeys)
            {
                var combo = (ComboBox?)FindName("cmbPanel_" + key);
                if (combo == null) continue;
                FillPanelCombo(combo);
                SelectPanelValue(combo, _settings.GetRegionPanel(key));
            }

            // ★★★ 自动更新 ★★★
            chkAutoCheckUpdate.IsChecked = _settingsData.AutoCheckUpdate;
            UpdateUpdateStatus();
        }

        private void UpdateUpdateStatus()
        {
            string owner = DynamicBird.Infrastructure.WinApi.UpdateService.GitHubOwner;
            string repo = DynamicBird.Infrastructure.WinApi.UpdateService.GitHubRepo;
            if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo))
            {
                txtUpdateStatus.Text = "更新源未配置（内置常量待填写）";
            }
            else
            {
                txtUpdateStatus.Text = $"更新源：github.com/{owner}/{repo}";
            }
        }

        private async void BtnCheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(DynamicBird.Infrastructure.WinApi.UpdateService.GitHubOwner) ||
                string.IsNullOrWhiteSpace(DynamicBird.Infrastructure.WinApi.UpdateService.GitHubRepo))
            {
                txtUpdateStatus.Text = "更新源未配置，请联系开发者填写";
                return;
            }

            btnCheckUpdate.IsEnabled = false;
            txtUpdateStatus.Text = "正在检查更新…";
            try
            {
                var current = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version
                              ?? new Version(1, 0, 0);
                var info = await DynamicBird.Infrastructure.WinApi.UpdateService
                    .CheckForUpdateAsync(current);
                txtUpdateStatus.Text = info == null
                    ? "已是最新版本"
                    : $"发现新版本 v{info.Version}（{info.Tag}），保存设置后可在通知坞点击更新";
            }
            catch
            {
                txtUpdateStatus.Text = "检查失败，请确认网络和仓库信息";
            }
            finally
            {
                btnCheckUpdate.IsEnabled = true;
            }
        }

        private static void FillPanelCombo(ComboBox combo)
        {
            combo.Items.Clear();
            foreach (var (value, display) in PanelOptions)
            {
                combo.Items.Add(new ComboBoxItem { Content = display, Tag = value });
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

        // ========== 保存/关闭 ==========

        private void BtnSave_Click(object sender, RoutedEventArgs e)
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
            _settingsData.DefaultNoteColor = txtDefaultNoteColor.Text;
            _settingsData.NoteShowTitleByDefault = chkNoteShowTitle.IsChecked ?? true;

            // 自适应
            _settingsData.AutoFitOnTrigger = chkAutoFitOnTrigger.IsChecked ?? true;

            // 勿扰
            _settingsData.RememberDndMode = chkRememberDndMode.IsChecked ?? false;

            // ★★★ 动画设置 ★★★
            _settingsData.AnimationsEnabled = chkAnimationsEnabled.IsChecked ?? true;

            string showHideEasing = SettingsUIHelper.GetEasingValue(
                (cmbShowHideEasing.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "立方缓动");
            _settingsData.ShowHideEasingType = showHideEasing;

            string transformEasing = SettingsUIHelper.GetEasingValue(
                (cmbTransformEasing.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "立方缓动");
            _settingsData.TransformEasingType = transformEasing;

            _settingsData.ShowHideDurationMs = (int)sldShowHideDuration.Value;
            _settingsData.TransformDurationMs = (int)sldTransformDuration.Value;
            _settingsData.HideDelayMs = (int)sldHideDelay.Value;
            _settingsData.FlyDurationMs = (int)sldFlyDuration.Value;

            // ★★★ 小鸟依人模式 ★★★
            _settingsData.ClingModeEnabled = chkClingMode.IsChecked ?? false;

            // ★★★ 区域防抖 ★★★
            _settingsData.RegionDebounceMs = (int)sldRegionDebounce.Value;

            // ★★★ 区域面板自定义 ★★★
            foreach (string key in RegionPanelKeys)
            {
                var combo = (ComboBox?)FindName("cmbPanel_" + key);
                if (combo == null) continue;
                _settings.SetRegionPanel(key, GetSelectedPanelValue(combo));
            }

            // ★★★ 自动更新 ★★★
            _settingsData.AutoCheckUpdate = chkAutoCheckUpdate.IsChecked ?? true;

            // 保存
            SettingsDataManager.Save(_settingsData);
            _settings.Reload();

            MessageBox.Show("设置已保存", "灵动鸟", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnOnboarding_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var onboarding = new DynamicBird.UI.Onboarding.OnboardingWindow(
                    () => _settings.OnboardingCompleted = true);
                onboarding.Owner = this;
                onboarding.ShowDialog();
            }
            catch (Exception ex)
            {
                DynamicBird.Core.Infrastructure.Logging.LogManager.Error("打开引导窗口失败", ex);
            }
        }
    }
}
