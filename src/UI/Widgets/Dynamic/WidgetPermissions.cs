using System.Collections.Generic;

namespace DynamicBird.UI.Widgets.Dynamic
{
    /// <summary>
    /// 小组件源码权限检测：导出/上传市场时由系统自动检测源码用到的能力并标注，
    /// 不再要求用户手动勾选权限。
    /// </summary>
    public static class WidgetPermissions
    {
        /// <summary>检测源码所需权限：network / clipboard / file（按顺序，可多个）。</summary>
        public static List<string> Detect(string source)
        {
            var perms = new List<string>();
            if (string.IsNullOrEmpty(source)) return perms;
            string lower = source.ToLower();

            if (lower.Contains("httpclient") || lower.Contains("webclient") ||
                lower.Contains("system.net") || lower.Contains("socket") ||
                lower.Contains("httplistener") || lower.Contains("uri(") ||
                lower.Contains("process.start") || lower.Contains("dllimport"))
            {
                perms.Add("network");
            }
            if (lower.Contains("clipboard"))
            {
                perms.Add("clipboard");
            }
            if (lower.Contains("system.io") || lower.Contains("file.") ||
                lower.Contains("directory.") || lower.Contains("filestream") ||
                lower.Contains("streamwriter") || lower.Contains("streamreader") ||
                lower.Contains("path.combine") || lower.Contains("savedialog") ||
                lower.Contains("opendialog"))
            {
                perms.Add("file");
            }
            return perms;
        }
    }
}
