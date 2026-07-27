using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace Keemya.Frontend.Views
{
    public static class ScrollViewerBehavior
    {
        public static readonly DependencyProperty AnimatedOffsetProperty =
            DependencyProperty.RegisterAttached("AnimatedOffset", typeof(double), typeof(ScrollViewerBehavior),
                new FrameworkPropertyMetadata(0.0, OnAnimatedOffsetChanged));

        public static double GetAnimatedOffset(DependencyObject obj) => (double)obj.GetValue(AnimatedOffsetProperty);
        public static void SetAnimatedOffset(DependencyObject obj, double value) => obj.SetValue(AnimatedOffsetProperty, value);

        private static void OnAnimatedOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ScrollViewer scrollViewer)
            {
                scrollViewer.ScrollToVerticalOffset((double)e.NewValue);
            }
        }
    }

    public partial class AuditLogsView : UserControl
    {
        private double _targetOffset = 0;

        public AuditLogsView()
        {
            InitializeComponent();
        }

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer)
            {
                e.Handled = true;

                double currentOffset = scrollViewer.VerticalOffset;
                
                // Sync target offset if it differs too much (e.g. from manual dragging or resizing)
                if (Math.Abs(_targetOffset - currentOffset) > 1.0)
                {
                    _targetOffset = currentOffset;
                }

                // Smoothly scroll
                double scrollSpeed = 1.0; // multiplier for scrolling speed
                _targetOffset -= e.Delta * scrollSpeed;
                _targetOffset = Math.Max(0, Math.Min(scrollViewer.ScrollableHeight, _targetOffset));

                var animation = new DoubleAnimation
                {
                    To = _targetOffset,
                    Duration = TimeSpan.FromMilliseconds(250),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };

                // Reset animation and set value to avoid jumps
                scrollViewer.BeginAnimation(ScrollViewerBehavior.AnimatedOffsetProperty, null);
                ScrollViewerBehavior.SetAnimatedOffset(scrollViewer, currentOffset);
                scrollViewer.BeginAnimation(ScrollViewerBehavior.AnimatedOffsetProperty, animation);
            }
        }
    }
}
