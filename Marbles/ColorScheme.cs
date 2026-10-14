using System.Text;

namespace Marbles;

/// <summary>
/// A color of a color scheme
/// </summary>
internal enum ColorItem
{
    Background, // The background of the screen (null: the background of the terminal)
    Text,
    Logo, // The logo and labels
    Dividers,
    Favorites,
    Accent, // The keys of the quick guide (and from the start the names in colored text: Syntax 2)
    EditorTitle,
    Highlight, // The background of the highlight (inverted)
    HighlightText,
    Attention,
    AttentionText,
    Warning,
    WarningText,
    TabFrames, // The frames with tabs (null: the color of the dividers)
    TabCorner, // The left side, corner and top of the tab shown in a frame with tabs (null: the tab frames)
    BoxBorders, // The borders of the boxes on top of a page (null: the color of the dividers)
    Tab, // The background of the tab shown (null: the color of the tab frames)
    TabText, // The text of the tab shown when it is highlighted (null: the color of TabShown)
    TabShown, // The name of the tab shown when it is not highlighted (null: the text)
    OtherTabs, // The names of the other tabs (null: the text)
    KeyHints, // The keys on the bottom line (null: the text)
    Unsaved, // The background of the label "Unsaved" (null: the text)
    UnsavedText, // The text of the label "Unsaved" (null: the background)
    LineNumbers, // The line numbers of the editor (null: the dividers)
    CurrentLineNumber, // The number of the line the cursor is on (null: the text)
    Selector, // The background of the selection arrow "->" (null: the highlight)
    SelectorText, // The arrow itself (null: the text of the highlight)
    FrameText, // The text in frames with tabs and in boxes, not the highlighted text (null: the text)

    // The colors of colored text in the editor (null: the color each has from the start, see SyntaxDefault).
    // 1 keywords, tags and selectors (bold and list marks in Markdown), 2 names of attributes and properties
    // (italic), 3 text in quotes and values (code), 4 numbers, 5 links, 6 comments (quotes, web addresses, rules),
    // 7-12 the headings 1 to 6 of Markdown
    Syntax1,
    Syntax2,
    Syntax3,
    Syntax4,
    Syntax5,
    Syntax6,
    Syntax7,
    Syntax8,
    Syntax9,
    Syntax10,
    Syntax11,
    Syntax12
}

/// <summary>
/// A color scheme: the colors of Marbles by name, and if the tab shown is highlighted. The built-in schemes are
/// in the code (BuiltIn); the ones made by the user are kept as files (ColorSchemes). A scheme is written as lines
/// of "name=value" (the value is the name of a color, or "default" for the colors that can have it).
/// It does not touch the console, so it can be tested.
/// </summary>
internal sealed class ColorScheme
{
    internal const string DefaultName = "Default";

    internal string Name { get; set; } = DefaultName;

    internal ConsoleColor? Background { get; set; }
    internal ConsoleColor Text { get; set; } = ConsoleColor.Gray;
    internal ConsoleColor Logo { get; set; } = ConsoleColor.Cyan;
    internal ConsoleColor Dividers { get; set; } = ConsoleColor.DarkGray;
    internal ConsoleColor Favorites { get; set; } = ConsoleColor.Green;
    internal ConsoleColor Accent { get; set; } = ConsoleColor.Yellow;
    internal ConsoleColor EditorTitle { get; set; } = ConsoleColor.Green;
    internal ConsoleColor Highlight { get; set; } = ConsoleColor.Cyan;
    internal ConsoleColor HighlightText { get; set; } = ConsoleColor.Black;
    internal ConsoleColor Attention { get; set; } = ConsoleColor.Yellow;
    internal ConsoleColor AttentionText { get; set; } = ConsoleColor.Black;
    internal ConsoleColor Warning { get; set; } = ConsoleColor.Red;
    internal ConsoleColor WarningText { get; set; } = ConsoleColor.White;
    internal ConsoleColor? TabFrames { get; set; }
    internal ConsoleColor? TabCorner { get; set; }
    internal ConsoleColor? BoxBorders { get; set; }
    internal ConsoleColor? Tab { get; set; }
    internal ConsoleColor? TabText { get; set; }
    internal ConsoleColor? TabShown { get; set; }
    internal ConsoleColor? OtherTabs { get; set; }
    internal ConsoleColor? KeyHints { get; set; }
    internal ConsoleColor? Unsaved { get; set; }
    internal ConsoleColor? UnsavedText { get; set; }
    internal ConsoleColor? LineNumbers { get; set; }
    internal ConsoleColor? CurrentLineNumber { get; set; }
    internal ConsoleColor? Selector { get; set; }
    internal ConsoleColor? SelectorText { get; set; }
    internal ConsoleColor? FrameText { get; set; }

