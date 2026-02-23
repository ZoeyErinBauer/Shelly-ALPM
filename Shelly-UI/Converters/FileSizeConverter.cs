using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Shelly_UI.Converters;

public class FileSizeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not long bytes) return "0 B";

        string[] suffixes = ["B", "KB", "MB", "GB"];
        var index = 0;
        double size = bytes;

        while (size >= 1024 && index < suffixes.Length - 1)
        {
            size /= 1024;
            index++;
        }

        return index == 0
            ? $"{size:0} {suffixes[index]}"
            : $"{size:0.##} {suffixes[index]}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}