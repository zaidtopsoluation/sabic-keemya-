using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Keemya.Frontend.Views
{
    public partial class CommandCenterView : UserControl
    {
        public CommandCenterView() => InitializeComponent();
    }

    public class LogColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string text)
            {
                if (text.Contains("Successfully sent") || text.Contains("ACK response received") || text.Contains("Successfully executed"))
                {
                    return new SolidColorBrush(Color.FromRgb(22, 163, 74)); // #16A34A (Green)
                }
                else if (text.Contains("Timeout") || text.Contains("failed") || text.Contains("Failover aborted") || text.Contains("Offline") || text.Contains("Error") || text.Contains("FAILED"))
                {
                    return new SolidColorBrush(Color.FromRgb(220, 38, 38)); // #DC2626 (Red)
                }
                else if (text.Contains("Redundant Transmit") || text.Contains("retry") || text.Contains("Waiting") || text.Contains("Retry") || text.Contains("transmitting"))
                {
                    return new SolidColorBrush(Color.FromRgb(217, 119, 6)); // #D97706 (Orange/Yellow)
                }
            }
            return new SolidColorBrush(Color.FromRgb(71, 85, 105)); // #475569 (Dark Slate Info)
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
