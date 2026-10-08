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

    private const int BoxLeft = 28;

    /// <summary>
    /// Let the user pick a color from a list shown on top of the page
    /// </summary>
    /// <param name="current">The color that is selected when the list opens</param>
    /// <param name="picked">The color picked, or null for the default color</param>
    /// <returns>False if the list was closed without picking</returns>
    internal static bool Pick(ConsoleColor? current, out ConsoleColor? picked)
    {
        return ColorPicker.Pick(DialogText.PinColorCaption, BoxLeft, true, current, null, null, out picked);
    }
}
