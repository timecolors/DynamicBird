using DynamicBird.UI.Widgets;
using DynamicBird.UI.Widgets.Dynamic;
using System;
using System.Threading;
using Xunit;

namespace DynamicBird.Tests;

/// <summary>验证 C# 插件编译链路：Roslyn 编译示例源码 → 加载 → 反射创建 IWidget 实例。</summary>
/// <remarks>实例化在 STA 线程执行（WPF UI 组件构造需要）；纯编译校验无需。</remarks>
public class WidgetCompilerTests
{
    [Fact]
    public void Compile_ClockSample_Compiles()
    {
        Assert.Equal("", WidgetCompiler.Validate("test_clock", WidgetSamples.Clock));
    }

    [Fact]
    public void Compile_CounterSample_Compiles()
    {
        Assert.Equal("", WidgetCompiler.Validate("test_counter", WidgetSamples.Counter));
    }

    [Fact]
    public void Compile_NoteSample_Instantiates()
    {
        var (widget, err) = RunOnSta(() => WidgetCompiler.Compile("test_note", WidgetSamples.Note));

        Assert.True(widget != null, "失败: " + err);
        Assert.Equal("便签", widget!.Name);
    }

    [Fact]
    public void Compile_ShortcutSample_Instantiates()
    {
        var (widget, err) = RunOnSta(() => WidgetCompiler.Compile("test_shortcut", WidgetSamples.Shortcut));

        Assert.True(widget != null, "失败: " + err);
        Assert.Equal("快捷打开", widget!.Name);
    }

    [Fact]
    public void Compile_InvalidCode_ReturnsError()
    {
        var (widget, err) = WidgetCompiler.Compile("test_bad", "public class Broken { ");

        Assert.Null(widget);
        Assert.NotEmpty(err);
    }

    [Fact]
    public void Compile_NoIWidget_ReturnsError()
    {
        var (widget, err) = WidgetCompiler.Compile("test_nowidget", "public class Foo { public int X; }");

        Assert.Null(widget);
        Assert.Contains("IWidget", err);
    }

    /// <summary>在 STA 线程执行编译+实例化（WPF 控件构造需要）。</summary>
    private static (IWidget?, string) RunOnSta(Func<(IWidget?, string)> action)
    {
        (IWidget?, string) result = (null, "");
        Exception? ex = null;
        var t = new Thread(() =>
        {
            try { result = action(); }
            catch (Exception e) { ex = e; }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        if (ex != null) throw ex;
        return result;
    }
}
