using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace KidWall.App.Converters;

public sealed class BooleanToOpacityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? 1d : 0d;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        BindingOperations.DoNothing;
}

public sealed class BooleanToTransformConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isOpen = value is true;
        return parameter?.ToString() switch
        {
            "preview" => isOpen ? new ScaleTransform(1, 1) : new ScaleTransform(0.92, 0.92),
            "drawer" => isOpen ? new TranslateTransform(0, 0) : new TranslateTransform(420, 0),
            "toast" => isOpen ? new TranslateTransform(0, 0) : new TranslateTransform(0, 80),
            "actions" => isOpen ? new TranslateTransform(0, 0) : new TranslateTransform(0, 8),
            _ => new TranslateTransform(),
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        BindingOperations.DoNothing;
}

public sealed class BooleanToBlurEffectConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? new BlurEffect { Radius = 5 } : null!;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        BindingOperations.DoNothing;
}
