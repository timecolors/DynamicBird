using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ShoreHue.UI.Panels
{
    public partial class TaskbarView
    {
        private void SetScrollDirection(ScrollViewer scroller, LayoutMode mode)
        {
            if (mode == LayoutMode.Horizontal)
            {
                scroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
                scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            }
            else
            {
                scroller.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                scroller.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            }
            scroller.CanContentScroll = false;
        }

        private ScrollViewer CreateScrollViewer(string tag)
        {
            return new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                CanContentScroll = false,
                Background = System.Windows.Media.Brushes.Transparent,
                Padding = new Thickness(0),
                Tag = tag
            };
        }

        private ItemsControl CreateItemsControl(IEnumerable itemsSource, string templateKey)
        {
            var panelFactory = new FrameworkElementFactory(typeof(WrapPanel));
            panelFactory.SetValue(WrapPanel.OrientationProperty, Orientation.Horizontal);
            panelFactory.SetValue(WrapPanel.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
            panelFactory.SetValue(WrapPanel.VerticalAlignmentProperty, VerticalAlignment.Center);
            var panelTemplate = new ItemsPanelTemplate(panelFactory);

            return new ItemsControl
            {
                Background = System.Windows.Media.Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                ItemsSource = itemsSource,
                ItemTemplate = (DataTemplate)this.Resources[templateKey],
                ItemsPanel = panelTemplate
            };
        }

        private void AddHorizontalDivider()
        {
            var divider = new Border
            {
                Background = System.Windows.Media.Brushes.Gray,
                Height = DIVIDER_THICKNESS + 2,
                Width = double.NaN,
                Margin = new Thickness(2, 2, 2, 2),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Cursor = this.ActualWidth > DIVIDER_MIN_WIDTH ? Cursors.SizeNS : Cursors.Arrow
            };

            divider.PreviewMouseDown += Divider_PreviewMouseDown;
            divider.PreviewMouseMove += Divider_PreviewMouseMove;
            divider.PreviewMouseUp += Divider_PreviewMouseUp;

            Grid.SetRow(divider, _shortcutRows);
            Grid.SetColumn(divider, 0);
            Panel.SetZIndex(divider, 100);
            MainGrid.Children.Add(divider);

            _dividerElement = divider;
        }

        private void AddVerticalDivider()
        {
            var divider = new Border
            {
                Background = System.Windows.Media.Brushes.Gray,
                Width = DIVIDER_THICKNESS,
                Height = double.NaN,
                Margin = new Thickness(0, 2, 0, 2),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
                Cursor = this.ActualWidth > DIVIDER_MIN_WIDTH ? Cursors.SizeWE : Cursors.Arrow
            };

            divider.PreviewMouseDown += Divider_PreviewMouseDown;
            divider.PreviewMouseMove += Divider_PreviewMouseMove;
            divider.PreviewMouseUp += Divider_PreviewMouseUp;

            Grid.SetRow(divider, 0);
            Grid.SetColumn(divider, 1);
            Panel.SetZIndex(divider, 100);
            MainGrid.Children.Add(divider);

            _dividerElement = divider;
        }
    }
}