using ShoreHue.Core.Services;
using ShoreHue.Core.Services.Configuration;
using ShoreHue.src.core.Services.Notes;
using ShoreHue.UI.Localization;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WinForms = System.Windows.Forms;

namespace ShoreHue.UI.Widgets.Notes
{
    public partial class NoteWidget : UserControl, IWidget
    {
        private readonly INoteService _noteService;
        private readonly ISettingsService _settings;
        private bool _isUpdating = false;

        private Button? _btnNewNote;
        private Button? _btnDeleteNote;
        private TextBlock? _statusText;

        public NoteWidgetViewModel ViewModel { get; } = new NoteWidgetViewModel();

        public NoteWidget(INoteService noteService, ISettingsService settings)
        {
            _noteService = noteService;
            _settings = settings;
            InitializeComponent();
            DataContext = ViewModel;

            NoteTabs.ItemsSource = _noteService.Notes;
            _noteService.NotesChanged += OnNotesChanged;

            if (_noteService.CurrentNote != null)
            {
                ViewModel.CurrentNote = _noteService.CurrentNote;
            }

            UpdateUI();
            UpdateStatus();
        }

        public new string Name => LocalizationManager.Instance["WidgetTabs_Notes"];

        public UserControl CreateView() => this;

        public void OnActivated()
        {
            _noteService.SetCurrentNote(_noteService.CurrentNote);
            UpdateUI();
        }

        public void OnDeactivated()
        {
            SaveCurrentNote();
        }

        public FrameworkElement GetFooterControl()
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };

