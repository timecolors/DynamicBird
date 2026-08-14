using System;
using System.Windows.Media;
using DynamicBird.Core.Services;

namespace DynamicBird.UI.Panels
{
    /// <summary>
    /// 任务栏面板项目类型
    /// </summary>
    public enum TaskbarItemType
    {
        /// <summary>用户自定义快捷方式</summary>
        Shortcut,
        /// <summary>正在运行的任务窗口</summary>
        Window
    }

    /// <summary>
    /// 任务栏面板项目数据模型
    /// </summary>
    public class TaskbarItem
    {
        /// <summary>
        /// 唯一标识
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// 项目类型
        /// </summary>
        public TaskbarItemType Type { get; set; }

        /// <summary>
        /// 显示名称（快捷方式名或窗口标题）
        /// </summary>
        public string DisplayName { get; set; } = "";

        /// <summary>
        /// 应用路径（Shortcut 为快捷方式目标；Window 为所属进程 exe，供“固定到任务栏”使用）
        /// </summary>
        public string? Path { get; set; }

        /// <summary>
        /// 窗口句柄（仅 Window 类型有效）
        /// </summary>
        public IntPtr? Handle { get; set; }

        /// <summary>
        /// 图标
        /// </summary>
        public ImageSource? Icon { get; set; }

        /// <summary>
        /// 排序顺序
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// 是否可见（用于快捷方式隐藏）
        /// </summary>
        public bool IsVisible { get; set; } = true;

        /// <summary>
        /// 对应的快捷方式数据（仅 Shortcut 类型有效）
        /// </summary>
        public ShortcutData? ShortcutData { get; set; }

        /// <summary>
        /// 是否正在运行（仅 Shortcut 类型有效）
        /// </summary>
        public bool IsRunning { get; set; }

        // ============ 工厂方法 ============

        public static TaskbarItem FromShortcut(ShortcutData data, ImageSource? icon)
        {
            return new TaskbarItem
            {
                Id = data.Id,
                Type = TaskbarItemType.Shortcut,
                DisplayName = data.Name,
                Path = data.Path,
                Icon = icon,
                Order = data.Order,
                IsVisible = data.IsVisible,
                ShortcutData = data,
                IsRunning = false
            };
        }

        public static TaskbarItem FromWindow(IntPtr handle, string title, ImageSource? icon, string? exePath = null)
        {
            return new TaskbarItem
            {
                Id = $"window_{handle.ToInt64()}",
                Type = TaskbarItemType.Window,
                DisplayName = title,
                Handle = handle,
                Path = exePath,
                Icon = icon,
                Order = 0,
                IsVisible = true,
                IsRunning = true
            };
        }
    }
}
