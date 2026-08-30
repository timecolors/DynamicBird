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

        /// <summary>自定义面板：生成 C# 面板源码提示词（动态编译为真实可运行面板，详细规范见 docs/WIDGET-SPEC.md）。</summary>
        private static string GeneratePanelSource(string panelName, string currentSource)
        {
            var sb = new StringBuilder();
            sb.AppendLine("【角色】你是灵动鸟 DynamicBird 的 WPF 小组件/面板开发专家。请严格按下面的规范，为自定义面板「" + panelName + "」生成完整可编译的 C# 源码（动态编译为真实可运行的面板）。");
            sb.AppendLine();
            sb.AppendLine("【必须遵守的接口契约】");
            sb.AppendLine("1. 类必须公开（public class），命名空间随意但避免与内置冲突；");
            sb.AppendLine("2. 必须继承 System.Windows.Controls.UserControl 并实现 DynamicBird.UI.Widgets.IWidget 接口；");
            sb.AppendLine("3. 必须实现四个成员：string Name（面板显示名，中文）；UserControl CreateView()（固定返回 this）；void OnActivated()（激活回调）；void OnDeactivated()（切走回调，这里停止定时器等资源）；");
            sb.AppendLine("4. 全部用 C# 代码构建界面（new StackPanel/Grid/TextBlock/Button/TextBox/ListBox/ScrollViewer...），不要使用 XAML；");
            sb.AppendLine("5. 构造函数里把根控件赋给 Content。");
            sb.AppendLine();
            sb.AppendLine("【界面风格要求】");
            sb.AppendLine("1. 面板背景深色（近似 #1E1E1E / 半透明白），文字白色或浅灰（#FFFFFF / #EEEEEE / #BBBBBB），与系统一致；");
            sb.AppendLine("2. 布局简洁留白合理，适合卡片式面板（宽 300-500、高 200-400，内容自适应）；");
            sb.AppendLine("3. 按钮高 26-28、字号 12、圆角 6；可用统一样式：FindResource(\"AccentButton\") 主按钮、FlatButton 工具按钮、CardStyle 卡片、DarkTextBox 深色输入框；");
            sb.AppendLine("4. 需要图标用内置线性图标：FindResource(\"IconLogo\") 等。");
            sb.AppendLine();
            sb.AppendLine("【线程与可靠性要求】");
            sb.AppendLine("1. 定时器必须用 System.Windows.Threading.DispatcherTimer（自动回 UI 线程），禁止 System.Threading.Timer；");
            sb.AppendLine("2. UI 更新必须在 UI 线程：async/await 后更新界面先 await Dispatcher.InvokeAsync(...)；");
            sb.AppendLine("3. 联网（HttpClient）设 Timeout（如 8 秒），所有异常 catch 并显示友好提示，禁止崩溃；");
            sb.AppendLine("4. 不要在构造函数里做同步耗时/网络操作——用异步 + 加载中占位；");
            sb.AppendLine("5. 剪贴板操作在 UI 线程直接调用。");
            sb.AppendLine();
            sb.AppendLine("【沙箱注意（若将发布到市场需避免；本地自用可忽略）】");
            sb.AppendLine("禁止：System.Diagnostics（Process）、System.Reflection（GetMethod/Activator/Assembly.Load/Type.GetType）、System.Runtime.InteropServices（DllImport/Marshal）、System.Management（WMI）、Microsoft.Win32（注册表）、System.DirectoryServices；以及 FindWindow/EnumWindows/SetWindowsHookEx/SendInput/SetForegroundWindow/PostMessage/SendMessage/keybd_event/mouse_event/CopyFromScreen/PrintWindow/BitBlt/File.Write*/File.Delete/FileStream/StreamWriter/Directory.Create/Clipboard/IDataObject 等；如需打开网址用 Process.Start(new ProcessStartInfo(url){UseShellExecute=true}) 并注明仅本地自用。");
            sb.AppendLine();
            sb.AppendLine("【当前源码】现有源码如下（可整体替换或基于它修改）：");
            sb.AppendLine(currentSource);
            sb.AppendLine();
            sb.AppendLine("【输出格式要求】");
            sb.AppendLine("1. 只输出一个完整 C# 源码文件（所有 using 齐全），不要 Markdown 代码块标记（三个反引号不要），不要任何解释文字；");
            sb.AppendLine("2. 代码必须能直接编译：类名、继承、接口、using 完整；");
            sb.AppendLine("3. 不要写占位注释；变量名清晰；");
            sb.AppendLine("4. 若需要用户数据/设置，说明假设的数据来源（如字段默认值）。");
            sb.AppendLine();
            sb.AppendLine("【自检清单（输出前逐项核对）】");
            sb.AppendLine("1. 类是否 public 且 : UserControl, IWidget？2. Name/CreateView/OnActivated/OnDeactivated 是否都实现、CreateView 是否返回 this？3. 是否全代码构建 UI 并赋给 Content？4. using 是否完整？5. 定时器是否 DispatcherTimer、OnDeactivated 是否 Stop？6. 网络/异步是否有 Timeout、catch、UI 提示？7. 是否误用沙箱禁止 API（面向市场时）？8. 是否无占位符、可直接编译？");
            sb.AppendLine();
            sb.AppendLine("【报错处理】如果用户把编译报错信息粘贴回来，请指出错误位置（文件/行/列）和原因，并给出修正后的完整代码（仍遵守以上全部规范）。");
            sb.AppendLine();
            sb.AppendLine("【用户需求】");
            sb.AppendLine("（在这里描述你想要的面板效果：显示什么、有哪些交互）");
            return sb.ToString();
        }
    }
}
