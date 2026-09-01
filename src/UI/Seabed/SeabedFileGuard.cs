using System;
using System.IO;
using System.Windows;
using ShoreHue.Infrastructure.Utils;

namespace ShoreHue.UI.Seabed
{
    /// <summary>
    /// ShoreHue 内部文件守卫：删除任何 ShoreHue 管理的内部文件前弹窗警告（防误删导致运行异常）。
    /// 「用户自定义内容」（便签/快捷方式/网页收藏/剪贴板历史等用户主动创建的数据）不适用；
    /// 卸载 ShoreHue 不适用（走独立卸载流程）。
    /// </summary>
    public static class SeabedFileGuard
    {
        /// <summary>ShoreHue 内部数据根目录。</summary>
        public static string DataRoot => AppPaths.DataRoot;

        /// <summary>
        /// 判定路径是否属于"ShoreHue 内部文件"（应用配置/预设/海床文件夹等 ShoreHue 管理的文件）。
        /// 用户在资源管理器里手动放的自定义文件（如 seabed 里的用户 .cs）也算 ShoreHue 管理的文件——删了可能影响对应功能。
        /// </summary>
        public static bool IsInternalFile(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return false;
                string full = Path.GetFullPath(path);
                string root = Path.GetFullPath(DataRoot);
                // 数据根目录内的文件都是 ShoreHue 管理的
                if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return true;
                return false;
            }
            catch { return false; }
        }

        /// <summary>删除前守卫：弹警告，返回 true=可以删除，false=用户取消。</summary>
        public static bool ConfirmDelete(Window owner, string what, string? title = null, string? message = null)
        {
            var warn = new ConfirmDialog(
                title ?? "删除 ShoreHue 内部文件",
                message ?? ("「" + what + "」是 ShoreHue 内部文件，删除可能导致运行异常。\n\n确定要删除吗？（删除自定义内容与卸载 ShoreHue 不受此提示影响）"),
                "确定删除", "取消")
            {
                Owner = owner
            };
            return warn.ShowDialog() == true;
        }
    }
}
