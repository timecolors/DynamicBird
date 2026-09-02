using ShoreHue.Core.Models;
using ShoreHue.Core.Services.Configuration;
using ShoreHue.UI.Seabed;
using ShoreHue.UI.Settings.Pages;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ShoreHue.Tests;

/// <summary>
/// 字段文档字典（SettingsFieldDocs）防漂移测试：
/// 1) 配置树里每个叶子字段都应有中文说明（新增字段但忘记写说明时本测试失败）；
/// 2) BuildConfigCode 生成的配置代码为每个字段附带注释行；
/// 3) 生成的代码仍可编译（注释不影响语法）。
/// </summary>
public class SettingsFieldDocsTests
{
    /// <summary>收集配置树全部叶子字段名。</summary>
    private static List<string> AllLeafFields()
    {
        var root = ConfigTreeBuilder.Build();
        var fields = new List<string>();
        void Walk(ConfigNode n)
        {
            if (n.IsLeaf)
            {
                fields.AddRange(n.FieldNames);
                return;
            }
            foreach (var c in n.Children) Walk(c);
        }
        Walk(root);
        return fields.Distinct().ToList();
    }

    [Fact]
    public void EveryLeafField_HasDoc()
    {
        var missing = AllLeafFields()
            .Where(f => SettingsFieldDocs.TryGet(f) == null)
            .ToList();
        Assert.True(missing.Count == 0,
            "以下树叶子字段缺少中文说明（请加入 SettingsFieldDocs）：" + string.Join(", ", missing));
    }

    [Fact]
    public void BuildConfigCode_EmitsCommentPerField()
    {
        var node = ConfigTreeBuilder.FindNodeByKey("anim-show");
        Assert.NotNull(node);

        string code = SeabedPage.BuildConfigCode(node!);
        // 每个字段都应有 "// 字段名：" 注释行
        foreach (var f in node!.FieldNames)
        {
            Assert.Contains("// " + f + "：", code);
        }
    }

    [Fact]
    public void BuildConfigCode_CommentContainsChineseDoc()
    {
        var node = ConfigTreeBuilder.FindNodeByKey("inter-trigger");
        Assert.NotNull(node);

        string code = SeabedPage.BuildConfigCode(node!);
        // 说明行包含真实中文说明（不是字段名回退）
        Assert.Contains("// TriggerDistancePx：边缘触发距离 px", code);
    }

    [Fact]
    public void BuildConfigCode_WithComments_StillCompiles()
    {
        var root = ConfigTreeBuilder.Build();
        foreach (var c1 in root.Children)
        {
            foreach (var c2 in c1.Children)
            {
                if (c2.Children.Count == 0)
                {
                    string code = SeabedPage.BuildConfigCode(c2);
                    string err = ShoreHue.UI.Widgets.Dynamic.WidgetCompiler.Validate("doc_cfg_" + c2.Key, code);
                    Assert.Equal("", err);
                }
                foreach (var c3 in c2.Children)
                {
                    string code = SeabedPage.BuildConfigCode(c3);
                    string err = ShoreHue.UI.Widgets.Dynamic.WidgetCompiler.Validate("doc_cfg_" + c3.Key, code);
                    Assert.Equal("", err);
                }
            }
        }
    }

    [Fact]
    public void DocOrName_FallsBackToFieldName()
    {
        Assert.Equal("NoSuchFieldXyz", SettingsFieldDocs.DocOrName("NoSuchFieldXyz"));
        Assert.Equal("面板背景色（#RRGGBB）", SettingsFieldDocs.DocOrName("BackgroundColor"));
    }
}
