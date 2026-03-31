using System;
using System.Globalization;
using System.Windows.Data;

namespace SimpleVPNApp.Helpers;

/// <summary>
/// 바이트 단위 속도를 읽기 쉬운 Mbps 단위로 변환해주는 컨버터입니다.
/// </summary>
public class SpeedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is long speedBytes)
        {
            double mbps = speedBytes / 1000000.0;
            return $"{mbps:F1} Mbps";
        }
        return "0.0 Mbps";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
