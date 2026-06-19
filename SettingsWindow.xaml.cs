using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LingDongBird.Core;
using Microsoft.Win32;
using WinForms = System.Windows.Forms;

namespace LingDongBird
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();

            // 滑块数值显示联动
            sldOpacity.ValueChanged += (s, e) => txtOpacityValue.Text = sldOpacity.Value.ToString("F2");
            sldCornerRadius.ValueChanged += (s, e) => txtCornerRadiusValue.Text = sldCornerRadius.Value.ToString("F0");

            sldStripLength.ValueChanged += (s, e) => txtStripLength.Text = (sldStripLength.Value * 100).ToString("F0") + "%";
            sldStripWidth.ValueChanged += (s, e) => txtStripWidth.Text = sldStripWidth.Value.ToString("F1");
            sldSquareShort.ValueChanged += (s, e) => txtSquareShort.Text = sldSquareShort.Value.ToString("F1");
            sldGolden.ValueChanged += (s, e) => txtGolden.Text = sldGolden.Value.ToString("F2");
            sldRegionRatio.ValueChanged += (s, e) => txtRegionRatio.Text = (sldRegionRatio.Value * 100).ToString("F0") + "%";
            sldAnimDuration.ValueChanged += (s, e) => txtAnimDuration.Text = sldAnimDuration.Value.ToString("F0");
            sldHorizontalThreshold.ValueChanged += (s, e) => txtHorizontalThreshold.Text = (sldHorizontalThreshold.Value * 100).ToString("F0") + "%";
            sldTagWidth.ValueChanged += (s, e) => txtTagWidth.Text = sldTagWidth.Value.ToString("F0");
        }

        private void LoadSettings()
        {
            // ---- 触发位置 ----
            chkTop.IsChecked = SettingsManager.IsEdgeEnabled("Top");
            chkBottom.IsChecked = SettingsManager.IsEdgeEnabled("Bottom");
            chkLeft.IsChecked = SettingsManager.IsEdgeEnabled("Left");
            chkRight.IsChecked = SettingsManager.IsEdgeEnabled("Right");
            chkTopLeft.IsChecked = SettingsManager.IsCornerEnabled("TopLeft");
            chkTopRight.IsChecked = SettingsManager.IsCornerEnabled("TopRight");
            chkBottomLeft.IsChecked = SettingsManager.IsCornerEnabled("BottomLeft");
            chkBottomRight.IsChecked = SettingsManager.IsCornerEnabled("BottomRight");

            // ---- 边模式 ----
            SetComboSelected(cmbTopMode, SettingsManager.GetEdgeMode("Top"));
            SetComboSelected(cmbBottomMode, SettingsManager.GetEdgeMode("Bottom"));
            SetComboSelected(cmbLeftMode, SettingsManager.GetEdgeMode("Left"));
            SetComboSelected(cmbRightMode, SettingsManager.GetEdgeMode("Right"));

            // ---- 固定位置形状 ----
            SetShapeComboSelected(cmbFixedShapeTop, SettingsManager.GetFixedShape("Top"));
            SetShapeComboSelected(cmbFixedShapeBottom, SettingsManager.GetFixedShape("Bottom"));
            SetShapeComboSelected(cmbFixedShapeLeft, SettingsManager.GetFixedShape("Left"));
            SetShapeComboSelected(cmbFixedShapeRight, SettingsManager.GetFixedShape("Right"));

            // ---- 区域形状 ----
            SetShapeComboSelected(cmbTopLeft, SettingsManager.GetRegionShape("Top", "Left"));
            SetShapeComboSelected(cmbTopCenter, SettingsManager.GetRegionShape("Top", "Center"));
            SetShapeComboSelected(cmbTopRight, SettingsManager.GetRegionShape("Top", "Right"));
            SetShapeComboSelected(cmbBottomLeft, SettingsManager.GetRegionShape("Bottom", "Left"));
            SetShapeComboSelected(cmbBottomCenter, SettingsManager.GetRegionShape("Bottom", "Center"));
            SetShapeComboSelected(cmbBottomRight, SettingsManager.GetRegionShape("Bottom", "Right"));
            SetShapeComboSelected(cmbLeftTop, SettingsManager.GetRegionShape("Left", "Top"));
            SetShapeComboSelected(cmbLeftCenter, SettingsManager.GetRegionShape("Left", "Center"));
            SetShapeComboSelected(cmbLeftBottom, SettingsManager.GetRegionShape("Left", "Bottom"));
            SetShapeComboSelected(cmbRightTop, SettingsManager.GetRegionShape("Right", "Top"));
            SetShapeComboSelected(cmbRightCenter, SettingsManager.GetRegionShape("Right", "Center"));
            SetShapeComboSelected(cmbRightBottom, SettingsManager.GetRegionShape("Right", "Bottom"));

            // ---- 外观 ----
            txtBgColor.Text = SettingsManager.BackgroundColor;
            txtTextColor.Text = SettingsManager.TextColor;
            sldOpacity.Value = SettingsManager.Opacity;
            sldCornerRadius.Value = SettingsManager.CornerRadius;
            chkShowSystemStatus.IsChecked = SettingsManager.ShowSystemStatus;
            txtCustomIcon.Text = SettingsManager.CustomIconPath;

            // ---- 形状与动画 ----
            sldStripLength.Value = SettingsManager.StripLengthRatio;
            sldStripWidth.Value = SettingsManager.StripWidthMultiplier;
            sldSquareShort.Value = SettingsManager.SquareShortSideMultiplier;
            sldGolden.Value = SettingsManager.GoldenRatio;
            sldRegionRatio.Value = SettingsManager.TriggerRegionRatio;
            sldAnimDuration.Value = SettingsManager.AnimationDurationMs;

            // ---- 布局 ----
            sldHorizontalThreshold.Value = SettingsManager.HorizontalLayoutThreshold;
            sldTagWidth.Value = SettingsManager.TagWidth;

            // ---- 当前模式 ----
            txtCurrentMode.Text = SettingsManager.CurrentMode == "Taskbar" ? "任务栏模式" : "应用辅助模式";
        }

        private void SetComboSelected(System.Windows.Controls.ComboBox combo, string mode)
        {
            foreach (ComboBoxItem item in combo.Items)
            {
                string content = item.Content.ToString();
                if ((mode == "Follow" && content == "跟随鼠标") ||
                    (mode == "Fixed" && content == "固定位置"))
                {
                    combo.SelectedItem = item;
                    break;
                }
            }
        }

        private void SetShapeComboSelected(System.Windows.Controls.ComboBox combo, string shape)
        {
            foreach (ComboBoxItem item in combo.Items)
            {
                string content = item.Content.ToString();
                if (content == GetShapeDisplayName(shape))
                {
                    combo.SelectedItem = item;
                    break;
                }
            }
        }

        private string GetShapeDisplayName(string shape)
        {
            return shape switch
            {
                "Square" => "方形",
                "StripH" => "横条",
                "StripV" => "竖条",
                _ => "默认"
            };
        }

        private string GetShapeValue(System.Windows.Controls.ComboBox combo)
        {
            var item = combo.SelectedItem as ComboBoxItem;
            if (item == null) return "Default";
            string content = item.Content.ToString();
            return content switch
            {
                "方形" => "Square",
                "横条" => "StripH",
                "竖条" => "StripV",
                _ => "Default"
            };
        }

        private string GetComboMode(System.Windows.Controls.ComboBox combo)
        {
            var item = combo.SelectedItem as ComboBoxItem;
            if (item == null) return "Follow";
            return item.Content.ToString() == "跟随鼠标" ? "Follow" : "Fixed";
        }

        // ---------- 颜色选择器 ----------
        private void BtnBgColorPicker_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new WinForms.ColorDialog();
            dialog.Color = HexToDrawingColor(txtBgColor.Text);
            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                txtBgColor.Text = DrawingColorToHex(dialog.Color);
            }
        }

        private void BtnTextColorPicker_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new WinForms.ColorDialog();
            dialog.Color = HexToDrawingColor(txtTextColor.Text);
            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                txtTextColor.Text = DrawingColorToHex(dialog.Color);
            }
        }

        private System.Drawing.Color HexToDrawingColor(string hex)
        {
            try
            {
                if (string.IsNullOrEmpty(hex)) return System.Drawing.Color.FromArgb(45, 45, 45);
                if (hex.StartsWith("#")) hex = hex.Substring(1);
                byte a = 255, r = 0, g = 0, b = 0;
                if (hex.Length == 6)
                {
                    r = Convert.ToByte(hex.Substring(0, 2), 16);
                    g = Convert.ToByte(hex.Substring(2, 2), 16);
                    b = Convert.ToByte(hex.Substring(4, 2), 16);
                }
                else if (hex.Length == 8)
                {
                    a = Convert.ToByte(hex.Substring(0, 2), 16);
                    r = Convert.ToByte(hex.Substring(2, 2), 16);
                    g = Convert.ToByte(hex.Substring(4, 2), 16);
                    b = Convert.ToByte(hex.Substring(6, 2), 16);
                }
                else return System.Drawing.Color.FromArgb(45, 45, 45);
                return System.Drawing.Color.FromArgb(a, r, g, b);
            }
            catch { return System.Drawing.Color.FromArgb(45, 45, 45); }
        }

        private string DrawingColorToHex(System.Drawing.Color color)
        {
            return $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        // ---------- 自定义图标 ----------
        private void BtnSelectIcon_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Filter = "图片文件|*.png;*.jpg;*.jpeg;*.bmp;*.ico|所有文件|*.*";
            dialog.Title = "选择自定义图标";
            if (dialog.ShowDialog() == true)
            {
                txtCustomIcon.Text = dialog.FileName;
            }
        }

        // ---------- 保存 ----------
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // ---- 触发位置 ----
            SettingsManager.SetEdgeEnabled("Top", chkTop.IsChecked ?? true);
            SettingsManager.SetEdgeEnabled("Bottom", chkBottom.IsChecked ?? true);
            SettingsManager.SetEdgeEnabled("Left", chkLeft.IsChecked ?? true);
            SettingsManager.SetEdgeEnabled("Right", chkRight.IsChecked ?? true);
            SettingsManager.SetCornerEnabled("TopLeft", chkTopLeft.IsChecked ?? true);
            SettingsManager.SetCornerEnabled("TopRight", chkTopRight.IsChecked ?? true);
            SettingsManager.SetCornerEnabled("BottomLeft", chkBottomLeft.IsChecked ?? true);
            SettingsManager.SetCornerEnabled("BottomRight", chkBottomRight.IsChecked ?? true);

            // ---- 边模式 ----
            SettingsManager.SetEdgeMode("Top", GetComboMode(cmbTopMode));
            SettingsManager.SetEdgeMode("Bottom", GetComboMode(cmbBottomMode));
            SettingsManager.SetEdgeMode("Left", GetComboMode(cmbLeftMode));
            SettingsManager.SetEdgeMode("Right", GetComboMode(cmbRightMode));

            // ---- 固定位置形状 ----
            SettingsManager.SetFixedShape("Top", GetShapeValue(cmbFixedShapeTop));
            SettingsManager.SetFixedShape("Bottom", GetShapeValue(cmbFixedShapeBottom));
            SettingsManager.SetFixedShape("Left", GetShapeValue(cmbFixedShapeLeft));
            SettingsManager.SetFixedShape("Right", GetShapeValue(cmbFixedShapeRight));

            // ---- 区域形状 ----
            SettingsManager.SetRegionShape("Top", "Left", GetShapeValue(cmbTopLeft));
            SettingsManager.SetRegionShape("Top", "Center", GetShapeValue(cmbTopCenter));
            SettingsManager.SetRegionShape("Top", "Right", GetShapeValue(cmbTopRight));
            SettingsManager.SetRegionShape("Bottom", "Left", GetShapeValue(cmbBottomLeft));
            SettingsManager.SetRegionShape("Bottom", "Center", GetShapeValue(cmbBottomCenter));
            SettingsManager.SetRegionShape("Bottom", "Right", GetShapeValue(cmbBottomRight));
            SettingsManager.SetRegionShape("Left", "Top", GetShapeValue(cmbLeftTop));
            SettingsManager.SetRegionShape("Left", "Center", GetShapeValue(cmbLeftCenter));
            SettingsManager.SetRegionShape("Left", "Bottom", GetShapeValue(cmbLeftBottom));
            SettingsManager.SetRegionShape("Right", "Top", GetShapeValue(cmbRightTop));
            SettingsManager.SetRegionShape("Right", "Center", GetShapeValue(cmbRightCenter));
            SettingsManager.SetRegionShape("Right", "Bottom", GetShapeValue(cmbRightBottom));

            // ---- 外观 ----
            SettingsManager.BackgroundColor = txtBgColor.Text;
            SettingsManager.TextColor = txtTextColor.Text;
            SettingsManager.Opacity = sldOpacity.Value;
            SettingsManager.CornerRadius = (int)sldCornerRadius.Value;
            SettingsManager.ShowSystemStatus = chkShowSystemStatus.IsChecked ?? true;

            // 自定义图标
            string iconPath = txtCustomIcon.Text;
            if (!string.IsNullOrEmpty(iconPath) && System.IO.File.Exists(iconPath))
            {
                SettingsManager.CustomIconPath = iconPath;
            }
            else
            {
                SettingsManager.CustomIconPath = "";
            }

            // ---- 形状与动画 ----
            SettingsManager.StripLengthRatio = sldStripLength.Value;
            SettingsManager.StripWidthMultiplier = sldStripWidth.Value;
            SettingsManager.SquareShortSideMultiplier = sldSquareShort.Value;
            SettingsManager.GoldenRatio = sldGolden.Value;
            SettingsManager.TriggerRegionRatio = sldRegionRatio.Value;
            SettingsManager.AnimationDurationMs = (int)sldAnimDuration.Value;

            // ---- 布局 ----
            SettingsManager.HorizontalLayoutThreshold = sldHorizontalThreshold.Value;
            SettingsManager.TagWidth = sldTagWidth.Value;

            System.Windows.MessageBox.Show("设置已保存", "灵动鸟", MessageBoxButton.OK, MessageBoxImage.Information);
            this.Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}