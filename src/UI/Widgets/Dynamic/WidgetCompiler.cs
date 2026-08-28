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

        /// <summary>编译源码并创建小组件实例。失败时 widget 为 null、error 含错误信息。</summary>
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
