namespace Marbles;

/// <summary>
/// The numbers of the rows of a list, as 12gg counts them (on or off for each list: the settings on the Lists page of
/// Configuration, and ":number" in the list). They have the colors of the line numbers of the editor: the number of
/// the selected row the color of the current line number
/// </summary>
internal static class ListNumbers
{
    /// <summary>
    /// The cells the numbers take in front of the rows: the largest number and a blank space (0 when they are off,
    /// or the list is empty)
    /// </summary>
    internal static int Width(bool on, int count) => on && count > 0 ? count.ToString().Length + 1 : 0;

    /// <summary>
    /// Write the number of a row (counted from 0) at the cursor, right-aligned in the width (nothing when it is 0)
    /// </summary>
    internal static void Write(int index, int width, bool selected)
    {
        if (width == 0) return;
        ConsoleColors.SetColors(selected
            ? ConsoleColors.ColorPreset.CurrentLineNumber
            : ConsoleColors.ColorPreset.LineNumber);
        Console.Write((index + 1).ToString().PadLeft(width - 1) + " ");
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }
}
