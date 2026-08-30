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
    public partial class SettingsWindow
    {
        // ========== 触发/隐藏动画类型（类型 ⇄ 中文标签） ==========

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

        private static void SelectComboByAnimType(ComboBox cmb, string type, bool isHide)
        {
            string label = AnimTypeToLabel(type, isHide);
            foreach (ComboBoxItem item in cmb.Items)
            {
                if (item.Content?.ToString() == label) { cmb.SelectedItem = item; return; }
            }
            cmb.SelectedIndex = 0;
        }

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
    }
}
