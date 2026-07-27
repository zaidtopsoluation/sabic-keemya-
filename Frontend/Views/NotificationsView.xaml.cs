using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Data;

namespace Keemya.Frontend.Views
{
    public partial class NotificationsView : UserControl
    {
        private double _targetOffset = 0;

        public NotificationsView()
        {
            InitializeComponent();
        }

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer)
            {
                e.Handled = true;

                double currentOffset = scrollViewer.VerticalOffset;
                
                if (Math.Abs(_targetOffset - currentOffset) > 1.0)
                {
                    _targetOffset = currentOffset;
                }

                double scrollSpeed = 1.0;
                _targetOffset -= e.Delta * scrollSpeed;
                _targetOffset = Math.Max(0, Math.Min(scrollViewer.ScrollableHeight, _targetOffset));

                var animation = new DoubleAnimation
                {
                    To = _targetOffset,
                    Duration = TimeSpan.FromMilliseconds(250),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };

                scrollViewer.BeginAnimation(ScrollViewerBehavior.AnimatedOffsetProperty, null);
                ScrollViewerBehavior.SetAnimatedOffset(scrollViewer, currentOffset);
                scrollViewer.BeginAnimation(ScrollViewerBehavior.AnimatedOffsetProperty, animation);
            }
        }
    }

    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool b)
            {
                return b ? Visibility.Collapsed : Visibility.Visible;
            }
            if (value is int count)
            {
                return count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ColorToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is string colorStr)
            {
                try
                {
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorStr));
                }
                catch
                {
                    return Brushes.Transparent;
                }
            }
            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

