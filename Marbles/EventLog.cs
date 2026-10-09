namespace Marbles;

/// <summary>
/// The log of what has happened since Marbles was started: startup, logins, commands and file operations.
/// It is kept in memory only (nothing is written to disk), and holds a limited number of lines: the oldest
/// are removed when it is full.
///
/// The log can be read from the login page without a password. For that reason the commands given while
/// logged in to a data file, and in the editor, are logged by name only: titles, search texts and other
/// things typed after a command are left out.
/// </summary>
internal static class EventLog
{
    /// <summary>
    /// The largest number of lines kept
    /// </summary>
    internal const int MaxLines = 1000;

    private const int MaxLineLength = 400;
    private static readonly List<string> Lines = new();
    private static int _removed; // The number of lines removed to keep the log within its limit

    internal static int Count => Lines.Count;

    internal static IReadOnlyList<string> All => Lines;

    /// <summary>
    /// Add a line to the log (with the time)
    /// </summary>
    internal static void Add(string text)
    {
        text = text.Replace('\r', ' ').Replace('\n', ' ');
        if (text.Length > MaxLineLength) text = text[..MaxLineLength] + "...";
        Lines.Add("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + text);
        if (Lines.Count <= MaxLines) return;
        var excess = Lines.Count - MaxLines;
        Lines.RemoveRange(0, excess);
        _removed += excess;
    }

    // The commands of the menus, lists and editor. Only these names are logged: anything else typed on a
    // command line (by mistake, it could be a password) is not
    private static readonly HashSet<string> KnownCommands = new(StringComparer.Ordinal)
    {
        "q", "quit", "q!", "quit!", "qa", "quitall", "exit", "logout", "h", "help", "about", "new", "browse",
        "import", "export", "transfer", "undelete", "vignette", "backup", "defaults", "scheme", "bw", "size",
        "password", "logo", "color", "restart", "e", "edit", "close", "delete", "revert", "title", "pin",
        "folder", "rename", "remove", "goto", "info",
        "w", "write", "wq", "wq!", "x", "noh", "nohlsearch", "u", "undo", "red", "redo", "ts", "timestamp", "pw",
        "cal", "calendar", "trim", "clear", "reg", "registers", "regclear", "putall", "spell", "nospell", "set",
        "spellnext", "spellprev", "spellsuggest", "suggest", "spellgood", "spelladd", "replace", "rep", "s", "d",
        "y", "yank", "syntax", "find", "fav", "favorite", "move", "e!", "edit!", "reload", "reload!"
    };

    /// <summary>
    /// What is logged for a command: its name only, without the lines it works on and what follows it.
    /// Titles, search texts and the text of a replace command are never logged
    /// </summary>
    /// <param name="command">The command as typed, without the ":"</param>
    internal static string CommandName(string command)
    {
        command = command.Trim();
        // The lines a command works on come first ("%", "2,5", "'<,'>")
        var start = 0;
        while (start < command.Length && !char.IsLetter(command[start])) start++;
        if (start >= command.Length) return command.Length > 0 ? "(number)" : string.Empty;
        var end = start;
        while (end < command.Length && (char.IsLetter(command[end]) || command[end] == '!')) end++;
        var name = command[start..end].ToLowerInvariant();
        // A command followed directly by its text, as in "s/old/new/"
        if (end < command.Length && !char.IsWhiteSpace(command[end])) name = name[..1];
        if (!KnownCommands.Contains(name)) return "(not a command)";
        return name + (end < command.Length ? " ..." : string.Empty);
    }

    /// <summary>
    /// Log a command by its name
    /// </summary>
    /// <param name="where">Where the command was given ("Command", "Editor")</param>
    /// <param name="command">The command as typed, without the ":"</param>
    internal static void AddCommand(string where, string command)
    {
        var name = CommandName(command);
        if (name.Length > 0) Add(where + ": :" + name);
    }

    internal static void Clear()
    {
        Lines.Clear();
        _removed = 0;
    }

    /// <summary>
    /// The lines containing a text (all lines if the text is empty)
    /// </summary>
    internal static List<string> Filter(string text)
    {
        return text.Length == 0
            ? new List<string>(Lines)
            : Lines.Where(line => line.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private const int TimeWidth = 11; // "[12:34:56] "

    /// <summary>
    /// The rows to show for a list of lines: a line that is wider than the window continues on the next rows,
    /// below its text (after the time)
    /// </summary>
    internal static List<string> Wrap(List<string> lines, int width)
    {
        var rows = new List<string>();
        var rest = Math.Max(10, width - TimeWidth);
        foreach (var line in lines)
        {
            if (line.Length <= width)
            {
                rows.Add(line);
                continue;
            }

            rows.Add(line[..width]);
            for (var i = width; i < line.Length; i += rest)
            {
                rows.Add(new string(' ', TimeWidth) + line.Substring(i, Math.Min(rest, line.Length - i)));
            }
        }

        return rows;
    }

    /// <summary>
    /// Show the log in pages, starting at the end. "/text" only shows the lines containing a text
    /// </summary>
    internal static void Show()
    {
        var filter = string.Empty;
        var top = int.MaxValue;
        Tools.HideCursor();
        while (true)
        {
            Init.ApplyAutoSize();
            var lines = Filter(filter);
            var rows = Wrap(lines, Init.LineWidth - 2);
            var pageSize = Math.Max(1, Init.LineCount - 4);
            var maxTop = Math.Max(0, rows.Count - pageSize);
            top = Math.Clamp(top, 0, maxTop);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Tools.ClearScreen();
            Menu.WriteModeLabel(DialogText.LogReview, string.Empty, 0);
            Tools.SetCursorEx(1, 1);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write(lines.Count + (lines.Count == 1 ? DialogText.LogLine : DialogText.LogLines) +
                          (filter.Length > 0 ? DialogText.LogOf + Lines.Count + DialogText.LogFiltered + filter : "") +
                          (_removed > 0 ? " (" + _removed + DialogText.LogRemoved + ")" : ""));
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            for (var row = 0; row < pageSize && top + row < rows.Count; row++)
            {
                var line = rows[top + row];
                Tools.SetCursorEx(2 + row, 0);
                // The start of each run of Marbles stands out
                if (line.Contains(DialogText.ApplicationName + " " + DialogText.ApplicationVersion) ||
                    line.Contains(DialogText.Restart))
                {
                    ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
                }

                Console.Write(line);
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            }

            if (lines.Count == 0)
            {
                Tools.SetCursorEx(3, 1);
                Console.Write(DialogText.InitNoData);
            }

            var pages = Math.Max(1, (rows.Count + pageSize - 1) / pageSize);
            var page = top >= maxTop ? pages : top / pageSize + 1;
            Tools.SetCursorEx(Init.LineCount - 1, 0);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write(" Page " + page + " of " + pages + " " + DialogText.LogKeys);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);

            var key = Console.ReadKey(true);
            if (key.Key is ConsoleKey.Escape or ConsoleKey.Q)
            {
                // Esc removes a filter before it closes the log
                if (key.Key == ConsoleKey.Escape && filter.Length > 0)
                {
                    filter = string.Empty;
                    top = int.MaxValue;
                    continue;
                }

                break;
            }

            if (key.KeyChar == '/')
            {
                filter = CommandLine.Read('/', false) ?? string.Empty;
                top = int.MaxValue;
            }
            else if (key.KeyChar == 'G' || key.Key == ConsoleKey.End)
            {
                top = maxTop;
            }
            else if (key.KeyChar == 'g')
            {
                if (Console.ReadKey(true).KeyChar == 'g') top = 0;
            }
            else if (key.Key == ConsoleKey.Home)
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
}
