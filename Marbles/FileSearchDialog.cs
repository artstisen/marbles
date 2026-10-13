namespace Marbles;

/// <summary>
/// The box of the file browser where a search for files is filled in (the key "S" and the command ":find").
/// It is shown on top of the browser. What was entered is remembered until Marbles is closed.
/// </summary>
internal static class FileSearchDialog
{
    /// <summary>
    /// What to search for, as it was filled in last
    /// </summary>
    internal static FileSearch.Options Options { get; } = new();

    private const int RowName = 0;
    private const int RowText = 1;
    private const int RowRegex = 2;
    private const int RowMatchCase = 3;
    private const int RowWhere = 4;
    private const int RowRun = 5;
    private const int RowCount = 6;

    private const int InnerWidth = 62; // Between the borders
    // The labels take the room they need in the language in use
    private static int LabelWidth =>
        Math.Max(15, new[] { DialogText.SearchName, DialogText.SearchText, DialogText.ReplaceWhere }.Max(l => l.Length) + 1);

    private static int FieldWidth => InnerWidth - LabelWidth - 7;
    private const int MaxTextLength = 200;

    /// <summary>
    /// The width and height of the box
    /// </summary>
    internal const int Width = InnerWidth + 2;

    internal const int Height = RowCount + 5;

    private static int _top;
    private static int _left;
    private static string _message = string.Empty;

    /// <summary>
    /// Show the box and let the user fill it in
    /// </summary>
    /// <param name="directory">The directory that will be searched (shown in the box)</param>
    /// <returns>True to search, false if the box was closed</returns>
    internal static bool Show(string directory)
    {
        // (the tab of the title is on the row above the box)
        _top = Math.Max(2, (Init.LineCount - Height - 1) / 2 + 1);
        _left = Math.Max(0, (Init.LineWidth - Width) / 2);
        _message = string.Empty;
        Tools.HideCursor();
        DrawFrame(directory);
        var selected = RowName;
        // Start by writing the name
        var editNext = true;
        while (true)
        {
            for (var row = 0; row < RowCount; row++)
            {
                DrawRow(row, row == selected);
            }

            DrawMessage();
            if (editNext)
            {
                editNext = false;
                // After the name comes "Search": the text in the files is asked for by going to it
                if (EditField(selected)) selected = RowRun;
                continue;
            }

            var key = Console.ReadKey(true);
            _message = string.Empty;
            if (key.Key is ConsoleKey.Escape or ConsoleKey.Q) return false;
            if (key.Key is ConsoleKey.J or ConsoleKey.DownArrow or ConsoleKey.Tab)
            {
                selected = (selected + 1) % RowCount;
            }
            else if (key.Key is ConsoleKey.K or ConsoleKey.UpArrow)
            {
                selected = (selected + RowCount - 1) % RowCount;
            }
            else if (key.Key is ConsoleKey.Enter or ConsoleKey.Spacebar or ConsoleKey.L or ConsoleKey.RightArrow or
                     ConsoleKey.I)
            {
                switch (selected)
                {
                    case RowName:
                    case RowText:
                        editNext = true;
                        break;
                    case RowRegex:
                        Options.IsRegex = !Options.IsRegex;
                        break;
                    case RowMatchCase:
                        Options.MatchCase = !Options.MatchCase;
                        break;
                    case RowWhere:
                        Options.Subfolders = !Options.Subfolders;
                        break;
                    default:
                        if (key.Key != ConsoleKey.Enter) break;
                        var result = FileSearch.Validate(Options);
                        if (result == DialogText.Success) return true;
                        _message = result;
                        break;
                }
            }
        }
    }

