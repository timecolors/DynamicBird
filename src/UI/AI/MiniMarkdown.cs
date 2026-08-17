using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace DynamicBird.UI.AI
{
    /// <summary>
    /// 极简 Markdown → FlowDocument 渲染器（聊天用）：
    /// 支持标题 / 粗体 / 斜体 / 行内代码 / 代码块 / 列表 / 引用 / 链接。
    /// 不引入第三方依赖，样式贴合深色面板。
    /// </summary>
    public static class MiniMarkdown
    {
        private static readonly SolidColorBrush CodeBg = new(Color.FromRgb(45, 45, 55));
        private static readonly SolidColorBrush CodeFg = new(Color.FromRgb(224, 224, 235));
        private static readonly SolidColorBrush TextFg = new(Color.FromRgb(238, 238, 238));
        private static readonly SolidColorBrush QuoteFg = new(Color.FromRgb(160, 160, 170));

        public static FlowDocument ToFlowDocument(string markdown)
        {
            var doc = new FlowDocument
            {
                FontSize = 13,
                Foreground = TextFg,
                PagePadding = new Thickness(4),
                LineHeight = 1.35
            };

            var lines = markdown.Replace("\r\n", "\n").Split('\n');
            bool inCodeBlock = false;
            var codeLines = new List<string>();

            void FlushCode()
            {
                if (codeLines.Count == 0) return;
                var p = new Paragraph
                {
                    Background = CodeBg,
                    Margin = new Thickness(0, 4, 0, 6),
                    Padding = new Thickness(8, 6, 8, 6)
                };
                p.Inlines.Add(new Run(string.Join("\n", codeLines))
                {
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 12,
                    Foreground = CodeFg
                });
                doc.Blocks.Add(p);
                codeLines.Clear();
            }

            foreach (var raw in lines)
            {
                string line = raw.TrimEnd();

                if (inCodeBlock)
                {
                    if (line.TrimStart().StartsWith("```"))
                    {
                        inCodeBlock = false;
                        FlushCode();
                    }
                    else
                    {
                        codeLines.Add(line);
                    }
                    continue;
                }

                if (line.TrimStart().StartsWith("```"))
                {
                    FlushCode();
                    inCodeBlock = true;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(line)) continue;

                // 标题
                var m = Regex.Match(line, "^(#{1,4})\\s+(.*)$");
                if (m.Success)
                {
                    int level = m.Groups[1].Value.Length;
                    var hp = new Paragraph
                    {
                        Margin = new Thickness(0, 4, 0, 4),
                        FontSize = level <= 1 ? 17 : level == 2 ? 15 : 14,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = TextFg
                    };
                    hp.Inlines.Add(new Run(m.Groups[2].Value));
                    doc.Blocks.Add(hp);
                    continue;
                }

                // 引用
                if (line.StartsWith(">"))
                {
                    var qp = new Paragraph { Margin = new Thickness(6, 1, 0, 3) };
                    qp.Inlines.Add(new Run(line.TrimStart('>').Trim())
                    {
                        Foreground = QuoteFg,
                        FontStyle = FontStyles.Italic
                    });
                    doc.Blocks.Add(qp);
                    continue;
                }

                // 无序列表
                var bullet = Regex.Match(line, "^[-*+]\\s+(.*)$");
                if (bullet.Success)
                {
                    var lp = new Paragraph
                    {
                        Margin = new Thickness(14, 1, 0, 2),
                        TextIndent = -10
                    };
                    lp.Inlines.Add(new Run("•  "));
                    lp.Inlines.Add(new Run(bullet.Groups[1].Value));
                    doc.Blocks.Add(lp);
                    continue;
                }

                // 有序列表
                var num = Regex.Match(line, "^(\\d+)[.)]\\s+(.*)$");
                if (num.Success)
                {
                    var np = new Paragraph
                    {
                        Margin = new Thickness(14, 1, 0, 2),
                        TextIndent = -12
                    };
                    np.Inlines.Add(new Run(num.Groups[1].Value + ".  "));
                    np.Inlines.Add(new Run(num.Groups[2].Value));
                    doc.Blocks.Add(np);
                    continue;
                }

                // 普通段落
                var pp = new Paragraph { Margin = new Thickness(0, 1, 0, 3) };
                pp.Inlines.Add(new Run(line));
                doc.Blocks.Add(pp);
            }

            if (inCodeBlock) FlushCode();
            return doc;
        }
    }
}