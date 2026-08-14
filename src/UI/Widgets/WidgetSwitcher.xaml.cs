using DynamicBird.Core.Services;
using DynamicBird.Core.Services.Configuration;
using DynamicBird.src.core.Services.Clipboard;
using DynamicBird.src.core.Services.Notes;
using DynamicBird.UI.Widgets.Calculator;
using DynamicBird.UI.Widgets.ClipboardHistory;
using DynamicBird.UI.Widgets.Notes;
using DynamicBird.UI.Widgets.Timer;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DynamicBird.UI.Widgets
{
    public partial class WidgetSwitcher : UserControl, IWidget
    {
        private readonly ClipboardHistoryWidget _clipboardWidget;
        private readonly NoteWidget _noteWidget;
        private readonly TimerWidget _timerWidget;
        private readonly CalculatorWidget _calculatorWidget;
        private readonly ISettingsService _settings;
        private string _currentTab;

        public WidgetSwitcher(ISettingsService settings, IClipboardService clipboardService, INoteService noteService)
        {
            _settings = settings;
            InitializeComponent();

            _clipboardWidget = new ClipboardHistoryWidget(clipboardService);
            _noteWidget = new NoteWidget(noteService, settings);
            _timerWidget = new TimerWidget();
            _calculatorWidget = new CalculatorWidget();

            _currentTab = _settings.LastWidgetTab;
            if (_currentTab is not ("Clipboard" or "Note" or "Timer" or "Calculator"))
            {
                _currentTab = "Clipboard";
            }

            SelectTab(_currentTab);
        }

        private void SelectTab(string tab)
        {
            DeactivateCurrent();
            _currentTab = tab;
            _settings.LastWidgetTab = tab;

            ApplyTabStyle(BtnClipboard, tab == "Clipboard");
            ApplyTabStyle(BtnNote, tab == "Note");
            ApplyTabStyle(BtnTimer, tab == "Timer");
            ApplyTabStyle(BtnCalc, tab == "Calculator");

            switch (tab)
            {
                case "Clipboard":
                    ContentContainer.Content = _clipboardWidget;
                    _clipboardWidget.OnActivated();
                    FooterPanel.Child = _clipboardWidget.GetFooterControl();
                    break;
                case "Note":
                    ContentContainer.Content = _noteWidget;
                    _noteWidget.OnActivated();
                    FooterPanel.Child = _noteWidget.GetFooterControl();
                    break;
                case "Timer":
                    ContentContainer.Content = _timerWidget;
                    _timerWidget.OnActivated();
                    FooterPanel.Child = _timerWidget.GetFooterControl();
                    break;
                case "Calculator":
                default:
                    ContentContainer.Content = _calculatorWidget;
                    _calculatorWidget.OnActivated();
                    FooterPanel.Child = _calculatorWidget.GetFooterControl();
                    break;
            }
        }

        private void ApplyTabStyle(Button button, bool active)
        {
            button.Style = (System.Windows.Style)FindResource(active ? "AccentButton" : "FlatButton");
        }

        public new string Name => "小组件";

        public UserControl CreateView() => this;

        public void OnActivated()
        {
            switch (_currentTab)
            {
                case "Clipboard": _clipboardWidget.OnActivated(); break;
                case "Note": _noteWidget.OnActivated(); break;
                case "Timer": _timerWidget.OnActivated(); break;
                case "Calculator": _calculatorWidget.OnActivated(); break;
            }
        }

        public void OnDeactivated()
        {
            DeactivateCurrent();
        }

        private void DeactivateCurrent()
        {
            switch (_currentTab)
            {
                case "Clipboard": _clipboardWidget.OnDeactivated(); break;
                case "Note": _noteWidget.OnDeactivated(); break;
                case "Timer": _timerWidget.OnDeactivated(); break;
                case "Calculator": _calculatorWidget.OnDeactivated(); break;
            }
        }

        private void BtnClipboard_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTab == "Clipboard") return;
            SelectTab("Clipboard");
        }

        private void BtnNote_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTab == "Note") return;
            SelectTab("Note");
        }

        private void BtnTimer_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTab == "Timer") return;
            SelectTab("Timer");
        }

        private void BtnCalc_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTab == "Calculator") return;
            SelectTab("Calculator");
        }
    }
}
