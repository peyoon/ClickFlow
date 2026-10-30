using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ClickFlow.Core.Models;

namespace ClickFlow.App.Converters;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is true;
        if (Invert) flag = !flag;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is Visibility.Visible ^ Invert;
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hasValue = value is not null && value is not string { Length: 0 };
        if (Invert) hasValue = !hasValue;
        return hasValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class ZeroToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isZero = value is 0 or 0L or 0.0 or null;
        if (Invert) isZero = !isZero;
        return isZero ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class EnumEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null && parameter is string name && string.Equals(value.ToString(), name, StringComparison.Ordinal);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true && parameter is string name ? Enum.Parse(targetType, name) : Binding.DoNothing;
}

public sealed class EnumEqualsToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is not string names) return Visibility.Collapsed;
        var current = value.ToString();
        foreach (var name in names.Split('|'))
        {
            if (string.Equals(current, name, StringComparison.Ordinal)) return Visibility.Visible;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class ExecutionStateToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            ExecutionState.Running => "Brush.Success",
            ExecutionState.Waiting or ExecutionState.Preparing => "Brush.Accent",
            ExecutionState.Paused => "Brush.Warning",
            ExecutionState.Error => "Brush.Danger",
            ExecutionState.Stopped => "Brush.Warning",
            ExecutionState.Completed => "Brush.Success",
            _ => "Brush.TextMuted",
        };
        return Application.Current.TryFindResource(key) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class ExecutionStateToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        ExecutionState.Idle => "대기 중",
        ExecutionState.Preparing => "준비 중",
        ExecutionState.Waiting => "예약 대기",
        ExecutionState.Running => "실행 중",
        ExecutionState.Paused => "일시정지",
        ExecutionState.Completed => "완료",
        ExecutionState.Stopped => "중지됨",
        ExecutionState.Error => "오류",
        _ => string.Empty,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class LogLevelToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            LogLevel.Error => "Brush.Danger",
            LogLevel.Warning => "Brush.Warning",
            LogLevel.Debug => "Brush.TextMuted",
            _ => "Brush.Text",
        };
        return Application.Current.TryFindResource(key) as Brush ?? Brushes.White;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class TimeSpanToCountdownConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not TimeSpan ts) return "--:--:--.---";
        if (ts < TimeSpan.Zero) ts = TimeSpan.Zero;
        return ts.TotalHours >= 24
            ? $"{(int)ts.TotalDays}일 {ts:hh\\:mm\\:ss}"
            : ts.ToString(@"hh\:mm\:ss\.fff");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class IndexPlusOneConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int i ? (i + 1).ToString(culture) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class DelayTextConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        var before = values.Length > 0 && values[0] is int b ? b : 0;
        var after = values.Length > 1 && values[1] is int a ? a : 0;
        if (before == 0 && after == 0) return "—";
        if (before == 0) return $"후 {after} ms";
        if (after == 0) return $"전 {before} ms";
        return $"전 {before} / 후 {after} ms";
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
