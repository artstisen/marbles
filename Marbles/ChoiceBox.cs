namespace Marbles;

/// <summary>
/// A box drawn on top of the page, which is not cleared: for asking before something is done (deleting a
/// file ...), for a name or a key, or for a message. The caller draws the page again when the box is closed.
/// A box that follows another box (the page has not been drawn again in between) covers the one before it
/// </summary>
internal static class ChoiceBox
{
    private const int MinInner = 40; // The narrowest box (inside the frame)

    // Where the last box is (its frame), until the screen is cleared
    private static (int Top, int Left, int Right, int Bottom)? _shown;

    /// <summary>
    /// The screen has been cleared: no box is shown
    /// </summary>
    internal static void Forget()
    {
        _shown = null;
    }

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
        var (top, left, width, _) = DrawFrame(caption, inner, choiceTop + choices.Count + 1);
        WriteText(top, left, questionLines, noteLines, warning, width);
        for (var number = 0; number < choices.Count; number++)
        {
            Tools.SetCursorEx(top + 1 + choiceTop + number, left + 1);
            Console.Write("   " + keys[number].PadRight(keyWidth) + " .... " +
                          Tools.TruncateString(choices[number], width - keyWidth - 11, true));
        }

        WriteHints(DialogText.ChoiceKeys);
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
    /// Show a message (highlighted) and a note in a box until a key is pressed
    /// </summary>
    internal static void Message(string caption, string message, string note)
    {
        var inner = Math.Clamp(Math.Max(Math.Max(TextMetrics.WidthOf(message), TextMetrics.WidthOf(caption)) + 4,
            MinInner), 10, Init.LineWidth - 6);
        var messageLines = Wrap(message, inner - 4);
        var noteLines = note.Length == 0 ? new List<string>() : Wrap(note, inner - 4);
        // A blank row, the message, the note, a blank row, "Press any key" and a blank row
        var keyRow = 2 + messageLines.Count + noteLines.Count;
        var (top, left, _, _) = DrawFrame(caption, inner, keyRow + 2);
        WriteText(top, left, messageLines, noteLines, false, inner);
        Tools.SetCursorEx(top + 1 + keyRow, left + 3);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(DialogText.HitAnyKeyToContinue);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Tools.SetCursorEx(Init.LineCount - 1, 0);
        Console.Write(new string(' ', Init.LineWidth - 1));
        Tools.SetCursorEx(Init.LineCount - 1, 0);
        Console.ReadKey(true);
    }

