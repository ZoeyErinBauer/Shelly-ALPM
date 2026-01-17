using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Shelly_UI.Converters;

public class PaneMinHeightConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isOpen)
        {
            return isOpen ? 100.0 : 0.0;
        }
        return 0.0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BottomPanelHeightConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool and true)
        {
            return new Avalonia.Controls.GridLength(10);
        }
        return new Avalonia.Controls.GridLength(150, Avalonia.Controls.GridUnitType.Pixel);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}