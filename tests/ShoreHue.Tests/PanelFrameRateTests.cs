using ShoreHue.Core.Services.Configuration;
using System.Text.Json;
using Xunit;

namespace ShoreHue.Tests;

/// <summary>验证面板运行帧率设置：序列化持久化 + 默认值。</summary>
public class PanelFrameRateTests
{
    [Fact]
    public void Default_IsZero_Auto()
    {
        var d = new SettingsData();
        Assert.Equal(0, d.PanelFrameRate);
    }

    [Fact]
    public void Serialize_RoundTrip()
    {
        var d = new SettingsData { PanelFrameRate = 120 };
        string json = JsonSerializer.Serialize(d);
        var back = JsonSerializer.Deserialize<SettingsData>(json);
        Assert.NotNull(back);
        Assert.Equal(120, back!.PanelFrameRate);
    }

    [Fact]
    public void Serialize_ZeroExcluded_DefaultsBackToAuto()
    {
        // 0 = 自动：序列化后反序列化仍为 0（默认）
        var d = new SettingsData { PanelFrameRate = 0 };
        string json = JsonSerializer.Serialize(d);
        var back = JsonSerializer.Deserialize<SettingsData>(json);
        Assert.NotNull(back);
        Assert.Equal(0, back!.PanelFrameRate);
    }

    [Fact]
    public void ValidFpsValues_AllPersist()
    {
        foreach (int fps in new[] { 30, 60, 90, 120 })
        {
            var d = new SettingsData { PanelFrameRate = fps };
            string json = JsonSerializer.Serialize(d);
            var back = JsonSerializer.Deserialize<SettingsData>(json);
            Assert.Equal(fps, back!.PanelFrameRate);
        }
    }
}