    internal const int SyntaxCount = 12;

    // The colors of colored text (Syntax 1 to 12), null for the ones that have their color from the start
    private ConsoleColor?[] _syntax = new ConsoleColor?[SyntaxCount];

    /// <summary>
    /// The tab (and sub-tab) shown is highlighted in the colors of the tabs; else only its frame tells it apart
    /// </summary>
    internal bool TabHighlight { get; set; } = true;

    // The colors as used (the ones that can be "default" have the color they stand for)
    internal ConsoleColor BackgroundColor => Background ?? ConsoleColor.Black;
    internal ConsoleColor TabFrameColor => TabFrames ?? Dividers;
    internal ConsoleColor TabCornerColor => TabCorner ?? TabFrameColor;
    internal ConsoleColor BoxBorderColor => BoxBorders ?? Dividers;
    internal ConsoleColor TabColor => Tab ?? TabFrameColor;
    internal ConsoleColor TabTextColor => TabText ?? TabShownColor;
    internal ConsoleColor TabShownColor => TabShown ?? Text;
    internal ConsoleColor OtherTabsColor => OtherTabs ?? Text;
    internal ConsoleColor KeyHintsColor => KeyHints ?? Text;
    internal ConsoleColor UnsavedColor => Unsaved ?? Text;
    internal ConsoleColor UnsavedTextColor => UnsavedText ?? BackgroundColor;
    internal ConsoleColor LineNumbersColor => LineNumbers ?? Dividers;
    internal ConsoleColor CurrentLineNumberColor => CurrentLineNumber ?? Text;
    internal ConsoleColor SelectorColor => Selector ?? Highlight;
    internal ConsoleColor SelectorTextColor => SelectorText ?? HighlightText;
    internal ConsoleColor FrameTextColor => FrameText ?? Text;

    // True for the colors of colored text (Syntax 1 to 12), with the number of the color (from 0)
    private static bool IsSyntax(ColorItem item, out int number)
    {
        number = item - ColorItem.Syntax1;
        return number is >= 0 and < SyntaxCount;
    }

    /// <summary>
    /// The color a kind of colored text has when the scheme does not set it: those of the scheme it had before
    /// there were colors for it (the logo, the accent, the favorites, the editor title, the dividers), and fixed
    /// colors for numbers and the headings of Markdown
    /// </summary>
    internal ConsoleColor SyntaxDefault(int number)
    {
        return number switch
        {
            0 => Logo,
            1 => Accent,
            2 => Favorites,
            3 => ConsoleColor.Magenta,
            4 => EditorTitle,
            5 => Dividers,
            6 => ConsoleColor.Red,
            7 => ConsoleColor.Magenta,
            8 => ConsoleColor.Yellow,
            9 => ConsoleColor.Cyan,
            10 => ConsoleColor.Green,
            _ => ConsoleColor.Blue
        };
    }

    /// <summary>
    /// The color of a kind of colored text (Syntax 1 to 12, counted from 0)
    /// </summary>
    internal ConsoleColor SyntaxColor(int number) => _syntax[number] ?? SyntaxDefault(number);

    /// <summary>
    /// The logo that is used together with the scheme: when the scheme is selected, the logo with this name is
    /// used too (empty: the logo stays as it is). "uselogo" in a scheme file
    /// </summary>
    internal string UseLogo { get; set; } = string.Empty;

    internal ColorScheme Clone()
    {
        var copy = (ColorScheme)MemberwiseClone();
        copy._syntax = (ConsoleColor?[])_syntax.Clone();
        return copy;
    }

    /// <summary>
    /// True if the color can be "default" (it then has the color of another item)
    /// </summary>
    internal static bool CanBeDefault(ColorItem item) =>
        item is ColorItem.Background or ColorItem.TabFrames or ColorItem.TabCorner or ColorItem.BoxBorders
            or ColorItem.Tab
            or ColorItem.TabText or ColorItem.TabShown or ColorItem.OtherTabs or ColorItem.KeyHints
            or ColorItem.Unsaved or ColorItem.UnsavedText or ColorItem.LineNumbers or ColorItem.CurrentLineNumber
            or ColorItem.FrameText || IsSyntax(item, out _);

