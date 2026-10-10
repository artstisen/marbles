namespace Marbles;

/// <summary>
/// A box drawn on top of the page, which is not cleared: for asking before something is done (deleting a
/// file ...), or for a name. The caller draws the page again when the box is closed
/// </summary>
internal static class ChoiceBox
{
    private const int MinInner = 40; // The narrowest box (inside the frame)

    /// <summary>
    /// Ask yes or no. Y and N answer at once, j/k select and Enter picks, Esc is no
    /// </summary>
    /// <param name="caption">The title of the box</param>
    /// <param name="question">The question, highlighted</param>
    /// <param name="note">Shown below the question (can be empty)</param>
    /// <param name="warning">True for something that cannot be undone: the question is shown as a warning,
    /// and No is selected from the start</param>
    /// <returns>True for yes</returns>
    internal static bool Confirm(string caption, string question, string note, bool warning)
    {
        return Pick(caption, question, note, new[] { DialogText.ChoiceYes, DialogText.ChoiceNo },
            new[] { "Y", "N" }, warning ? 1 : 0, warning) == 0;
    }

    /// <summary>
    /// Pick one of a few choices. The key of each choice is shown in front of it and picks it at once
    /// </summary>
    /// <returns>The number of the choice, or -1 if the box was closed with Esc</returns>
    internal static int Pick(string caption, string question, string note, IReadOnlyList<string> choices,
        IReadOnlyList<string> keys, int selected, bool warning)
    {
        var keyWidth = keys.Max(TextMetrics.WidthOf);
        var widest = Math.Max(TextMetrics.WidthOf(caption) + 4,
            Math.Max(TextMetrics.WidthOf(question) + 4, choices.Max(TextMetrics.WidthOf) + keyWidth + 12));
        var inner = Math.Clamp(Math.Max(widest, MinInner), 10, Init.LineWidth - 6);
        var questionLines = Wrap(question, inner - 4);
        var noteLines = note.Length == 0 ? new List<string>() : Wrap(note, inner - 4);
        // A blank row, the question, the note, a blank row, the choices and a blank row
        var textRows = questionLines.Count + noteLines.Count;
        var choiceTop = 2 + textRows;
        var height = choiceTop + choices.Count + 1;
        var left = Math.Max(0, (Init.LineWidth - inner - 2) / 2);
        var top = Math.Max(1, (Init.LineCount - height - 2) / 2);

        Tools.HideCursor();
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Tools.SetCursorEx(top, left);
        Console.Write(AsciiStyles.GetTopCorner() + new string('-', inner) + AsciiStyles.GetTopCorner());
        for (var row = 0; row < height; row++)
        {
            Tools.SetCursorEx(top + 1 + row, left);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write('|');
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            var used = 0;
            var line = row - 1;
            if (line >= 0 && line < questionLines.Count)
            {
                Console.Write(" ");
                ConsoleColors.SetColors(warning
                    ? ConsoleColors.ColorPreset.WarningInverted
                    : ConsoleColors.ColorPreset.AttentionInverted);
                Console.Write(" " + questionLines[line] + " ");
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                used = TextMetrics.WidthOf(questionLines[line]) + 3;
            }
            else if (line >= questionLines.Count && line < textRows)
            {
                var text = "  " + noteLines[line - questionLines.Count];
                Console.Write(text);
                used = TextMetrics.WidthOf(text);
            }
            else if (row >= choiceTop && row < choiceTop + choices.Count)
            {
                var number = row - choiceTop;
                var text = "   " + keys[number].PadRight(keyWidth) + " .... " +
                           Tools.TruncateString(choices[number], inner - keyWidth - 11, true);
                Console.Write(text);
                used = TextMetrics.WidthOf(text);
            }

            Console.Write(new string(' ', Math.Max(0, inner - used)));
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write('|');
        }

        Tools.SetCursorEx(top + height + 1, left);
        Console.Write(AsciiStyles.GetBottomCorner() + new string('-', inner) + AsciiStyles.GetBottomCorner());
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.HeaderColor);
        Tools.SetCursorEx(top, left + 2);
        Console.Write(" " + Tools.TruncateString(caption, inner - 4, true) + " ");
        // The keys of the box
        Tools.SetCursorEx(Init.LineCount - 1, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Console.Write(new string(' ', Init.LineWidth - 1));
        Tools.SetCursorEx(Init.LineCount - 1, 0);
        Menu.WriteKeyHints(DialogText.ChoiceKeys);

        while (true)
        {
            for (var i = 0; i < choices.Count; i++)
            {
                Tools.SetCursorEx(top + 1 + choiceTop + i, left + 1);
                ConsoleColors.SetColors(i == selected
                    ? ConsoleColors.ColorPreset.HighlightInverted
                    : ConsoleColors.ColorPreset.Normal);
                Console.Write(i == selected ? "->" : "  ");
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            }

            Tools.SetCursorEx(Init.LineCount - 1, 0);
            var key = Console.ReadKey(true);
            var navigation = Keyboard.ToNavigationKey(key).Key;
            if (key.Key is ConsoleKey.Escape or ConsoleKey.Q || navigation == ConsoleKey.LeftArrow) return -1;
            if (key.Key is ConsoleKey.Enter or ConsoleKey.Spacebar || navigation == ConsoleKey.RightArrow)
            {
                return selected;
            }

            // The selection stops at the first and the last choice (it does not go round from No to Yes)
            if (navigation == ConsoleKey.DownArrow) selected = Math.Min(selected + 1, choices.Count - 1);
            if (navigation == ConsoleKey.UpArrow) selected = Math.Max(selected - 1, 0);
            // gg and G: the first and the last choice
            if (key.KeyChar == 'G') selected = choices.Count - 1;
            if (key.KeyChar == 'g' && Console.ReadKey(true).KeyChar == 'g') selected = 0;
            // The key of a choice picks it
            var typed = char.ToUpperInvariant(key.KeyChar).ToString();
            for (var i = 0; i < keys.Count; i++)
            {
                if (keys[i] == typed) return i;
            }
        }
    }

