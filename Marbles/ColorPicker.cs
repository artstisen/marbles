namespace Marbles;

/// <summary>
/// A list of the 16 colors of the console, shown in a box on top of the page, to pick a color from
/// </summary>
internal static class ColorPicker
{
    /// <summary>
    /// The number of colors to select from
    /// </summary>
    internal const int Count = 16;

    private const int BoxTop = 3; // The tab of the title is on the row above
    private const int BoxWidth = 24; // Between the borders

    /// <summary>
    /// The name of a color: "Dark Blue"
    /// </summary>
    internal static string Name(ConsoleColor color)
    {
        var name = color.ToString();
        // The names are listed in Lang.Names
        return Lang.T(name.StartsWith("Dark", StringComparison.Ordinal) && name.Length > 4 ? "Dark " + name[4..] : name);
    }

    /// <summary>
    /// Let the user pick a color
    /// </summary>
    /// <param name="caption">The caption of the box</param>
    /// <param name="left">The column of the box</param>
    /// <param name="withDefault">True to have "Default" (no color) as the first choice</param>
    /// <param name="current">The color that is selected when the list opens</param>
    /// <param name="isAllowed">Tells if a color can be picked (null = all can)</param>
    /// <param name="setSampleColors">Sets the colors of the console for writing the name of a color
    /// (null = the color as text on the normal background)</param>
    /// <param name="picked">The color picked, or null for the default</param>
    /// <returns>False if the list was closed without picking</returns>
    internal static bool Pick(string caption, int left, bool withDefault, ConsoleColor? current,
        Func<ConsoleColor, bool>? isAllowed, Action<ConsoleColor>? setSampleColors, out ConsoleColor? picked)
    {
        picked = current;
        var first = withDefault ? 1 : 0; // The row of the first color
        var rows = Count + first;
        var selected = current == null ? 0 : (int)current.Value + first;
        bool CanSelect(int row) => row < first || isAllowed == null || isAllowed((ConsoleColor)(row - first));

        Tools.HideCursor();
        ChoiceBox.DrawTitleTab(BoxTop - 1, left, BoxWidth, caption);
        for (var row = 0; row < rows; row++)
        {
            Tools.SetCursorEx(BoxTop + 1 + row, left);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write('|');
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            var number = row < first ? "  " : (row - first + 1).ToString().PadLeft(2);
            Console.Write("    " + number + "  ");
            string name;
            if (row < first)
            {
                // Not in a color of its own: it stands for the color the item has without one
                name = DialogText.ColorDefault;
            }
            else
            {
                var color = (ConsoleColor)(row - first);
                name = Name(color);
                if (!CanSelect(row))
                {
                    // In use for the text or background it would be shown on
                    ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                    name = "(" + name + ")";
                }
                else if (ConsoleColors.Monochrome)
                {
                    // The colors are not shown in black and white
                }
                else if (setSampleColors != null)
                {
                    setSampleColors(color);
                }
                else if (ConsoleColors.IsBackgroundColor(color))
                {
                    // The color of the background cannot be seen as text: it is shown inverted
                    ConsoleColors.SetColors(ConsoleColors.ColorPreset.NormalInverted);
                }
                else
                {
                    Console.ForegroundColor = color;
                }
            }

            Console.Write(name);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Console.Write(new string(' ', BoxWidth - 8 - name.Length));
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write('|');
        }

        Tools.SetCursorEx(BoxTop + rows + 1, left);
        Console.Write(AsciiStyles.GetBottomCorner() + new string('-', BoxWidth) + AsciiStyles.GetBottomCorner());
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);

        while (true)
        {
            for (var row = 0; row < rows; row++)
            {
                Tools.SetCursorEx(BoxTop + 1 + row, left + 2);
                if (row == selected) ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
                Console.Write(row == selected ? "->" : "  ");
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            }

            var key = Console.ReadKey(true);
            if (key.Key is ConsoleKey.Escape or ConsoleKey.Q) return false;
            if (key.Key is ConsoleKey.Enter or ConsoleKey.Spacebar or ConsoleKey.RightArrow || key.KeyChar == 'l')
            {
                if (!CanSelect(selected)) continue;
                picked = selected < first ? null : (ConsoleColor)(selected - first);
                return true;
            }

            // Move the selection, passing the colors that cannot be picked
            var step = 0;
            if (key.Key is ConsoleKey.J or ConsoleKey.DownArrow)
            {
                step = 1;
            }
            else if (key.Key is ConsoleKey.K or ConsoleKey.UpArrow)
            {
                step = -1;
            }
            else if (key.Key == ConsoleKey.Home || key.KeyChar == 'g')
            {
                selected = rows - 1;
                step = 1;
            }
            else if (key.Key == ConsoleKey.End || key.KeyChar == 'G')
            {
                selected = 0;
                step = -1;
            }

            if (step == 0) continue;
            for (var tries = 0; tries < rows; tries++)
            {
                selected = (selected + step + rows) % rows;
                if (CanSelect(selected)) break;
            }
        }
    }
}