    internal ConsoleColor? Get(ColorItem item)
    {
        if (IsSyntax(item, out var syntax)) return _syntax[syntax];
        return item switch
        {
            ColorItem.Background => Background,
            ColorItem.Text => Text,
            ColorItem.Logo => Logo,
            ColorItem.Dividers => Dividers,
            ColorItem.Favorites => Favorites,
            ColorItem.Accent => Accent,
            ColorItem.EditorTitle => EditorTitle,
            ColorItem.Highlight => Highlight,
            ColorItem.HighlightText => HighlightText,
            ColorItem.Attention => Attention,
            ColorItem.AttentionText => AttentionText,
            ColorItem.Warning => Warning,
            ColorItem.WarningText => WarningText,
            ColorItem.TabFrames => TabFrames,
            ColorItem.TabCorner => TabCorner,
            ColorItem.BoxBorders => BoxBorders,
            ColorItem.Tab => Tab,
            ColorItem.TabText => TabText,
            ColorItem.TabShown => TabShown,
            ColorItem.OtherTabs => OtherTabs,
            ColorItem.KeyHints => KeyHints,
            ColorItem.Unsaved => Unsaved,
            ColorItem.UnsavedText => UnsavedText,
            ColorItem.LineNumbers => LineNumbers,
            ColorItem.CurrentLineNumber => CurrentLineNumber,
            ColorItem.Selector => Selector,
            ColorItem.FrameText => FrameText,
            _ => SelectorText
        };
    }

    /// <summary>
    /// The color an item is shown in (for the ones that are "default": the color they stand for)
    /// </summary>
    internal ConsoleColor Shown(ColorItem item)
    {
        if (IsSyntax(item, out var syntax)) return SyntaxColor(syntax);
        return item switch
        {
            ColorItem.Background => BackgroundColor,
            ColorItem.TabFrames => TabFrameColor,
            ColorItem.TabCorner => TabCornerColor,
            ColorItem.BoxBorders => BoxBorderColor,
            ColorItem.Tab => TabColor,
            ColorItem.TabText => TabTextColor,
            ColorItem.TabShown => TabShownColor,
            ColorItem.OtherTabs => OtherTabsColor,
            ColorItem.KeyHints => KeyHintsColor,
            ColorItem.Unsaved => UnsavedColor,
            ColorItem.UnsavedText => UnsavedTextColor,
            ColorItem.LineNumbers => LineNumbersColor,
            ColorItem.CurrentLineNumber => CurrentLineNumberColor,
            ColorItem.Selector => SelectorColor,
            ColorItem.SelectorText => SelectorTextColor,
            ColorItem.FrameText => FrameTextColor,
            _ => Get(item)!.Value
        };
    }

    /// <summary>
    /// Set a color (null: "default", only for the items that can have it)
    /// </summary>
    internal void Set(ColorItem item, ConsoleColor? color)
    {
        if (color == null && !CanBeDefault(item)) return;
        if (IsSyntax(item, out var syntax))
        {
            _syntax[syntax] = color;
            return;
        }

        switch (item)
        {
            case ColorItem.Background: Background = color; break;
            case ColorItem.Text: Text = color!.Value; break;
            case ColorItem.Logo: Logo = color!.Value; break;
            case ColorItem.Dividers: Dividers = color!.Value; break;
            case ColorItem.Favorites: Favorites = color!.Value; break;
            case ColorItem.Accent: Accent = color!.Value; break;
            case ColorItem.EditorTitle: EditorTitle = color!.Value; break;
            case ColorItem.Highlight: Highlight = color!.Value; break;
            case ColorItem.HighlightText: HighlightText = color!.Value; break;
            case ColorItem.Attention: Attention = color!.Value; break;
            case ColorItem.AttentionText: AttentionText = color!.Value; break;
            case ColorItem.Warning: Warning = color!.Value; break;
            case ColorItem.WarningText: WarningText = color!.Value; break;
            case ColorItem.TabFrames: TabFrames = color; break;
            case ColorItem.TabCorner: TabCorner = color; break;
            case ColorItem.BoxBorders: BoxBorders = color; break;
            case ColorItem.Tab: Tab = color; break;
            case ColorItem.TabText: TabText = color; break;
            case ColorItem.TabShown: TabShown = color; break;
            case ColorItem.OtherTabs: OtherTabs = color; break;
            case ColorItem.KeyHints: KeyHints = color; break;
            case ColorItem.Unsaved: Unsaved = color; break;
            case ColorItem.UnsavedText: UnsavedText = color; break;
            case ColorItem.LineNumbers: LineNumbers = color; break;
            case ColorItem.CurrentLineNumber: CurrentLineNumber = color; break;
            case ColorItem.Selector: Selector = color; break;
            case ColorItem.FrameText: FrameText = color; break;
            default: SelectorText = color; break;
        }
    }

