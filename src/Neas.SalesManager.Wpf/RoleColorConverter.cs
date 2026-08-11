// src/Neas.SalesManager.Wpf/RoleColorConverter.cs
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Neas.SalesManager.Wpf
{
    public class RoleColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isPrimary && isPrimary)
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2563EB")); // Primary Blue
            }
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));    // Secondary Slate
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}