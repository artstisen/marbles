namespace Marbles;

/// <summary>
/// The page with the clipboard settings: where yanked text goes (the system clipboard through OSC 52, the paste
/// bin file), when (every yank, or only with "+ in front), whether text may leave the notes of a data file, and the
/// paste bin file. Opened with ":clipboard" (main menu and editor) and from the Configuration page.
/// A change is saved in the settings file at once.
/// </summary>
internal static class ClipboardPage
{
    private const int FirstRow = 3;
    private const int Left = 1;

    private enum Row
    {
        Osc52,
        PasteBin,
        When,
        FromNotes,
        File,
        Append,
        Clear
    }

    private static readonly Row[] Rows = Enum.GetValues<Row>();

    private static string Check(bool isChecked) => isChecked ? "[x] " : "[ ] ";

    private static string RowText(Row row)
    {
        const string indent = "    ";
        return row switch
        {
            Row.Osc52 => Check(SystemClipboard.Osc52) + Lang.T("Copy to the system clipboard (OSC 52)"),
            Row.PasteBin => Check(SystemClipboard.PasteBin) + Lang.T("Copy to the paste bin file"),
            Row.When => indent + (SystemClipboard.EveryYank
                ? Lang.T("Copied: every yank (h/l: only with \"+y)")
                : Lang.T("Copied: only with \"+y (h/l: every yank)")),
            Row.FromNotes => Check(SystemClipboard.FromNotes) + Lang.T("Also from the notes of a data file"),
            Row.File => indent + Lang.F("Paste bin file: {0}", SystemClipboard.PasteBinFile),
            Row.Append => indent + (SystemClipboard.PasteBinAppend
                ? Lang.T("Paste bin: add to the end")
                : Lang.T("Paste bin: replace the text")),
            _ => indent + Lang.T("Empty the paste bin")
        };
    }

    /// <summary>
    /// Show the page until it is closed
    /// </summary>
    internal static void Show()
    {
        var selected = 0;
        var redraw = true;
        var shown = -1;
        while (true)
        {
            if (Init.ApplyAutoSize()) redraw = true;
            if (redraw)
            {
                Draw();
                shown = -1;
            }

            // The selection pointer
            if (shown >= 0 && shown != selected)
            {
                Tools.SetCursorEx(FirstRow + shown, Left);
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                Console.Write("  ");
            }

            Tools.SetCursorEx(FirstRow + selected, Left);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
            Console.Write("->");
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            shown = selected;
            Tools.SetCursorEx(Init.LineCount - 1, 0);

            redraw = true;
            var key = Console.ReadKey(true);
            var navigation = Keyboard.ToNavigationKey(key).Key;
            if (key.Key is ConsoleKey.Escape or ConsoleKey.Q || key.KeyChar == '-') break;
            if (navigation == ConsoleKey.DownArrow)
            {
                selected = (selected + 1) % Rows.Length;
                redraw = false;
            }
            else if (navigation == ConsoleKey.UpArrow)
            {
                selected = (selected + Rows.Length - 1) % Rows.Length;
                redraw = false;
            }
            else if (key.KeyChar == 'G')
            {
                selected = Rows.Length - 1;
                redraw = false;
            }
            else if (key.KeyChar == 'g')
            {
                if (Console.ReadKey(true).KeyChar == 'g') selected = 0;
                redraw = false;
            }
            else if (key.KeyChar >= '1' && key.KeyChar - '1' < Rows.Length)
            {
                selected = key.KeyChar - '1';
                redraw = false;
            }
            else if (key.KeyChar == '?')
            {
                Tools.WriteEditorHelp(false, Tools.HelpTopic.Clipboard);
            }
            else if (key.Key is ConsoleKey.Enter or ConsoleKey.Spacebar ||
                     navigation is ConsoleKey.RightArrow or ConsoleKey.LeftArrow &&
                     Rows[selected] is Row.When or Row.Append)
            {
                Change(Rows[selected]);
            }
            else
            {
                redraw = false;
            }
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Tools.ClearScreen();
    }

    private static void Change(Row row)
    {
        switch (row)
        {
            case Row.Osc52:
                SystemClipboard.Osc52 = !SystemClipboard.Osc52;
                break;
            case Row.PasteBin:
                SystemClipboard.PasteBin = !SystemClipboard.PasteBin;
                break;
            case Row.When:
                SystemClipboard.EveryYank = !SystemClipboard.EveryYank;
                break;
            case Row.FromNotes:
                SystemClipboard.FromNotes = !SystemClipboard.FromNotes;
                break;
            case Row.Append:
                SystemClipboard.PasteBinAppend = !SystemClipboard.PasteBinAppend;
                break;
            case Row.File:
            {
                var path = ChoiceBox.AskText(Lang.T("Paste bin file"),
                    Lang.T("Type the path of the file and press Enter"), DialogText.NameColon,
                    SystemClipboard.PasteBinFile);
                if (path == null) return;
                var directory = Path.GetDirectoryName(Path.GetFullPath(path));
                if (directory != null && !Directory.Exists(directory))
                {
                    ChoiceBox.Message(Lang.T("Paste bin file"), DialogText.DirectoryNotFound, path);
                    return;
                }

                SystemClipboard.PasteBinFile = path;
                break;
            }
            default:
            {
                if (!ChoiceBox.Confirm(Lang.T("Paste bin file"), Lang.T("Empty the paste bin?"),
                        SystemClipboard.PasteBinFile, true))
                {
                    return;
                }

                var result = SystemClipboard.ClearPasteBin();
                ChoiceBox.Message(Lang.T("Paste bin file"),
                    result == DialogText.Success ? DialogText.PasteBinCleared : result, string.Empty);
                return;
            }
        }

        FileIo.SaveSettings();
    }

    private static void Draw()
    {
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Tools.ClearScreen();
        Menu.WriteModeLabel(DialogText.CaptionsClipboard, string.Empty, 0);
        Menu.WriteNameInTopBar(false);
        Tools.SetCursorEx(1, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(new string('-', Init.LineWidth));
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        for (var i = 0; i < Rows.Length; i++)
        {
            Tools.SetCursorEx(FirstRow + i, Left + 2);
            Console.Write(Tools.FitLine(" " + RowText(Rows[i]), Left + 2));
        }

        // What the two ways of copying are for
        var row = FirstRow + Rows.Length + 1;
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        foreach (var line in ChoiceBox.Wrap(DialogText.ClipboardNote, Init.LineWidth - 4))
        {
            if (row >= Init.LineCount - 2) break;
            Tools.SetCursorEx(row++, 2);
            Console.Write(line);
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Tools.SetCursorEx(Init.LineCount - 1, 0);
        Menu.WriteKeyHints(DialogText.ClipboardKeys);
    }
}
