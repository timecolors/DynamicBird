using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DynamicBird.UI.Onboarding
{
    public partial class OnboardingWindow : Window
    {
        private readonly Action? _onCompleted;
        private int _currentPage;
        private const int PageCount = 6;

        private readonly StackPanel[] _pages;
        private readonly Border[] _dots;
        private readonly TextBlock[] _nums;
        private readonly TextBlock[] _labels;

        public OnboardingWindow(Action? onCompleted = null)
        {
            InitializeComponent();
            _onCompleted = onCompleted;
            // 无论点完成/跳过/直接关闭，都视为完成引导
            Closed += (_, _) =>
            {
                try { _onCompleted?.Invoke(); } catch { }
            };

            _pages = new[] { Page1, Page2, Page3, Page4, Page5, Page6 };
            _dots = new[] { Step1Dot, Step2Dot, Step3Dot, Step4Dot, Step5Dot, Step6Dot };
            _nums = new[] { Step1Num, Step2Num, Step3Num, Step4Num, Step5Num, Step6Num };
            _labels = new[] { Step1Label, Step2Label, Step3Label, Step4Label, Step5Label, Step6Label };

            UpdateNavigation();
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < PageCount - 1)
            {
                _currentPage++;
                UpdateNavigation();
            }
            else
            {
                Finish();
            }
        }

        private void Prev_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 0)
            {
                _currentPage--;
                UpdateNavigation();
            }
        }

        private void Skip_Click(object sender, RoutedEventArgs e) => Finish();

        private void Finish()
        {
            Close();
        }

        private void UpdateNavigation()
        {
            for (int i = 0; i < PageCount; i++)
            {
                _pages[i].Visibility = i == _currentPage ? Visibility.Visible : Visibility.Collapsed;
                bool active = i == _currentPage;
                bool done = i < _currentPage;

                var brush = active
                    ? new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4))
                    : done
                        ? new SolidColorBrush(Color.FromRgb(0x9A, 0xC8, 0xEB))
                        : new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
                _dots[i].Background = brush;
                _nums[i].Foreground = active || done
                    ? new SolidColorBrush(Colors.White)
                    : new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66));
                _labels[i].Foreground = active
                    ? new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E))
                    : new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
            }

            BtnPrev.Visibility = _currentPage > 0 ? Visibility.Visible : Visibility.Collapsed;
            BtnNext.Content = _currentPage == PageCount - 1 ? "开始使用" : "下一步";
        }
    }
}
