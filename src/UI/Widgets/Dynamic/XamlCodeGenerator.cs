using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicBird.UI.Widgets.Dynamic
{
    /// <summary>
    /// XAML → C# 补全代码生成器（完全编程模式）：
    /// 把 .xaml + .xaml.cs 转成可动态编译的 C#（XamlReader 加载 + 命名元素绑定 + 事件挂钩）。
    /// XAML 以 base64 嵌入避免转义；正则全部用 verbatim 字符串。
    /// </summary>
    public static class XamlCodeGenerator
    {
        private static readonly string[] EventNames =
        {
            "Click", "MouseDown", "MouseUp", "KeyDown", "KeyUp", "TextChanged",
            "SelectionChanged", "MouseLeftButtonDown", "MouseLeftButtonUp",
            "LostFocus", "GotFocus", "PreviewMouseDown", "PreviewMouseUp",
            "Checked", "Unchecked", "ValueChanged", "DragOver", "Drop"
        };

        public static string? Generate(string xaml, string xamlCs)
        {
            if (string.IsNullOrWhiteSpace(xaml) || string.IsNullOrWhiteSpace(xamlCs)) return null;
            if (!xamlCs.Contains("partial class")) return null;

            var nsM = System.Text.RegularExpressions.Regex.Match(xamlCs, @"namespace\s+([\w.]+)");
            string ns = nsM.Success ? nsM.Groups[1].Value : "";

            var classM = System.Text.RegularExpressions.Regex.Match(xamlCs,
                @"(?:public|internal)\s+partial\s+class\s+([A-Za-z_][A-Za-z0-9_]*)");
            string className = classM.Success ? classM.Groups[1].Value : "DynamicXamlWidget";

            var bindings = ExtractBindings(xaml);
            var named = ExtractNamedElements(xaml);
            string cleanXaml = CleanXaml(xaml);

            string xamlB64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(cleanXaml));
            var sb = new StringBuilder();
            sb.AppendLine("// ===== 自动生成：InitializeComponent（XamlReader 加载 + 绑定） =====");
            if (!string.IsNullOrEmpty(ns)) { sb.AppendLine("namespace " + ns); sb.AppendLine("{"); }
            sb.AppendLine("public partial class " + className);
            sb.AppendLine("{");
            sb.AppendLine("    private void InitializeComponent()");
            sb.AppendLine("    {");
            sb.AppendLine("        var xamlB64 = \"" + xamlB64 + "\";");
            sb.AppendLine("        var xaml = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(xamlB64));");
            sb.AppendLine("        var root = (System.Windows.Controls.UserControl)System.Windows.Markup.XamlReader.Parse(xaml);");
            sb.AppendLine("        this.Content = ((System.Windows.Controls.UserControl)root).Content;");
            foreach (var kv in named)
            {
                sb.AppendLine("        var " + kv.Key + "Field = (" + kv.Value + ")((System.Windows.FrameworkElement)root).FindName(\"" + kv.Key + "\");");
                sb.AppendLine("        if (" + kv.Key + "Field != null) this." + kv.Key + " = " + kv.Key + "Field;");
            }
            foreach (var (elem, evt, handler) in bindings)
            {
                sb.AppendLine("        if (" + elem + "Field != null) " + elem + "Field." + evt + " += " + handler + ";");
            }
            sb.AppendLine("    }");
            // ★ 命名元素字段声明（由生成器提供，保证代码自包含）
            foreach (var kv in named)
                sb.AppendLine("    private " + kv.Value + " " + kv.Key + ";");
            sb.AppendLine("}");
            if (!string.IsNullOrEmpty(ns)) sb.AppendLine("}");

            return "using System;" + Environment.NewLine + "using System.Windows;" + Environment.NewLine + xamlCs + Environment.NewLine + sb;
        }

        private static List<(string Elem, string Evt, string Handler)> ExtractBindings(string xaml)
        {
            var result = new List<(string, string, string)>();
            string evtPattern = string.Join("|", EventNames);
            var re = new System.Text.RegularExpressions.Regex(@"(" + evtPattern + @")=""([A-Za-z0-9_]+)""");
            foreach (System.Text.RegularExpressions.Match m in re.Matches(xaml))
            {
                string before = xaml.Substring(0, m.Index);
                var nameMs = System.Text.RegularExpressions.Regex.Matches(before, @"x:Name=""([A-Za-z0-9_]+)""");
                string elem = nameMs.Count > 0 ? nameMs[nameMs.Count - 1].Groups[1].Value : "Root";
                result.Add((elem, m.Groups[1].Value, m.Groups[2].Value));
            }
            return result;
        }

        private static Dictionary<string, string> ExtractNamedElements(string xaml)
        {
            var result = new Dictionary<string, string>();
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(
                xaml, @"<([A-Za-z0-9_.]+)[^>]*x:Name=""([A-Za-z0-9_]+)"""))
            {
                string name = m.Groups[2].Value;
                if (!result.ContainsKey(name)) result[name] = InferType(m.Groups[1].Value);
            }
            return result;
        }

        private static string CleanXaml(string xaml)
        {
            string result = xaml;
            string evtPattern = string.Join("|", EventNames);
            // 移除事件属性（verbatim 正则：s 直接写、"" 转义引号）
            result = System.Text.RegularExpressions.Regex.Replace(result,
                @"\s+(" + evtPattern + @")=""[A-Za-z0-9_]+""", "");
            // 移除 x:Class（verbatim）
            result = System.Text.RegularExpressions.Regex.Replace(result,
                @"\s*x:Class=""[^""]*""", "");
            return result;
        }

        private static string InferType(string tag)
        {
            string shortName = tag.Contains('.') ? tag.Substring(tag.LastIndexOf('.') + 1) : tag;
            if (shortName.Contains("<")) shortName = shortName.Substring(0, shortName.IndexOf("<"));
            return shortName switch
            {
                "TextBlock" => "System.Windows.Controls.TextBlock",
                "Button" => "System.Windows.Controls.Button",
                "TextBox" => "System.Windows.Controls.TextBox",
                "Label" => "System.Windows.Controls.Label",
                "StackPanel" => "System.Windows.Controls.StackPanel",
                "Grid" => "System.Windows.Controls.Grid",
                "Border" => "System.Windows.Controls.Border",
                "Image" => "System.Windows.Controls.Image",
                "ComboBox" => "System.Windows.Controls.ComboBox",
                "ListBox" => "System.Windows.Controls.ListBox",
                "CheckBox" => "System.Windows.Controls.CheckBox",
                "Slider" => "System.Windows.Controls.Slider",
                "ProgressBar" => "System.Windows.Controls.ProgressBar",
                "Expander" => "System.Windows.Controls.Expander",
                "Popup" => "System.Windows.Controls.Primitives.Popup",
                "ScrollViewer" => "System.Windows.Controls.ScrollViewer",
                "TabControl" => "System.Windows.Controls.TabControl",
                "WrapPanel" => "System.Windows.Controls.WrapPanel",
                "Canvas" => "System.Windows.Controls.Canvas",
                "DockPanel" => "System.Windows.Controls.DockPanel",
                "UserControl" => "System.Windows.Controls.UserControl",
                _ => "System.Windows.FrameworkElement"
            };
        }
    }
}