using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ShoreHue.UI.Widgets;

namespace ShoreHue.Builtin
{
    // 计算器 · 纯代码版（动态编译运行，与内置风格一致：AccentButton/FlatButton/CardStyle）
    public class CalcPanel : UserControl, IWidget
    {
        private double _left;
        private double _right;
        private string _pendingOp = "";
        private bool _enteringNewNumber = true;
        private bool _error;

        private enum CalcMode { Standard, Scientific, Programmer }
        private CalcMode _mode = CalcMode.Standard;
        private int _radix = 10;
        private bool _useDegrees = true;

        private TextBlock _exprText;
        private TextBlock _displayText;
        private TextBlock _radixText;
        private Button _btnStd, _btnSci, _btnProg, _btnDeg;
        private WrapPanel _sciPanel;
        private StackPanel _progPanel;

        public CalcPanel()
        {
            BuildUi();
            Focusable = true;
            PreviewKeyDown += OnPreviewKeyDown;
            SetMode(CalcMode.Standard);
        }

        public string Name => "计算器";
        public UserControl CreateView() => this;
        public void OnActivated() { }
        public void OnDeactivated() { }

        private Style AccentBtn => (Style)FindResource("AccentButton");
        private Style FlatBtn => (Style)FindResource("FlatButton");

        private void BuildUi()
        {
            // 模式切换
            _btnStd = MakeButton("标准", FlatBtn, 24, 8); _btnStd.Click += (_, _) => SetMode(CalcMode.Standard);
            _btnSci = MakeButton("科学", FlatBtn, 24, 8); _btnSci.Margin = new Thickness(6, 0, 0, 0); _btnSci.Click += (_, _) => SetMode(CalcMode.Scientific);
            _btnProg = MakeButton("程序员", FlatBtn, 24, 8); _btnProg.Margin = new Thickness(6, 0, 0, 0); _btnProg.Click += (_, _) => SetMode(CalcMode.Programmer);
            _radixText = new TextBlock { FontSize = 10, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(43, 136, 216)), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
            var modeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            modeRow.Children.Add(_btnStd); modeRow.Children.Add(_btnSci); modeRow.Children.Add(_btnProg); modeRow.Children.Add(_radixText);

            // 显示区
            _exprText = new TextBlock { FontSize = 11, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(138, 138, 138)), TextAlignment = TextAlignment.Right, TextTrimming = TextTrimming.CharacterEllipsis };
            _displayText = new TextBlock { Text = "0", FontSize = 26, FontWeight = FontWeights.Light, Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(238, 238, 238)), TextAlignment = TextAlignment.Right, Margin = new Thickness(0, 4, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };
            var displayCol = new StackPanel { Children = { _exprText, _displayText } };
            var displayBorder = new Border { Style = (Style)FindResource("CardStyle"), Margin = new Thickness(0, 0, 0, 6), Child = displayCol };

