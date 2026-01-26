using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Shelly_UI.Converters;

public class EnumEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value?.Equals(parameter) == true;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => (bool)value! ? parameter : BindingOperations.DoNothing;
}