            _btnNewNote = new Button
            {
                Content = LocalizationManager.Instance["Note_New"],
                Width = 80,
                Height = 26,
                FontSize = 11,
                Background = new SolidColorBrush(Color.FromRgb(64, 64, 64)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            _btnNewNote.Click += NewNote_Click;

            _btnDeleteNote = new Button
            {
                Content = LocalizationManager.Instance["Note_Delete"],
                Width = 70,
                Height = 26,
                FontSize = 11,
                Background = new SolidColorBrush(Color.FromRgb(85, 51, 51)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                Margin = new Thickness(8, 0, 0, 0)
            };
            _btnDeleteNote.Click += DeleteNote_Click;

            _statusText = new TextBlock
            {
                Text = LocalizationManager.Instance["UI_TimerWidget_416"],
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0)
            };

            panel.Children.Add(_btnNewNote);
            panel.Children.Add(_btnDeleteNote);
            panel.Children.Add(_statusText);

            return panel;
        }

        private void UpdateUI()
        {
            var current = _noteService.CurrentNote;
            ViewModel.CurrentNote = current;
            // ★★★ 移除 Count <= 1 的限制 ★★★
            ViewModel.CanDelete = current != null;
            ViewModel.ShowTitle = current?.ShowTitle ?? true;
            ViewModel.StatusText = current != null
                    ? string.Format(LocalizationManager.Instance["Note_Status"], _noteService.Notes.IndexOf(current) + 1, _noteService.Notes.Count)
                    : LocalizationManager.Instance["Note_NoNote"];

            ViewModel.RefreshColorBrush();

            foreach (var note in _noteService.Notes)
            {
                note.IsCurrent = note == current;
            }

            if (_btnDeleteNote != null)
                _btnDeleteNote.IsEnabled = ViewModel.CanDelete;
            if (_statusText != null)
                _statusText.Text = ViewModel.StatusText;
        }

        private void UpdateStatus()
        {
            var current = _noteService.CurrentNote;
            ViewModel.StatusText = current != null
                    ? string.Format(LocalizationManager.Instance["Note_Status"], _noteService.Notes.IndexOf(current) + 1, _noteService.Notes.Count)
                    : LocalizationManager.Instance["Note_NoNote"];
            if (_statusText != null)
                _statusText.Text = ViewModel.StatusText;
        }

        private void SaveCurrentNote()
        {
            // 内容已通过绑定自动更新
        }

        private void OnNotesChanged(object? sender, EventArgs e)
        {
            UpdateUI();
            UpdateStatus();
        }

        private void NewNote_Click(object sender, RoutedEventArgs e)
        {
            string defaultTitle = string.Format(LocalizationManager.Instance["Note_DefaultTitle"], _noteService.Notes.Count + 1);
            var note = _noteService.CreateNote(defaultTitle);
            ViewModel.CurrentNote = note;
            _noteService.SetCurrentNote(note);
            _isUpdating = true;
            ContentEditor.Text = "";
            TitleEditor.Text = defaultTitle;
            _isUpdating = false;
            UpdateUI();
            UpdateStatus();
        }

        private void DeleteNote_Click(object sender, RoutedEventArgs e)
        {
            var current = _noteService.CurrentNote;
            if (current == null) return;

            // ★★★ 移除“至少保留一个”的限制 ★★★
            var result = MessageBox.Show(string.Format(LocalizationManager.Instance["Note_DeleteConfirm"], current.Title),
                    LocalizationManager.Instance["Note_Confirm"], MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                _noteService.DeleteNote(current);
                ViewModel.CurrentNote = _noteService.CurrentNote;
                if (ViewModel.CurrentNote != null)
                {
                    _isUpdating = true;
                    ContentEditor.Text = ViewModel.CurrentNote.Content;
                    TitleEditor.Text = ViewModel.CurrentNote.Title;
                    _isUpdating = false;
                }
                else
                {
                    // 无边签，清空编辑器
                    _isUpdating = true;
                    ContentEditor.Text = "";
                    TitleEditor.Text = "";
                    _isUpdating = false;
                }
                UpdateUI();
                UpdateStatus();
            }
        }

        private void Tab_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is NoteItem note)
            {
                SaveCurrentNote();
                _noteService.SetCurrentNote(note);
                ViewModel.CurrentNote = note;
                _isUpdating = true;
                ContentEditor.Text = note.Content;
                TitleEditor.Text = note.Title;
                _isUpdating = false;
                UpdateUI();
                UpdateStatus();
            }
        }

        private void Title_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating) return;
            if (ViewModel.CurrentNote != null && TitleEditor.Text != ViewModel.CurrentNote.Title)
            {
                _noteService.UpdateNoteTitle(ViewModel.CurrentNote, TitleEditor.Text);
            }
        }

        private void Content_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating) return;
            if (ViewModel.CurrentNote != null && ContentEditor.Text != ViewModel.CurrentNote.Content)
            {
                _noteService.UpdateNoteContent(ViewModel.CurrentNote, ContentEditor.Text);
                UpdateStatus();
            }
        }

        private void ToggleTitle_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.CurrentNote == null) return;
            bool newValue = !ViewModel.CurrentNote.ShowTitle;
            _noteService.UpdateNoteShowTitle(ViewModel.CurrentNote, newValue);
            ViewModel.ShowTitle = newValue;
        }

        private void ColorPicker_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.CurrentNote == null) return;

            using var dialog = new WinForms.ColorDialog();
            dialog.Color = HexToDrawingColor(ViewModel.CurrentNote.Color);
            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                string hex = $"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}";
                _noteService.UpdateNoteColor(ViewModel.CurrentNote, hex);
                UpdateUI();
            }
        }

        private System.Drawing.Color HexToDrawingColor(string hex)
        {
            try
            {
                if (string.IsNullOrEmpty(hex)) return System.Drawing.Color.FromArgb(255, 255, 255, 153);
                if (hex.StartsWith("#")) hex = hex.Substring(1);
                byte a = 255, r = 0, g = 0, b = 0;
                if (hex.Length == 6)
                {
                    r = Convert.ToByte(hex.Substring(0, 2), 16);
                    g = Convert.ToByte(hex.Substring(2, 2), 16);
                    b = Convert.ToByte(hex.Substring(4, 2), 16);
                }
                else if (hex.Length == 8)
                {
                    a = Convert.ToByte(hex.Substring(0, 2), 16);
                    r = Convert.ToByte(hex.Substring(2, 2), 16);
                    g = Convert.ToByte(hex.Substring(4, 2), 16);
                    b = Convert.ToByte(hex.Substring(6, 2), 16);
                }
                else return System.Drawing.Color.FromArgb(255, 255, 255, 153);
                return System.Drawing.Color.FromArgb(a, r, g, b);
            }
            catch { return System.Drawing.Color.FromArgb(255, 255, 255, 153); }
        }
    }

    // NoteWidgetViewModel 和转换器保持不变
    public class NoteWidgetViewModel : System.ComponentModel.INotifyPropertyChanged
    {
        private NoteItem? _currentNote;
        private bool _canDelete;
        private bool _showTitle;
        private string _statusText = "";
        private SolidColorBrush _colorBrush = new SolidColorBrush(Color.FromRgb(255, 255, 153));

        public NoteItem? CurrentNote
        {
            get => _currentNote;
            set
            {
                _currentNote = value;
                OnPropertyChanged(nameof(CurrentNote));
                RefreshColorBrush();
                OnPropertyChanged(nameof(ShowTitleVisibility));
            }
        }

        public bool CanDelete
        {
            get => _canDelete;
            set { _canDelete = value; OnPropertyChanged(nameof(CanDelete)); }
        }

        public bool ShowTitle
        {
            get => _showTitle;
            set
            {
                _showTitle = value;
                OnPropertyChanged(nameof(ShowTitle));
                OnPropertyChanged(nameof(ShowTitleVisibility));
            }
        }

        public Visibility ShowTitleVisibility => ShowTitle ? Visibility.Visible : Visibility.Collapsed;

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(nameof(StatusText)); }
        }

        public SolidColorBrush ColorBrush
        {
            get => _colorBrush;
            set { _colorBrush = value; OnPropertyChanged(nameof(ColorBrush)); }
        }

        public void RefreshColorBrush()
        {
            if (CurrentNote == null)
            {
                ColorBrush = new SolidColorBrush(Color.FromRgb(64, 64, 64));
                return;
            }

            try
            {
                var color = (Color)ColorConverter.ConvertFromString(CurrentNote.Color)!;
                ColorBrush = new SolidColorBrush(color);
            }
            catch
            {
                ColorBrush = new SolidColorBrush(Color.FromRgb(255, 255, 153));
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }

    public class StringToBrushConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is string colorStr && !string.IsNullOrEmpty(colorStr))
            {
                try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorStr)!); }
                catch { }
            }
            return new SolidColorBrush(Color.FromRgb(255, 255, 153));
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class BoolToVisibilityConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => value is Visibility v && v == Visibility.Visible;
    }
}