    // The text colors shown on the background, and the pairs of a background and the text on it
    private static readonly ColorItem[] OnBackground =
    {
        ColorItem.Text, ColorItem.Logo, ColorItem.Dividers, ColorItem.Favorites, ColorItem.Accent,
        ColorItem.EditorTitle, ColorItem.TabFrames, ColorItem.TabCorner, ColorItem.BoxBorders, ColorItem.TabShown,
        ColorItem.OtherTabs,
        ColorItem.KeyHints, ColorItem.LineNumbers, ColorItem.CurrentLineNumber, ColorItem.FrameText
    };

    private static readonly (ColorItem Background, ColorItem Text)[] Pairs =
    {
        (ColorItem.Highlight, ColorItem.HighlightText), (ColorItem.Attention, ColorItem.AttentionText),
        (ColorItem.Warning, ColorItem.WarningText), (ColorItem.Tab, ColorItem.TabText),
        (ColorItem.Unsaved, ColorItem.UnsavedText), (ColorItem.Selector, ColorItem.SelectorText)
    };

    /// <summary>
    /// False if a color cannot be used for an item, as text would not be visible on its background: the background
    /// cannot have the color of the text on it, and the text and background of a highlight cannot be the same
    /// </summary>
    internal bool Allowed(ColorItem item, ConsoleColor color)
    {
        if (item == ColorItem.Background) return OnBackground.All(text => Shown(text) != color);
        if (item == ColorItem.TabShown && TabText == null && color == TabColor) return false;
        if (OnBackground.Contains(item) || IsSyntax(item, out _)) return color != BackgroundColor;
        foreach (var (background, text) in Pairs)
        {
            if (item == background) return color != Shown(text);
            if (item == text) return color != Shown(background);
        }

        // The text of the focused tab is also the text of the highlight, when that has no text color of its own
        if (item == ColorItem.TabShown && TabText == null && color == TabColor) return false;

        return true;
    }

    #region Files

    // The names of the items in a file, in the order they are written
    private static readonly (string Key, ColorItem Item)[] Keys =
    {
        ("background", ColorItem.Background), ("text", ColorItem.Text), ("logo", ColorItem.Logo),
        ("dividers", ColorItem.Dividers), ("favorites", ColorItem.Favorites),
        ("accent", ColorItem.Accent), ("editortitle", ColorItem.EditorTitle),
        ("highlight", ColorItem.Highlight), ("highlighttext", ColorItem.HighlightText),
        ("attention", ColorItem.Attention), ("attentiontext", ColorItem.AttentionText),
        ("warning", ColorItem.Warning), ("warningtext", ColorItem.WarningText),
        ("tabframes", ColorItem.TabFrames), ("tabcorner", ColorItem.TabCorner),
        ("boxborders", ColorItem.BoxBorders), ("tab", ColorItem.Tab),
        ("tabtext", ColorItem.TabText), ("tabshown", ColorItem.TabShown), ("othertabs", ColorItem.OtherTabs),
        ("keyhints", ColorItem.KeyHints), ("unsaved", ColorItem.Unsaved), ("unsavedtext", ColorItem.UnsavedText),
        ("linenumbers", ColorItem.LineNumbers), ("currentline", ColorItem.CurrentLineNumber),
        ("selector", ColorItem.Selector), ("selectortext", ColorItem.SelectorText),
        ("frametext", ColorItem.FrameText),
        ("syntax1", ColorItem.Syntax1), ("syntax2", ColorItem.Syntax2), ("syntax3", ColorItem.Syntax3),
        ("syntax4", ColorItem.Syntax4), ("syntax5", ColorItem.Syntax5), ("syntax6", ColorItem.Syntax6),
        ("syntax7", ColorItem.Syntax7), ("syntax8", ColorItem.Syntax8), ("syntax9", ColorItem.Syntax9),
        ("syntax10", ColorItem.Syntax10), ("syntax11", ColorItem.Syntax11), ("syntax12", ColorItem.Syntax12)
    };

