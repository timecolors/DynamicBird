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
            // ===== 危险 API（词边界匹配，防子串误伤如 Dispatcher.Invoke） =====
            // ★ 整命名空间拦截已移除：System.Diagnostics 等含无害类（Stopwatch/Debug），
            //   精确拦截交给 CheckSandboxSymbols（编译符号级：只拦 Process/反射/Interop 等危险类型）。
            string lower = source.ToLower();

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
                // ★ 剪贴板不硬拦（2026-08 用户修正）：剪贴板读写是常见低危能力，
                //   归入"权限声明类"——安装时弹窗提示"使用剪贴板"，用户确认后放行（与网络/文件读一致）
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

        // ==================== 编译符号级沙箱检查（补文本扫描的绕过洞） ====================

        /// <summary>
        /// 编译符号级检查：解析源码每个成员访问/对象创建/类型引用的真实符号（编译器解析，与书写方式无关），
        /// 命中类型级/成员级黑名单即拦截。文本扫描可被换皮绕过（如 File.Open 写文件、Assembly.GetType 反射），
        /// 符号级不可绕过——只要引用了危险类型/成员，无论怎么写都会命中。
        /// </summary>
        public static List<string> CheckSandboxSymbols(string source)
        {
            var blocked = new List<string>();
            if (string.IsNullOrWhiteSpace(source)) return blocked;
            try
            {
                var tree = CSharpSyntaxTree.ParseText(source);
                var compilation = CSharpCompilation.Create(
                    "sandbox_check_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                    new[] { tree },
                    BuildReferences(),
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
                var model = compilation.GetSemanticModel(tree);

                foreach (var node in tree.GetRoot().DescendantNodes())
                {
                    try
                    {
                        ISymbol? sym = null;
                        if (node is Microsoft.CodeAnalysis.CSharp.Syntax.MemberAccessExpressionSyntax ma)
                            sym = model.GetSymbolInfo(ma).Symbol;
                        else if (node is Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax inv)
                        {
                            var info = model.GetSymbolInfo(inv);
                            sym = info.Symbol ?? info.CandidateSymbols.FirstOrDefault();
                        }
                        else if (node is Microsoft.CodeAnalysis.CSharp.Syntax.ObjectCreationExpressionSyntax oc)
                            sym = model.GetSymbolInfo(oc).Symbol;
                        else if (node is Microsoft.CodeAnalysis.CSharp.Syntax.IdentifierNameSyntax idn)
                        {
                            if (model.GetSymbolInfo(idn).Symbol is ITypeSymbol ts && IsBlockedTypeSymbol(ts))
                            {
                                string tn = ts.ToString() ?? "";
                                if (!blocked.Contains("禁止类型: " + tn)) blocked.Add("禁止类型: " + tn);
                            }
                            continue;
                        }
                        if (sym == null) continue;
                        var containing = sym.ContainingType;
                        if (containing == null) continue;
                        string typeName = containing.ToString() ?? "";
                        if (IsBlockedTypeName(typeName))
                        {
                            if (!blocked.Contains("禁止类型: " + typeName)) blocked.Add("禁止类型: " + typeName);
                        }
                        else if (IsBlockedMember(typeName, sym.Name))
                        {
                            string label = "禁止成员: " + typeName + "." + sym.Name;
                            if (!blocked.Contains(label)) blocked.Add(label);
                        }
                    }
                    catch { }
                }
            }
            catch { /* 编译失败交给正式编译报错；符号检查静默跳过 */ }
            return blocked;
        }

        private static List<MetadataReference> BuildReferences()
        {
            var refs = new List<MetadataReference>();
            var trusted = ((AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string) ?? "")
                .Split(Path.PathSeparator);
            foreach (var p in trusted)
            {
                try { refs.Add(MetadataReference.CreateFromFile(p)); } catch { }
            }
            try { refs.Add(MetadataReference.CreateFromFile(typeof(IWidget).Assembly.Location)); } catch { }
            return refs;
        }

        /// <summary>类型级黑名单：命中即整类型拦截（进程/反射/Interop/WMI/注册表/AD/剪贴板/写流/输入注入）。</summary>
        private static bool IsBlockedTypeName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return false;
            string[] prefixes =
            {
                "System.Diagnostics.Process", "System.Reflection", "System.Runtime.InteropServices",
                "System.Management", "Microsoft.Win32", "System.DirectoryServices",
                // ★ Clipboard 不硬拦（权限声明类，见 CheckSandbox 注释）
                "System.IO.FileStream", "System.IO.StreamWriter", "System.IO.BinaryWriter",
                "System.Windows.Forms.SendKeys"
            };
            foreach (var p in prefixes)
            {
                if (typeName.StartsWith(p, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static bool IsBlockedTypeSymbol(ITypeSymbol ts)
        {
            var t = ts;
            while (t != null)
            {
                if (IsBlockedTypeName(t.ToString() ?? "")) return true;
                t = t.BaseType;
            }
            return false;
        }

        /// <summary>成员级黑名单：类型允许但特定成员危险（File 读允许、写/删/移拦截；Environment.Exit 拦截）。</summary>
        private static bool IsBlockedMember(string typeName, string member)
        {
            if (typeName == "System.IO.File")
            {
                return member.StartsWith("Write", StringComparison.Ordinal) ||
                       member.StartsWith("Append", StringComparison.Ordinal) ||
                       member is "Open" or "Create" or "Delete" or "Move" or "Copy" or "Replace" or "SetAttributes";
            }
            if (typeName == "System.IO.Directory")
            {
                return member is "CreateDirectory" or "Delete" or "Move";
            }
            if (typeName == "System.Environment")
            {
                return member == "Exit";
            }
            return false;
        }

        /// <summary>沙箱校验并汇总为错误文本（非空 = 有被拦截项，编译前应先拒绝）。文本预检 + 编译符号检查合并。</summary>
        public static string SandboxErrors(string source)
        {
            var blocked = CheckSandbox(source);
            blocked.AddRange(CheckSandboxSymbols(source));
            return blocked.Count == 0 ? "" : "市场来源代码被沙箱拦截，禁止以下能力：" + System.Environment.NewLine + "  - " + string.Join(System.Environment.NewLine + "  - ", blocked.Distinct());
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