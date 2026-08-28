using DynamicBird.UI.Theme;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace DynamicBird.UI.Widgets.Dynamic
{
    /// <summary>
    /// C# 小组件编辑器：编写 C# 代码（实现 IWidget，任意 WPF UI），
    /// 编译预览后保存为本地插件。支持插入内置示例作为起点。
    /// </summary>
    public partial class WidgetEditorWindow : Window
    {
        private IWidget? _previewWidget;

        public WidgetEditorWindow(WidgetPlugin? existing = null)
        {
            Icon = AppIconHelper.LoadAppIcon();
            InitializeComponent();

            if (existing != null)
            {
                txtName.Text = existing.Name;
                txtId.Text = existing.Id;
                txtCode.Text = existing.Source;
            }
            else
            {
                // ★ 唯一 ID：自动生成且不可修改（编辑器中只读）
                string id;
                do
                {
                    id = "widget_" + Guid.NewGuid().ToString("N").Substring(0, 6);
                }
                while (WidgetPluginStore.GetById(id) != null);
                txtId.Text = id;
            }
        }

                /// <summary>AI 生成入口（兼容旧入口）：检查配置并聚焦输入框。</summary>
        public void PrepareForAi()
        {
            BtnAiGenerate_Click(this, new RoutedEventArgs());
        }

        private void BtnAiGenerate_Click(object sender, RoutedEventArgs e)
        {
            var ai = DynamicBird.Core.Services.Ai.AiSettingsStore.Load();
            if (string.IsNullOrWhiteSpace(ai.BaseUrl) || string.IsNullOrWhiteSpace(ai.ApiKey))
            {
                MessageBox.Show("请先在 设置 → AI 助手 中配置 API 地址与密钥。" + "\n" + "（也可以用其他 AI 工具生成 C# 代码后粘贴到上方代码区）",
                    "AI 生成", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            btnAiGenerateGo.IsEnabled = true;
            txtAiPrompt.Focus();
            txtCompileStatus.Text = "描述你想要的小组件（如“一个番茄钟 / 汇率换算”），然后点“生成”";
            txtCompileStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(136, 136, 136));
        }

        private async void BtnAiGenerateGo_Click(object sender, RoutedEventArgs e)
        {
            string prompt = txtAiPrompt.Text.Trim();
            if (prompt.Length == 0)
            {
                txtCompileStatus.Text = "请先在上方输入框描述你想要的小组件";
                return;
            }
            var ai = DynamicBird.Core.Services.Ai.AiSettingsStore.Load();
            if (string.IsNullOrWhiteSpace(ai.BaseUrl) || string.IsNullOrWhiteSpace(ai.ApiKey))
            {
                MessageBox.Show("请先在 设置 → AI 助手 中配置 API 地址与密钥。", "AI 生成", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            btnAiGenerateGo.IsEnabled = false;
            txtCompileStatus.Text = "AI 正在生成代码…";
            txtCompileStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(136, 136, 136));
            try
            {
                using var client = new DynamicBird.Core.Services.Ai.AiChatClient();
                // 用代码生成器角色提示词（仅本次请求生效，不写回存储）
                ai.SystemPrompt =
                    "你是灵动鸟的 C# 小组件代码生成器。根据用户需求生成一个完整的 C# 类：" +
                    "public 类、继承 UserControl 并实现 DynamicBird.UI.Widgets.IWidget 接口" +
                    "（Name 属性、CreateView() 返回 this、OnActivated()/OnDeactivated() 空实现）。" +
                    "只能用 WPF 基础控件（System.Windows.*、System.Windows.Controls.*），不要引用第三方包。" +
                    "输出完整可编译的 C# 代码，不要解释文字，不要 Markdown 代码块标记。";
                string code = await client.StreamChatAsync(
                    ai,
                    new System.Collections.Generic.List<DynamicBird.Core.Services.Ai.ChatMessage>(),
                    prompt,
                    delta => Dispatcher.Invoke(() => txtCode.AppendText(delta)));

                txtCode.Text = code
                    .Replace("```csharp", "")
                    .Replace("```C#", "")
                    .Replace("```cs", "")
                    .Replace("```", "")
                    .Trim();
                txtCompileStatus.Text = "✅ AI 生成完成，可修改后编译预览";
                txtCompileStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 170, 90));
            }
            catch (Exception ex)
            {
                txtCompileStatus.Text = "❌ AI 生成失败：" + ex.Message;
                txtCompileStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 80, 70));
            }
            finally
            {
                btnAiGenerateGo.IsEnabled = true;
            }
        }

        /// <summary>插入完整 AI 提示词到输入框：复制到免费网页版 AI 生成代码后粘贴回来。</summary>
        private void InsertPrompt_Click(object sender, RoutedEventArgs e)
        {
            txtAiPrompt.Text = "请为“灵动鸟”桌面助手生成一个 C# 小组件代码。\n需求：<在这里描述你想要的小组件，例如“一个番茄钟 / 一个便签列表”>\n要求：\n1. 定义一个 public 类，继承 System.Windows.Controls.UserControl 并实现 DynamicBird.UI.Widgets.IWidget 接口：Name 属性返回小组件名称；CreateView() 返回 this；OnActivated()/OnDeactivated() 空实现。\n2. 只使用 WPF 基础控件（System.Windows / System.Windows.Controls），不要引用第三方包。\n3. 输出完整、可直接编译的 C# 代码，不要任何解释文字，不要 Markdown 代码块标记。";
            txtAiPrompt.SelectAll();
            txtAiPrompt.Focus();
            txtCompileStatus.Text = "提示词已填入，可复制到网页版 AI 生成代码（或直接点“生成”）";
            txtCompileStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 170, 90));
        }

        private void Preview_Click(object sender, RoutedEventArgs e)
        {
            string id = txtId.Text.Trim();
            if (id.Length == 0) id = "widget_preview";
            var (widget, err) = WidgetCompiler.Compile(id, txtCode.Text);
            if (widget == null)
            {
                txtCompileStatus.Text = "❌ 编译失败：\n" + err;
                txtCompileStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 80, 70));
                _previewWidget = null;
                PreviewHost.Content = null;
                return;
            }
            _previewWidget = widget;
            PreviewHost.Content = widget.CreateView();
            txtCompileStatus.Text = "✅ 编译成功，下方为预览效果";
            txtCompileStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 170, 90));
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            string id = txtId.Text.Trim();
            string name = txtName.Text.Trim();
            if (name.Length == 0) name = "我的小组件";

            // ★ 权限不在此手动勾选：导出/上传市场时由系统自动检测并标注
            var plugin = new WidgetPlugin
            {
                Id = id,
                Name = name,
                Description = "",
                Permissions = new List<string>(),
                Source = txtCode.Text
            };

            // 保存前编译校验
            string err = WidgetCompiler.Validate(id.Length == 0 ? "widget_save" : id, plugin.Source);
            if (err.Length > 0)
            {
                MessageBox.Show("编译不通过，请先修复：\n" + err, "灵动鸟", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (id.Length == 0)
            {
                MessageBox.Show("请填写小组件 ID（仅英文/数字/下划线）", "灵动鸟", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string saveErr = WidgetPluginStore.Save(plugin);
            if (saveErr.Length > 0)
            {
                MessageBox.Show(saveErr, "灵动鸟", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
