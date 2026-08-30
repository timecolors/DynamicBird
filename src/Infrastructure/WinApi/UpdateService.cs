using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DynamicBird.Infrastructure.Utils;

namespace DynamicBird.Infrastructure.WinApi
{
    /// <summary>
    /// 自动更新（GitHub Releases）：
    /// 检查最新 Release，下载资产（zip/exe），SHA256 校验，解压出 exe，
    /// 通过 PowerShell 脚本等主进程退出后替换并重启。
    /// </summary>
    public static class UpdateService
    {
        // ★ 更新源（GitHub Releases）写死在这里：发布时把 DynamicBird.exe 或 zip 上传到
        //   github.com/{GitHubOwner}/{GitHubRepo}/releases，tag 用版本号（如 v1.0.1）。
        public const string GitHubOwner = "timecolors";
        public const string GitHubRepo = "DynamicBird";

        public sealed class UpdateInfo
        {
            public Version Version { get; set; } = new(0, 0, 0);
            public string Tag { get; set; } = "";
            public string DownloadUrl { get; set; } = "";
            public string FileName { get; set; } = "";
            public string Sha256 { get; set; } = "";
            public string Notes { get; set; } = "";
        }

        /// <summary>检查 GitHub 最新 Release；无更新或检查失败返回 null。</summary>
        public static async Task<UpdateInfo?> CheckForUpdateAsync(Version current)
        {
            if (AppPaths.IsPackaged) return null; // 商店版由 Microsoft Store 负责更新
            if (string.IsNullOrWhiteSpace(GitHubOwner) || string.IsNullOrWhiteSpace(GitHubRepo)) return null;
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("DynamicBird");

                string url = $"https://api.github.com/repos/{Uri.EscapeDataString(GitHubOwner)}/{Uri.EscapeDataString(GitHubRepo)}/releases/latest";
                string json = await http.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string? tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
                string? body = root.TryGetProperty("body", out var b) ? b.GetString() : null;
                if (string.IsNullOrEmpty(tag)) return null;

                Version? version = ParseVersion(tag);
                if (version == null || version <= current) return null;

                string? assetUrl = null;
                string? assetName = null;
                if (root.TryGetProperty("assets", out var assets))
                {
                    foreach (var a in assets.EnumerateArray())
                    {
                        string? name = a.TryGetProperty("name", out var n) ? n.GetString() : null;
                        if (name == null) continue;
                        if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                            name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            assetUrl = a.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
                            assetName = name;
                            break;
                        }
                    }
                }
                if (string.IsNullOrEmpty(assetUrl)) return null;

