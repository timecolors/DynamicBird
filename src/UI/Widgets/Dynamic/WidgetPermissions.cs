using System.Collections.Generic;
using System.Linq;

namespace DynamicBird.UI.Widgets.Dynamic
{
    /// <summary>
    /// 小组件/单预设源码权限检测：扫描源码用到的能力并自动标注。
    /// 仅在「其他鸟笼」市场上传/导出时调用 Detect(源码) 做检测并随包下发风险标签，
    /// 导入方用 PermissionLabel/Describe 展示提醒用户；本地编程不做检测。
    /// 检测是保守的（宁可多标不可漏标）：命中关键字即标注，供风险提示用，不代表一定执行。
    /// </summary>
    public static class WidgetPermissions
    {
        /// <summary>检测源码所需权限（可多个，按 network/clipboard/file/process/system/window/screen 顺序）。</summary>
        public static List<string> Detect(string source)
        {
            var perms = new List<string>();
            if (string.IsNullOrEmpty(source)) return perms;
            string lower = source.ToLower();

            // 🌐 联网：HTTP/网络请求
            if (lower.Contains("httpclient") || lower.Contains("webclient") ||
                lower.Contains("httprequest") || lower.Contains("webrequest") ||
                lower.Contains("httplistener") || lower.Contains("system.net") ||
                lower.Contains("tcpclient") || lower.Contains("udpclient") ||
                lower.Contains("websocket") || lower.Contains("downloadstring") ||
                lower.Contains("getstringasync") || lower.Contains("postasync") ||
                lower.Contains("sendasync") || lower.Contains("uri("))
            {
                perms.Add("network");
            }

            // 📋 剪贴板
            if (lower.Contains("clipboard") || lower.Contains("idataobject"))
            {
                perms.Add("clipboard");
            }

            // 📁 本地文件
            if (lower.Contains("system.io") || lower.Contains("file.") ||
                lower.Contains("directory.") || lower.Contains("filestream") ||
                lower.Contains("streamwriter") || lower.Contains("streamreader") ||
                lower.Contains("path.combine") || lower.Contains("savedialog") ||
                lower.Contains("opendialog") || lower.Contains("fileinfo"))
            {
                perms.Add("file");
            }

            // ⚙️ 进程与命令执行
            if (lower.Contains("process.start") || lower.Contains("processstartinfo") ||
                lower.Contains("new process(") || lower.Contains("cmd.exe") ||
                lower.Contains("powershell") || lower.Contains("useshellexecute") ||
                lower.Contains("shell.execute"))
            {
                perms.Add("process");
            }

            // 🖥️ 系统信息 / 注册表 / 原生调用
            if (lower.Contains("system.management") || lower.Contains("managementobject") ||
                lower.Contains("registry") || lower.Contains("performancecounter") ||
                lower.Contains("dllimport") || lower.Contains("environment.getenvironmentvariable") ||
                lower.Contains("wmi"))
            {
                perms.Add("system");
            }

            // 🪟 窗口与输入（可能影响其他应用或监听输入）
            if (lower.Contains("findwindow") || lower.Contains("enumwindows") ||
                lower.Contains("setforegroundwindow") || lower.Contains("getforegroundwindow") ||
                lower.Contains("windowfrompoint") || lower.Contains("sendmessage") ||
                lower.Contains("sendinput") || lower.Contains("setwindowshookex") ||
                lower.Contains("keybd_event") || lower.Contains("mouse_event") ||
                lower.Contains("getcursorpos") || lower.Contains("setcursorpos"))
            {
                perms.Add("window");
            }

            // 📸 屏幕捕获
            if (lower.Contains("copyfromscreen") || lower.Contains("printwindow") ||
                lower.Contains("bitblt") || lower.Contains("dwmthumbnail") ||
                lower.Contains("screencapture") || lower.Contains("getwindowrect") ||
                lower.Contains("graphics.copy"))
            {
                perms.Add("screen");
            }

            return perms;
        }

        /// <summary>权限 → 显示标签。</summary>
        public static string PermissionLabel(string p) => p switch
        {
            "network" => "🌐 联网",
            "clipboard" => "📋 剪贴板",
            "file" => "📁 本地文件",
            "process" => "⚙️ 进程执行",
            "system" => "🖥️ 系统信息",
            "window" => "🪟 窗口操作",
            "screen" => "📸 屏幕捕获",
            _ => "🔒 无权限"
        };

        /// <summary>把权限列表渲染为紧凑可读文本（空/未知 → "🔒 无权限"）。</summary>
        public static string Describe(IEnumerable<string>? perms)
        {
            if (perms == null) return "🔒 无权限";
            var list = perms.ToList();
            if (list.Count == 0) return "🔒 无权限";
            return string.Join(" ", list.Select(PermissionLabel));
        }
    }
}
