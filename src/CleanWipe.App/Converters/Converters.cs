using System.Globalization;
using System.Windows;
using System.Windows.Data;
using CleanWipe.Core.Models;

namespace CleanWipe.App.Converters;

/// <summary>bool → Visibility. Parameter "invert" invierte la lógica.</summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool b = value is bool v && v;
        if (parameter as string == "invert") b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility vis && vis == Visibility.Visible;
}

/// <summary>Invierte un booleano.</summary>
public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && !b;
}

/// <summary>string nulo/vacío → Collapsed; con contenido → Visible.</summary>
public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Cuenta > 0 → Visible.</summary>
public class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is int c && c > 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Compara value con parameter (string) y devuelve bool.</summary>
public class EqualityToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b ? parameter! : Binding.DoNothing;
}

/// <summary>
/// TraceType → glyph de Segoe MDL2 Assets. Los code points se construyen numéricamente
/// para evitar incrustar caracteres del área de uso privado en el código fuente.
/// </summary>
public class TraceTypeToIconConverter : IValueConverter
{
    private static string Glyph(int code) => char.ConvertFromUtf32(code);

    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is TraceType t ? t switch
        {
            TraceType.File => Glyph(0xE8A5),          // Document
            TraceType.Folder => Glyph(0xE8B7),        // Folder
            TraceType.RegistryKey => Glyph(0xE74C),   // Permissions (llave)
            TraceType.Shortcut => Glyph(0xE71B),      // Link
            TraceType.ScheduledTask => Glyph(0xE916), // Stopwatch
            TraceType.Service => Glyph(0xE713),       // Settings (engranaje)
            _ => Glyph(0xE7C3)
        } : Glyph(0xE7C3);

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>TraceType → etiqueta legible en español.</summary>
public class TraceTypeToLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
        => value is TraceType t ? t switch
        {
            TraceType.File => "Archivo",
            TraceType.Folder => "Carpeta",
            TraceType.RegistryKey => "Registro",
            TraceType.Shortcut => "Acceso directo",
            TraceType.ScheduledTask => "Tarea programada",
            TraceType.Service => "Servicio",
            _ => "Ítem"
        } : "Ítem";

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
