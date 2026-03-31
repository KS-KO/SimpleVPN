using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SimpleVPNApp.Helpers;

/// <summary>
/// Boolean 값이 False일 때 Visible을 반환하는 컨버터입니다.
/// </summary>
public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
