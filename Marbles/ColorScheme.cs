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
    PreviewTitle,
    EditorTitle,
    Highlight, // The background of the highlight (inverted)
    HighlightText,
    Attention,
    AttentionText,
    Warning,
    WarningText,
    TabFrames, // The frames with tabs (null: the color of the dividers)
    BoxBorders, // The borders of the boxes on top of a page (null: the color of the dividers)
    Tab, // The background of the tab shown (null: the color of the tab frames)
    TabText, // The text of the tab shown when it is highlighted (null: the color of TabShown)
    TabShown, // The name of the tab shown when it is not highlighted (null: the text)
    OtherTabs, // The names of the other tabs (null: the text)
    KeyHints, // The keys on the bottom line (null: the text)
    Unsaved, // The background of the label "Unsaved" (null: the text)
    UnsavedText // The text of the label "Unsaved" (null: the background)
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
    internal ConsoleColor PreviewTitle { get; set; } = ConsoleColor.Yellow;
    internal ConsoleColor EditorTitle { get; set; } = ConsoleColor.Green;
    internal ConsoleColor Highlight { get; set; } = ConsoleColor.Cyan;
    internal ConsoleColor HighlightText { get; set; } = ConsoleColor.Black;
    internal ConsoleColor Attention { get; set; } = ConsoleColor.Yellow;
    internal ConsoleColor AttentionText { get; set; } = ConsoleColor.Black;
    internal ConsoleColor Warning { get; set; } = ConsoleColor.Red;
    internal ConsoleColor WarningText { get; set; } = ConsoleColor.White;
    internal ConsoleColor? TabFrames { get; set; }
    internal ConsoleColor? BoxBorders { get; set; }
    internal ConsoleColor? Tab { get; set; }
    internal ConsoleColor? TabText { get; set; }
    internal ConsoleColor? TabShown { get; set; }
    internal ConsoleColor? OtherTabs { get; set; }
    internal ConsoleColor? KeyHints { get; set; }
    internal ConsoleColor? Unsaved { get; set; }
    internal ConsoleColor? UnsavedText { get; set; }

    /// <summary>
    /// The tab (and sub-tab) shown is highlighted in the colors of the tabs; else only its frame tells it apart
    /// </summary>
    internal bool TabHighlight { get; set; } = true;

    // The colors as used (the ones that can be "default" have the color they stand for)
    internal ConsoleColor BackgroundColor => Background ?? ConsoleColor.Black;
    internal ConsoleColor TabFrameColor => TabFrames ?? Dividers;
    internal ConsoleColor BoxBorderColor => BoxBorders ?? Dividers;
    internal ConsoleColor TabColor => Tab ?? TabFrameColor;
    internal ConsoleColor TabTextColor => TabText ?? TabShownColor;
    internal ConsoleColor TabShownColor => TabShown ?? Text;
    internal ConsoleColor OtherTabsColor => OtherTabs ?? Text;
    internal ConsoleColor KeyHintsColor => KeyHints ?? Text;
    internal ConsoleColor UnsavedColor => Unsaved ?? Text;
    internal ConsoleColor UnsavedTextColor => UnsavedText ?? BackgroundColor;

    internal ColorScheme Clone() => (ColorScheme)MemberwiseClone();

    /// <summary>
    /// True if the color can be "default" (it then has the color of another item)
    /// </summary>
    internal static bool CanBeDefault(ColorItem item) =>
        item is ColorItem.Background or ColorItem.TabFrames or ColorItem.BoxBorders or ColorItem.Tab
            or ColorItem.TabText or ColorItem.TabShown or ColorItem.OtherTabs or ColorItem.KeyHints
            or ColorItem.Unsaved or ColorItem.UnsavedText;

    internal ConsoleColor? Get(ColorItem item)
    {
        return item switch
        {
            ColorItem.Background => Background,
            ColorItem.Text => Text,
            ColorItem.Logo => Logo,
            ColorItem.Dividers => Dividers,
            ColorItem.Favorites => Favorites,
            ColorItem.PreviewTitle => PreviewTitle,
            ColorItem.EditorTitle => EditorTitle,
            ColorItem.Highlight => Highlight,
            ColorItem.HighlightText => HighlightText,
            ColorItem.Attention => Attention,
            ColorItem.AttentionText => AttentionText,
            ColorItem.Warning => Warning,
            ColorItem.WarningText => WarningText,
            ColorItem.TabFrames => TabFrames,
            ColorItem.BoxBorders => BoxBorders,
            ColorItem.Tab => Tab,
            ColorItem.TabText => TabText,
            ColorItem.TabShown => TabShown,
            ColorItem.OtherTabs => OtherTabs,
            ColorItem.KeyHints => KeyHints,
            ColorItem.Unsaved => Unsaved,
            _ => UnsavedText
        };
    }

    /// <summary>
    /// The color an item is shown in (for the ones that are "default": the color they stand for)
    /// </summary>
    internal ConsoleColor Shown(ColorItem item)
    {
        return item switch
        {
            ColorItem.Background => BackgroundColor,
            ColorItem.TabFrames => TabFrameColor,
            ColorItem.BoxBorders => BoxBorderColor,
            ColorItem.Tab => TabColor,
            ColorItem.TabText => TabTextColor,
            ColorItem.TabShown => TabShownColor,
            ColorItem.OtherTabs => OtherTabsColor,
            ColorItem.KeyHints => KeyHintsColor,
            ColorItem.Unsaved => UnsavedColor,
            ColorItem.UnsavedText => UnsavedTextColor,
            _ => Get(item)!.Value
        };
    }

    /// <summary>
    /// Set a color (null: "default", only for the items that can have it)
    /// </summary>
    internal void Set(ColorItem item, ConsoleColor? color)
    {
        if (color == null && !CanBeDefault(item)) return;
        switch (item)
        {
            case ColorItem.Background: Background = color; break;
            case ColorItem.Text: Text = color!.Value; break;
            case ColorItem.Logo: Logo = color!.Value; break;
            case ColorItem.Dividers: Dividers = color!.Value; break;
            case ColorItem.Favorites: Favorites = color!.Value; break;
            case ColorItem.PreviewTitle: PreviewTitle = color!.Value; break;
            case ColorItem.EditorTitle: EditorTitle = color!.Value; break;
            case ColorItem.Highlight: Highlight = color!.Value; break;
            case ColorItem.HighlightText: HighlightText = color!.Value; break;
            case ColorItem.Attention: Attention = color!.Value; break;
            case ColorItem.AttentionText: AttentionText = color!.Value; break;
            case ColorItem.Warning: Warning = color!.Value; break;
            case ColorItem.WarningText: WarningText = color!.Value; break;
            case ColorItem.TabFrames: TabFrames = color; break;
            case ColorItem.BoxBorders: BoxBorders = color; break;
            case ColorItem.Tab: Tab = color; break;
            case ColorItem.TabText: TabText = color; break;
            case ColorItem.TabShown: TabShown = color; break;
            case ColorItem.OtherTabs: OtherTabs = color; break;
            case ColorItem.KeyHints: KeyHints = color; break;
            case ColorItem.Unsaved: Unsaved = color; break;
            default: UnsavedText = color; break;
        }
    }

    // The text colors shown on the background, and the pairs of a background and the text on it
    private static readonly ColorItem[] OnBackground =
    {
        ColorItem.Text, ColorItem.Logo, ColorItem.Dividers, ColorItem.Favorites, ColorItem.PreviewTitle,
        ColorItem.EditorTitle, ColorItem.TabFrames, ColorItem.BoxBorders, ColorItem.TabShown, ColorItem.OtherTabs,
        ColorItem.KeyHints
    };

    private static readonly (ColorItem Background, ColorItem Text)[] Pairs =
    {
        (ColorItem.Highlight, ColorItem.HighlightText), (ColorItem.Attention, ColorItem.AttentionText),
        (ColorItem.Warning, ColorItem.WarningText), (ColorItem.Tab, ColorItem.TabText),
        (ColorItem.Unsaved, ColorItem.UnsavedText)
    };

    /// <summary>
    /// False if a color cannot be used for an item, as text would not be visible on its background: the background
    /// cannot have the color of the text on it, and the text and background of a highlight cannot be the same
    /// </summary>
    internal bool Allowed(ColorItem item, ConsoleColor color)
    {
        if (item == ColorItem.Background) return OnBackground.All(text => Shown(text) != color);
        if (item == ColorItem.TabShown && TabText == null && color == TabColor) return false;
        if (OnBackground.Contains(item)) return color != BackgroundColor;
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
        ("previewtitle", ColorItem.PreviewTitle), ("editortitle", ColorItem.EditorTitle),
        ("highlight", ColorItem.Highlight), ("highlighttext", ColorItem.HighlightText),
        ("attention", ColorItem.Attention), ("attentiontext", ColorItem.AttentionText),
        ("warning", ColorItem.Warning), ("warningtext", ColorItem.WarningText),
        ("tabframes", ColorItem.TabFrames), ("boxborders", ColorItem.BoxBorders), ("tab", ColorItem.Tab),
        ("tabtext", ColorItem.TabText), ("tabshown", ColorItem.TabShown), ("othertabs", ColorItem.OtherTabs),
        ("keyhints", ColorItem.KeyHints), ("unsaved", ColorItem.Unsaved), ("unsavedtext", ColorItem.UnsavedText)
    };

    private const string HighlightKey = "tabhighlight";
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
        return text.ToString();
    }

    /// <summary>
    /// A scheme from its text. What is missing or cannot be read has the colors of the default scheme
    /// </summary>
    internal static ColorScheme Parse(string text, string name)
    {
        var scheme = BuiltIn[0].Clone();
        scheme.Name = name;
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
            Favorites = favorites, PreviewTitle = previewTitle, EditorTitle = editorTitle, Highlight = highlight,
            HighlightText = highlightText, Attention = attention, AttentionText = attentionText, Warning = warning,
            WarningText = warningText
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

    /// <summary>
    /// The color schemes that come with Marbles (the first is the default)
    /// </summary>
    internal static readonly IReadOnlyList<ColorScheme> BuiltIn = new[]
    {
        WithTabs(Make(DefaultName, null, ConsoleColor.Gray, ConsoleColor.Cyan, ConsoleColor.DarkGray,
                ConsoleColor.Green, ConsoleColor.Yellow, ConsoleColor.Green, ConsoleColor.Cyan, ConsoleColor.Black,
                ConsoleColor.Yellow, ConsoleColor.Black, ConsoleColor.Red, ConsoleColor.White),
            null, ConsoleColor.Gray, ConsoleColor.DarkGray, null, ConsoleColor.Cyan, ConsoleColor.DarkGray, false),
        Make("Deep Blue", ConsoleColor.DarkBlue, ConsoleColor.Gray, ConsoleColor.Cyan, ConsoleColor.Cyan,
            ConsoleColor.Green, ConsoleColor.Magenta, ConsoleColor.White, ConsoleColor.Blue, ConsoleColor.Cyan,
            ConsoleColor.Yellow, ConsoleColor.Black, ConsoleColor.Red, ConsoleColor.White),
        Make("Marbles Quest", null, ConsoleColor.Green, ConsoleColor.Cyan, ConsoleColor.Blue, ConsoleColor.Magenta,
            ConsoleColor.Magenta, ConsoleColor.Green, ConsoleColor.DarkBlue, ConsoleColor.Cyan, ConsoleColor.Yellow,
            ConsoleColor.Black, ConsoleColor.Red, ConsoleColor.White),
        Make("Night Writer", null, ConsoleColor.Cyan, ConsoleColor.DarkGreen, ConsoleColor.Magenta,
            ConsoleColor.Magenta, ConsoleColor.Magenta, ConsoleColor.Green, ConsoleColor.DarkMagenta,
            ConsoleColor.Cyan, ConsoleColor.Magenta, ConsoleColor.White, ConsoleColor.Red, ConsoleColor.White),
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
