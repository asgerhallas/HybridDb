using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HybridDb.Studio.Converters
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value as bool?) == true ? Visibility.Visible : Visibility.Hidden;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value as Visibility?) == Visibility.Visible;
        }
    }
}