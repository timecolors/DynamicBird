using DynamicBird.Animation;
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
        // ========== 触发/隐藏动画类型（类型 ⇄ 中文标签，含自定义动画） ==========

        /// <summary>自定义动画项 Tag 前缀（XAML 静态项无 Tag，用前缀区分以便重建）。</summary>
        private const string CustomAnimTagPrefix = "custom_anim:";

        /// <summary>类型 → 中文标签：自定义动画（注册表 Id 命中）返回其显示名；内置走映射。</summary>
        private static string AnimTypeToLabel(string type, bool isHide)
        {
            if (!string.IsNullOrEmpty(type) && AnimationRegistry.TryGet(type, out var custom) && custom != null)
                return custom.Name;
            return type switch
            {
                "Fade" => isHide ? "淡出" : "淡入",
                "Zoom" => isHide ? "缩小" : "缩放",
                "Elastic" => "弹性",
                _ => isHide ? "滑出" : "滑入"
            };
        }

        /// <summary>中文标签 → 类型：自定义动画（Name 命中）返回其 Id；内置走映射。</summary>
        private static string LabelToAnimType(ComboBox cmb, bool isHide)
        {
            var item = cmb.SelectedItem as ComboBoxItem;
            string label = item?.Content?.ToString() ?? "";
            // 自定义动画项：Tag 直接存 Id（比 Name 反查更稳，同名不冲突）
            if (item?.Tag is string tag && tag.StartsWith(CustomAnimTagPrefix, StringComparison.Ordinal))
                return tag.Substring(CustomAnimTagPrefix.Length);
            // 兜底：按 Name 反查注册表（旧配置/手工构造的项）
            foreach (var a in AnimationRegistry.All)
            {
                if (a.Name == label) return a.Id;
            }
            return label switch
            {
                "淡入" or "淡出" => "Fade",
                "缩放" or "缩小" => "Zoom",
                "弹性" => "Elastic",
                _ => "Slide"
            };
        }

        /// <summary>把自定义动画（鸟笼「动画」分组）作为选项加入动画类型下拉：Content=Name，Tag=custom_anim:&lt;Id&gt;。</summary>
        private static void RefreshCustomAnimItems(ComboBox cmb)
        {
            if (cmb == null) return;
            // 移除旧的自定义项（保留 XAML 静态内置项）
            var stale = cmb.Items.Cast<ComboBoxItem>()
                .Where(i => i.Tag is string t && t.StartsWith(CustomAnimTagPrefix, StringComparison.Ordinal))
                .ToList();
            foreach (var s in stale) cmb.Items.Remove(s);
            // 追加当前注册的自定义动画
            foreach (var a in AnimationRegistry.All)
            {
                cmb.Items.Add(new ComboBoxItem
                {
                    Content = a.Name,
                    Tag = CustomAnimTagPrefix + a.Id
                });
            }
        }

        private static void SelectComboByAnimType(ComboBox cmb, string type, bool isHide)
        {
            string label = AnimTypeToLabel(type, isHide);
            foreach (ComboBoxItem item in cmb.Items)
            {
                if (item.Content?.ToString() == label) { cmb.SelectedItem = item; return; }
            }
            cmb.SelectedIndex = 0;
        }

        /// <summary>是否选中的是自定义动画项（隐藏 Zoom/Elastic 特化参数行）。</summary>
        private static bool IsCustomAnimSelected(ComboBox cmb)
        {
            return (cmb.SelectedItem as ComboBoxItem)?.Tag is string t
                && t.StartsWith(CustomAnimTagPrefix, StringComparison.Ordinal);
        }

        private void UpdateShowAnimRows()
        {
            string label = (cmbShowAnimType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "滑入";
            bool custom = IsCustomAnimSelected(cmbShowAnimType);
            bool zoom = label == "缩放";
            bool elastic = label == "弹性";
            SetAnimRow(lblShowZoom, sldShowZoomFrom, txtShowZoomFrom, zoom);
            SetAnimRow(lblShowOsc, sldShowOsc, txtShowOsc, elastic);
            SetAnimRow(lblShowSpring, sldShowSpring, txtShowSpring, elastic);
            // 时长行始终显示：自定义动画同样使用「时长」设置（ms 参数）
        }

        private void UpdateHideAnimRows()
        {
            string label = (cmbHideAnimType.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "滑出";
            bool custom = IsCustomAnimSelected(cmbHideAnimType);
            bool zoom = label == "缩小";
            bool elastic = label == "弹性";
            SetAnimRow(lblHideZoom, sldHideZoomTo, txtHideZoomTo, zoom);
            SetAnimRow(lblHideOsc, sldHideOsc, txtHideOsc, elastic);
            SetAnimRow(lblHideSpring, sldHideSpring, txtHideSpring, elastic);
            // 时长行始终显示：自定义动画同样使用「时长」设置（ms 参数）
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