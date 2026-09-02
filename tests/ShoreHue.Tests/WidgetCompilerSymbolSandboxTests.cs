using ShoreHue.UI.Widgets.Dynamic;
using Xunit;

namespace ShoreHue.Tests
{
    /// <summary>
    /// 编译符号级沙箱检查（WidgetCompiler.CheckSandboxSymbols / SandboxErrors）：
    /// 补文本扫描的绕过洞——换皮写法（File.Open 写文件、Assembly.GetType 反射）必须被符号级拦截；
    /// 合法 API（File.ReadAllText 读、Stopwatch、HttpClient）不得误拦。
    /// </summary>
    public class WidgetCompilerSymbolSandboxTests
    {
        [Fact]
        public void FileOpen_WriteBypass_Blocked()
        {
            const string src = "using System.IO;\npublic class A { void M() { var s = File.Open(\"a.txt\", FileMode.Create); } }";
            var blocked = WidgetCompiler.SandboxErrors(src);
            Assert.Contains("System.IO.File.Open", blocked);
        }

        [Fact]
        public void AssemblyGetType_Bypass_Blocked()
        {
            const string src = "using System.Reflection;\npublic class A { void M() { var t = typeof(A).Assembly.GetType(\"X\"); } }";
            var blocked = WidgetCompiler.SandboxErrors(src);
            Assert.Contains("System.Reflection", blocked);
        }

        [Fact]
        public void Process_StillBlocked_ByText()
        {
            Assert.Contains("进程", WidgetCompiler.SandboxErrors("Process.Start(\"cmd.exe\");"));
        }

        [Fact]
        public void StreamWriter_Blocked()
        {
            Assert.Contains("System.IO.StreamWriter", WidgetCompiler.SandboxErrors("using System.IO; public class A { void M() { var w = new StreamWriter(\"a.txt\"); } }"));
        }

        [Fact]
        public void Clipboard_Allowed_PermissionDeclared()
        {
            // ★ 剪贴板降为权限声明类（2026-08 用户修正）：不硬拦，安装时弹窗提示
            Assert.Equal("", WidgetCompiler.SandboxErrors("System.Windows.Clipboard.SetText(\"x\");"));
        }

        [Fact]
        public void FileRead_Allowed_NoFalsePositive()
        {
            const string src = "using System.IO;\npublic class A { string M() => File.ReadAllText(\"a.txt\"); }";
            Assert.DoesNotContain("File", WidgetCompiler.SandboxErrors(src));
        }

        [Fact]
        public void Stopwatch_Allowed_NoFalsePositive()
        {
            const string src = "using System.Diagnostics;\npublic class A { void M() { var sw = Stopwatch.StartNew(); } }";
            Assert.Equal("", WidgetCompiler.SandboxErrors(src));
        }

        [Fact]
        public void HttpClient_Allowed()
        {
            const string src = "using System.Net.Http;\npublic class A { void M() { var c = new HttpClient(); } }";
            Assert.Equal("", WidgetCompiler.SandboxErrors(src));
        }

        [Fact]
        public void CleanCode_Passes()
        {
            const string src = "using System.Windows.Controls;\npublic class A { TextBlock T => new TextBlock(); }";
            Assert.Equal("", WidgetCompiler.SandboxErrors(src));
        }
    }
}