    /// <summary>
    /// Pick a number (a year) in a box: h/l (or the arrow keys) change it, Enter accepts and Esc cancels
    /// </summary>
    /// <returns>The number picked, or null if canceled</returns>
    internal static int? PickNumber(string caption, string prompt, int value)
    {
        var inner = Math.Clamp(Math.Max(TextMetrics.WidthOf(prompt) + 4, MinInner), 10, Init.LineWidth - 6);
        var promptLines = Wrap(prompt, inner - 4);
        // A blank row, the prompt, a blank row, the number and a blank row
        var valueRow = promptLines.Count + 2;
        var (top, left, width, _) = DrawFrame(caption, inner, valueRow + 2);
        WriteText(top, left, new List<string>(), promptLines, false, width);
        WriteHints(DialogText.PickNumberKeys);
        while (true)
        {
            Tools.SetCursorEx(top + 1 + valueRow, left + 3);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Console.Write("<-  ");
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
            Console.Write(" " + value + " ");
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Console.Write("  ->   ");
            Tools.SetCursorEx(Init.LineCount - 1, 0);
            var key = Console.ReadKey(true);
            var navigation = Keyboard.ToNavigationKey(key).Key;
            if (key.Key is ConsoleKey.Escape or ConsoleKey.Q) return null;
            if (key.Key == ConsoleKey.Enter) return value;
            if (navigation is ConsoleKey.LeftArrow or ConsoleKey.DownArrow) value--;
            if (navigation is ConsoleKey.RightArrow or ConsoleKey.UpArrow) value++;
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
        var (fieldTop, fieldLeft, fieldWidth) = DrawField(caption, prompt, label);
        var textArea = new TextArea(fieldLeft, fieldTop, fieldWidth, 1, text, 0, text.Length > 0, false, false, 0);
        var typed = textArea.GetText().Trim();
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        return textArea.Canceled || typed.Length == 0 ? null : typed;
    }

    /// <summary>
    /// Ask for a key (a password) in a box: what is typed is shown as *
    /// </summary>
    /// <returns>The key typed, or null if canceled</returns>
    internal static string? AskSecret(string caption, string prompt, string label)
    {
        var (fieldTop, fieldLeft, _) = DrawField(caption, prompt, label);
        Tools.SetCursorEx(fieldTop, fieldLeft + 1);
        var typed = Tools.ReadMaskedSimple(true);
        return typed == DialogText.CancelCommand ? null : typed;
    }

    // A box with a line telling what to do and a label for a field. Returns where the field is
    private static (int Top, int Left, int Width) DrawField(string caption, string prompt, string label)
    {
        var inner = Math.Clamp(Math.Max(TextMetrics.WidthOf(prompt) + 4, 64), 10, Init.LineWidth - 6);
        var promptLines = Wrap(prompt, inner - 4);
        // A blank row, the prompt, a blank row, the field and a blank row
        var fieldRow = promptLines.Count + 2;
        var (top, left, width, _) = DrawFrame(caption, inner, fieldRow + 2);
        WriteText(top, left, new List<string>(), promptLines, false, width);
        Tools.SetCursorEx(top + 1 + fieldRow, left + 3);
        Console.Write(label);
        WriteHints(DialogText.AskTextKeys);
        // The field takes the rest of the row
        var fieldLeft = left + 3 + TextMetrics.WidthOf(label);
        return (top + 1 + fieldRow, fieldLeft, Math.Max(4, left + width - fieldLeft));
    }

    /// <summary>
    /// Show rows of information (a label and a value) in a box until a key is pressed. A long value (a path)
    /// continues on the next rows
    /// </summary>
    /// <param name="caption">The title of the box</param>
    /// <param name="rows">The labels and values</param>
    /// <param name="width">The width inside the box (0: the width of the screen)</param>
    internal static void ShowRows(string caption, IReadOnlyList<(string Label, string Value)> rows, int width = 0)
    {
        var inner = width > 0 ? Math.Min(width, Init.LineWidth - 6) : Init.LineWidth - 6;
        // The labels take the width of the longest (translated) label
        var labelWidth = Math.Max(10, rows.Max(row => TextMetrics.WidthOf(row.Label)) + 1);
        var room = Math.Max(10, inner - labelWidth - 2);
        var lines = new List<(string Label, string Value)>();
        foreach (var (label, value) in rows)
        {
            var rest = value;
            var first = true;
            do
            {
                var part = rest.Length <= room ? rest : rest[..room];
                lines.Add((first ? label : string.Empty, part));
                rest = rest[part.Length..];
                first = false;
            } while (rest.Length > 0);
        }

        // The box fits on the screen
        var most = Math.Max(1, Init.LineCount - 4);
        if (lines.Count > most) lines = lines.GetRange(0, most);
        var (top, left, _, _) = DrawFrame(caption, inner, lines.Count);
        for (var i = 0; i < lines.Count; i++)
        {
            Tools.SetCursorEx(top + 1 + i, left + 2);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write(lines[i].Label);
            Tools.SetCursorEx(top + 1 + i, left + 2 + labelWidth);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Console.Write(lines[i].Value);
        }

        Tools.SetCursorEx(Init.LineCount - 1, 0);
        Console.ReadKey(true);
    }

    // Draw the frame of a box with its title and a blank inside, in the middle of the screen. A box shown
    // before it (the page not drawn again) is covered. Returns where the box is and how big it is inside
    internal static (int Top, int Left, int Inner, int Height) DrawFrame(string caption, int inner, int height)
    {
        var left = Math.Max(0, (Init.LineWidth - inner - 2) / 2);
        var top = Math.Max(1, (Init.LineCount - height - 2) / 2);
        if (_shown is { } shown)
        {
            var right = Math.Max(left + inner + 1, shown.Right);
            var bottom = Math.Max(top + height + 1, shown.Bottom);
            top = Math.Min(top, shown.Top);
            left = Math.Min(left, shown.Left);
            inner = right - left - 1;
            height = bottom - top - 1;
        }

        _shown = (top, left, left + inner + 1, top + height + 1);
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
            Console.Write(new string(' ', inner));
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write('|');
        }

        Tools.SetCursorEx(top + height + 1, left);
        Console.Write(AsciiStyles.GetBottomCorner() + new string('-', inner) + AsciiStyles.GetBottomCorner());
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.HeaderColor);
        Tools.SetCursorEx(top, left + 2);
        Console.Write(" " + Tools.TruncateString(caption, inner - 4, true) + " ");
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        return (top, left, inner, height);
    }

    // The highlighted lines (a question or a message) and the lines below them, from the second row of a box
    private static void WriteText(int top, int left, List<string> highlighted, List<string> plain, bool warning,
        int inner)
    {
        var row = top + 2;
        foreach (var line in highlighted)
        {
            Tools.SetCursorEx(row++, left + 2);
            ConsoleColors.SetColors(warning
                ? ConsoleColors.ColorPreset.WarningInverted
                : ConsoleColors.ColorPreset.AttentionInverted);
            Console.Write(" " + Tools.TruncateString(line, inner - 3, true) + " ");
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        foreach (var line in plain)
        {
            Tools.SetCursorEx(row++, left + 3);
            Console.Write(Tools.TruncateString(line, inner - 3, true));
        }
    }

    // The keys of a box, on the bottom line
    internal static void WriteHints(string hints)
    {
        Tools.SetCursorEx(Init.LineCount - 1, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Console.Write(new string(' ', Init.LineWidth - 1));
        Tools.SetCursorEx(Init.LineCount - 1, 0);
        Menu.WriteKeyHints(hints);
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
