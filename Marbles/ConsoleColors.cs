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
    /// True when the terminal says it has 8 colors (TERM=linux, the text console of Linux and of a Raspberry Pi, and
    /// a few others): .NET then shows the bright colors as the dark ones, and dark gray as black, so the bright
    /// colors are set again here with the codes those terminals have for them (90-97, 100-107)
    /// </summary>
    internal static bool EightColors { get; } = !OperatingSystem.IsWindows() && HasEightColors(
        Environment.GetEnvironmentVariable("TERM"));

    // The terminals whose description (terminfo) has 8 colors
    internal static bool HasEightColors(string? term) =>
        term is "linux" or "xterm" or "screen" or "tmux" or "vt100" or "vt220" or "ansi" or "rxvt" or "xterm-color" ||
        term?.StartsWith("linux", StringComparison.Ordinal) == true;

    // The colors of the terminal (ANSI) in the order of ConsoleColor: black, blue, green, cyan, red, magenta,
    // yellow, white
    private static readonly int[] Ansi = { 0, 4, 2, 6, 1, 5, 3, 7 };

    /// <summary>
    /// The escape code that sets a bright color on a terminal of 8 colors (null for a dark color, which .NET sets
    /// right)
    /// </summary>
    internal static string? BrightCode(ConsoleColor color, bool background)
    {
        var value = (int)color;
        if (value < 8) return null;
        return "\u001b[" +((background ? 100 : 90) + Ansi[value - 8]) + "m";
    }

    /// <summary>
    /// Set the color of the text (all colors are set here: see EightColors)
    /// </summary>
    internal static void SetForeground(ConsoleColor color)
    {
        _foreground = color;
        Console.ForegroundColor = color;
        WriteBrightCodes();
    }

    /// <summary>
    /// Set the color of the background (all colors are set here: see EightColors)
    /// </summary>
    internal static void SetBackground(ConsoleColor color)
    {
        _background = color;
        Console.BackgroundColor = color;
        WriteBrightCodes();
    }

    // The colors set last (null after a reset: the colors of the terminal)
    private static ConsoleColor? _foreground;
    private static ConsoleColor? _background;

    // On a terminal of 8 colors: the bright colors in use, again. .NET writes both colors each time one is set (a
    // reset, then both, as 8 colors), so a bright code written for the other one before is gone
    private static void WriteBrightCodes()
    {
        if (!EightColors) return;
        if (_foreground is { } foreground && BrightCode(foreground, false) is { } fore) Console.Write(fore);
        if (_background is { } background && BrightCode(background, true) is { } back) Console.Write(back);
    }

    /// <summary>
    /// Back to the colors of the terminal (all resets are done here, so the colors set last are known)
    /// </summary>
    internal static void Reset()
    {
        _foreground = null;
        _background = null;
        Console.ResetColor();
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
        Accent,
        EditorTitle,
        HighlightInverted,
        AttentionInverted,
        WarningInverted,
        TabFrame, // The frames with tabs
        TabCorner, // The left side, corner and top of the tab shown
        BoxBorder, // The borders of the boxes on top of a page
        TabShown, // The name of the tab shown, when the tab is not highlighted
        OtherTabs, // The names of the tabs not shown
        KeyHint, // The keys on the bottom line: what they do
        KeyHintInverted, // The keys on the bottom line: the key
        UnsavedInverted, // The label "Unsaved" (a text has changes that are not saved)
        LineNumber, // The line numbers of the editor
        CurrentLineNumber, // The number of the line the cursor is on
        SelectorInverted, // The selection arrow "->" of lists, menus and settings
        FrameText // The text in frames with tabs and in boxes
    }

    // The colors of text on the normal background: the background of the color scheme, or that of the terminal
    private static void SetNormalBackground(ColorScheme scheme)
    {
        Reset();
        if (scheme.Background is { } background) SetBackground(background);
    }

    // The colors used when everything is shown in black and white
    private static void SetMonochromeColors(ColorPreset preset)
    {
        switch (preset)
        {
            case ColorPreset.HeaderColor:
            case ColorPreset.Favorites:
            case ColorPreset.Accent:
            case ColorPreset.EditorTitle:
                Reset();
                SetForeground(ConsoleColor.White);
                break;
            case ColorPreset.NormalInverted:
            case ColorPreset.KeyHintInverted:
            case ColorPreset.UnsavedInverted:
            case ColorPreset.SelectorInverted:
            case ColorPreset.HighlightInverted:
            case ColorPreset.AttentionInverted:
            case ColorPreset.WarningInverted:
                SetBackground(ConsoleColor.Gray);
                SetForeground(ConsoleColor.Black);
                break;
            default:
                Reset();
                SetForeground(ConsoleColor.Gray);
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

        // In a frame with tabs or a box the normal text has the color of text there
        SetStyle(Current, preset == ColorPreset.Normal && _inFrame ? ColorPreset.FrameText : preset);
    }

    // True while the text in a frame with tabs or a box is written
    private static bool _inFrame;

    /// <summary>
    /// While the scope is open (until it is disposed), the normal text is written in the color of the text in
    /// frames with tabs and in boxes ("Tab text" of the color scheme). The highlighted text keeps its colors.
    /// The normal colors are set when it opens and when it is disposed, for text written without setting them
    /// </summary>
    internal static IDisposable FrameTextScope()
    {
        var before = _inFrame;
        _inFrame = true;
        SetColors(ColorPreset.Normal);
        return new FrameScope(before);
    }

    // Puts back what was before the scope when it is disposed
    private sealed class FrameScope(bool before) : IDisposable
    {
        public void Dispose()
        {
            _inFrame = before;
            SetColors(ColorPreset.Normal);
        }
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
                SetBackground(scheme.Text);
                SetForeground(scheme.BackgroundColor);
                return;
            case ColorPreset.KeyHintInverted:
                SetBackground(scheme.KeyHintsColor);
                SetForeground(scheme.BackgroundColor);
                return;
            case ColorPreset.UnsavedInverted:
                SetBackground(scheme.UnsavedColor);
                SetForeground(scheme.UnsavedTextColor);
                return;
            case ColorPreset.SelectorInverted:
                SetBackground(scheme.SelectorColor);
                SetForeground(scheme.SelectorTextColor);
                return;
            case ColorPreset.HighlightInverted:
                SetBackground(scheme.Highlight);
                SetForeground(scheme.HighlightText);
                return;
            case ColorPreset.AttentionInverted:
                SetBackground(scheme.Attention);
                SetForeground(scheme.AttentionText);
                return;
            case ColorPreset.WarningInverted:
                SetBackground(scheme.Warning);
                SetForeground(scheme.WarningText);
                return;
        }

        SetNormalBackground(scheme);
        SetForeground(preset switch
        {
            ColorPreset.HeaderColor => scheme.Logo,
            ColorPreset.Divider => scheme.Dividers,
            ColorPreset.Favorites => scheme.Favorites,
            ColorPreset.Accent => scheme.Accent,
            ColorPreset.EditorTitle => scheme.EditorTitle,
            ColorPreset.TabFrame => scheme.TabFrameColor,
            ColorPreset.TabCorner => scheme.TabCornerColor,
            ColorPreset.BoxBorder => scheme.BoxBorderColor,
            ColorPreset.TabShown => scheme.TabShownColor,
            ColorPreset.OtherTabs => scheme.OtherTabsColor,
            ColorPreset.KeyHint => scheme.KeyHintsColor,
            ColorPreset.LineNumber => scheme.LineNumbersColor,
            ColorPreset.CurrentLineNumber => scheme.CurrentLineNumberColor,
            ColorPreset.FrameText => scheme.FrameTextColor,
            _ => scheme.Text
        });
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
            SetForeground(ConsoleColor.White);
        }
        else if (!IsBackgroundColor(color))
        {
            SetForeground(color);
        }
    }

    /// <summary>
    /// Write colored text of the editor in its color (Syntax 1 to 12 of the color scheme, counted from 0). With
    /// preview: the colors being edited
    /// </summary>
    internal static void SetSyntaxColor(int number, bool preview = false)
    {
        if (preview)
        {
            SetStyle(Preview, ColorPreset.Normal);
            SetForeground(Preview.SyntaxColor(number));
            return;
        }

        SetTextColor(Current.SyntaxColor(number));
    }

    /// <summary>
    /// Write in the colors of the tab shown in a frame with tabs, and of the titles of boxes (with preview: the
    /// colors being edited). Highlighted: the background of the highlight (by default the color of the tab frames)
    /// with the text in the color of the focused tab. When the highlight is off, that color on the normal
    /// background. In black and white the highlight is inverted
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

        SetBackground(scheme.TabColor);
        SetForeground(scheme.TabTextColor);
    }

    #endregion
}
