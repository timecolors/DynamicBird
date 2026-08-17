using System.Text.Json;
using DynamicBird.Core.Services.Configuration;
using Xunit;

namespace DynamicBird.Tests;

/// <summary>
/// 配置序列化稳定性测试：保证旧配置文件（缺字段）能无损加载、
/// 新字段写入后能完整读回 —— 是设置系统升级的回归护栏。
/// </summary>
public class SettingsDataJsonTests
{
    private static readonly JsonSerializerOptions SaveOptions = new() { WriteIndented = true };

    [Fact]
    public void RoundTrip_Preserves_All_Values()
    {
        var data = new SettingsData
        {
            Edge_Top = false,
            Edge_Right = true,
            Opacity = 0.72,
            CornerRadius = 20,
            ShowSystemStatus = false,
            ClipboardMaxCount = 50,
            TaskbarIconSize = 32.0,
            AnimationsEnabled = false,
            ShowHideDurationMs = 300,
            HideDelayMs = 400,
            RegionPanel_TopLeft = "QuickSettings",
            RegionPanel_BottomRight = "Notification",
            OnboardingCompleted = true,
            AutoCheckUpdate = false,
            ClingModeEnabled = true,
            RegionDebounceMs = 120
        };

        string json = JsonSerializer.Serialize(data, SaveOptions);
        var loaded = JsonSerializer.Deserialize<SettingsData>(json);

        Assert.NotNull(loaded);
        Assert.False(loaded!.Edge_Top);
        Assert.True(loaded.Edge_Right);
        Assert.Equal(0.72, loaded.Opacity);
        Assert.Equal(20, loaded.CornerRadius);
        Assert.False(loaded.ShowSystemStatus);
        Assert.Equal(50, loaded.ClipboardMaxCount);
        Assert.Equal(32.0, loaded.TaskbarIconSize);
        Assert.False(loaded.AnimationsEnabled);
        Assert.Equal(300, loaded.ShowHideDurationMs);
        Assert.Equal(400, loaded.HideDelayMs);
        Assert.Equal("QuickSettings", loaded.RegionPanel_TopLeft);
        Assert.Equal("Notification", loaded.RegionPanel_BottomRight);
        Assert.True(loaded.OnboardingCompleted);
        Assert.False(loaded.AutoCheckUpdate);
        Assert.True(loaded.ClingModeEnabled);
        Assert.Equal(120, loaded.RegionDebounceMs);
    }

    [Fact]
    public void Old_Config_Without_New_Fields_Loads_With_Defaults()
    {
        // 模拟旧版配置文件：只含老字段，缺后续版本新增的字段
        string oldJson = """
        {
          "Edge_Top": true,
          "Edge_Bottom": true,
          "Opacity": 0.85,
          "CornerRadius": 16
        }
        """;

        var loaded = JsonSerializer.Deserialize<SettingsData>(oldJson);

        Assert.NotNull(loaded);
        // 老字段保留
        Assert.True(loaded!.Edge_Top);
        Assert.Equal(0.85, loaded.Opacity);
        // 新字段回退默认值
        Assert.True(loaded.AnimationsEnabled);
        Assert.Equal(150, loaded.ShowHideDurationMs);
        Assert.Equal("Default", loaded.RegionPanel_TopLeft);
        Assert.False(loaded.OnboardingCompleted);
    }

    [Fact]
    public void Corrupted_Json_Throws_Which_Manager_Swallows()
    {
        // System.Text.Json 对坏 JSON 抛 JsonException；SettingsFileManager.Load
        // 的 try/catch 会捕获并返回 new SettingsData() —— 应用不会崩溃。
        string badJson = "{ not valid json !!!";

        Assert.ThrowsAny<System.Text.Json.JsonException>(() =>
            System.Text.Json.JsonSerializer.Deserialize<SettingsData>(badJson));
    }

    [Fact]
    public void Default_Instance_Serializes_And_Deserializes()
    {
        var data = new SettingsData();
        string json = JsonSerializer.Serialize(data, SaveOptions);
        var loaded = JsonSerializer.Deserialize<SettingsData>(json);

        Assert.NotNull(loaded);
        Assert.True(loaded!.Edge_Top);
        Assert.Equal(1.0 / 3.0, loaded.TriggerRegionRatio);
        Assert.Equal("#2D2D2D", loaded.BackgroundColor);
        Assert.Equal("Follow", loaded.EdgeMode_Top);
    }
}
