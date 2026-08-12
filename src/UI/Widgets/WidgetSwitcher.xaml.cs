using DynamicBird.Core.Services;
using DynamicBird.Core.Services.Configuration;
using DynamicBird.src.core.Services.Clipboard;
using DynamicBird.src.core.Services.Notes;
using DynamicBird.UI.Widgets.ClipboardHistory;
using DynamicBird.UI.Widgets.Notes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DynamicBird.UI.Widgets
{
    public partial class WidgetSwitcher : UserControl
    {
        private readonly ClipboardHistoryWidget _clipboardWidget;
        private readonly NoteWidget _noteWidget;
        private readonly ISettingsService _settings;
        private string _currentTab;

        private readonly SolidColorBrush HighlightBrush = new SolidColorBrush(Color.FromRgb(0, 120, 212));
        private readonly SolidColorBrush DefaultBrush = new SolidColorBrush(Color.FromRgb(64, 64, 64));

        public WidgetSwitcher(ISettingsService settings, IClipboardService clipboardService, INoteService noteService)
        {
            _settings = settings;
            InitializeComponent();

            _clipboardWidget = new ClipboardHistoryWidget(clipboardService);
            _noteWidget = new NoteWidget(noteService, settings);

            _currentTab = _settings.LastWidgetTab;
            if (string.IsNullOrEmpty(_currentTab)) _currentTab = "Clipboard";

            SelectTab(_currentTab);
        }

        private void SelectTab(string tab)
        {
            _currentTab = tab;
            _settings.LastWidgetTab = tab;

            BtnClipboard.Background = tab == "Clipboard" ? HighlightBrush : DefaultBrush;
            BtnNote.Background = tab == "Note" ? HighlightBrush : DefaultBrush;

            if (tab == "Clipboard")
            {
                ContentContainer.Content = _clipboardWidget;
                _clipboardWidget.OnActivated();
                _noteWidget.OnDeactivated();
                FooterPanel.Child = _clipboardWidget.GetFooterControl();
            }
            else
            {
                ContentContainer.Content = _noteWidget;
                _noteWidget.OnActivated();
                _clipboardWidget.OnDeactivated();
                FooterPanel.Child = _noteWidget.GetFooterControl();
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
    }
}