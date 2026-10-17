namespace Marbles;

/// <summary>
/// The quick guide: a short introduction to Marbles for a new user, shown on a page of its own.
/// It tells what the two parts of Marbles are for, how to move around, and how the modes of the editor work.
/// It is kept short on purpose: the help ("?") has all the keys. The short guides of the Help tab (the Jump menu,
/// the file browser, the text editor and Note Management) are shown the same way (QuickGuide.Guides.cs).
/// </summary>
internal static partial class QuickGuide
{
    /// <summary>
    /// The guides, in the order of the Help tab
    /// </summary>
    internal enum Guide
    {
        Quick,
        Jump,
        Files,
        Editor,
        Notes
    }

    internal static readonly Guide[] All = { Guide.Quick, Guide.Jump, Guide.Files, Guide.Editor, Guide.Notes };

    /// <summary>
    /// The name of a guide, in the language in use
    /// </summary>
    internal static string Title(Guide guide) => guide switch
    {
        Guide.Jump => Lang.T("Jump menu guide"),
        Guide.Files => Lang.T("File browser guide"),
        Guide.Editor => Lang.T("Text editor guide"),
        Guide.Notes => Lang.T("Note Management guide"),
        _ => DialogText.QuickGuide
    };

    // The help shown with "?" in a guide: the keys of the part it is about (the quick guide: all of them)
    private static Tools.HelpTopic HelpOf(Guide guide) => guide switch
    {
        Guide.Jump => Tools.HelpTopic.Jump,
        Guide.Files => Tools.HelpTopic.Files,
        Guide.Editor => Tools.HelpTopic.Editor,
        Guide.Notes => Tools.HelpTopic.MainMenu,
        _ => Tools.HelpTopic.All
    };

    private static List<(Kind Kind, string Key, string Text)> Paragraphs(Guide guide) => guide switch
    {
        Guide.Jump => JumpGuide(),
        Guide.Files => FilesGuide(),
        Guide.Editor => EditorGuide(),
        Guide.Notes => NotesGuide(),
        _ => Paragraphs()
    };

    private enum Kind
    {
        Title,
        Heading,
        Item, // A key (or name) and what it does
        Text
    }

    // The keys of the items take at least KeyWidth columns: more when a key (a name in another language) is
    // longer, up to MaxKeyWidth
    private const int KeyWidth = 16;
    private const int MaxKeyWidth = 24;

    // The guide in the language in use: each item is one paragraph, wrapped to the width of the window.
    // The keys and the names of the editor modes are the same in all languages
    private static List<(Kind Kind, string Key, string Text)> Paragraphs() => new()
    {
        (Kind.Title, "", Lang.T("Quick guide")),
        (Kind.Text, "", ""),
        (Kind.Heading, "", Lang.T("Marbles has two parts, selected on the start page")),
        (Kind.Item, Lang.T("Note Management"), Lang.T("Your notes, kept in a data file that is encrypted with your password. " +
                                                       "Log in, then press N for a new note, S or / to search and F for " +
                                                       "your favorites.")),
        (Kind.Item, Lang.T("Text Editor"), Lang.T("A file browser for ordinary text and code files (they are not " +
                                                   "encrypted). Enter opens a file in the editor. Manage files and " +
                                                   "organize favorites.")),
        (Kind.Text, "", ""),
        (Kind.Heading, "", Lang.T("Moving around (the same keys everywhere)")),
        (Kind.Item, "j  k", Lang.T("Down/Up. The arrow keys work too")),
        (Kind.Item, "h  l", Lang.T("Left/Right: the previous/next page, or back/open")),
        (Kind.Item, "gg  G", Lang.T("The first/last")),
        (Kind.Item, "/", Lang.T("Search or filter")),
        (Kind.Item, ":", Lang.T("Type a command, like :new or :q (the menus show them)")),
        (Kind.Item, "Esc", Lang.T("Back, cancel or close")),
        (Kind.Item, "?", Lang.T("Help: the keys of the page you are on")),
        (Kind.Text, "", ""),
        (Kind.Heading, "", Lang.T("The editor has modes, shown at the bottom left")),
        (Kind.Item, "NORMAL", Lang.T("Keys move the cursor and give commands. You start here")),
        (Kind.Item, "INSERT", Lang.T("Keys type text. Press i to type, and Esc when done")),
        (Kind.Item, "VISUAL", Lang.T("Select text: press v (V for whole lines, Q a block), move, then y to copy " +
                                     "or d to delete")),
        (Kind.Text, "", ""),
        (Kind.Heading, "", Lang.T("The few editor keys you need to begin (in NORMAL mode)")),
        (Kind.Item, "i", Lang.T("Type text (Esc when done)")),
        (Kind.Item, ":w", Lang.T("Save")),
        (Kind.Item, ":q  " + Lang.T("or") + "  Esc", Lang.T("Close the editor (asks if there are unsaved changes)")),
        (Kind.Item, "u", Lang.T("Undo")),
        (Kind.Item, "dd  yy  p", Lang.T("Delete a line / Copy a line / Paste")),
        (Kind.Item, "/text", Lang.T("Search (n: the next match)")),
        (Kind.Text, "", ""),
        (Kind.Heading, "", Lang.T("Commands")),
        (Kind.Item, ":guide  :w  ...", Lang.T("A word with a colon in front is a command: press : and type the " +
                                              "word, then Enter (Esc cancels). Commands can be given on most " +
                                              "pages. The menus and the help show them.")),
        (Kind.Item, "Tab", Lang.T("Completes the command you are typing. Press it again for the next command " +
                                  "that starts the same.")),
        (Kind.Text, "", ""),
        (Kind.Text, "", Lang.T("There is much more. Press ? on any page, or type :help in the editor, to see the " +
                               "keys that can be used there."))
    };

