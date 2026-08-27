using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace Keemya.Frontend.Views
{
    public partial class ServiceManagementView : UserControl
    {
        public ServiceManagementView()
        {
            InitializeComponent();
        }
    }

    // bool → Green (true) / Grey (false/no data)
    public class BoolToGreenBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b)
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22C55E"));
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9CA3AF"));
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // bool → Green (OK) / Red (Fail)
    public class StatusOkFailColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b) return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16A34A"));
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    // [bool value, bool hasData] → Green / Red / Grey
    // ConverterParameter="invert" → flips the green/red meaning
    //   Normal:   true=GREEN (good/on),   false+hasData=RED (fail/off), false+noData=GREY
    //   Inverted: true=RED  (bad/active), false+hasData=GREEN (clear/ok), false+noData=GREY
    // Used for Intrusion (true=intrusion detected=RED is correct)
    public class StatusWithDataConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2)
                return new SolidColorBrush(Colors.Gray);

            bool isOn = values[0] is bool b && b;
            bool hasData = values[1] is bool d && d;
            bool invert = parameter is string s && s.Equals("invert", StringComparison.OrdinalIgnoreCase);

            // Grey when no data yet (before SI test)
            if (!hasData)
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9CA3AF"));

            bool showGreen = invert ? !isOn : isOn;
            if (showGreen)
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16A34A")); // Green = OK
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626")); // Red = Fail
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // Converts status hex string → SolidColorBrush
    public class StatusHexToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string hex)
            {
                try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
                catch { }
            }
            return new SolidColorBrush(Colors.Gray);
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    // Kept for backward compat
    public class StatusOnOffColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b) return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#16A34A"));
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DC2626"));
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}