    private const string HighlightKey = "tabhighlight";
    private const string UseLogoKey = "uselogo";
    private const string DefaultValue = "default";

    /// <summary>
    /// The scheme as text: a line for each color ("text=Gray", "tab=default") and "tabhighlight=true"
    /// </summary>
    internal string Serialize()
    {
        var text = new StringBuilder();
        text.Append("; Marbles color scheme: ").Append(Name).Append('\n'); // English
        text.Append("; The colors: Black, DarkBlue, DarkGreen, DarkCyan, DarkRed, DarkMagenta, DarkYellow, Gray,\n");
        text.Append("; DarkGray, Blue, Green, Cyan, Red, Magenta, Yellow, White (or default)\n");
        foreach (var (key, item) in Keys)
        {
            text.Append(key).Append('=').Append(Get(item)?.ToString() ?? DefaultValue).Append('\n');
        }

        text.Append(HighlightKey).Append('=').Append(TabHighlight ? "true" : "false").Append('\n');
        // The logo used with the scheme, when it has one
        if (UseLogo.Length > 0) text.Append(UseLogoKey).Append('=').Append(UseLogo).Append('\n');
        return text.ToString();
    }

    /// <summary>
    /// A scheme from its text. What is missing or cannot be read has the colors of the default scheme
    /// </summary>
    internal static ColorScheme Parse(string text, string name)
    {
        var scheme = BuiltIn[0].Clone();
        scheme.Name = name;
        // The selector has colors of its own: without them in the text, the ones of the highlight of the text
        scheme.Selector = null;
        scheme.SelectorText = null;
        scheme.UseLogo = string.Empty;
        // The colors that can be "default" are that when they are not in the text (not the ones the default
        // scheme has picked for them)
        foreach (var (_, item) in Keys)
        {
            if (CanBeDefault(item)) scheme.Set(item, null);
        }

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line[0] == ';') continue;
            var equals = line.IndexOf('=');
            if (equals < 0) continue;
            var key = line[..equals].Trim().ToLowerInvariant();
            var value = line[(equals + 1)..].Trim();
            if (key == HighlightKey)
            {
                scheme.TabHighlight = !value.Equals("false", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (key == UseLogoKey)
            {
                scheme.UseLogo = value;
                continue;
            }

            if (key == "previewtitle") key = "accent"; // The name it had before
            var found = Array.FindIndex(Keys, k => k.Key == key);
            if (found < 0) continue;
            var item = Keys[found].Item;
            if (value.Equals(DefaultValue, StringComparison.OrdinalIgnoreCase))
            {
                scheme.Set(item, null);
            }
            else if (Enum.TryParse<ConsoleColor>(value, true, out var color) && Enum.IsDefined(color))
            {
                scheme.Set(item, color);
            }
        }

        scheme.Selector ??= scheme.Highlight;
        scheme.SelectorText ??= scheme.HighlightText;
        return scheme;
    }

    #endregion

    #region Built-in schemes

    private static ColorScheme Make(string name, ConsoleColor? background, ConsoleColor text, ConsoleColor logo,
        ConsoleColor dividers, ConsoleColor favorites, ConsoleColor previewTitle, ConsoleColor editorTitle,
        ConsoleColor highlight, ConsoleColor highlightText, ConsoleColor attention, ConsoleColor attentionText,
        ConsoleColor warning, ConsoleColor warningText)
    {
        return new ColorScheme
        {
            Name = name, Background = background, Text = text, Logo = logo, Dividers = dividers,
            Favorites = favorites, Accent = previewTitle, EditorTitle = editorTitle, Highlight = highlight,
            HighlightText = highlightText, Attention = attention, AttentionText = attentionText, Warning = warning,
            WarningText = warningText,
            // The selector starts with the colors of the highlight, and keeps them when the highlight is changed
            Selector = highlight, SelectorText = highlightText
        };
    }

