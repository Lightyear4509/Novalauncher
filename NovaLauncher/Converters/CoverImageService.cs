using System;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace NovaLauncher.Converters;

public sealed class CoverImageConverter : IValueConverter
{
    public object? Convert(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        if (value is not string imagePath ||
            string.IsNullOrWhiteSpace(imagePath) ||
            !File.Exists(imagePath))
        {
            return null;
        }

        try
        {
            return new Bitmap(imagePath);
        }
        catch
        {
            return null;
        }
    }

    public object? ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}