using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WinUIMusicPlayer.Helper;

/// <summary>
/// 字符图标附加属性：让 <see cref="FontIcon"/> 通过 <see cref="IconKind"/> 枚举引用图标，
/// 自动套用 <see cref="IconService.GetIconChar"/> 的字形与 Segoe Fluent Icons 字体。
/// 用法：<c>&lt;FontIcon helper:IconGlyph.IconKind="Play"/&gt;</c>
/// </summary>
public static class IconGlyph
{
    /// <summary>Segoe Fluent Icons 字体名（WinUI 默认 FontIcon 字体为 Segoe MDL2 Assets，需显式指定以正确渲染 Fluent 字形）。</summary>
    public const string SegoeFluentIconsFontFamily = "Segoe Fluent Icons";

    public static readonly DependencyProperty IconKindProperty =
        DependencyProperty.RegisterAttached(
            "IconKind",
            typeof(IconKind),
            typeof(IconGlyph),
            new PropertyMetadata(IconKind.None, OnIconKindChanged));

    public static void SetIconKind(DependencyObject element, IconKind value) => element.SetValue(IconKindProperty, value);

    public static IconKind GetIconKind(DependencyObject element) => (IconKind)element.GetValue(IconKindProperty);

    private static void OnIconKindChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FontIcon fontIcon)
        {
            return;
        }

        var kind = (IconKind)e.NewValue;
        fontIcon.Glyph = IconService.GetIconChar(kind);
        // 本服务集中管理的字形均为 Segoe Fluent Icons，始终套用该字体以保证正确渲染
        // （WinUI FontIcon 默认字体为 Segoe MDL2 Assets，不覆盖会导致 Fluent 字形显示异常）
        fontIcon.FontFamily = new Microsoft.UI.Xaml.Media.FontFamily(SegoeFluentIconsFontFamily);
    }
}
