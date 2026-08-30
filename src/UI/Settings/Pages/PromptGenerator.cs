using DynamicBird.Core.Models;
using System.Linq;
using System.Text;

namespace DynamicBird.UI.Settings.Pages
{
    /// <summary>
    /// AI 提示词生成器：按配置树节点动态生成完整提示词（含可定制字段清单、当前配置、
    /// 格式要求与报错处理说明），用户复制后粘贴到任意 AI（如 DeepSeek 网页版）即可生成配置。
    /// </summary>
    public static class PromptGenerator
    {
        public static string Generate(ConfigNode node, string currentJson)
        {
            // 自定义面板：生成 C# 源码（动态编译），而非 JSON 配置
            if (!string.IsNullOrEmpty(node.CustomId))
            {
                return GeneratePanelSource(node.Name, currentJson);
            }
            return GenerateConfigJson(node, currentJson);
        }

        /// <summary>标准节点：生成 C# 配置代码提示词（编译校验，保存小预设时在同级创建新项）。</summary>
        private static string GenerateConfigJson(ConfigNode node, string currentJson)
        {
            var sb = new StringBuilder();
            sb.AppendLine("【角色】你是灵动鸟 DynamicBird 的「" + node.Name + "」配置代码专家。");
            sb.AppendLine("【目标】根据下面的需求，生成一段 C# 配置代码（赋值语句，编译校验后保存）。");
            sb.AppendLine();
            sb.AppendLine("【可编辑字段】只允许出现以下字段（赋值给 data.字段）：");
            foreach (var f in node.FieldNames)
            {
                sb.AppendLine("  - " + f);
            }
            sb.AppendLine();
            sb.AppendLine("【代码模板】输出如下结构的完整代码（Apply 方法内每行一个赋值）：");
            sb.AppendLine("public static class ConfigCode");
            sb.AppendLine("{");
            sb.AppendLine("    public static void Apply(DynamicBird.Core.Services.Configuration.SettingsData data)");
            sb.AppendLine("    {");
            sb.AppendLine("        // 每行一个赋值，例如：");
            sb.AppendLine("        // data.ShowAnimationType = \"Slide\";");
            sb.AppendLine("        // data.ShowAnimationDurationMs = 150;");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("【当前配置】当前值如下（JSON），请基于它修改：");
            sb.AppendLine(currentJson);
            sb.AppendLine();
            sb.AppendLine("【输出要求】");
            sb.AppendLine("1. 只输出完整 C# 代码（含 public static class ConfigCode 与 Apply 方法），不要 Markdown 代码块标记；");
            sb.AppendLine("2. 字段名必须与【可编辑字段】一致，属性名完全匹配 SettingsData；");
            sb.AppendLine("3. 类型必须正确（布尔 true/false、数字不带引号、字符串带双引号）；");
            sb.AppendLine("4. 未提到的字段不要出现在代码里。");
            sb.AppendLine();
            sb.AppendLine("【报错处理】如果用户把编译报错信息粘贴回来，请指出错误位置并给出修正后的完整代码。");
            sb.AppendLine();
            sb.AppendLine("【用户需求】");
            sb.AppendLine("（在这里描述你想要的效果）");
            return sb.ToString();
        }

        /// <summary>自定义面板：生成 C# 面板源码提示词（动态编译为真实可运行面板）。</summary>
        private static string GeneratePanelSource(string panelName, string currentSource)
        {
            var sb = new StringBuilder();
            sb.AppendLine("【角色】你是灵动鸟 DynamicBird 的 WPF 面板开发专家。");
            sb.AppendLine("【目标】为自定义面板「" + panelName + "」生成完整的 C# 源码（可动态编译运行的 WPF 面板）。");
            sb.AppendLine();
            sb.AppendLine("【技术要求】");
            sb.AppendLine("1. 用 C# 代码构建界面（不使用 XAML），可参考 System.Windows.Controls 各类（StackPanel/Grid/TextBlock/Button/TextBox…）；");
            sb.AppendLine("2. 定义一个公开类，继承 UserControl 并实现 DynamicBird.UI.Widgets.IWidget 接口；");
            sb.AppendLine("3. 必须实现：Name 属性、CreateView() 返回 this、OnActivated()/OnDeactivated() 空实现；");
            sb.AppendLine("4. 构造函数里构建界面并赋给 Content；");
            sb.AppendLine("5. 必要的 using：System、System.Windows、System.Windows.Controls、DynamicBird.UI.Widgets；");
            sb.AppendLine("6. 面板底色可透明/深色，文字白色或浅色，风格与系统一致；");
            sb.AppendLine("7. 需要计时器/事件/动画均可（using System.Windows.Threading 等）。");
            sb.AppendLine();
            sb.AppendLine("【当前源码】现有源码如下（可整体替换或基于它修改）：");
            sb.AppendLine(currentSource);
            sb.AppendLine();
            sb.AppendLine("【输出要求】");
            sb.AppendLine("1. 只输出一个完整 C# 源码文件，不要 Markdown 代码块标记（三个反引号不要），不要解释文字；");
            sb.AppendLine("2. 源码必须可直接编译（类名、接口、using 都完整）；");
            sb.AppendLine("3. 不要在源码中写“// 这里填…”之类的占位注释。");
            sb.AppendLine();
            sb.AppendLine("【报错处理】如果用户把编译报错信息粘贴回来，请指出错误位置并给出修正后的完整源码。");
            sb.AppendLine();
            sb.AppendLine("【用户需求】");
            sb.AppendLine("（在这里描述你想要的面板效果：显示什么、有哪些交互）");
            return sb.ToString();
        }
    }
}