    /// <summary>
    /// Ask for a text (a name) in a box: a line telling what to do, and a field to type in
    /// </summary>
    /// <param name="caption">The title of the box</param>
    /// <param name="prompt">What to do, above the field</param>
    /// <param name="label">In front of the field ("Name:")</param>
    /// <param name="text">The text the field starts with (the cursor is placed at its end)</param>
    /// <returns>The text typed (trimmed), or null if canceled or empty</returns>
    internal static string? AskText(string caption, string prompt, string label, string text)
    {
        var inner = Math.Clamp(Math.Max(TextMetrics.WidthOf(prompt) + 4, 64), 10, Init.LineWidth - 6);
        var promptLines = Wrap(prompt, inner - 4);
        // A blank row, the prompt, a blank row, the field and a blank row
        var fieldRow = promptLines.Count + 2;
        var height = fieldRow + 2;
        var left = Math.Max(0, (Init.LineWidth - inner - 2) / 2);
        var top = Math.Max(1, (Init.LineCount - height - 2) / 2);

        Tools.HideCursor();
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Tools.SetCursorEx(top, left);
        Console.Write(AsciiStyles.GetTopCorner() + new string('-', inner) + AsciiStyles.GetTopCorner());
        for (var row = 0; row < height; row++)
        {
            Tools.SetCursorEx(top + 1 + row, left);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write('|');
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            var line = row >= 1 && row <= promptLines.Count ? "  " + promptLines[row - 1] : string.Empty;
            if (row == fieldRow) line = "  " + label;
            Console.Write(line + new string(' ', Math.Max(0, inner - TextMetrics.WidthOf(line))));
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write('|');
        }

        Tools.SetCursorEx(top + height + 1, left);
        Console.Write(AsciiStyles.GetBottomCorner() + new string('-', inner) + AsciiStyles.GetBottomCorner());
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.HeaderColor);
        Tools.SetCursorEx(top, left + 2);
        Console.Write(" " + Tools.TruncateString(caption, inner - 4, true) + " ");
        // The keys of the box
        Tools.SetCursorEx(Init.LineCount - 1, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Console.Write(new string(' ', Init.LineWidth - 1));
        Tools.SetCursorEx(Init.LineCount - 1, 0);
        Menu.WriteKeyHints(DialogText.AskTextKeys);

        // The field takes the rest of the row
        var fieldLeft = left + 3 + TextMetrics.WidthOf(label);
        var textArea = new TextArea(fieldLeft, top + 1 + fieldRow, left + inner - fieldLeft, 1, text, 0,
            text.Length > 0, false, false, 0);
        var typed = textArea.GetText().Trim();
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        return textArea.Canceled || typed.Length == 0 ? null : typed;
    }

    /// <summary>
    /// Break a text into lines no wider than a width, between words (a word that is too long is cut)
    /// </summary>
    internal static List<string> Wrap(string text, int width)
    {
        var lines = new List<string>();
        var line = string.Empty;
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var joined = line.Length == 0 ? word : line + " " + word;
            if (TextMetrics.WidthOf(joined) <= width)
            {
                line = joined;
                continue;
            }

            if (line.Length > 0) lines.Add(line);
            line = TextMetrics.WidthOf(word) <= width ? word : Tools.TruncateString(word, width, true);
        }

        if (line.Length > 0 || lines.Count == 0) lines.Add(line);
        return lines;
    }
}
