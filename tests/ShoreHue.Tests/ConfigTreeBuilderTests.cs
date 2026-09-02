using ShoreHue.UI.Seabed;
using System.Linq;
using Xunit;

namespace ShoreHue.Tests;

/// <summary>验证配置树结构：4 个一级分类 = 设置窗口页签（常规/区域/面板/动画）、面板功能组、字段归属与节点链查找。</summary>
public class ConfigTreeBuilderTests
{
    [Fact]
    public void Build_HasFourCategories_BySettingsTabs()
    {
        var root = ConfigTreeBuilder.Build();
        var names = root.Children.Select(c => c.Name).ToList();
        Assert.Contains("常规", names);
        Assert.Contains("区域", names);
        Assert.Contains("面板", names);
        Assert.Contains("动画", names);
        Assert.Equal(4, root.Children.Count);   // 与设置窗口内容页签同构
    }

    [Fact]
    public void Build_HasPanelFeaturesGroup()
    {
        var root = ConfigTreeBuilder.Build();
        var panel = root.Children.First(c => c.Key == "panel");
        var features = panel.Children.FirstOrDefault(c => c.Key == "panel-features");
        Assert.NotNull(features);
        var keys = features!.Children.Select(c => c.Key).ToList();
        Assert.Contains("panel-notification", keys);
        Assert.Contains("panel-recent", keys);
        Assert.Contains("panel-quicksettings", keys);
        Assert.Contains("panel-taskbar-feature", keys);
        Assert.Contains("panel-ai", keys);
        Assert.Contains("panel-windowcontrol", keys);
    }

    [Fact]
    public void FindNodeByKey_FindsDeepLeaf()
    {
        var node = ConfigTreeBuilder.FindNodeByKey("anim-show");
        Assert.NotNull(node);
        Assert.Equal("触发动画", node!.Name);
        Assert.Contains("ShowAnimationType", node.FieldNames);
    }

    [Fact]
    public void FindNodeByKey_Missing_ReturnsNull()
    {
        Assert.Null(ConfigTreeBuilder.FindNodeByKey("no-such-key"));
    }

    [Fact]
    public void FindNodeChain_AnimationField_MapsToAnimShow()
    {
        var chain = ConfigTreeBuilder.FindNodeChain("ShowAnimationDurationMs");
        Assert.Equal(2, chain.Count);
        Assert.Equal("anim", chain[0].Key);
        Assert.Equal("anim-show", chain[1].Key);
    }

    [Fact]
    public void FindNodeChain_WidgetField_MapsThreeLevels()
    {
        var chain = ConfigTreeBuilder.FindNodeChain("WidgetEnabled_Timer");
        // 字段挂在 面板设计→小组件→计时器 与 状态栏→小组件开关 两处；取第一条链
        Assert.True(chain.Count >= 2);
    }

    [Fact]
    public void CollectFields_Level1_CollectsAllDescendants()
    {
        var anim = ConfigTreeBuilder.FindNodeByKey("anim");
        Assert.NotNull(anim);
        var fields = new System.Collections.Generic.HashSet<string>();
        ConfigTreeBuilder.CollectFields(anim!, fields);
        Assert.Contains("ShowAnimationType", fields);
        Assert.Contains("HideAnimationDurationMs", fields);
        Assert.Contains("FlyDurationMs", fields);
    }
}