            // 科学函数行
            _sciPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 6), Visibility = Visibility.Collapsed };
            foreach (var (label, tag, handler) in new (string, string, Action<string>)[]
            {
                ("sin","sin",SciFn), ("cos","cos",SciFn), ("tan","tan",SciFn),
                ("asin","asin",SciFn), ("acos","acos",SciFn), ("atan","atan",SciFn),
                ("sinh","sinh",SciFn), ("cosh","cosh",SciFn), ("tanh","tanh",SciFn),
                ("√","sqrt",SciFn), ("x²","sqr",SciFn), ("xʸ","pow",Op),
                ("1/x","inv",SciFn), ("n!","fact",SciFn), ("π","pi",SciFn), ("e","e",SciFn),
                ("eˣ","exp",SciFn), ("10ˣ","pow10",SciFn), ("ln","ln",SciFn), ("log","log",SciFn)
            })
            {
                var b = new Button { Content = label, Style = FlatBtn, FontSize = 11, Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(0, 0, 3, 0), Tag = tag };
                b.Click += (s, _) => { var bt = (Button)s; handler((string)bt.Tag); };
                _sciPanel.Children.Add(b);
            }
            _btnDeg = new Button { Content = "DEG", Style = FlatBtn, FontSize = 10, Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(3, 0, 0, 0), Tag = "deg" };
            _btnDeg.Click += (_, _) => { _useDegrees = !_useDegrees; _btnDeg.Content = _useDegrees ? "DEG" : "RAD"; };
            _sciPanel.Children.Add(_btnDeg);

            // 程序员行
            _progPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 6), Visibility = Visibility.Collapsed };
            var radixRow = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var (label, r) in new (string, string)[] { ("HEX","16"), ("DEC","10"), ("OCT","8"), ("BIN","2") })
            {
                var b = new Button { Content = label, Style = FlatBtn, FontSize = 10, Padding = new Thickness(5, 1, 5, 1), Margin = new Thickness(0, 0, 3, 0), Tag = r };
                b.Click += (s, _) => { var bt = (Button)s; if (int.TryParse((string)bt.Tag, out int rr)) { _radix = rr; _radixText.Text = RadixName(rr); UpdateDisplayRadix(); } };
                radixRow.Children.Add(b);
            }
            var bitRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 0) };
            foreach (var (label, tag) in new (string, string)[] { ("AND","&"), ("OR","|"), ("XOR","^") })
            {
                var b = new Button { Content = label, Style = FlatBtn, FontSize = 10, Padding = new Thickness(5, 1, 5, 1), Margin = new Thickness(0, 0, 3, 0), Tag = tag };
                b.Click += (s, _) => { var bt = (Button)s; InputOperator((string)bt.Tag); };
                bitRow.Children.Add(b);
            }
            var btnNot = new Button { Content = "NOT", Style = FlatBtn, FontSize = 10, Padding = new Thickness(5, 1, 5, 1), Margin = new Thickness(0, 0, 3, 0) };
            btnNot.Click += (_, _) => BitNot();
            var btnShl = new Button { Content = "<<", Style = FlatBtn, FontSize = 10, Padding = new Thickness(5, 1, 5, 1), Margin = new Thickness(0, 0, 3, 0), Tag = "l" };
            btnShl.Click += (s, _) => { var bt = (Button)s; BitShift((string)bt.Tag); };
            var btnShr = new Button { Content = ">>", Style = FlatBtn, FontSize = 10, Padding = new Thickness(5, 1, 5, 1), Tag = "r" };
            btnShr.Click += (s, _) => { var bt = (Button)s; BitShift((string)bt.Tag); };
            bitRow.Children.Add(btnNot); bitRow.Children.Add(btnShl); bitRow.Children.Add(btnShr);
            _progPanel.Children.Add(radixRow); _progPanel.Children.Add(bitRow);

            // 键盘 4x5
            var keys = new Grid { Margin = new Thickness(0) };
            for (int i = 0; i < 5; i++) keys.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            for (int i = 0; i < 4; i++) keys.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            void Put(int r, int c, string content, Action handler, bool accent = false, double fontSize = 15, string? opTag = null)
            {
                var b = new Button { Content = content, Style = accent ? AccentBtn : FlatBtn, FontSize = fontSize, Margin = new Thickness(1), Foreground = opTag != null ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(43, 136, 216)) : null };
                b.Click += (_, _) => handler();
                Grid.SetRow(b, r); Grid.SetColumn(b, c);
                keys.Children.Add(b);
            }
            Put(0,0,"C", Clear); Put(0,1,"±", Negate); Put(0,2,"%", Percent);
            Put(0,3,"÷", () => InputOperator("/"), opTag:"/");
            Put(1,0,"7", () => InputDigit("7"), fontSize:16); Put(1,1,"8", () => InputDigit("8"), fontSize:16); Put(1,2,"9", () => InputDigit("9"), fontSize:16);
            Put(1,3,"×", () => InputOperator("*"), opTag:"*");
            Put(2,0,"4", () => InputDigit("4"), fontSize:16); Put(2,1,"5", () => InputDigit("5"), fontSize:16); Put(2,2,"6", () => InputDigit("6"), fontSize:16);
            Put(2,3,"−", () => InputOperator("-"), opTag:"-");
            Put(3,0,"1", () => InputDigit("1"), fontSize:16); Put(3,1,"2", () => InputDigit("2"), fontSize:16); Put(3,2,"3", () => InputDigit("3"), fontSize:16);
            Put(3,3,"+", () => InputOperator("+"), opTag:"+");
            Put(4,0,"0", () => InputDigit("0"), fontSize:16); Put(4,1,".", InputDot, fontSize:16); Put(4,2,"⌫", Backspace, fontSize:14);
            Put(4,3,"=", Calculate, accent:true, fontSize:16);

            var root = new Grid { Margin = new Thickness(2) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(modeRow, 0); Grid.SetRow(displayBorder, 1); Grid.SetRow(_sciPanel, 2); Grid.SetRow(_progPanel, 2); Grid.SetRow(keys, 3);
            root.Children.Add(modeRow); root.Children.Add(displayBorder); root.Children.Add(_sciPanel); root.Children.Add(_progPanel); root.Children.Add(keys);
            Content = root;
        }

        private Button MakeButton(string content, Style style, double height, double pad)
        {
            return new Button { Content = content, Style = style, Height = height, Padding = new Thickness(pad, 0, pad, 0) };
        }

        private void SetMode(CalcMode mode)
        {
            _mode = mode;
            _btnStd.Style = mode == CalcMode.Standard ? AccentBtn : FlatBtn;
            _btnSci.Style = mode == CalcMode.Scientific ? AccentBtn : FlatBtn;
            _btnProg.Style = mode == CalcMode.Programmer ? AccentBtn : FlatBtn;
            _sciPanel.Visibility = mode == CalcMode.Scientific ? Visibility.Visible : Visibility.Collapsed;
            _progPanel.Visibility = mode == CalcMode.Programmer ? Visibility.Visible : Visibility.Collapsed;
            _radixText.Text = mode == CalcMode.Programmer ? RadixName(_radix) : "";
        }

        // ============ 逻辑（与内置一致） ============
        private void InputDigit(string digit)
        {
            if (_error) return;
            if (_enteringNewNumber) { _displayText.Text = digit; _enteringNewNumber = false; }
            else if (_displayText.Text == "0") _displayText.Text = digit;
            else if (_displayText.Text.Length < 16) _displayText.Text += digit;
        }
        private void InputDot()
        {
            if (_error) return;
            if (_enteringNewNumber) { _displayText.Text = "0."; _enteringNewNumber = false; }
            else if (!_displayText.Text.Contains('.')) _displayText.Text += ".";
        }
        private void InputOperator(string op)
        {
            if (_error) return;
            if (!_enteringNewNumber && !string.IsNullOrEmpty(_pendingOp)) Calculate();
            if (double.TryParse(_displayText.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)) _left = v;
            _pendingOp = op; _enteringNewNumber = true;
            string displayOp = op == "pow" ? "^" : op;
            _exprText.Text = Format(_left) + " " + displayOp;
        }
        private void Calculate()
        {
            if (string.IsNullOrEmpty(_pendingOp) || _error) return;
            if (!double.TryParse(_displayText.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double right)) right = _right;
            _right = right;
            double result = _pendingOp switch
            {
                "+" => _left + right,
                "-" => _left - right,
                "*" => _left * right,
                "/" => Math.Abs(right) < 1e-12 ? double.NaN : _left / right,
                "&" => (double)((long)Math.Round(_left) & (long)Math.Round(right)),
                "|" => (double)((long)Math.Round(_left) | (long)Math.Round(right)),
                "^" => (double)((long)Math.Round(_left) ^ (long)Math.Round(right)),
                "pow" => Math.Pow(_left, right),
                _ => right
            };
            if (double.IsNaN(result) || double.IsInfinity(result)) { _error = true; _displayText.Text = "错误"; _exprText.Text = ""; _pendingOp = ""; return; }
            string displayOp = _pendingOp == "pow" ? "^" : _pendingOp;
            _exprText.Text = Format(_left) + " " + displayOp + " " + Format(right) + " =";
            _left = result; _displayText.Text = Format(result); _pendingOp = ""; _enteringNewNumber = true;
        }
        private void Clear() { _left = 0; _right = 0; _pendingOp = ""; _enteringNewNumber = true; _error = false; _exprText.Text = ""; _displayText.Text = "0"; }
        private void Negate() { if (_error) return; if (double.TryParse(_displayText.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)) _displayText.Text = Format(-v); }
        private void Percent() { if (_error) return; if (double.TryParse(_displayText.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)) _displayText.Text = Format(v / 100); }
        private void Backspace() { if (_error || _enteringNewNumber) return; if (_displayText.Text.Length > 1) _displayText.Text = _displayText.Text[..^1]; else { _displayText.Text = "0"; _enteringNewNumber = true; } }
        private void SciFn(string fn)
        {
            if (_error) return;
            if (!double.TryParse(_displayText.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)) v = 0;
            double ToRad(double d) => _useDegrees ? d * Math.PI / 180.0 : d;
            double FromRad(double r) => _useDegrees ? r * 180.0 / Math.PI : r;
            double result = fn switch
            {
                "sin" => Math.Sin(ToRad(v)), "cos" => Math.Cos(ToRad(v)), "tan" => Math.Tan(ToRad(v)),
                "asin" => FromRad(Math.Asin(v)), "acos" => FromRad(Math.Acos(v)), "atan" => FromRad(Math.Atan(v)),
                "sinh" => Math.Sinh(v), "cosh" => Math.Cosh(v), "tanh" => Math.Tanh(v),
                "sqrt" => v < 0 ? double.NaN : Math.Sqrt(v), "sqr" => v * v,
                "inv" => Math.Abs(v) < 1e-12 ? double.NaN : 1.0 / v,
                "pi" => Math.PI, "e" => Math.E, "exp" => Math.Exp(v), "pow10" => Math.Pow(10, v),
                "fact" => Factorial((long)Math.Round(v)),
                "ln" => v <= 0 ? double.NaN : Math.Log(v), "log" => v <= 0 ? double.NaN : Math.Log10(v),
                _ => v
            };
            if (double.IsNaN(result) || double.IsInfinity(result)) { _error = true; _displayText.Text = "错误"; _exprText.Text = ""; return; }
            _displayText.Text = Format(result); _enteringNewNumber = true;
        }
        private static double Factorial(long n) { if (n < 0 || n > 170) return double.NaN; double r = 1; for (long i = 2; i <= n; i++) r *= i; return r; }
        private void BitNot() { if (_error) return; if (double.TryParse(_displayText.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)) { _displayText.Text = Format(~(long)Math.Round(v)); _enteringNewNumber = true; } }
        private void BitShift(string dir) { if (_error) return; if (!double.TryParse(_displayText.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)) return; long lv = (long)Math.Round(v); _displayText.Text = Format(dir == "l" ? lv * 2 : lv / 2); _enteringNewNumber = true; }
        private void Op(string op) => InputOperator(op);
        private static string RadixName(int radix) => radix switch { 16 => "HEX", 8 => "OCT", 2 => "BIN", _ => "DEC" };
        private void UpdateDisplayRadix() { if (_mode == CalcMode.Programmer && double.TryParse(_displayText.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)) _displayText.Text = Format(v); }
        private string Format(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "错误";
            bool isInteger = Math.Abs(v - Math.Round(v)) < 1e-12 && Math.Abs(v) < 9.2e18;
            return _mode == CalcMode.Programmer && isInteger ? FormatRadix((long)Math.Round(v))
                : isInteger && Math.Abs(v) < 1e15 ? v.ToString("0", CultureInfo.InvariantCulture)
                : v.ToString("G12", CultureInfo.InvariantCulture);
        }
        private string FormatRadix(long v) => _radix switch
        {
            16 => "0x" + Convert.ToString(v, 16).ToUpperInvariant(),
            8 => "0o" + Convert.ToString(v, 8),
            2 => "0b" + Convert.ToString(v, 2),
            _ => v.ToString(CultureInfo.InvariantCulture)
        };

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            void D(string d) { InputDigit(d); e.Handled = true; }
            switch (e.Key)
            {
                case Key.D0: case Key.NumPad0: D("0"); break;
                case Key.D1: case Key.NumPad1: D("1"); break;
                case Key.D2: case Key.NumPad2: D("2"); break;
                case Key.D3: case Key.NumPad3: D("3"); break;
                case Key.D4: case Key.NumPad4: D("4"); break;
                case Key.D5: if (e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Shift)) { Percent(); e.Handled = true; break; } D("5"); break;
                case Key.NumPad5: D("5"); break;
                case Key.D6: case Key.NumPad6: D("6"); break;
                case Key.D7: case Key.NumPad7: D("7"); break;
                case Key.D8: case Key.NumPad8: D("8"); break;
                case Key.D9: case Key.NumPad9: D("9"); break;
                case Key.Decimal: case Key.OemPeriod: InputDot(); e.Handled = true; break;
                case Key.Add: InputOperator("+"); e.Handled = true; break;
                case Key.OemPlus: if (e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Shift)) InputOperator("+"); else Calculate(); e.Handled = true; break;
                case Key.Subtract: case Key.OemMinus: InputOperator("-"); e.Handled = true; break;
                case Key.Multiply: InputOperator("*"); e.Handled = true; break;
                case Key.Divide: case Key.OemQuestion: InputOperator("/"); e.Handled = true; break;
                case Key.Enter: Calculate(); e.Handled = true; break;
                case Key.Back: Backspace(); e.Handled = true; break;
                case Key.Escape: Clear(); e.Handled = true; break;
            }
        }
    }
}