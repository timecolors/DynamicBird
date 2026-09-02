using ShoreHue.UI.Widgets.Dynamic;
using System.Linq;
using Xunit;

namespace ShoreHue.Tests;

/// <summary>验证源码权限检测：联网/剪贴板/文件/进程/系统/窗口/屏幕 自动标注。</summary>
public class WidgetPermissionsTests
{
    [Fact]
    public void Detect_HttpClient_FlagsNetwork()
    {
        var perms = WidgetPermissions.Detect("using System.Net.Http; var c = new HttpClient();");
        Assert.Contains("network", perms);
        Assert.DoesNotContain("file", perms);
    }

    [Fact]
    public void Detect_Clipboard_FlagsClipboard()
    {
        var perms = WidgetPermissions.Detect("System.Windows.Clipboard.SetText(" + "\"" + "hi" + "\"" + ");");
        Assert.Contains("clipboard", perms);
    }

    [Fact]
    public void Detect_FileIo_FlagsFile()
    {
        var perms = WidgetPermissions.Detect("File.WriteAllText(" + "\"" + "a.txt" + "\"" + ", " + "\"" + "x" + "\"" + "); using System.IO;");
        Assert.Contains("file", perms);
    }

    [Fact]
    public void Detect_ProcessStart_FlagsProcess()
    {
        var perms = WidgetPermissions.Detect("Process.Start(" + "\"" + "notepad.exe" + "\"" + ");");
        Assert.Contains("process", perms);
    }

    [Fact]
    public void Detect_WindowApi_FlagsWindow()
    {
        var perms = WidgetPermissions.Detect("FindWindow(null, " + "\"" + "title" + "\"" + "); SendMessage(hwnd, 0x10, 0, 0);");
        Assert.Contains("window", perms);
    }

    [Fact]
    public void Detect_ScreenCapture_FlagsScreen()
    {
        var perms = WidgetPermissions.Detect("Graphics.CopyFromScreen(0, 0, 0, 0, size);");
        Assert.Contains("screen", perms);
    }

    [Fact]
    public void Detect_HarmlessUi_ReturnsEmpty()
    {
        var perms = WidgetPermissions.Detect("var tb = new TextBlock { Text = " + "\"" + "hello" + "\"" + " };");
        Assert.Empty(perms);
    }

    [Fact]
    public void Describe_Empty_ShowsNoPermission()
    {
        Assert.Equal("无权限", WidgetPermissions.Describe(new System.Collections.Generic.List<string>()));
        Assert.Equal("无权限", WidgetPermissions.Describe(null));
    }

    [Fact]
    public void Describe_Multiple_JoinsLabels()
    {
        var perms = WidgetPermissions.Describe(new[] { "network", "clipboard" });
        Assert.Contains("", perms);
        Assert.Contains("", perms);
        Assert.Contains("联网", perms);
        Assert.Contains("剪贴板", perms);
    }
}
