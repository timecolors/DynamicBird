using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using ShoreHue.Core.Infrastructure.Logging;
using ShoreHue.Infrastructure.Utils;

namespace ShoreHue.Infrastructure.WinApi
{
    /// <summary>
    /// 卸载助手（非商店版）：生成并启动 PowerShell 卸载脚本——
    /// 停止应用 → 删除开机启动项 → 删除开始菜单快捷方式 → 删除安装目录 →
    /// 按选择删除本地数据（%LOCALAPPDATA%\ShoreHue）→ 清理更新临时目录 → 删除脚本自身。
    /// 脚本放 %TEMP%（不在安装目录），应用退出后由独立进程执行。
    /// 商店（MSIX）版由系统负责卸载，不提供此入口。
    /// </summary>
    public static class UninstallHelper
    {
        /// <summary>生成并启动卸载脚本。返回是否成功启动（脚本随后会杀掉应用进程）。</summary>
        public static bool LaunchUninstall(bool deleteData)
        {
            try
            {
                if (AppPaths.IsPackaged) return false;

                string exeDir = AppContext.BaseDirectory;
                string dataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ShoreHue");
                string lnk = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Programs), "ShoreHue.lnk");
                string tempUpdate = Path.Combine(Path.GetTempPath(), "ShoreHueUpdate");
                string ps = Path.Combine(Path.GetTempPath(), "uninstall_shorehue.ps1");
                string nl = Environment.NewLine;

                string script =
                    // ★ 1) 停止应用并等待进程真正退出（轮询，最多 5 秒；不再固定睡 800ms——
                    //    句柄未释放时立即删目录会静默失败、残留文件）
                    "Stop-Process -Name ShoreHue -Force -ErrorAction SilentlyContinue" + nl +
                    "for ($i = 0; $i -lt 10 -and (Get-Process -Name ShoreHue -ErrorAction SilentlyContinue); $i++) { Start-Sleep -Milliseconds 500 }" + nl +
                    // 2) 删除开机启动项 + 开始菜单快捷方式
                    "Remove-ItemProperty -Path 'HKCU:\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run' -Name 'ShoreHue' -ErrorAction SilentlyContinue" + nl +
                    "Remove-Item -LiteralPath '" + Escape(lnk) + "' -Force -ErrorAction SilentlyContinue" + nl +
                    // 3) 删除目录（安装目录 + 按选择的数据目录 + 更新临时目录），带存在性检查与重试
                    "$dirs = @('" + Escape(exeDir) + "'" +
                    (deleteData ? ", '" + Escape(dataDir) + "'" : "") +
                    ", '" + Escape(tempUpdate) + "')" + nl +
                    "foreach ($d in $dirs) {" + nl +
                    "  if (Test-Path -LiteralPath $d) {" + nl +
                    "    for ($i = 0; $i -lt 5; $i++) {" + nl +
                    "      try { Remove-Item -LiteralPath $d -Recurse -Force -ErrorAction Stop; break }" + nl +
                    "      catch { Start-Sleep -Milliseconds 600 }" + nl +
                    "    }" + nl +
                    "  }" + nl +
                    "}" + nl +
                    // 4) 自删脚本
                    "Remove-Item -LiteralPath '" + Escape(ps) + "' -Force -ErrorAction SilentlyContinue";

                File.WriteAllText(ps, script, new UTF8Encoding(true));

                Process.Start(new ProcessStartInfo("powershell.exe",
                    "-NoProfile -ExecutionPolicy Bypass -File \"" + ps + "\"")
                {
                    UseShellExecute = true,
                    CreateNoWindow = true
                });
                return true;
            }
            catch (Exception ex)
            {
                LogManager.Error("启动卸载脚本失败", ex);
                return false;
            }
        }

        private static string Escape(string s) => s.Replace("'", "''");
    }
}
