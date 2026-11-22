// Ported from FireAxe_Alt\FireAxe.GUI\ (spread across ValueConverters\, ObjectConverters and the MultiBinding
// StringFormat usages in Views\MainWindow.axaml).
//
// Why functions instead of converters: x:Bind supports function bindings ({x:Bind local:XBind.Foo(path)}),
// which re-evaluate whenever the argument path changes. That keeps the format strings localized
// (they come from FireAxe.Resources.Texts, which P1 moved into the ViewModel layer) without
// IValueConverter boilerplate or {Binding} fallbacks.
using System;
using System.Globalization;
using FireAxe.Resources;
using FireAxe.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace FireAxe.Views;

/// <summary>Binding helpers for <c>{x:Bind}</c> function bindings in the shell and in later stages' windows.</summary>
public static class XBind
{
    public static Visibility VisibleIf(bool value)
    {
        return value ? Visibility.Visible : Visibility.Collapsed;
    }

    public static Visibility VisibleIfNotNull(object? value)
    {
        return value is null ? Visibility.Collapsed : Visibility.Visible;
    }

    public static Visibility VisibleIfPositive(int value)
    {
        return value > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public static Visibility VisibleIfZero(int value)
    {
        return value == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Formats <see cref="Texts.ProblemsFoundWithCount"/>.</summary>
    public static string ProblemsFound(int problemCount)
    {
        return Texts.ProblemsFoundWithCount.FormatNoThrow(problemCount);
    }

    /// <summary>Formats <see cref="Texts.LastPushedAt"/>; empty when nothing has been pushed yet.</summary>
    public static string LastPushedAt(DateTime? dateTime)
    {
        if (dateTime is not { } value)
        {
            return "";
        }
        return Texts.LastPushedAt.FormatNoThrow(value.ToString("HH:mm:ss", CultureInfo.CurrentCulture));
    }

    public static string WindowTitle(string titleExtraInfo)
    {
        return "FireAxeWinUI3" + titleExtraInfo;
    }

    /// <summary>
    /// The "RandomlySelect [for selected items]" menu captions: the Avalonia version used a MultiBinding
    /// with <c>StringFormat="{}{0} {1}"</c> over two resources.
    /// </summary>
    public static string WithSelectedItemsHint(string title)
    {
        return $"{title} {Texts.HintSelectedItems}";
    }

    // ---------------------------------------------------------------- P3 (AddonNodeExplorer) helpers

    /// <summary>Literal <c>true</c> / <c>false</c> for <c>CommandParameter="{x:Bind ...}"</c>.</summary>
    public static bool True => true;

    /// <summary>Literal <c>true</c> / <c>false</c> for <c>CommandParameter="{x:Bind ...}"</c>.</summary>
    public static bool False => false;

    /// <summary>Unwraps the ViewModel layer's opaque image handle (was <c>Bitmap</c> in the Avalonia version).</summary>
    public static ImageSource? ToImageSource(IImageSource? source)
    {
        return ImageLoader.ToImageSource(source);
    }

    public static Visibility VisibleIfEmpty(int count)
    {
        return count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public static Visibility VisibleIfNotEmpty(int count)
    {
        return count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Formats <see cref="Utils.GetReadableBytes(double)"/>; empty for a missing size.</summary>
    public static string ReadableBytes(long? bytes)
    {
        return bytes is { } value ? XBind.ReadableBytes((double)value) : "";
    }

    /// <summary>Formats <see cref="Utils.GetReadableBytes(double)"/>.</summary>
    public static string ReadableBytes(double bytes)
    {
        return Utils.GetReadableBytes(bytes);
    }

    /// <summary>Localizes an enum via the <c>Enum_{Type}_{Value}</c> resources (see <see cref="EnumDescriptionConverter"/>).</summary>
    public static string EnumDescription(object? value)
    {
        return EnumDescriptionConverter.Instance.Convert(value!, typeof(string), null!, null!) as string ?? "";
    }

    /// <summary>Formats <see cref="Texts.MultipleItemsSelected"/>.</summary>
    public static string MultipleItemsSelected(int count)
    {
        return Texts.MultipleItemsSelected.FormatNoThrow(count);
    }

    /// <summary>The Avalonia version used <c>StringFormat='(ID: {0})'</c> for a missing reference target.</summary>
    public static string IdInParentheses(string? addonId)
    {
        return $"(ID: {addonId})";
    }

    /// <summary><c>Text="{Binding Addon.CreationTime}"</c> in the Avalonia version rendered via ToString().</summary>
    public static string LocalDateTime(DateTime value)
    {
        return value.ToString(CultureInfo.CurrentCulture);
    }
}