    // The colors of the frames and tabs of a scheme (null: the default of the color)
    private static ColorScheme WithTabs(ColorScheme scheme, ConsoleColor? tabFrames, ConsoleColor? boxBorders,
        ConsoleColor? tab, ConsoleColor? tabText, ConsoleColor? tabShown, ConsoleColor? otherTabs, bool highlight)
    {
        scheme.TabFrames = tabFrames;
        scheme.BoxBorders = boxBorders;
        scheme.Tab = tab;
        scheme.TabText = tabText;
        scheme.TabShown = tabShown;
        scheme.OtherTabs = otherTabs;
        scheme.TabHighlight = highlight;
        return scheme;
    }

    // The default scheme: white box borders, the focused tab cyan (not highlighted) with a gray corner, the other
    // tabs dark gray, headings in colors of their own, and the logo "Marbles", which is used when the scheme is
    // selected
    private static ColorScheme DefaultScheme()
    {
        var scheme = WithTabs(Make(DefaultName, null, ConsoleColor.Gray, ConsoleColor.Cyan, ConsoleColor.DarkGray,
                ConsoleColor.Green, ConsoleColor.Yellow, ConsoleColor.Green, ConsoleColor.Cyan, ConsoleColor.Black,
                ConsoleColor.Yellow, ConsoleColor.Black, ConsoleColor.Red, ConsoleColor.White),
            null, ConsoleColor.White, ConsoleColor.DarkGray, null, ConsoleColor.Cyan, ConsoleColor.DarkGray, false);
        scheme.TabCorner = ConsoleColor.Gray;
        scheme.CurrentLineNumber = ConsoleColor.White;
        // Heading 1 to 5 (Syntax 7 to 11)
        scheme.Set(ColorItem.Syntax7, ConsoleColor.Yellow);
        scheme.Set(ColorItem.Syntax8, ConsoleColor.Cyan);
        scheme.Set(ColorItem.Syntax9, ConsoleColor.Green);
        scheme.Set(ColorItem.Syntax10, ConsoleColor.Magenta);
        scheme.Set(ColorItem.Syntax11, ConsoleColor.Red);
        scheme.UseLogo = LogoArt.BuiltInName;
        return scheme;
    }

    // "Deep Blue": a dark blue background with cyan frames, the focused tab white on blue with a white corner,
    // headings in colors of their own, and the logo "Marbles", which is used when the scheme is selected
    private static ColorScheme DeepBlue()
    {
        var scheme = WithTabs(Make("Deep Blue", ConsoleColor.DarkBlue, ConsoleColor.Gray, ConsoleColor.Cyan,
                ConsoleColor.Cyan, ConsoleColor.Green, ConsoleColor.Magenta, ConsoleColor.White, ConsoleColor.Blue,
                ConsoleColor.Cyan, ConsoleColor.Yellow, ConsoleColor.Black, ConsoleColor.Red, ConsoleColor.White),
            null, null, ConsoleColor.Blue, null, ConsoleColor.White, ConsoleColor.Gray, true);
        scheme.TabCorner = ConsoleColor.White;
        scheme.KeyHints = ConsoleColor.Gray;
        scheme.LineNumbers = ConsoleColor.Gray;
        scheme.CurrentLineNumber = ConsoleColor.White;
        scheme.Selector = ConsoleColor.Blue;
        scheme.SelectorText = ConsoleColor.White;
        // Names (Syntax 2), comments (Syntax 6) and heading 1 to 5 (Syntax 7 to 11)
        scheme.Set(ColorItem.Syntax2, ConsoleColor.Yellow);
        scheme.Set(ColorItem.Syntax6, ConsoleColor.Gray);
        scheme.Set(ColorItem.Syntax7, ConsoleColor.Yellow);
        scheme.Set(ColorItem.Syntax8, ConsoleColor.Green);
        scheme.Set(ColorItem.Syntax9, ConsoleColor.Cyan);
        scheme.Set(ColorItem.Syntax10, ConsoleColor.Magenta);
        scheme.Set(ColorItem.Syntax11, ConsoleColor.Red);
        scheme.UseLogo = LogoArt.BuiltInName;
        return scheme;
    }

