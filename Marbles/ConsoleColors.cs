namespace Marbles;

/// <summary>
/// The colors of Marbles: the color scheme in use (Current) and the one being edited in the color scheme editor
/// (Preview), and the styles text is written in (ColorPreset), which take their colors from the scheme.
/// With black and white on, the styles are shown in gray, white and black, whatever the scheme is.
/// </summary>
internal static class ConsoleColors
{
    /// <summary>
    /// The color scheme in use (setting "colorscheme": its name)
    /// </summary>
    internal static ColorScheme Current { get; private set; } = ColorScheme.Default;

    /// <summary>
    /// The color scheme being edited in the color scheme editor (nothing is used until it is saved)
    /// </summary>
    internal static ColorScheme Preview { get; set; } = ColorScheme.Default;

    /// <summary>
    /// Use a color scheme (a copy is kept)
    /// </summary>
    internal static void Use(ColorScheme scheme)
    {
        Current = scheme.Clone();
        SetColors(ColorPreset.Normal);
    }

    /// <summary>
    /// The background color of the color scheme, or null to use the background of the terminal
    /// </summary>
    internal static ConsoleColor? Background => Current.Background;

    /// <summary>
    /// True if text in this color cannot be seen on the background of the color scheme in use
    /// </summary>
    internal static bool IsBackgroundColor(ConsoleColor color) => color == Current.BackgroundColor;

    /// <summary>
    /// True to show everything in black and white, whatever the colors of the color scheme are
    /// (setting "monochrome"). The color scheme is kept, and is used again when this is turned off
    /// </summary>
    internal static bool Monochrome { get; set; }

    #region The styles

    public enum ColorPreset
    {
        Reset, // Use the default color scheme
        Normal,
        NormalInverted,
        HeaderColor,
        Divider,
        Favorites,
        PreviewTitle,
        EditorTitle,
        HighlightInverted,
        AttentionInverted,
        WarningInverted,
        TabFrame, // The frames with tabs
        BoxBorder, // The borders of the boxes on top of a page
        TabShown, // The name of the tab shown, when the tab is not highlighted
        OtherTabs // The names of the tabs not shown
    }

    // The colors of text on the normal background: the background of the color scheme, or that of the terminal
    private static void SetNormalBackground(ColorScheme scheme)
    {
        Console.ResetColor();
        if (scheme.Background is { } background) Console.BackgroundColor = background;
    }

    // The colors used when everything is shown in black and white
    private static void SetMonochromeColors(ColorPreset preset)
    {
        switch (preset)
        {
            case ColorPreset.HeaderColor:
            case ColorPreset.Favorites:
            case ColorPreset.PreviewTitle:
            case ColorPreset.EditorTitle:
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.White;
                break;
            case ColorPreset.NormalInverted:
            case ColorPreset.HighlightInverted:
            case ColorPreset.AttentionInverted:
            case ColorPreset.WarningInverted:
                Console.BackgroundColor = ConsoleColor.Gray;
                Console.ForegroundColor = ConsoleColor.Black;
                break;
            default:
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.Gray;
                break;
        }
    }

    /// <summary>
    /// Write in a style, in the colors of the color scheme in use
    /// </summary>
    public static void SetColors(ColorPreset preset)
    {
        if (preset == ColorPreset.Reset)
        {
            Current = ColorScheme.Default;
            preset = ColorPreset.Normal;
        }

        if (Monochrome)
        {
            SetMonochromeColors(preset);
            return;
        }

        SetStyle(Current, preset);
    }

    /// <summary>
    /// Write in a style, in the colors of the color scheme being edited (black and white is not used here)
    /// </summary>
    public static void SetColorsFromPreview(ColorPreset preset)
    {
        SetStyle(Preview, preset == ColorPreset.Reset ? ColorPreset.Normal : preset);
    }

    private static void SetStyle(ColorScheme scheme, ColorPreset preset)
    {
        switch (preset)
        {
            case ColorPreset.NormalInverted:
                Console.BackgroundColor = scheme.Text;
                Console.ForegroundColor = scheme.BackgroundColor;
                return;
            case ColorPreset.HighlightInverted:
                Console.BackgroundColor = scheme.Highlight;
                Console.ForegroundColor = scheme.HighlightText;
                return;
            case ColorPreset.AttentionInverted:
                Console.BackgroundColor = scheme.Attention;
                Console.ForegroundColor = scheme.AttentionText;
                return;
            case ColorPreset.WarningInverted:
                Console.BackgroundColor = scheme.Warning;
                Console.ForegroundColor = scheme.WarningText;
                return;
        }

        SetNormalBackground(scheme);
        Console.ForegroundColor = preset switch
        {
            ColorPreset.HeaderColor => scheme.Logo,
            ColorPreset.Divider => scheme.Dividers,
            ColorPreset.Favorites => scheme.Favorites,
            ColorPreset.PreviewTitle => scheme.PreviewTitle,
            ColorPreset.EditorTitle => scheme.EditorTitle,
            ColorPreset.TabFrame => scheme.TabFrameColor,
            ColorPreset.BoxBorder => scheme.BoxBorderColor,
            ColorPreset.TabShown => scheme.TabShownColor,
            ColorPreset.OtherTabs => scheme.OtherTabsColor,
            _ => scheme.Text
        };
    }

    /// <summary>
    /// Write text in a color on the normal background (the normal text color is used if the color is that
    /// of the background)
    /// </summary>
    internal static void SetTextColor(ConsoleColor color)
    {
        SetColors(ColorPreset.Normal);
        if (Monochrome)
        {
            // No colors: text with a color of its own is shown brighter
            Console.ForegroundColor = ConsoleColor.White;
        }
        else if (!IsBackgroundColor(color))
        {
            Console.ForegroundColor = color;
        }
    }

    /// <summary>
    /// Write in the colors of the tab shown in a frame with tabs, and of the titles of boxes (with preview: the
    /// colors being edited). By default the colors of the tab frames inverted. When the highlight of the tabs is
    /// off, in the color of the tab shown on the normal background
    /// </summary>
    internal static void SetTabColors(bool preview = false)
    {
        var scheme = preview ? Preview : Current;
        if (Monochrome && !preview)
        {
            if (Current.TabHighlight) SetMonochromeColors(ColorPreset.HighlightInverted);
            else SetMonochromeColors(ColorPreset.Normal);
            return;
        }

        if (!scheme.TabHighlight)
        {
            SetStyle(scheme, ColorPreset.TabShown);
            return;
        }

        Console.BackgroundColor = scheme.TabColor;
        Console.ForegroundColor = scheme.TabTextColor;
    }

    #endregion
}