                return new UpdateInfo
                {
                    Version = version,
                    Tag = tag,
                    DownloadUrl = assetUrl,
                    FileName = assetName ?? "DynamicBird.zip",
                    Sha256 = ParseSha256(body),
                    Notes = body ?? ""
                };
            }
            catch { return null; }
        }

        /// <summary>下载更新包到临时目录并做 SHA256 校验，返回文件路径；失败返回 null。</summary>
        public static async Task<string?> DownloadUpdateAsync(UpdateInfo info)
        {
            try
            {
                string dir = Path.Combine(Path.GetTempPath(), "DynamicBirdUpdate");
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, SanitizeFileName(info.FileName));

                using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
                http.DefaultRequestHeaders.UserAgent.ParseAdd("DynamicBird");
                byte[] bytes = await http.GetByteArrayAsync(info.DownloadUrl);

                if (!string.IsNullOrEmpty(info.Sha256))
                {
                    string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                    if (!string.Equals(hash, info.Sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        DynamicBird.Core.Infrastructure.Logging.LogManager.Warning(
                            $"[Update] SHA256 校验失败: 期望 {info.Sha256} 实际 {hash}");
                        return null;
                    }
                }

                await File.WriteAllBytesAsync(file, bytes);
                return file;
            }
            catch (Exception ex)
            {
                DynamicBird.Core.Infrastructure.Logging.LogManager.Error("下载更新失败", ex);
                return null;
            }
        }

        /// <summary>从 zip 更新包中解压出 DynamicBird.exe；非 zip 直接返回原路径。</summary>
        public static async Task<string?> ExtractExeAsync(string packagePath)
        {
            try
            {
                if (packagePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    string extractDir = Path.Combine(Path.GetTempPath(), "DynamicBirdUpdate", "extract");
                    if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
                    Directory.CreateDirectory(extractDir);
                    ZipFile.ExtractToDirectory(packagePath, extractDir);

                    return FindFile(extractDir, "DynamicBird.exe");
                }
                return packagePath;
            }
            catch (Exception ex)
            {
                DynamicBird.Core.Infrastructure.Logging.LogManager.Error("解压更新包失败", ex);
                return null;
            }
        }

        /// <summary>
        /// 应用更新：把新 exe 暂存到程序目录，生成 PowerShell 替换脚本并启动。
        /// 主进程退出后脚本完成替换并重启新版本。
        /// </summary>
        /// <summary>
        /// 应用更新：把新 exe 暂存到程序目录，生成 PowerShell 替换脚本并启动。
        /// 主进程退出后脚本完成替换并重启新版本。
        /// ★ 健壮性：替换失败不再静默——写 update_failed.txt，下次启动提示"仍为旧版本"；
        ///   成功才重启新 exe；脚本自身最后删除（含残留清理）。
        /// </summary>
        public static bool ApplyUpdate(string newExePath)
        {
            try
            {
                if (AppPaths.IsPackaged) return false; // 商店版不使用 GitHub 更新
                string exeDir = AppContext.BaseDirectory;
                string currentExe = Path.Combine(exeDir, "DynamicBird.exe");
                if (!File.Exists(newExePath) || !File.Exists(currentExe)) return false;

                string staged = Path.Combine(exeDir, "DynamicBird.new.exe");
                File.Copy(newExePath, staged, true);

                string ps = Path.Combine(exeDir, "apply_update.ps1");
                string failMarker = Path.Combine(exeDir, "update_failed.txt");
                string nl = Environment.NewLine;
                string script =
                    "Start-Sleep -Seconds 2" + nl +
                    "$exe = '" + EscapePs(currentExe) + "'" + nl +
                    "$new = '" + EscapePs(staged) + "'" + nl +
                    "$marker = '" + EscapePs(failMarker) + "'" + nl +
                    "for ($i = 0; $i -lt 10 -and (Get-Process -Name DynamicBird -ErrorAction SilentlyContinue); $i++) { Start-Sleep -Milliseconds 500 }" + nl +
                    "$ok = $false" + nl +
                    "for ($i = 0; $i -lt 5; $i++) { try { Copy-Item $new $exe -Force -ErrorAction Stop; $ok = $true; break } catch { Start-Sleep -Milliseconds 600 } }" + nl +
                    "if ($ok) {" + nl +
                    "  Remove-Item $new -ErrorAction SilentlyContinue" + nl +
                    "  Remove-Item $marker -ErrorAction SilentlyContinue" + nl +
                    "  Start-Process $exe" + nl +
                    "} else {" + nl +
                    "  Remove-Item $new -ErrorAction SilentlyContinue" + nl +
                    "  try { Set-Content -Path $marker -Value ('UPDATE_FAILED ' + (Get-Date)) -Encoding UTF8 } catch { }" + nl +
                    "}" + nl +
                    "Remove-Item '" + EscapePs(ps) + "' -ErrorAction SilentlyContinue";
                File.WriteAllText(ps, script, new UTF8Encoding(true));

                Process.Start(new ProcessStartInfo("powershell.exe",
                    "-NoProfile -ExecutionPolicy Bypass -File \" + ps + \"")
                {
                    UseShellExecute = true,
                    CreateNoWindow = true
                });
                return true;
            }
            catch (Exception ex)
            {
                DynamicBird.Core.Infrastructure.Logging.LogManager.Error("应用更新失败", ex);
                return false;
            }
        }

        /// <summary>
        /// 启动时清理更新残留（.new.exe / apply_update.ps1 / update_failed.txt）。
        /// 返回 true = 上次更新失败（调用方应提示用户仍为旧版本）；同时顺带清理残留文件。
        /// </summary>
        public static bool CleanupStaleFiles()
        {
            bool failed = false;
            try
            {
                if (AppPaths.IsPackaged) return false;
                string exeDir = AppContext.BaseDirectory;
                string marker = Path.Combine(exeDir, "update_failed.txt");
                failed = File.Exists(marker);

                foreach (var name in new[] { "DynamicBird.new.exe", "apply_update.ps1", "update_failed.txt" })
                {
                    string p = Path.Combine(exeDir, name);
                    try { if (File.Exists(p)) File.Delete(p); } catch { }
                }
            }
            catch { }
            return failed;
        }

        internal static Version? ParseVersion(string tag)
        {
            string v = tag.TrimStart('v', 'V');
            int cut = v.IndexOfAny(new[] { '-', '+' });
            if (cut >= 0) v = v[..cut];
            return Version.TryParse(v, out var ver) ? ver : null;
        }

        internal static string ParseSha256(string? body)
        {
            if (string.IsNullOrEmpty(body)) return "";
            foreach (var line in body.Split('\n'))
            {
                int idx = line.IndexOf("SHA256", StringComparison.OrdinalIgnoreCase);
                if (idx < 0) continue;
                foreach (var part in line.Split(new[] { ':', '=', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (part.Length == 64 && part.All(Uri.IsHexDigit))
                        return part.ToLowerInvariant();
                }
            }
            return "";
        }

        private static string? FindFile(string dir, string name)
        {
            foreach (var f in Directory.EnumerateFiles(dir, name, SearchOption.AllDirectories))
            {
                return f;
            }
            return null;
        }

        private static string EscapePs(string s) => s.Replace("'", "''");

        private static string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }
    }
}