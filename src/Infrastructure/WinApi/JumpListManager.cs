using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Shell;

namespace DynamicBird.Infrastructure.WinApi
{
    /// <summary>
    /// 任务栏 Jump List（右键任务栏/开始菜单快捷方式 → 常用操作）。
    /// WPF 原生 JumpList + JumpTask：任务通过命令行参数传给进程；
    /// 单实例下由主窗口解析参数执行对应动作（打开设置/切换勿扰/呼出面板）。
    /// ★ 依赖开始菜单快捷方式的 System.AppUserModel.ID（SystemToast.EnsureRegistered 已创建）。
    /// 托盘常驻应用价值有限（需任务栏按钮/快捷方式可见），但固定到任务栏后可用。
    /// </summary>
    public static class JumpListManager
    {
        // ===== 命令行参数（JumpTask.Arguments 传递） =====
        public const string ArgOpenSettings = "--open-settings";
        public const string ArgToggleDnd = "--toggle-dnd";
        public const string ArgTogglePanel = "--toggle-panel";

        /// <summary>配置并应用 Jump List（启动时调用；AppUserModelID 关联开始菜单快捷方式）。</summary>
        public static void Configure()
        {
            try
            {
                string exe = Environment.ProcessPath ?? "";
                if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
                {
                    return;
                }

                var jumpList = new JumpList
                {
                    ShowRecentCategory = false,
                    ShowFrequentCategory = false
                };

                // ★ 任务：指向本进程 exe + 参数（单实例下参数由主窗口解析执行）
                jumpList.JumpItems.Add(new JumpTask
                {
                    Title = DynamicBird.UI.Localization.LocalizationManager.Instance["Jump_OpenSettings"],
                    Description = DynamicBird.UI.Localization.LocalizationManager.Instance["Jump_OpenSettingsDesc"],
                    ApplicationPath = exe,
                    Arguments = ArgOpenSettings,
                    IconResourcePath = exe,
                    IconResourceIndex = 0
                });
                jumpList.JumpItems.Add(new JumpTask
                {
                    Title = DynamicBird.UI.Localization.LocalizationManager.Instance["Jump_ToggleDnd"],
                    Description = DynamicBird.UI.Localization.LocalizationManager.Instance["Jump_ToggleDndDesc"],
                    ApplicationPath = exe,
                    Arguments = ArgToggleDnd,
                    IconResourcePath = exe,
                    IconResourceIndex = 0
                });
                jumpList.JumpItems.Add(new JumpTask
                {
                    Title = DynamicBird.UI.Localization.LocalizationManager.Instance["Jump_TogglePanel"],
                    Description = DynamicBird.UI.Localization.LocalizationManager.Instance["Jump_TogglePanelDesc"],
                    ApplicationPath = exe,
                    Arguments = ArgTogglePanel,
                    IconResourcePath = exe,
                    IconResourceIndex = 0
                });

                // ★ 通过 AppUserModelID 关联（与 Toast 同一标识，任务栏分组/跳转列表归属一致）
                JumpList.SetJumpList(System.Windows.Application.Current, jumpList);
            }
            catch (Exception ex)
            {
                DynamicBird.Core.Infrastructure.Logging.LogManager.Error("配置 Jump List 失败", ex);
            }
        }

        /// <summary>解析命令行参数，返回要执行的动作集合（空 = 无动作）。</summary>
        public static List<string> ParseActions(string[] args)
        {
            var actions = new List<string>();
            if (args == null) return actions;
            foreach (var a in args)
            {
                if (a == ArgOpenSettings || a == ArgToggleDnd || a == ArgTogglePanel)
                {
                    if (!actions.Contains(a)) actions.Add(a);
                }
            }
            return actions;
        }
    }
}
