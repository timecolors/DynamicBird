using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ShoreHue.UI.Seabed
{
    /// <summary>
    /// 放流弹窗：把海床里的自定义项发布到「其他海床」市场。
    /// 预填当前选中项的名称/源码/权限，用户补充 id/分类/描述后一键发布。
    /// 主题跟随系统（浅色白底 / 深色黑底）。
    /// </summary>
    public sealed class PublishWindow : Window
    {
        private readonly string _source;
        private readonly string _defaultName;
        private readonly string _kind;
        private readonly string _baseType;
        private readonly string _parentKey;
        private readonly string _sourceKey;
        private readonly string _defaultCategory;
        private readonly System.Collections.Generic.List<string> _permissions;

        private readonly TextBox _idBox = new();
        private readonly TextBox _nameBox = new();
        private readonly ComboBox _catBox = new();
        private readonly TextBox _descBox = new();
        private readonly TextBlock _status = new() { FontSize = 11, TextWrapping = TextWrapping.Wrap };
        private readonly Button _publishBtn = new();
        private bool _busy;

        /// <summary>null = 未发布；非 null = 错误信息（成功返回 null 由调用方判定）。</summary>
        public string? ResultError { get; private set; }
        public bool Published { get; private set; }
        /// <summary>发布时最终使用的名称（成功后可读）。</summary>
        public string NameResult { get; private set; } = "";

        private readonly System.Collections.Generic.List<GitHubMarketService.PackageFile> _extraFiles;

        public PublishWindow(string source, string defaultName, string kind, string baseType,
            string parentKey, string sourceKey, string defaultCategory, System.Collections.Generic.List<string> permissions,
            System.Collections.Generic.List<GitHubMarketService.PackageFile>? extraFiles = null)
        {
            _source = source ?? "";
            _extraFiles = extraFiles ?? new();
            _defaultName = defaultName ?? "";
            _kind = kind ?? "Widget";
            _baseType = baseType ?? "Widget";
            _parentKey = parentKey ?? "";
            _sourceKey = sourceKey ?? "";
            _defaultCategory = defaultCategory ?? "小组件";
            _permissions = permissions ?? new System.Collections.Generic.List<string>();

            bool light = ShoreHue.Infrastructure.Utils.SystemTheme.IsLightTheme();
            var cBg = light ? Color.FromRgb(0xF9, 0xF9, 0xF9) : Color.FromRgb(0x1E, 0x1E, 0x1E);
            var cCard = light ? Colors.White : Color.FromRgb(0x2A, 0x2A, 0x2A);
            var cText = light ? Color.FromRgb(0x1E, 0x1E, 0x1E) : Colors.White;
            var cSub = light ? Color.FromRgb(0x66, 0x66, 0x66) : Color.FromRgb(0xBB, 0xBB, 0xBB);
            var cBorder = light ? Color.FromRgb(0xE0, 0xE0, 0xE0) : Color.FromRgb(0x40, 0x40, 0x40);

            Title = "放流 · 发布到其他海床";
            Width = 480;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Background = new SolidColorBrush(cBg);
            FontFamily = new FontFamily("Microsoft YaHei UI");

            var root = new StackPanel { Margin = new Thickness(22, 18, 22, 18) };
            root.Children.Add(new TextBlock
            {
                Text = "放流 · 发布到其他海床",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(cText)
            });
            root.Children.Add(new TextBlock
            {
                Text = "把当前选中的「" + _defaultName + "」发布到市场，其他 ShoreHue 用户即可拾贝。代码将公开托管在 GitHub。",
                FontSize = 11.5,
                Foreground = new SolidColorBrush(cSub),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0)
            });

            // 表单卡片
            var card = new Border
            {
                Background = new SolidColorBrush(cCard),
                BorderBrush = new SolidColorBrush(cBorder),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 12, 0, 0)
            };
            var form = new Grid { Margin = new Thickness(0) };
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            form.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            form.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            AddField(form, 0, "包 ID", _idBox, "英文/数字/下划线/连字符，如 my-widget");
            AddField(form, 1, "名称", _nameBox, "");
            AddField(form, 2, "分类", _catBox, "");
            AddField(form, 3, "描述", _descBox, "一句话介绍（可留空）");

            _idBox.Text = SanitizeId(_defaultName);
            _nameBox.Text = _defaultName;
            string[] cats = { "小组件", "面板功能", "面板设计", "动画", "外观", "交互", "状态栏" };
            _catBox.ItemsSource = cats;
            int idx = Array.IndexOf(cats, _defaultCategory);
            _catBox.SelectedIndex = idx >= 0 ? idx : 0;   // ★ 分类自动带出（选中项的分类），无需手动选
            _descBox.Text = "";
            _idBox.FontSize = 12; _nameBox.FontSize = 12; _catBox.FontSize = 12; _descBox.FontSize = 12;
            _idBox.Height = 28; _nameBox.Height = 28; _catBox.Height = 28; _descBox.Height = 28;
            _descBox.VerticalContentAlignment = VerticalAlignment.Center;

            card.Child = form;
            root.Children.Add(card);

            // ★ 作者：直接用 GitHub 登录名（只读展示，无需填写个人信息）
            root.Children.Add(new TextBlock
            {
                Text = "作者：" + (GitHubMarketService.CurrentUser ?? "未登录"),
                FontSize = 11,
                Foreground = new SolidColorBrush(cSub),
                Margin = new Thickness(0, 8, 0, 0)
            });

            // 权限提示
            string permDesc = _permissions.Count > 0
                ? "⚠ 将标注权限：" + string.Join(", ", _permissions)
                : "无风险权限标注";
            root.Children.Add(new TextBlock
            {
                Text = permDesc,
                FontSize = 11,
                Foreground = new SolidColorBrush(_permissions.Count > 0 ? Color.FromRgb(0xB0, 0x70, 0x20) : cSub),
                Margin = new Thickness(0, 8, 0, 0)
            });

            _status.Text = "就绪";
            _status.Foreground = new SolidColorBrush(cSub);
            _status.Margin = new Thickness(0, 10, 0, 0);
            root.Children.Add(_status);

            var bottom = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            _publishBtn.Content = "放流";
            _publishBtn.Width = 100; _publishBtn.Height = 30; _publishBtn.FontSize = 12; _publishBtn.Margin = new Thickness(0, 0, 8, 0);
            _publishBtn.Style = (Style)FindResource("Win11Button");
            _publishBtn.Click += async (_, _) => await PublishAsync();
            bottom.Children.Add(_publishBtn);
            var cancel = new Button { Content = "取消", Width = 80, Height = 30, FontSize = 12 };
            cancel.Style = (Style)FindResource("Win11Button");
            cancel.Click += (_, _) => Close();
            bottom.Children.Add(cancel);
            root.Children.Add(bottom);

            Content = root;
        }

        private static void AddField(Grid grid, int row, string label, Control control, string hint)
        {
            var lb = new TextBlock
            {
                Text = label,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, row == 0 ? 0 : 8, 8, 0)
            };
            Grid.SetRow(lb, row); Grid.SetColumn(lb, 0);
            grid.Children.Add(lb);
            Grid.SetRow(control, row); Grid.SetColumn(control, 1);
            grid.Children.Add(control);
            if (!string.IsNullOrEmpty(hint)) control.ToolTip = hint;
        }

        private static string SanitizeId(string name)
        {
            var sb = new System.Text.StringBuilder();
            foreach (char c in (name ?? "").ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-') sb.Append(c);
                if (sb.Length >= 32) break;
            }
            return sb.Length >= 2 ? sb.ToString() : "my-widget";
        }

        private async Task PublishAsync()
        {
            if (_busy) return;
            string id = _idBox.Text.Trim();
            if (!System.Text.RegularExpressions.Regex.IsMatch(id, "^[A-Za-z0-9_-]{2,64}$"))
            {
                _status.Text = "包 ID 仅允许 英文/数字/下划线/连字符（2-64 字符）";
                _status.Foreground = new SolidColorBrush(Color.FromRgb(0xD4, 0x50, 0x45));
                return;
            }
            try
            {
                _busy = true;
                _publishBtn.IsEnabled = false;
                _status.Text = "正在放流…";
                _status.Foreground = new SolidColorBrush(Colors.Gray);
                string name = string.IsNullOrWhiteSpace(_nameBox.Text) ? _defaultName : _nameBox.Text.Trim();
                string? err = await GitHubMarketService.PublishPackageAsync(
                    id, name, _kind, _catBox.SelectedItem?.ToString() ?? "小组件", "1.0.0",
                    _descBox.Text.Trim(), _baseType, _parentKey, _sourceKey, _permissions, _source, _extraFiles);
                if (err != null)
                {
                    _status.Text = err;
                    _status.Foreground = new SolidColorBrush(Color.FromRgb(0xD4, 0x50, 0x45));
                    return;
                }
                ResultError = null;
                Published = true;
                NameResult = name;
                _status.Text = "已放流「" + name + "」！稍后其他 ShoreHue 用户可在市场拾贝（CDN 缓存约几分钟）";
                _status.Foreground = new SolidColorBrush(Color.FromRgb(0x3C, 0xA8, 0x5C));
                _publishBtn.IsEnabled = true;
            }
            catch (Exception ex)
            {
                _status.Text = "放流失败：" + ex.Message;
                _status.Foreground = new SolidColorBrush(Color.FromRgb(0xD4, 0x50, 0x45));
                _busy = false;
                _publishBtn.IsEnabled = true;
            }
        }
    }
}
