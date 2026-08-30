using System;
using System.IO;
using System.Linq;
using DynamicBird.UI.Widgets.Dynamic;

namespace MarketValidator
{
    /// <summary>
    /// CI 用：编译验证 market/packages/**/main.cs（复用 WidgetCompiler 的 Roslyn 编译 + 沙箱检查）。
    /// 任一包编译失败 → exit 1（挂掉 PR），保证市场包质量。
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            string root = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "market");
            // 定位仓库根：从 exe 目录向上找 market/（CI 里 cwd 为仓库根）
            string cwd = Directory.GetCurrentDirectory();
            string marketDir = Directory.Exists(Path.Combine(cwd, "market")) ? Path.Combine(cwd, "market") : root;
            if (!Directory.Exists(marketDir))
            {
                Console.WriteLine("market 目录不存在: " + marketDir);
                return 1;
            }

            var files = Directory.GetFiles(Path.Combine(marketDir, "packages"), "main.cs", SearchOption.AllDirectories);
            if (files.Length == 0)
            {
                Console.WriteLine("未发现 market/packages/**/main.cs（空市场，视为通过）");
                return 0;
            }

            int pass = 0, fail = 0;
            foreach (var f in files.OrderBy(x => x, StringComparer.Ordinal))
            {
                string source = File.ReadAllText(f);
                string id = Path.GetFileName(Path.GetDirectoryName(f) ?? "pkg");
                var sandbox = WidgetCompiler.CheckSandbox(source);
                if (sandbox.Count > 0)
                {
                    Console.WriteLine("FAIL  " + f.Replace(cwd + Path.DirectorySeparatorChar, "") + " [沙箱拦截] " + string.Join("; ", sandbox));
                    fail++;
                    continue;
                }
                var (_, err) = WidgetCompiler.Compile(id, source);
                if (string.IsNullOrEmpty(err))
                {
                    Console.WriteLine("PASS  " + f.Replace(cwd + Path.DirectorySeparatorChar, ""));
                    pass++;
                }
                else
                {
                    Console.WriteLine("FAIL  " + f.Replace(cwd + Path.DirectorySeparatorChar, "") + " [编译] " + err.Split('\n')[0]);
                    fail++;
                }
            }
            Console.WriteLine(fail == 0
                ? "MARKET OK (" + pass + " 包可编译)"
                : "MARKET FAILED: " + fail + " 包编译失败");
            return fail == 0 ? 0 : 1;
        }
    }
}
