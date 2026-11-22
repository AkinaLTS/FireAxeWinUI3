// Ported from FireAxe_Alt\FireAxe.GUI\ValueConverters\ (ReadableBytesConverter / EnumDescriptionConverter)
// and FireAxe_Alt\FireAxe.GUI\ValueConverters\AddonNodeConverters.cs (IImageSource -> ImageSource, P1 introduced
// IImageSource so the ViewModel layer stays UI-framework free).
//
// These are only used where a {x:Bind} function binding would be awkward (list-item templates, where the
// value type varies per element). Everything else goes through the static helpers in XBind.cs.
using System;
using System.Globalization;
using FireAxe.Resources;
using FireAxe.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;

namespace FireAxe.Views;

/// <summary>Unwraps the ViewModel layer's opaque <see cref="IImageSource"/> for <c>Image.Source</c>.</summary>
public sealed class ImageSourceConverter : IValueConverter
{
    public static ImageSourceConverter Instance { get; } = new();

    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        return ImageLoader.ToImageSource(value as IImageSource);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Formats a byte count like the Avalonia <c>ReadableBytesConverter</c> (1 decimal digit + unit, 1024 based).
/// </summary>
public sealed class ReadableBytesConverter : IValueConverter
{
    public static ReadableBytesConverter Instance { get; } = new();

    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is not IConvertible convertible)
        {
            return null;
        }

        double bytes;
        try
        {
            bytes = convertible.ToDouble(CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            return null;
        }

        Utils.GetReadableBytes(bytes, out double readable, out string unit);
        return readable.ToString("F1") + unit;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Localizes an enum value through the <c>Enum_{TypeName}_{ValueName}</c> resources
/// (same keys the Avalonia version used, so the resx did not change).
/// </summary>
public sealed class EnumDescriptionConverter : IValueConverter
{
    public static EnumDescriptionConverter Instance { get; } = new();

    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is null)
        {
            return null;
        }

        var type = value.GetType();
        if (!type.IsEnum)
        {
            return null;
        }

        string className = type.Name;
        foreach (string name in Enum.GetNames(type))
        {
            if (!Enum.Parse(type, name).Equals(value))
            {
                continue;
            }

            string? text = Texts.ResourceManager.GetString($"Enum_{className}_{name}");
            if (text is not null)
            {
                return text;
            }
        }

        return $"[missing text: Enum_{className}_{value}]";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

/// <summary>Visible only for <c>true</c>. Used where a function binding is not possible.</summary>
public sealed class VisibleWhenTrueConverter : IValueConverter
{
    public static VisibleWhenTrueConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is true ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Visible only for <c>false</c>; a null value is treated as false, i.e. visible.
/// Replaces Avalonia's <c>{Binding !SomeBool}</c> negation syntax.
/// </summary>
public sealed class VisibleWhenFalseConverter : IValueConverter
{
    public static VisibleWhenFalseConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is true ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Visible when a count is greater than zero; a null or unparsable value counts as empty.
/// Replaces Avalonia's <c>CollectionConverters.IsNotEmpty</c>.
/// </summary>
public sealed class NotEmptyToVisibilityConverter : IValueConverter
{
    public static NotEmptyToVisibilityConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool isNotEmpty = false;
        try
        {
            isNotEmpty = value is IConvertible convertible && convertible.ToInt32(CultureInfo.InvariantCulture) > 0;
        }
        catch (Exception)
        {
            isNotEmpty = false;
        }

        return isNotEmpty ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Renders an addon id as <c>(ID: xxxxxxxx-...)</c>.
/// Replaces the Avalonia <c>StringFormat='(ID: {0})'</c> binding, which WinUI 3 does not support.
/// </summary>
public sealed class IdInParenthesesConverter : IValueConverter
{
    public static IdInParenthesesConverter Instance { get; } = new();

    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        return value is null ? null : $"(ID: {value})";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Visible when the value is <c>null</c>; pass <c>Parameter="notnull"</c> to invert.
/// Replaces Avalonia's <c>ObjectConverters.IsNull/IsNotNull</c> (ObjectConverters does not exist in WinUI).
/// </summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public static NullToVisibilityConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool isNotNull = value is not null;
        bool invert = string.Equals(parameter as string, "notnull", StringComparison.OrdinalIgnoreCase);
        return isNotNull == invert ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Visible while an <see cref="System.Collections.IEnumerable"/> has no items (null counts as empty).
/// Replaces Avalonia's <c>CollectionConverters.IsEnumerableEmpty</c> -- used by the "no tags yet" hint.
/// </summary>
public sealed class EmptyEnumerableToVisibilityConverter : IValueConverter
{
    public static EmptyEnumerableToVisibilityConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool isEmpty = true;

        if (value is System.Collections.IEnumerable enumerable)
        {
            var enumerator = enumerable.GetEnumerator();
            try
            {
                isEmpty = !enumerator.MoveNext();
            }
            finally
            {
                (enumerator as IDisposable)?.Dispose();
            }
        }

        return isEmpty ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Visible while a count is zero (null counts as zero).
/// Bind this to <c>CurrentNodes.Count</c> rather than to the collection itself: the search
/// pipeline fills one long-lived collection asynchronously, so a converter bound to the
/// collection instance would only ever be re-evaluated when the instance is replaced and the
/// "empty" hint would stay visible next to the results.
/// </summary>
public sealed class ZeroToVisibilityConverter : IValueConverter
{
    public static ZeroToVisibilityConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool isEmpty = value switch
        {
            null => true,
            int count => count <= 0,
            _ => false,
        };

        return isEmpty ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Turns any object into the explanation text the Alt <c>ObjectExplanationConverter</c> produced,
/// through <see cref="ObjectExplanationManager.Default"/> (same manager, same fallbacks).
/// </summary>
public sealed class ObjectExplanationConverter : IValueConverter
{
    public static ObjectExplanationConverter Instance { get; } = new();

    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        return ObjectExplanationManager.Default.Get(value, parameter);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Formats a <see cref="DateTime"/> with the current culture.
/// Replaces the Avalonia <c>Text="{Binding Addon.CreationTime}"</c> implicit ToString() conversion.
/// </summary>
public sealed class LocalDateTimeConverter : IValueConverter
{
    public static LocalDateTimeConverter Instance { get; } = new();

    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        return value switch
        {
            DateTime dateTime => XBind.LocalDateTime(dateTime),
            DateTimeOffset dateTimeOffset => XBind.LocalDateTime(dateTimeOffset.LocalDateTime),
            _ => value?.ToString(),
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// Formats <see cref="Texts.MultipleItemsSelected"/> for the "N items are selected" header of the
/// detail pane. Avalonia expressed this with <c>StringFormat</c>, which WinUI bindings do not support.
/// </summary>
public sealed class MultipleItemsSelectedConverter : IValueConverter
{
    public static MultipleItemsSelectedConverter Instance { get; } = new();

    public object? Convert(object value, Type targetType, object parameter, string language)
    {
        return value is int count ? XBind.MultipleItemsSelected(count) : "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotSupportedException();
    }
}