    // "Tasty Bytes": magenta dividers, highlighted tabs in dark blue with a yellow corner, and the logo
    // "Reflections", which is used when the scheme is selected
    private static ColorScheme TastyBytes()
    {
        var scheme = WithTabs(Make("Tasty Bytes", null, ConsoleColor.Gray, ConsoleColor.Cyan, ConsoleColor.Magenta,
                ConsoleColor.Green, ConsoleColor.Magenta, ConsoleColor.Green, ConsoleColor.DarkBlue, ConsoleColor.Cyan,
                ConsoleColor.Yellow, ConsoleColor.Red, ConsoleColor.Red, ConsoleColor.White),
            null, ConsoleColor.Cyan, ConsoleColor.DarkBlue, ConsoleColor.White, null, ConsoleColor.Cyan, true);
        scheme.TabCorner = ConsoleColor.Yellow;
        scheme.KeyHints = ConsoleColor.Green;
        scheme.Unsaved = ConsoleColor.DarkMagenta;
        scheme.UnsavedText = ConsoleColor.White;
        scheme.LineNumbers = ConsoleColor.DarkMagenta;
        scheme.CurrentLineNumber = ConsoleColor.Cyan;
        scheme.Selector = ConsoleColor.Blue;
        scheme.SelectorText = ConsoleColor.White;
        scheme.UseLogo = "Reflections";
        return scheme;
    }

    /// <summary>
    /// The color schemes that come with Marbles (the first is the default)
    /// </summary>
    internal static readonly IReadOnlyList<ColorScheme> BuiltIn = new[]
    {
        DefaultScheme(),
        DeepBlue(),
        Make("Marbles Quest", null, ConsoleColor.Green, ConsoleColor.Cyan, ConsoleColor.Blue, ConsoleColor.Magenta,
            ConsoleColor.Magenta, ConsoleColor.Green, ConsoleColor.DarkBlue, ConsoleColor.Cyan, ConsoleColor.Yellow,
            ConsoleColor.Black, ConsoleColor.Red, ConsoleColor.White),
        TastyBytes(),
        Make("Yellow Monochrome", null, ConsoleColor.Yellow, ConsoleColor.DarkYellow, ConsoleColor.DarkYellow,
            ConsoleColor.Yellow, ConsoleColor.Yellow, ConsoleColor.Yellow, ConsoleColor.DarkYellow,
            ConsoleColor.Yellow, ConsoleColor.Yellow, ConsoleColor.DarkYellow, ConsoleColor.Yellow,
            ConsoleColor.DarkYellow),
        Make("Green Monochrome", null, ConsoleColor.Green, ConsoleColor.Green, ConsoleColor.DarkGreen,
            ConsoleColor.Green, ConsoleColor.Green, ConsoleColor.Green, ConsoleColor.DarkGreen, ConsoleColor.Green,
            ConsoleColor.Green, ConsoleColor.DarkGreen, ConsoleColor.Green, ConsoleColor.DarkGreen),
        Make("Ice Warp", null, ConsoleColor.White, ConsoleColor.Cyan, ConsoleColor.Blue, ConsoleColor.Magenta,
            ConsoleColor.Yellow, ConsoleColor.Green, ConsoleColor.DarkBlue, ConsoleColor.Cyan, ConsoleColor.Yellow,
            ConsoleColor.Black, ConsoleColor.Red, ConsoleColor.White),
        Make("Water Sport", null, ConsoleColor.DarkCyan, ConsoleColor.Green, ConsoleColor.DarkCyan,
            ConsoleColor.Yellow, ConsoleColor.White, ConsoleColor.Red, ConsoleColor.DarkGreen, ConsoleColor.White,
            ConsoleColor.DarkCyan, ConsoleColor.White, ConsoleColor.DarkMagenta, ConsoleColor.White)
    };

    /// <summary>
    /// The default color scheme (a copy)
    /// </summary>
    internal static ColorScheme Default => BuiltIn[0].Clone();

    #endregion
}

/// <summary>
/// The color schemes: the built-in ones, and the ones made by the user, kept as files in the directory
/// "colorschemes" in the data directory (the name of the scheme and ".txt")
/// </summary>
internal static class ColorSchemes
{
    internal const string DirectoryName = "colorschemes";
    private const string Extension = ".txt";

    /// <summary>
    /// The directory of the schemes. It can be set to another directory (for testing)
    /// </summary>
    internal static string? DirectoryOverride { get; set; }

    internal static string Directory =>
        DirectoryOverride ?? Path.Combine(FileIo.GetDataDirectoryName(), DirectoryName);