    private static void DrawFrame(string directory)
    {
        ChoiceBox.DrawTitleTab(_top - 1, _left, InnerWidth, DialogText.SearchCaption);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.BoxBorder);
        for (var row = 1; row < Height - 1; row++)
        {
            Tools.SetCursorEx(_top + row, _left);
            Console.Write('|');
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Console.Write(new string(' ', InnerWidth));
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.BoxBorder);
            Console.Write('|');
        }

        Tools.SetCursorEx(_top + Height - 1, _left);
        Console.Write(AsciiStyles.GetBottomCorner() + new string('-', InnerWidth) + AsciiStyles.GetBottomCorner());
        // Where the search starts (the end of a long path is shown)
        var room = InnerWidth - 4 - DialogText.SearchIn.Length;
        if (directory.Length > room) directory = "..." + directory[^(room - 3)..];
        Tools.SetCursorEx(_top + 1, _left + 2);
        Console.Write(DialogText.SearchIn + directory);
        Tools.SetCursorEx(_top + Height - 2, _left + 2);
        Console.Write(DialogText.ReplaceKeys);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    // What is wrong with what was filled in (on the row above the keys)
    private static void DrawMessage()
    {
        Tools.SetCursorEx(_top + Height - 3, _left + 2);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Console.Write(new string(' ', InnerWidth - 2));
        if (_message.Length == 0) return;
        Tools.SetCursorEx(_top + Height - 3, _left + 2);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
        Console.Write(" " + Tools.TruncateString(_message, InnerWidth - 6, true) + " ");
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    private static string FieldText(int row) => row == RowName ? Options.Name : Options.Text;

    // The end of a text that is too long for the field is shown
    private static string Shown(string text)
    {
        return text.Length <= FieldWidth ? text.PadRight(FieldWidth) : text[^FieldWidth..];
    }

    private static void DrawRow(int row, bool selected)
    {
        Tools.SetCursorEx(_top + 2 + row, _left + 2);
        if (selected) ConsoleColors.SetColors(ConsoleColors.ColorPreset.SelectorInverted);
        Console.Write(selected ? "->" : "  ");
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        var text = row switch
        {
            RowName => DialogText.SearchName.PadRight(LabelWidth) + "[" + Shown(Options.Name) + "]",
            RowText => DialogText.SearchText.PadRight(LabelWidth) + "[" + Shown(Options.Text) + "]",
            RowRegex => Check(Options.IsRegex) + DialogText.ReplaceRegex,
            RowMatchCase => Check(Options.MatchCase) + DialogText.ReplaceMatchCase,
            RowWhere => DialogText.ReplaceWhere.PadRight(LabelWidth) +
                        (Options.Subfolders ? DialogText.SearchSubfolders : DialogText.SearchThisDirectory),
            _ => DialogText.SearchRun
        };
        Console.Write(" " + text.PadRight(InnerWidth - 5));
    }

    private static string Check(bool isChecked) => isChecked ? "[x] " : "[ ] ";

    // Let the user write the text of a field. Enter accepts the text, Esc leaves it as it was
    private static bool EditField(int row)
    {
        var original = FieldText(row);
        var text = original;
        var column = _left + 2 + 3 + LabelWidth + 1;
        try
        {
            while (true)
            {
                Tools.SetCursorEx(_top + 2 + row, column);
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.NormalInverted);
                Console.Write(Shown(text));
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                Tools.SetCursorEx(_top + 2 + row, column + Math.Min(text.Length, FieldWidth - 1));
                Tools.ShowCursor();
                var key = Console.ReadKey(true);
                Tools.HideCursor();
                if (key.Key == ConsoleKey.Escape)
                {
                    text = original;
                    return false;
                }

                if (key.Key is ConsoleKey.Enter or ConsoleKey.Tab or ConsoleKey.DownArrow) return true;
                if (key.Key == ConsoleKey.Backspace)
                {
                    if (text.Length > 0) text = text[..^1];
                }
                else if (Keyboard.IsControl(key, ConsoleKey.U))
                {
                    text = string.Empty;
                }
                else if (!char.IsControl(key.KeyChar) && key.KeyChar != '\0' && text.Length < MaxTextLength)
                {
                    text += key.KeyChar;
                }
            }
        }
        finally
        {
            if (row == RowName)
            {
                Options.Name = text;
            }
            else
            {
                Options.Text = text;
            }
        }
    }
}
