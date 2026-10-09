namespace Marbles;

/// <summary>
/// The quick guide: a short introduction to Marbles for a new user, shown on a page of its own.
/// It tells what the two parts of Marbles are for, how to move around, and how the modes of the editor work.
/// It is kept short on purpose: the help ("?") has all the keys.
/// </summary>
internal static class QuickGuide
{
    private enum Kind
    {
        Title,
        Heading,
        Item, // A key (or name) and what it does
        Text
    }

    private const int KeyWidth = 16;

    private static readonly (Kind Kind, string Key, string Text)[] Lines =
    {
        (Kind.Title, "", "Quick guide"),
        (Kind.Text, "", ""),
        (Kind.Heading, "", "Marbles has two parts, selected on the start page"),
        (Kind.Item, "Note Management", "Your notes, kept in a data file that is encrypted with"),
        (Kind.Item, "", "your password. Log in, then press N for a new note,"),
        (Kind.Item, "", "S or / to search and F for your favorites."),
        (Kind.Item, "Text Editor", "A file browser for ordinary text and code files (they"),
        (Kind.Item, "", "are not encrypted). Enter opens a file in the editor."),
        (Kind.Item, "", "Manage files and organize favorites."),
        (Kind.Text, "", ""),
        (Kind.Heading, "", "Moving around (the same keys everywhere)"),
        (Kind.Item, "j  k", "Down/Up. The arrow keys work too"),
        (Kind.Item, "h  l", "Left/Right: the previous/next page, or back/open"),
        (Kind.Item, "gg  G", "The first/last"),
        (Kind.Item, "/", "Search or filter"),
        (Kind.Item, ":", "Type a command, like :new or :q (the menus show them)"),
        (Kind.Item, "Esc", "Back, cancel or close"),
        (Kind.Item, "?", "Help: the keys of the page you are on"),
        (Kind.Text, "", ""),
        (Kind.Heading, "", "The editor has modes, shown at the bottom left"),
        (Kind.Item, "NORMAL", "Keys move the cursor and give commands. You start here"),
        (Kind.Item, "INSERT", "Keys type text. Press i to type, and Esc when done"),
        (Kind.Item, "VISUAL", "Select text: press v (V for whole lines), move, then"),
        (Kind.Item, "", "y to copy or d to delete"),
        (Kind.Text, "", ""),
        (Kind.Heading, "", "The few editor keys you need to begin (in NORMAL mode)"),
        (Kind.Item, "i", "Type text (Esc when done)"),
        (Kind.Item, ":w", "Save"),
        (Kind.Item, ":q  or  Esc", "Close the editor (asks if there are unsaved changes)"),
        (Kind.Item, "u", "Undo"),
        (Kind.Item, "dd  yy  p", "Delete a line / Copy a line / Paste"),
        (Kind.Item, "/text", "Search (n: the next match)"),
        (Kind.Text, "", ""),
        (Kind.Heading, "", "Commands"),
        (Kind.Item, ":guide  :w  ...", "A word with a colon in front is a command: press : and"),
        (Kind.Item, "", "type the word, then Enter (Esc cancels). Commands can be"),
        (Kind.Item, "", "given on most pages. The menus and the help show them."),
        (Kind.Text, "", ""),
        (Kind.Text, "", "There is much more. Press ? on any page, or type :help in the editor,"),
        (Kind.Text, "", "to see the keys that can be used there.")
    };

    /// <summary>
    /// Show the guide until it is closed. The page that was shown must be drawn again afterwards
    /// </summary>
    internal static void Show()
    {
        var top = 0;
        Tools.HideCursor();
        while (true)
        {
            Init.ApplyAutoSize();
            var pageSize = Math.Max(1, Init.LineCount - 1);
            var maxTop = Math.Max(0, Lines.Length - pageSize);
            top = Math.Clamp(top, 0, maxTop);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Tools.ClearScreen();
            for (var row = 0; row < pageSize && top + row < Lines.Length; row++)
            {
                DrawLine(row, Lines[top + row]);
            }

            var pages = Math.Max(1, (Lines.Length + pageSize - 1) / pageSize);
            var page = top >= maxTop ? pages : top / pageSize + 1;
            Tools.SetCursorEx(Init.LineCount - 1, 0);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write(" Page " + page + " of " + pages + " " + DialogText.QuickGuideKeys);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);

            var key = Console.ReadKey(true);
            if (key.Key is ConsoleKey.Escape or ConsoleKey.Q) break;
            if (key.KeyChar == '?')
            {
                // All the keys
                Tools.WriteEditorHelp(false);
            }
            else if (key.KeyChar == 'G' || key.Key == ConsoleKey.End)
            {
                top = maxTop;
            }
            else if (key.Key == ConsoleKey.Home || (key.KeyChar == 'g' && Console.ReadKey(true).KeyChar == 'g'))
            {
                top = 0;
            }
            else if (key.Key is ConsoleKey.J or ConsoleKey.DownArrow)
            {
                top++;
            }
            else if (key.Key is ConsoleKey.K or ConsoleKey.UpArrow)
            {
                top--;
            }
            else if (key.Key is ConsoleKey.L or ConsoleKey.RightArrow or ConsoleKey.PageDown or ConsoleKey.Spacebar
                     or ConsoleKey.Enter)
            {
                top += pageSize;
            }
            else if (key.Key is ConsoleKey.H or ConsoleKey.LeftArrow or ConsoleKey.PageUp or ConsoleKey.B)
            {
                top -= pageSize;
            }
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Tools.ClearScreen();
    }

    private static void DrawLine(int row, (Kind Kind, string Key, string Text) line)
    {
        Tools.SetCursorEx(row, 0);
        switch (line.Kind)
        {
            case Kind.Title:
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
                Console.Write(" " + line.Text + " ");
                break;
            case Kind.Heading:
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.HeaderColor);
                Console.Write(" " + line.Text);
                break;
            case Kind.Item:
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.PreviewTitle);
                Console.Write("   " + line.Key.PadRight(KeyWidth));
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                Console.Write(" " + line.Text);
                break;
            default:
                Console.Write(" " + line.Text);
                break;
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }
}
