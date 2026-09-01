using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ShoreHue.Core.Services.Ai;
using ShoreHue.Infrastructure.WinApi;
using ShoreHue.UI.Localization;

namespace ShoreHue.UI.Widgets.TextAi
{
    /// <summary>
    /// 划词翻译 小组件：在任何应用中选中文字，按全局快捷键（引导页 / 设置中配置），
    /// 自动读取选中文本并调用 AI 翻译，结果流式显示在本面板内。
    /// 纯文本请求，所有 OpenAI 兼容模型都支持。
    /// </summary>
    public partial class TextAiWidget : UserControl, IWidget
    {
        private readonly AiChatClient _client = new();
        private CancellationTokenSource? _cts;

        /// <summary>面板内“打开设置”按钮被点击时触发（由主窗口订阅）。</summary>
        public static event Action? OpenSettingsRequested;

        public TextAiWidget()
        {
            InitializeComponent();
        }

        public new string Name => LocalizationManager.Instance["WidgetTabs_TextAi"];

        public UserControl CreateView() => this;

        public void OnActivated()
        {
        }

        public void OnDeactivated()
        {
            // 保持流式输出继续：面板切走时翻译继续，切回可见结果
        }

        public FrameworkElement GetFooterControl()
        {
            return new StackPanel();
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            OpenSettingsRequested?.Invoke();
        }

        /// <summary>一键复制译文（STA/UI 线程直接 SetText）。</summary>
        private void CopyResult_Click(object sender, RoutedEventArgs e)
        {
            string text = ResultText.Text.Trim();
            if (text.Length == 0) return;
            try
            {
                Clipboard.SetText(text);
                BtnCopyResult.Content = LocalizationManager.Instance["TextAi_Copied"];
                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1.5)
                };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    BtnCopyResult.Content = LocalizationManager.Instance["TextAi_Copy"];
                };
                timer.Start();
            }
            catch { }
        }

        // ============ 划词翻译 ============

        /// <summary>由全局热键触发：捕获前台窗口选中文本并翻译。</summary>
        public async Task CaptureAndTranslateAsync()
        {
            // 1. AI 是否已配置
            var ai = AiSettingsStore.Load();
            if (!ai.Enabled || string.IsNullOrWhiteSpace(ai.ApiKey))
            {
                ShowState(LocalizationManager.Instance["TextAi_NotConfigured"], true);
                return;
            }

            // 2. 捕获选中文本（必须在 STA/UI 线程，内部已处理剪贴板恢复）
            ShowState(LocalizationManager.Instance["TextAi_Reading"], false);
            var capture = await SelectedTextCapture.CaptureAsync(ownHwnd: GetOwnHwnd());
            if (!capture.Success)
            {
                ShowState(capture.Message.Length > 0
                    ? capture.Message
                    : LocalizationManager.Instance["TextAi_NoSelection"], true);
                return;
            }

            // 3. 翻译
            await TranslateAsync(capture.Text!, ai);
        }

        private IntPtr GetOwnHwnd()
        {
            try
            {
                var h = new System.Windows.Interop.WindowInteropHelper(Window.GetWindow(this)).Handle;
                return h;
            }
            catch { return IntPtr.Zero; }
        }

        private async Task TranslateAsync(string text, AiSettings ai)
        {
            Cancel();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            SourceText.Text = text.Length > 1000 ? text[..1000] + "…" : text;
            ResultText.Text = "";
            ResultText.Foreground = new SolidColorBrush(Color.FromRgb(238, 238, 238));
            ShowState(LocalizationManager.Instance["TextAi_Translating"], false);
            try
            {
                // 判断语言方向：含较多 CJK 字符 → 译为英文；否则译为中文
                bool chinese = CountCjk(text) >= Math.Max(3, text.Length / 6);
                string prompt = (chinese
                    ? "请将以下内容翻译成英文。只输出译文，不要任何解释、引号或多余文字：\n\n"
                    : "请将以下内容翻译成中文。只输出译文，不要任何解释、引号或多余文字：\n\n") + text;

                // 翻译用独立 SystemPrompt，避免默认助手提示词污染译文
                var translateSettings = new AiSettings
                {
                    Enabled = ai.Enabled,
                    BaseUrl = ai.BaseUrl,
                    ApiKey = ai.ApiKey,
                    Model = ai.Model,
                    Temperature = Math.Min(ai.Temperature, 0.5),
                    ContextWindowTokens = ai.ContextWindowTokens,
                    SystemPrompt = "你是翻译引擎，只输出译文。"
                };

                var history = new List<ChatMessage>();
                string full = await _client.StreamChatAsync(translateSettings, history, prompt, delta =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (!ct.IsCancellationRequested)
                        {
                            ResultText.Text += delta;
                        }
                    });
                }, ct);

                if (!ct.IsCancellationRequested)
                {
                    ShowState(full.Length > 0 ? "" : LocalizationManager.Instance["TextAi_EmptyResult"],
                        full.Length == 0);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                ShowState(LocalizationManager.Instance["TextAi_Failed"] + ex.Message, true);
            }
        }

        /// <summary>粗略统计 CJK（中日韩）字符数，用于判断翻译方向。</summary>
        private static int CountCjk(string text)
        {
            int count = 0;
            foreach (char c in text)
            {
                if (c >= 0x4E00 && c <= 0x9FFF) count++;   // CJK 统一表意文字
                else if (c >= 0x3040 && c <= 0x30FF) count++; // 日文假名
                else if (c >= 0xAC00 && c <= 0xD7AF) count++; // 韩文
            }
            return count;
        }

        private void ShowState(string? text, bool isError)
        {
            StateText.Text = text ?? "";
            StateText.Foreground = new SolidColorBrush(isError
                ? Color.FromRgb(255, 130, 120)
                : Color.FromRgb(138, 138, 138));
        }

        private void Cancel()
        {
            try
            {
                _cts?.Cancel();
            }
            catch { }
            _cts = null;
        }
    }
}