    /// <summary>
    /// The lines of the guide, with the paragraphs wrapped to a width. The key of an item is padded to the width of
    /// the keys of the guide, so the texts of the items start in the same column
    /// </summary>
    internal static List<(int Kind, string Key, string Text)> Lines(int width, Guide guide = Guide.Quick)
    {
        var paragraphs = Paragraphs(guide);
        var keyWidth = Math.Clamp(paragraphs.Where(p => p.Kind == Kind.Item).Max(p => TextMetrics.WidthOf(p.Key)),
            KeyWidth, MaxKeyWidth);
        var lines = new List<(int Kind, string Key, string Text)>();
        foreach (var (kind, key, text) in paragraphs)
        {
            // Where the text of an item starts
            var indent = kind == Kind.Item ? 3 + keyWidth + 1 : 1;
            var parts = TextMetrics.Wrap(text, Math.Max(20, width - indent - 1));
            for (var i = 0; i < parts.Count; i++)
            {
                var shownKey = kind == Kind.Item ? (i == 0 ? key : string.Empty).PadRight(keyWidth) : string.Empty;
                lines.Add(((int)kind, shownKey, parts[i]));
            }
        }

        return lines;
    }

    /// <summary>
    /// Show the guide until it is closed. The page that was shown must be drawn again afterwards
    /// </summary>
    internal static void Show(Guide guide = Guide.Quick)
    {
        var top = 0;
        Tools.HideCursor();
        while (true)
        {
            Init.ApplyAutoSize();
            var pageSize = Math.Max(1, Init.LineCount - 1);
            var lines = Lines(Init.LineWidth, guide);
            var maxTop = Math.Max(0, lines.Count - pageSize);
            top = Math.Clamp(top, 0, maxTop);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Tools.ClearScreen();
            for (var row = 0; row < pageSize && top + row < lines.Count; row++)
            {
                DrawLine(row, lines[top + row]);
            }

            var pages = Math.Max(1, (lines.Count + pageSize - 1) / pageSize);
            var page = top >= maxTop ? pages : top / pageSize + 1;
            Tools.SetCursorEx(Init.LineCount - 1, 0);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.KeyHint);
            Console.Write(Tools.FitLine(" " + Lang.F("Page {0} of {1}", page, pages) + " " +
                                         (guide == Guide.Quick ? DialogText.QuickGuideKeys : DialogText.GuideKeys), 0));
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);

            var key = Console.ReadKey(true);
            if (key.Key is ConsoleKey.Escape or ConsoleKey.Q) break;
            if (key.KeyChar == '?')
            {
                // All the keys (a guide of a part: the keys of that part)
                Tools.WriteEditorHelp(false, HelpOf(guide));
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

    private static void DrawLine(int row, (int Kind, string Key, string Text) line)
    {
        Tools.SetCursorEx(row, 0);
        switch ((Kind)line.Kind)
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
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Accent);
                Console.Write("   " + line.Key);
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
