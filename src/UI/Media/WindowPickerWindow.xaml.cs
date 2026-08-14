using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using DynamicBird.Infrastructure.WinApi;

namespace DynamicBird.UI.Media
{
    public class WindowPickItem
    {
        public IntPtr Handle { get; set; }
        public string DisplayName { get; set; } = "";
    }

    public partial class WindowPickerWindow : Window
    {
        public IntPtr? SelectedHandle { get; private set; }

        private WindowPickerWindow(List<WindowPickItem> items, bool embedMode)
        {
            InitializeComponent();
            WindowList.ItemsSource = items;
            if (items.Count > 0) WindowList.SelectedIndex = 0;

            if (embedMode)
            {
                Title = "选择要嵌入的窗口";
                ModeHintText.Text = "嵌入真实窗口：窗口会移动到面板内，可直接操作（第二块显示屏）。";
                BtnConfirm.Content = "开始嵌入";
            }
            else
            {
                Title = "选择要镜像的窗口";
                ModeHintText.Text = "截图镜像：原窗口位置不变，面板内显示其画面，可点击操作。";
                BtnConfirm.Content = "开始镜像";
            }
        }

        /// <summary>
        /// 弹出窗口选择器（embed=true 嵌入真实窗口，false 截图镜像），取消返回 null。
        /// </summary>
        public static IntPtr? Pick(Window owner, bool embedMode)
        {
            var items = GetPickableWindows();
            var picker = new WindowPickerWindow(items, embedMode)
            {
                Owner = owner
            };
            bool ok = picker.ShowDialog() == true;
            return ok ? picker.SelectedHandle : null;
        }

        private static List<WindowPickItem> GetPickableWindows()
        {
            uint currentPid = (uint)Environment.ProcessId;
            var result = new List<WindowPickItem>();

            foreach (var w in WindowListProvider.GetOpenWindows(WindowListProvider.WindowFilterMode.All))
            {
                if (w.IsToolWindow) continue;
                if (string.IsNullOrWhiteSpace(w.Title)) continue;
                if (GetWindowThreadProcessId(w.Handle, out uint pid) == 0 || pid == currentPid) continue;

                result.Add(new WindowPickItem
                {
                    Handle = w.Handle,
                    DisplayName = w.Title.Length > 60 ? w.Title[..60] + "…" : w.Title
                });
            }

            return result.OrderBy(i => i.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            if (WindowList.SelectedItem is WindowPickItem item)
            {
                SelectedHandle = item.Handle;
                DialogResult = true;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
