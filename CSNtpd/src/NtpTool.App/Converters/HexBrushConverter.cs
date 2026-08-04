using System.Globalization;
using System.Windows.Data;

namespace NtpTool.App.Converters;

/// <summary>将十六进制颜色字符串（#RRGGBB）转换为 <see cref="System.Windows.Media.Brush"/>，用于 Win11 状态彩点。</summary>
public sealed class HexBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
                return new System.Windows.Media.SolidColorBrush(color);
            }
            catch
            {
                // 解析失败回退为灰色
            }
        }

        return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x9D, 0x9D, 0x9D));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}