using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace DynamicBird.UI.Widgets.Dynamic
{
    /// <summary>
    /// C# 插件编译器：用 Roslyn 把用户编写的源码动态编译为内存程序集，
    /// 反射创建 IWidget 实例。用户拥有完整自由度（任意 WPF UI 与逻辑）。
    /// 注意：这是"本地自用"模型——用户编译运行的代码即用户自己的代码；
    /// 未来市场分发含代码插件时需引入沙箱/风险标记。
    /// </summary>
    public static class WidgetCompiler
    {
        // ★ 编译缓存：id → (源码签名, 实例)。源码未变时复用实例，避免重复编译同一程序集名导致
        //   "Assembly with same name is already loaded"（Default ALC 不允许同名程序集二次加载）。
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (string Hash, IWidget Widget)> _cache = new();

        /// <summary>源码签名（供 WidgetSwitcher 判断是否需要重建）。</summary>
        public static string SourceHash(string source) => ComputeHash(source ?? "");


        /// <summary>
        /// 把源码里 IWidget 的 Name 属性替换为指定名字（变体名注入）。
        /// 编译前调用：让动态编译的变体标签显示变体自己的名字（模板里 Name 写死）。
        /// </summary>
        /// <summary>
        /// 把源码里 IWidget 的 Name 属性替换为指定名字（变体名注入）。
        /// 编译前调用：让动态编译的变体标签显示变体自己的名字（模板里 Name 写死）。
        /// </summary>
        /// <summary>
        /// 把源码里 IWidget 的 Name 属性替换为指定名字（变体名注入）。
        /// 编译前调用：让动态编译的变体标签显示变体自己的名字（模板里 Name 写死）。
        /// </summary>
        public static string InjectWidgetName(string source, string name)
        {
            if (string.IsNullOrEmpty(source)) return source;
            int marker = source.IndexOf("Name => ");
            if (marker < 0) return source;
            // 用字符重载找真正的引号（空字符串 IndexOf 总是返回起始位，会错位）
            int quote = source.IndexOf('"', marker);
            if (quote < 0) return source;
            int end = source.IndexOf('"', quote + 1);
            if (end < 0) return source;
            return source.Substring(0, quote + 1) + name + source.Substring(end);
        }

        public static (IWidget? widget, string error) Compile(string id, string source)
        {
            try
            {
                string src = source ?? "";
                string hash = ComputeHash(src);
                if (_cache.TryGetValue(id, out var entry) && entry.Hash == hash)
                    return (entry.Widget, "");

                using var ms = new MemoryStream();
                if (!TryEmit(id, src, ms, out string errors))
                {
                    return (null, errors);
                }

                ms.Position = 0;
                var asm = AssemblyLoadContext.Default.LoadFromStream(ms);

                var type = asm.GetTypes()
                    .FirstOrDefault(t => typeof(IWidget).IsAssignableFrom(t) && !t.IsAbstract && t.IsPublic);
                if (type == null)
                {
                    return (null, "未找到 public 且实现 IWidget 的类。请定义一个公开类实现 DynamicBird.UI.Widgets.IWidget 接口。");
                }

                var widget = Activator.CreateInstance(type) as IWidget;
                if (widget == null) return (null, "实例化失败");
                _cache[id] = (hash, widget);
                return (widget, "");
            }
            catch (TargetInvocationException tie)
            {
                return (null, "构造异常：" + (tie.InnerException?.Message ?? tie.Message));
            }
            catch (Exception ex)
            {
                return (null, "编译异常：" + ex.Message);
            }
        }

        private static string ComputeHash(string source)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(source);
            var hash = sha.ComputeHash(bytes);
            return Convert.ToHexString(hash).Substring(0, 16);
        }

        /// <summary>仅编译校验（不实例化，避免副作用）。成功返回空串。</summary>
        public static string Validate(string id, string source)
        {
            try
            {
                using var ms = new MemoryStream();
                return TryEmit(id, source, ms, out string errors) ? "" : errors;
            }
            catch (Exception ex)
            {
                return "编译异常：" + ex.Message;
            }
        }

        /// <summary>
        /// 沙箱校验（市场来源代码，TrustedSource=false）：扫描源码中危险 API 并返回被拦截项列表（空 = 通过）。
        /// 参考成熟平台（Chrome MV3 禁远程代码 / Wallpaper Engine 移除 EXE）的"限制能力"思路：
        /// 硬拦截攻击类 API——进程执行/反射动态调用/P-Invoke/注册表/WMI/窗口与输入钩子/屏幕捕获/文件写/剪贴板；
        /// 网络与文件读属于"权限声明类"（导入时风险标签已提示），v1 不硬拦。
        /// 注意：静态扫描有理论绕过空间（混淆+反射），故同时硬拦反射/动态加载 API 把门槛抬高。
        /// </summary>
        public static List<string> CheckSandbox(string source)
        {
            var blocked = new List<string>();
            if (string.IsNullOrEmpty(source)) return blocked;
            string lower = source.ToLower();

            // ===== 危险 using 命名空间（整包拦截） =====
            string[] blockedUsings =
            {
                "using system.diagnostics;",       // Process
                "using system.reflection;",        // 反射
                "using system.runtime.interopservices;", // DllImport/Marshal
                "using system.management;",        // WMI
                "using microsoft.win32;",          // Registry
                "using system.directoryservices;", // AD/LDAP
            };
            foreach (var u in blockedUsings)
            {
                if (lower.Contains(u))
                {
                    blocked.Add("禁止命名空间: " + u.Replace("using ", "").TrimEnd(';'));
                }
            }

            // ===== 危险 API（词边界匹配，防子串误伤如 Dispatcher.Invoke） =====
            (string Pattern, string Label)[] blockedApis =
            {
                (@"\bprocess\b", "进程执行（Process）"),
                ("processstartinfo", "进程启动（ProcessStartInfo）"),
                (@"\.getmethod\(", "反射（GetMethod）"),
                (@"\.getproperty\(", "反射（GetProperty）"),
                ("activator.", "动态创建（Activator）"),
                ("dynamicmethod", "动态方法（DynamicMethod）"),
                ("assembly.load", "程序集加载（Assembly.Load）"),
                ("type.gettype", "动态类型（Type.GetType）"),
                ("dllimport", "原生调用（DllImport）"),
                ("marshal.", "原生内存（Marshal）"),
                ("registry", "注册表"),
                ("managementobject", "WMI"),
                ("findwindow", "窗口查找（FindWindow）"),
                ("enumwindows", "窗口枚举（EnumWindows）"),
                ("setwindowshookex", "输入钩子（SetWindowsHookEx）"),
                ("sendinput", "输入注入（SendInput）"),
                ("setforegroundwindow", "窗口抢占（SetForegroundWindow）"),
                ("postmessage", "窗口消息（PostMessage）"),
                ("sendmessage", "窗口消息（SendMessage）"),
                ("keybd_event", "键盘注入"),
                ("mouse_event", "鼠标注入"),
                ("copyfromscreen", "屏幕捕获"),
                ("printwindow", "窗口捕获"),
                ("bitblt", "位块传输"),
                ("file.write", "文件写入"),
                ("file.append", "文件写入"),
                ("file.delete", "文件删除"),
                ("file.move", "文件移动"),
                ("file.copy", "文件复制"),
                ("file.setattributes", "文件属性"),
                ("filestream", "文件流"),
                ("streamwriter", "文件写入流"),
                ("directory.create", "目录创建"),
                ("directory.delete", "目录删除"),
                (@"\bclipboard\b", "剪贴板"),
                ("idataobject", "剪贴板数据"),
            };

            foreach (var (pattern, label) in blockedApis)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(lower, pattern))
                {
                    blocked.Add(label);
                }
            }
            return blocked;
        }

        /// <summary>沙箱校验并汇总为错误文本（非空 = 有被拦截项，编译前应先拒绝）。</summary>
        public static string SandboxErrors(string source)
        {
            var blocked = CheckSandbox(source);
            return blocked.Count == 0 ? "" : "市场来源代码被沙箱拦截，禁止以下能力：" + System.Environment.NewLine + "  - " + string.Join(System.Environment.NewLine + "  - ", blocked);
        }

        /// <summary>
        /// 编译配置代码（赋值语句版）并返回可调用的 Apply 委托。
        /// 鸟笼里保存的单预设（Kind=Config）源码形如：
        ///   public static class ConfigCode { public static void Apply(SettingsData data) { data.X = 值; ... } }
        /// 编译后反射找到静态 ConfigCode.Apply(SettingsData) 并包装为委托。
        /// 调用方传入一个 SettingsData 实例，Apply 委托会就地修改其字段值（写回由调用方负责）。
        /// 编译失败返回 null，error 含诊断信息。
        /// </summary>
        public static Action<DynamicBird.Core.Services.Configuration.SettingsData>? CompileConfigApply(string source, out string error)
        {
            error = "";
            try
            {
                string wrapped = "using DynamicBird.Core.Services.Configuration;" + System.Environment.NewLine + (source ?? "");
                using var ms = new MemoryStream();
                if (!TryEmit("config_" + Guid.NewGuid().ToString("N").Substring(0, 8), wrapped, ms, out error))
                {
                    return null;
                }

                ms.Position = 0;
                var asm = AssemblyLoadContext.Default.LoadFromStream(ms);
                var type = asm.GetTypes()
                    .FirstOrDefault(t => t.Name == "ConfigCode" && t.IsAbstract && t.IsSealed && t.IsPublic);
                if (type == null)
                {
                    error = "未找到 public static class ConfigCode。请保留模板中的 ConfigCode 类结构。";
                    return null;
                }

                var method = type.GetMethod("Apply",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                    null,
                    new[] { typeof(DynamicBird.Core.Services.Configuration.SettingsData) },
                    null);
                if (method == null)
                {
                    error = "未找到 public static void Apply(SettingsData data) 方法。";
                    return null;
                }

                return data => method.Invoke(null, new object[] { data });
            }
            catch (Exception ex)
            {
                error = "编译异常：" + ex.Message;
                return null;
            }
        }

        /// <summary>Roslyn 编译到内存流。返回是否成功，失败时 errors 含诊断信息。</summary>
        private static bool TryEmit(string id, string source, MemoryStream ms, out string errors)
        {
            errors = "";
            var tree = CSharpSyntaxTree.ParseText(source);

            var refs = new List<MetadataReference>();
            var trusted = ((AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string) ?? "")
                .Split(Path.PathSeparator);
            foreach (var p in trusted)
            {
                try { refs.Add(MetadataReference.CreateFromFile(p)); } catch { }
            }
            try { refs.Add(MetadataReference.CreateFromFile(typeof(IWidget).Assembly.Location)); } catch { }

            // ★ 程序集名唯一：避免源码更新后重编译时 Default ALC 报"同名程序集已加载"
            string asmName = "DynamicBird.Widget." + id + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var compilation = CSharpCompilation.Create(
                asmName,
                new[] { tree },
                refs,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var result = compilation.Emit(ms);
            if (!result.Success)
            {
                errors = string.Join(Environment.NewLine, result.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Take(10)
                    .Select(d => d.ToString()));
                return false;
            }
            return true;
        }
    }
}