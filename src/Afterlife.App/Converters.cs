using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Afterlife.Infrastructure;

namespace Afterlife.App;

public sealed class BoolToVisibility : IValueConverter
{
    public bool Invert { get; set; }
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var b = value is true;
        if (Invert) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is Visibility.Visible ^ Invert;
}

public sealed class InverseBool : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
}

public sealed class NullToVisibility : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null || (value is string s && s.Length == 0) ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class ToneToBrush : IValueConverter
{
    public bool Tint { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = (value as Tone? ?? Tone.Neutral) switch
        {
            Tone.Ok => "G",
            Tone.Info => "C",
            Tone.Warn => "O",
            Tone.Block => "M",
            Tone.Brand => "Y",
            _ => "T3",
        };
        var brush = (SolidColorBrush)Application.Current.Resources[key];
        if (!Tint) return brush;
        var tinted = new SolidColorBrush(brush.Color) { Opacity = 0.10 };
        tinted.Freeze();
        return tinted;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>P51: chapter headers of collections get a manage button.</summary>
public sealed class CollectionChapterToVisibility : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        (value is string s && s.StartsWith(ViewModels.MainViewModel.CollectionChapterPrefix, StringComparison.Ordinal)) != (parameter as string == "invert")
            ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

/// <summary>P68: chapter name → its collection header view model.</summary>
public sealed class ChapterLookupConverter : IMultiValueConverter
{
    public object? Convert(object[] values, Type targetType, object? parameter, CultureInfo culture) =>
        values.Length == 2 && values[0] is string name && values[1] is IReadOnlyDictionary<string, ViewModels.CollectionChapterVm> map
            && map.TryGetValue(name, out var vm) ? vm : null;
    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) => [];
}