    internal static bool IsBuiltIn(string name) =>
        ColorScheme.BuiltIn.Any(scheme => scheme.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// A name a scheme can be saved with: not empty, not the name of a built-in scheme, and usable as a file name
    /// </summary>
    internal static bool IsValidName(string name)
    {
        name = name.Trim();
        return name.Length is > 0 and <= 40 && !IsBuiltIn(name) && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
               name.IndexOfAny(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' }) < 0 && name != "." && name != "..";
    }

    private static string PathOf(string name) => Path.Combine(Directory, name.Trim() + Extension);

    /// <summary>
    /// The names of the schemes made by the user (in alphabetical order)
    /// </summary>
    internal static List<string> CustomNames()
    {
        try
        {
            if (!System.IO.Directory.Exists(Directory)) return new List<string>();
            return System.IO.Directory.GetFiles(Directory, "*" + Extension)
                .Select(file => Path.GetFileNameWithoutExtension(file)!)
                .Where(name => !IsBuiltIn(name))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// All the schemes by name: the built-in ones, then the ones made by the user
    /// </summary>
    internal static List<string> AllNames() =>
        ColorScheme.BuiltIn.Select(scheme => scheme.Name).Concat(CustomNames()).ToList();

    /// <summary>
    /// A scheme by its name (a copy), or null if there is none with the name
    /// </summary>
    internal static ColorScheme? Load(string name)
    {
        name = name.Trim();
        var builtIn = ColorScheme.BuiltIn.FirstOrDefault(scheme =>
            scheme.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (builtIn != null) return builtIn.Clone();
        if (!IsValidName(name)) return null;
        try
        {
            var path = PathOf(name);
            return File.Exists(path) ? ColorScheme.Parse(File.ReadAllText(path, Encoding.UTF8), name) : null;
        }
        catch
        {
            return null;
        }
    }

    internal static bool Exists(string name) => IsBuiltIn(name) || (IsValidName(name) && File.Exists(PathOf(name)));

    /// <summary>
    /// Save a scheme made by the user (its name is the name of the file)
    /// </summary>
    /// <returns>DialogText.Success or an error message</returns>
    internal static string Save(ColorScheme scheme)
    {
        if (!IsValidName(scheme.Name)) return DialogText.SchemeNameInvalid;
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            FileIo.WriteAllBytesSafe(PathOf(scheme.Name), new UTF8Encoding(false).GetBytes(scheme.Serialize()));
            return DialogText.Success;
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }

    /// <summary>
    /// Give a scheme made by the user another name (its file is renamed). Another scheme with the name is not
    /// replaced
    /// </summary>
    /// <returns>DialogText.Success or an error message</returns>
    internal static string Rename(string name, string newName)
    {
        if (IsBuiltIn(newName)) return DialogText.SchemeNameBuiltIn;
        if (!IsValidName(name) || !IsValidName(newName)) return DialogText.SchemeNameInvalid;
        try
        {
            if (!File.Exists(PathOf(name))) return DialogText.SchemeNameInvalid;
            var sameScheme = name.Trim().Equals(newName.Trim(), StringComparison.OrdinalIgnoreCase);
            if (!sameScheme && File.Exists(PathOf(newName))) return DialogText.SchemeNameTaken;
            if (name.Trim() != newName.Trim()) File.Move(PathOf(name), PathOf(newName));
            return DialogText.Success;
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }

    /// <summary>
    /// Make a copy of a scheme (a built-in one, or one made by the user) as a scheme of the user's own with
    /// another name. Another scheme with the name is not replaced
    /// </summary>
    /// <returns>DialogText.Success or an error message</returns>
    internal static string Duplicate(string name, string newName)
    {
        if (IsBuiltIn(newName)) return DialogText.SchemeNameBuiltIn;
        if (!IsValidName(newName)) return DialogText.SchemeNameInvalid;
        if (Exists(newName)) return DialogText.SchemeNameTaken;
        var copy = Load(name);
        if (copy == null) return DialogText.SchemeNameInvalid;
        copy.Name = newName.Trim();
        return Save(copy);
    }

    /// <summary>
    /// Delete a scheme made by the user
    /// </summary>
    /// <returns>DialogText.Success or an error message</returns>
    internal static string Delete(string name)
    {
        if (!IsValidName(name)) return DialogText.SchemeNameInvalid;
        try
        {
            File.Delete(PathOf(name));
            return DialogText.Success;
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }

    /// <summary>
    /// The name of a scheme as shown (the default scheme in the language in use)
    /// </summary>
    internal static string DisplayName(string name) =>
        name.Equals(ColorScheme.DefaultName, StringComparison.OrdinalIgnoreCase) ? Lang.T("Default") : name;
}
