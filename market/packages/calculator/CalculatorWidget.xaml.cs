using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ShoreHue.UI.Localization;
using ShoreHue.UI.Widgets;

namespace ShoreHue.UI.Widgets.Calculator
{
    public partial class CalculatorWidget : UserControl, IWidget
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

        public CalculatorWidget()
        {
            InitializeComponent();
            Focusable = true;
            PreviewKeyDown += CalculatorWidget_PreviewKeyDown;
        }

        public new string Name => LocalizationManager.Instance["WidgetTabs_Calculator"];

        public UserControl CreateView() => this;

        public void OnActivated() { }

        public void OnDeactivated() { }

        public FrameworkElement GetFooterControl()
        {
            var status = new TextBlock
            {
                Text = ShoreHue.UI.Localization.LocalizationManager.Instance["Calculator_FooterHint"],
                FontSize = 10,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(102, 102, 102)),
                VerticalAlignment = VerticalAlignment.Center
            };
            return new StackPanel { Orientation = Orientation.Horizontal, Children = { status } };
        }

        // ================= 输入 =================

        private void Digit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string digit) InputDigit(digit);
        }

        private void Dot_Click(object sender, RoutedEventArgs e) => InputDot();

        private void Op_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string op) InputOperator(op);
        }

        private void Equals_Click(object sender, RoutedEventArgs e) => Calculate();

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            _left = 0; _right = 0; _pendingOp = "";
            _enteringNewNumber = true; _error = false;
            ExprText.Text = "";
            DisplayText.Text = "0";
        }

        private void Negate_Click(object sender, RoutedEventArgs e)
        {
            if (_error) return;
            if (double.TryParse(DisplayText.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
            {
                DisplayText.Text = Format(-v);
            }
        }

        private void Percent_Click(object sender, RoutedEventArgs e)
        {
            if (_error) return;
            if (double.TryParse(DisplayText.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
            {
                DisplayText.Text = Format(v / 100);
            }
        }

        // ================= 模式切换 =================

        private void ModeStd_Click(object sender, RoutedEventArgs e) => SetMode(CalcMode.Standard);
        private void ModeSci_Click(object sender, RoutedEventArgs e) => SetMode(CalcMode.Scientific);
        private void ModeProg_Click(object sender, RoutedEventArgs e) => SetMode(CalcMode.Programmer);

        private void SetMode(CalcMode mode)
        {
            _mode = mode;
            BtnStd.Style = (Style)FindResource(mode == CalcMode.Standard ? "AccentButton" : "FlatButton");
            BtnSci.Style = (Style)FindResource(mode == CalcMode.Scientific ? "AccentButton" : "FlatButton");
            BtnProg.Style = (Style)FindResource(mode == CalcMode.Programmer ? "AccentButton" : "FlatButton");
            SciPanel.Visibility = mode == CalcMode.Scientific ? Visibility.Visible : Visibility.Collapsed;
            ProgPanel.Visibility = mode == CalcMode.Programmer ? Visibility.Visible : Visibility.Collapsed;
            RadixText.Text = mode == CalcMode.Programmer ? RadixName(_radix) : "";
            UpdateDisplayRadix();
        }

        private void SciFn_Click(object sender, RoutedEventArgs e)
        {
            if (_error || sender is not Button btn || btn.Tag is not string fn) return;
            if (!double.TryParse(DisplayText.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
            {
                v = 0;
            }

            double ToRad(double d) => _useDegrees ? d * Math.PI / 180.0 : d;
            double FromRad(double r) => _useDegrees ? r * 180.0 / Math.PI : r;

            double result = fn switch
            {
                "sin" => Math.Sin(ToRad(v)),
                "cos" => Math.Cos(ToRad(v)),
                "tan" => Math.Tan(ToRad(v)),
                "asin" => FromRad(Math.Asin(v)),
                "acos" => FromRad(Math.Acos(v)),
                "atan" => FromRad(Math.Atan(v)),
                "sinh" => Math.Sinh(v),
                "cosh" => Math.Cosh(v),
                "tanh" => Math.Tanh(v),
                "sqrt" => v < 0 ? double.NaN : Math.Sqrt(v),
                "sqr" => v * v,
                "inv" => Math.Abs(v) < 1e-12 ? double.NaN : 1.0 / v,
                "pi" => Math.PI,
                "e" => Math.E,
                "exp" => Math.Exp(v),
                "pow10" => Math.Pow(10, v),
                "fact" => Factorial((long)Math.Round(v)),
                "ln" => v <= 0 ? double.NaN : Math.Log(v),
                "log" => v <= 0 ? double.NaN : Math.Log10(v),
                _ => v
            };

            if (double.IsNaN(result) || double.IsInfinity(result))
            {
                _error = true;
                DisplayText.Text = LocalizationManager.Instance["Calc_Error"];
                ExprText.Text = "";
                return;
            }

            DisplayText.Text = Format(result);
            _enteringNewNumber = true;
        }

        private static double Factorial(long n)
        {
            if (n < 0 || n > 170) return double.NaN;
            double r = 1;
            for (long i = 2; i <= n; i++) r *= i;
            return r;
        }

        private void DegRad_Click(object sender, RoutedEventArgs e)
        {
            _useDegrees = !_useDegrees;
            BtnDeg.Content = _useDegrees ? "DEG" : "RAD";
        }

        // ================= 程序员模式 =================

        private void Radix_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string s && int.TryParse(s, out int radix))
            {
                _radix = radix;
                RadixText.Text = RadixName(radix);
                UpdateDisplayRadix();
            }
        }

        private void BitOp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string op) InputOperator(op);
        }

        private void BitNot_Click(object sender, RoutedEventArgs e)
        {
            if (_error) return;
            if (double.TryParse(DisplayText.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
            {
                DisplayText.Text = Format(~(long)Math.Round(v));
                _enteringNewNumber = true;
            }
        }

        private void BitShift_Click(object sender, RoutedEventArgs e)
        {
            if (_error || sender is not Button btn || btn.Tag is not string dir) return;
            if (!double.TryParse(DisplayText.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)) return;
            long lv = (long)Math.Round(v);
            DisplayText.Text = Format(dir == "l" ? lv * 2 : lv / 2);
            _enteringNewNumber = true;
        }

        private static string RadixName(int radix) => radix switch
        {
            16 => "HEX",
            8 => "OCT",
            2 => "BIN",
            _ => "DEC"
        };

        private void UpdateDisplayRadix()
        {
            if (_mode == CalcMode.Programmer &&
                double.TryParse(DisplayText.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
            {
                DisplayText.Text = Format(v);
            }
        }

        private void Backspace_Click(object sender, RoutedEventArgs e)
        {
            if (_error || _enteringNewNumber) return;
            if (DisplayText.Text.Length > 1)
            {
                DisplayText.Text = DisplayText.Text[..^1];
            }
            else
            {
                DisplayText.Text = "0";
                _enteringNewNumber = true;
            }
        }

        private void InputDigit(string digit)
        {
            if (_error) return;
            if (_enteringNewNumber)
            {
                DisplayText.Text = digit;
                _enteringNewNumber = false;
            }
            else
            {
                if (DisplayText.Text == "0") DisplayText.Text = digit;
                else if (DisplayText.Text.Length < 16) DisplayText.Text += digit;
            }
        }

        private void InputDot()
        {
            if (_error) return;
            if (_enteringNewNumber)
            {
                DisplayText.Text = "0.";
                _enteringNewNumber = false;
            }
            else if (!DisplayText.Text.Contains('.'))
            {
                DisplayText.Text += ".";
            }
        }

        private void InputOperator(string op)
        {
            if (_error) return;

            if (!_enteringNewNumber && !string.IsNullOrEmpty(_pendingOp))
            {
                Calculate();
            }

            if (double.TryParse(DisplayText.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
            {
                _left = v;
            }
            _pendingOp = op;
            _enteringNewNumber = true;
            string displayOp = op switch { "pow" => "^", _ => op };
            ExprText.Text = $"{Format(_left)} {displayOp}";
        }

        private void Calculate()
        {
            if (string.IsNullOrEmpty(_pendingOp) || _error) return;
            if (!double.TryParse(DisplayText.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double right))
            {
                right = _right;
            }

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

            if (double.IsNaN(result) || double.IsInfinity(result))
            {
                _error = true;
                DisplayText.Text = LocalizationManager.Instance["Calc_Error"];
                ExprText.Text = "";
                _pendingOp = "";
                return;
            }

            string displayOp = _pendingOp switch { "pow" => "^", _ => _pendingOp };
            ExprText.Text = $"{Format(_left)} {displayOp} {Format(right)} =";
            _left = result;
            DisplayText.Text = Format(result);
            _pendingOp = "";
            _enteringNewNumber = true;
        }

        private string Format(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return LocalizationManager.Instance["Calc_Error"];
            bool isInteger = Math.Abs(v - Math.Round(v)) < 1e-12 && Math.Abs(v) < 9.2e18;
            return _mode == CalcMode.Programmer && isInteger
                ? FormatRadix((long)Math.Round(v))
                : isInteger && Math.Abs(v) < 1e15
                    ? v.ToString("0", CultureInfo.InvariantCulture)
                    : v.ToString("G12", CultureInfo.InvariantCulture);
        }

        private string FormatRadix(long v)
        {
            return _radix switch
            {
                16 => "0x" + Convert.ToString(v, 16).ToUpperInvariant(),
                8 => "0o" + Convert.ToString(v, 8),
                2 => "0b" + Convert.ToString(v, 2),
                _ => v.ToString(CultureInfo.InvariantCulture)
            };
        }

        // ================= 键盘输入 =================

        private void CalculatorWidget_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.D0:
                case Key.NumPad0:
                    InputDigit("0"); e.Handled = true; break;
                case Key.D1:
                case Key.NumPad1:
                    InputDigit("1"); e.Handled = true; break;
                case Key.D2:
                case Key.NumPad2:
                    InputDigit("2"); e.Handled = true; break;
                case Key.D3:
                case Key.NumPad3:
                    InputDigit("3"); e.Handled = true; break;
                case Key.D4:
                case Key.NumPad4:
                    InputDigit("4"); e.Handled = true; break;
                case Key.D5:
                    if (e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Shift))
                    {
                        Percent_Click(sender, e);
                        e.Handled = true;
                        break;
                    }
                    InputDigit("5"); e.Handled = true; break;
                case Key.NumPad5:
                    InputDigit("5"); e.Handled = true; break;
                case Key.D6:
                case Key.NumPad6:
                    InputDigit("6"); e.Handled = true; break;
                case Key.D7:
                case Key.NumPad7:
                    InputDigit("7"); e.Handled = true; break;
                case Key.D8:
                case Key.NumPad8:
                    InputDigit("8"); e.Handled = true; break;
                case Key.D9:
                case Key.NumPad9:
                    InputDigit("9"); e.Handled = true; break;
                case Key.Decimal:
                case Key.OemPeriod:
                    InputDot(); e.Handled = true; break;
                case Key.Add:
                    InputOperator("+"); e.Handled = true; break;
                case Key.OemPlus:
                    // 主键盘 +/-= 键：无 Shift 是 "="，Shift 是 "+"
                    if (e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Shift))
                    {
                        InputOperator("+");
                    }
                    else
                    {
                        Calculate();
                    }
                    e.Handled = true; break;
                case Key.Subtract:
                case Key.OemMinus:
                    InputOperator("-"); e.Handled = true; break;
                case Key.Multiply:
                    InputOperator("*"); e.Handled = true; break;
                case Key.Divide:
                case Key.OemQuestion:
                    InputOperator("/"); e.Handled = true; break;
                case Key.Enter:
                    Calculate(); e.Handled = true; break;
                case Key.Back:
                    Backspace_Click(sender, e); e.Handled = true; break;
                case Key.Escape:
                    Clear_Click(sender, e); e.Handled = true; break;
            }
        }
    }
}
