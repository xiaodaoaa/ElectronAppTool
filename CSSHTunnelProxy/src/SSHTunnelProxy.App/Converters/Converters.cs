using SSHTunnelProxy.Core.Models;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace SSHTunnelProxy.App.Converters;

/// <summary>隧道状态 → 状态文字。</summary>
public sealed class TunnelStateToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is TunnelState s ? s switch
        {
            TunnelState.Connected => "已连接",
            TunnelState.Connecting => "连接中",
            TunnelState.Reconnecting => "重连中",
            TunnelState.Error => "错误",
            _ => "未连接",
        } : "未知";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>隧道状态 → 前景色（绿/灰/红）。</summary>
public sealed class TunnelStateToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var brush = value is TunnelState s ? s switch
        {
            TunnelState.Connected => "SuccessBrush",
            TunnelState.Connecting or TunnelState.Reconnecting => "WarningBrush",
            TunnelState.Error => "DangerBrush",
            _ => "MutedForegroundBrush",
        } : "MutedForegroundBrush";

        return TryFindBrush(brush);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static Brush TryFindBrush(string key)
        => Application.Current.TryFindResource(key) as Brush ?? Brushes.Gray;
}

/// <summary>字节数 → 可读字符串（KB/MB/GB）。</summary>
public sealed class BytesToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not long bytes)
            return "0 B";

        var units = new[] { "B", "KB", "MB", "GB", "TB" };
        var size = (double)bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }
        return $"{size:0.##} {units[unit]}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>字节每秒 → 速率字符串。</summary>
public sealed class SpeedToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double speed)
        {
            var b = new BytesToTextConverter();
            return $"{b.Convert((long)speed, targetType, parameter, culture)}/s";
        }
        return "--";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
