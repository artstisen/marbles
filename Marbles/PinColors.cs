namespace Marbles;

/// <summary>
/// The text color of a pinned entry. The color is kept as a tag (one letter) in the options of the entry:
/// "A" to "P" for the 16 colors of the console. Data files from earlier versions have the tags "h", "i"
/// and "j" (three highlight styles), which are shown as the colors these styles had by default.
/// </summary>
internal static class PinColors
{
    private const string Tags = "ABCDEFGHIJKLMNOP"; // In the order of the ConsoleColor values
    private const string LegacyTags = "hij";

    private static readonly ConsoleColor[] LegacyColors =
        { ConsoleColor.Cyan, ConsoleColor.Yellow, ConsoleColor.Red };

    /// <summary>
    /// The number of colors to select from
    /// </summary>
    internal const int Count = 16;

    /// <summary>
    /// The color set in the options of an entry, or null if none is set
    /// </summary>
    internal static ConsoleColor? Get(string? options)
    {
        if (string.IsNullOrEmpty(options)) return null;
        foreach (var c in options)
        {
            var index = Tags.IndexOf(c);
            if (index >= 0) return (ConsoleColor)index;
        }

        foreach (var c in options)
        {
            var index = LegacyTags.IndexOf(c);
            if (index >= 0) return LegacyColors[index];
        }

        return null;
    }

    /// <summary>
    /// Set the color in the options of an entry
    /// </summary>
    /// <param name="options">The options</param>
    /// <param name="color">The color, or null to remove the color</param>
    /// <returns>The new options</returns>
    internal static string Set(string? options, ConsoleColor? color)
    {
        var result = new string((options ?? string.Empty)
            .Where(c => !Tags.Contains(c) && !LegacyTags.Contains(c)).ToArray());
        return color == null ? result : result + Tags[(int)color.Value];
    }

    /// <summary>
    /// The name of a color: "Dark Blue"
    /// </summary>
    internal static string Name(ConsoleColor color)
    {
        var name = color.ToString();
        return name.StartsWith("Dark", StringComparison.Ordinal) && name.Length > 4 ? "Dark " + name[4..] : name;
    }

    /// <summary>
    /// Set the colors of the console for writing the title of an entry
    /// </summary>
    /// <param name="entry">The entry</param>
    /// <param name="otherwise">The colors to use when the entry has no color of its own</param>
    internal static void Apply(Entry entry, ConsoleColors.ColorPreset otherwise)
    {
        var color = entry.Options.Contains(Entries.PinnedTag) ? Get(entry.Options) : null;
        if (color == null)
        {
            ConsoleColors.SetColors(otherwise);
        }
        else
        {
            ConsoleColors.SetTextColor(color.Value);
        }
    }

    private const int BoxTop = 2;
    private const int BoxLeft = 28;
    private const int BoxWidth = 24; // Between the borders

    /// <summary>
    /// Let the user pick a color from a list shown on top of the page
    /// </summary>
    /// <param name="current">The color that is selected when the list opens</param>
    /// <param name="picked">The color picked, or null for the default color</param>
    /// <returns>False if the list was closed without picking</returns>
    internal static bool Pick(ConsoleColor? current, out ConsoleColor? picked)
    {
        picked = current;
        // The first row is the default color, then the colors follow
        var selected = current == null ? 0 : (int)current.Value + 1;
        Tools.HideCursor();
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Tools.SetCursorEx(BoxTop, BoxLeft);
        Console.Write(AsciiStyles.GetTopCorner() + new string('-', BoxWidth) + AsciiStyles.GetTopCorner());
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.HeaderColor);
        Tools.SetCursorEx(BoxTop, BoxLeft + 2);
        Console.Write(" " + DialogText.PinColorCaption + " ");
        for (var row = 0; row <= Count; row++)
        {
            Tools.SetCursorEx(BoxTop + 1 + row, BoxLeft);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write('|');
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            var number = row == 0 ? "  " : row.ToString().PadLeft(2);
            Console.Write("    " + number + "  ");
            var name = row == 0 ? DialogText.PinColorDefault : Name((ConsoleColor)(row - 1));
            if (row == 0)
            {
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Favorites);
            }
            else if (ConsoleColors.IsBackgroundColor((ConsoleColor)(row - 1)))
            {
                // The color of the background cannot be seen as text: it is shown inverted
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.NormalInverted);
            }
            else
            {
                Console.ForegroundColor = (ConsoleColor)(row - 1);
            }

            Console.Write(name);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Console.Write(new string(' ', BoxWidth - 8 - name.Length));
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write('|');
        }

        Tools.SetCursorEx(BoxTop + Count + 2, BoxLeft);
        Console.Write(AsciiStyles.GetBottomCorner() + new string('-', BoxWidth) + AsciiStyles.GetBottomCorner());
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);

        while (true)
        {
            for (var row = 0; row <= Count; row++)
            {
                Tools.SetCursorEx(BoxTop + 1 + row, BoxLeft + 2);
                if (row == selected) ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
                Console.Write(row == selected ? "->" : "  ");
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            }

            var key = Console.ReadKey(true);
            if (key.Key is ConsoleKey.Escape or ConsoleKey.Q) return false;
            if (key.Key is ConsoleKey.Enter or ConsoleKey.Spacebar)
            {
                picked = selected == 0 ? null : (ConsoleColor)(selected - 1);
                return true;
            }

            if (key.Key is ConsoleKey.J or ConsoleKey.DownArrow)
            {
                selected = (selected + 1) % (Count + 1);
            }
            else if (key.Key is ConsoleKey.K or ConsoleKey.UpArrow)
            {
                selected = (selected + Count) % (Count + 1);
            }
            else if (key.Key == ConsoleKey.Home || key.KeyChar == 'g')
            {
                selected = 0;
            }
            else if (key.Key == ConsoleKey.End || key.KeyChar == 'G')
            {
                selected = Count;
            }
        }
    }
}
