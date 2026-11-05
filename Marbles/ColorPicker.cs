namespace Marbles;

/// <summary>
/// A list of the 16 colors of the console, shown in a box on top of the page, to pick a color from. Each row has a
/// number and a key, as the choices of the start page: the number (1-9) selects the row, and the key picks it at
/// once. The keys are the same in all languages, and leave out the ones the box uses (g j k l q)
/// </summary>
internal static class ColorPicker
{
    /// <summary>
    /// The number of colors to select from
    /// </summary>
    internal const int Count = 16;

    private const int BoxTop = 3; // The tab of the title is on the row above
    private const int MinBoxWidth = 24; // Between the borders, at least
    private const int NameStart = 11; // "|  -> 12 A  ": where the name starts, from the left border

    // The keys of the colors (in the order of ConsoleColor), and of Default
    private const string ColorKeys = "ABCDEFHIMNOPRSTU";
    internal const char DefaultKey = 'X';

    /// <summary>
    /// The key that picks a color in the list at once
    /// </summary>
    internal static char KeyOf(ConsoleColor color) => ColorKeys[(int)color];

    /// <summary>
    /// The color of a key (upper or lower case), or null if the key is not one
    /// </summary>
    internal static ConsoleColor? ColorOfKey(char key)
    {
        var index = ColorKeys.IndexOf(char.ToUpperInvariant(key));
        return index < 0 ? null : (ConsoleColor)index;
    }

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
        // In the colors of boxes
        using var box = ConsoleColors.BoxScope();

        picked = current;
        var first = withDefault ? 1 : 0; // The row of the first color
        var rows = Count + first;
        var selected = current == null ? 0 : (int)current.Value + first;
        bool CanSelect(int row) => row < first || isAllowed == null || isAllowed((ConsoleColor)(row - first));

        // The box is as wide as the longest name (in brackets when it cannot be picked), and stays on the page
        var longest = TextMetrics.WidthOf(DialogText.ColorDefault);
        for (var color = 0; color < Count; color++)
        {
            longest = Math.Max(longest, TextMetrics.WidthOf(Name((ConsoleColor)color)) + 2);
        }

        var boxWidth = Math.Min(Math.Max(MinBoxWidth, NameStart - 1 + longest), Init.LineWidth - 2);
        left = Math.Max(0, Math.Min(left, Init.LineWidth - boxWidth - 2));

        Tools.HideCursor();
        ChoiceBox.DrawBox(BoxTop - 1, left, boxWidth, rows, caption);
        for (var row = 0; row < rows; row++)
        {
            Tools.SetCursorEx(BoxTop + 1 + row, left + 1);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            // The number and the key of the row
            var number = row < first ? "  " : (row - first + 1).ToString().PadLeft(2);
            var letter = row < first ? DefaultKey : KeyOf((ConsoleColor)(row - first));
            Console.Write("    " + number + " " + letter + "  ");
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
                else if (ConsoleColors.IsHidden(color))
                {
                    // The color of the background cannot be seen as text: it is shown inverted
                    ConsoleColors.SetColors(ConsoleColors.ColorPreset.NormalInverted);
                }
                else
                {
                    ConsoleColors.SetForeground(color);
                }
            }

            name = TextMetrics.StartPart(name, boxWidth - NameStart + 1);
            Console.Write(name);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Console.Write(new string(' ', Math.Max(0, boxWidth - NameStart + 1 - TextMetrics.WidthOf(name))));
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);

        while (true)
        {
            for (var row = 0; row < rows; row++)
            {
                Tools.SetCursorEx(BoxTop + 1 + row, left + 2);
                if (row == selected) ConsoleColors.SetColors(ConsoleColors.ColorPreset.SelectorInverted);
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

            // The key of a color (or of Default) picks it at once; one that cannot be picked is selected
            if ((key.Modifiers & (ConsoleModifiers.Control | ConsoleModifiers.Alt)) == 0)
            {
                var direct = char.ToUpperInvariant(key.KeyChar) == DefaultKey && first > 0 ? 0
                    : ColorOfKey(key.KeyChar) is { } keyColor ? (int)keyColor + first
                    : -1;
                if (direct >= 0)
                {
                    selected = direct;
                    if (!CanSelect(selected)) continue;
                    picked = selected < first ? null : (ConsoleColor)(selected - first);
                    return true;
                }

                // The number of a color selects it (then Enter picks it)
                if (key.KeyChar is >= '1' and <= '9' && CanSelect(key.KeyChar - '1' + first))
                {
                    selected = key.KeyChar - '1' + first;
                    continue;
                }
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
