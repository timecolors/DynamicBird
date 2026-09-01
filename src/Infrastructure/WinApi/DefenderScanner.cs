using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ShoreHue.Infrastructure.WinApi
{
    /// <summary>
    /// Windows Defender（Microsoft Defender）扫描封装：
    /// 导入「其他海床」包前用 MpCmdRun 扫描，捕获嵌入的已知威胁（恶意二进制/脚本特征）。
    /// 诚实定位：源码类攻击（C# 文本）无特征可匹配，Defender 扫不出恶意意图——
    /// 这只是"已扫描"保证层 + 抓嵌入载荷，真正的安全边界是 WidgetCompiler 编译期沙箱。
    /// </summary>
    public static class DefenderScanner
    {
        public enum ScanResult { Clean, ThreatFound, Unavailable }

        public static async Task<(ScanResult Result, string Detail)> ScanFileAsync(string path)
        {
            string? exe = FindMpCmdRun();
            if (exe == null)
            {
                return (ScanResult.Unavailable, "未找到 Windows Defender（可能使用第三方杀软，跳过扫描）");
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = "-Scan -ScanType 3 -File \"" + path + "\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using var proc = Process.Start(psi);
                if (proc == null) return (ScanResult.Unavailable, "Defender 启动失败");
                var outTask = proc.StandardOutput.ReadToEndAsync();
                var errTask = proc.StandardError.ReadToEndAsync();
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                    await proc.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    try { proc.Kill(); } catch { }
                    return (ScanResult.Unavailable, "Defender 扫描超时");
                }
                string output = await outTask + Environment.NewLine + await errTask;

                var m = Regex.Match(output, @"Threats\s+Found:\s*(\d+)", RegexOptions.IgnoreCase);
                int threats = m.Success ? int.Parse(m.Groups[1].Value) : 0;
                if (threats > 0)
                {
                    return (ScanResult.ThreatFound, "Windows Defender 检出 " + threats + " 个威胁");
                }
                return (ScanResult.Clean, "Windows Defender 未发现已知威胁");
            }
            catch (Exception ex)
            {
                return (ScanResult.Unavailable, "Defender 扫描失败: " + ex.Message);
            }
        }

        private static string? FindMpCmdRun()
        {
            var candidates = new List<string>();
            try
            {
                candidates.Add(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "Windows Defender", "MpCmdRun.exe"));
            }
            catch { }
            try
            {
                string pd = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "Microsoft", "Windows Defender", "Platform");
                if (Directory.Exists(pd))
                {
                    foreach (var dir in Directory.GetDirectories(pd).OrderByDescending(d => d))
                    {
                        candidates.Add(Path.Combine(dir, "MpCmdRun.exe"));
                    }
                }
            }
            catch { }
            return candidates.FirstOrDefault(File.Exists);
        }
    }
}
