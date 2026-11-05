using System.Text.RegularExpressions;

namespace Marbles;

/// <summary>
/// The list of commands shown in a box above the command line while a command is typed (setting "commandlist"):
/// the commands of the place in columns, narrowed to those that start with what is typed (as Tab completes them),
/// and after a space what can follow the command where Marbles has a list of it (":play " the macros). The command
/// that is typed whole (or that Tab completed to) is highlighted, and the line at the bottom tells what it does,
/// from the help. The box keeps its size while the command line is open; the page draws again what it covered
/// </summary>
internal sealed class CommandList
{
    /// <summary>
    /// True to show the list (setting "commandlist")
    /// </summary>
    internal static bool Enabled { get; set; } = true;

    // The most rows of names in the box (it has a line at the top, one for what the command does below them, and a
    // line at the bottom, above the command line)
    internal const int MaxRows = 5;

    private readonly CommandContext _context;
    private int _rows = -1; // The rows of names (set when it is first drawn, and kept)

    internal CommandList(CommandContext context)
    {
        _context = context;
    }

    /// <summary>
    /// The rows of the window the box takes (0 before it is drawn): the page draws them again when it closes
    /// </summary>
    internal int Height => _rows < 0 ? 0 : _rows + 3;

    /// <summary>
    /// What the list shows for what is typed: the commands (or what can follow the command) that start with it,
    /// in order, the one typed whole, and the width of a column (that of the longest of all, so the columns stay
    /// where they are while typing)
    /// </summary>
    internal static (List<string> Names, string Typed, int Column) Matches(CommandContext context, string text)
    {
        // The lines a command works on, typed in front of it ("'<,'>", "%", "2,5"), are not part of its name
        text = Regex.Replace(text, "^[^a-zA-Z]*", string.Empty);
        var space = text.IndexOf(' ');
        IEnumerable<string> all;
        string typed;
        if (space < 0)
        {
            all = Commands.Names(context);
            typed = text;
        }
        else if (Commands.All(context).Contains(text[..space]) &&
                 CommandCompletion.Arguments.TryGetValue(text[..space], out var words) &&
                 !text[(space + 1)..].Contains(' '))
        {
            all = words().ToList();
            typed = text[(space + 1)..];
        }
        else
        {
            return (new List<string>(), string.Empty, 0);
        }

        var list = all.ToList();
        var column = list.Count == 0 ? 0 : list.Max(TextMetrics.WidthOf) + 2;
        var names = list.Where(name => name.StartsWith(typed, StringComparison.OrdinalIgnoreCase)).ToList();
        return (names, typed, column);
    }

    /// <summary>
    /// The names laid out in rows of columns, to a width and a number of rows. When they do not all fit, the last
    /// place tells how many more there are ("+12 more")
    /// </summary>
    internal static List<List<string>> Layout(IReadOnlyList<string> names, int column, int width, int rows)
    {
        var columns = Math.Max(1, width / Math.Max(1, column));
        var room = columns * rows;
        var shown = names.Count <= room ? names.ToList() : names.Take(room - 1).ToList();
        if (names.Count > room) shown.Add(Lang.F("+{0} more", names.Count - shown.Count));
        var lines = new List<List<string>>();
        for (var i = 0; i < shown.Count; i += columns)
        {
            lines.Add(shown.Skip(i).Take(columns).ToList());
        }

        return lines;
    }

    /// <summary>
    /// Clear the rows the box took (when the command line is closed): the page draws its own part of them again,
    /// and nothing of the box is left where the page does not draw (around a box on top of a page)
    /// </summary>
    internal void Clear()
    {
        if (Height == 0) return;
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        for (var row = Init.LineCount - 1 - Height; row < Init.LineCount - 1; row++)
        {
            if (row < 0) continue;
            Tools.SetCursorEx(row, 0);
            Console.Write(new string(' ', Init.LineWidth - 1));
        }
    }

    /// <summary>
    /// Draw the box above the command line, for the text typed on it (without the ":")
    /// </summary>
    internal void Draw(string text)
    {
        var width = Init.LineWidth - 1;
        var inner = width - 4;
        var (names, typed, column) = Matches(_context, text);
        if (_rows < 0)
        {
            // The size it keeps: room for all the commands of the place, at most MaxRows
            var (all, _, allColumn) = Matches(_context, string.Empty);
            _rows = Math.Clamp(Layout(all, allColumn, inner, MaxRows).Count, 1, MaxRows);
        }

        var top = Init.LineCount - 1 - Height;
        if (top < 1) return;
        // The frame: its top with the name of the box, and its sides (the top and the left side in the color of the
        // corner)
        Tools.SetCursorEx(top, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.CommandCorner);
        var title = "-- " + Lang.T("Commands") + " ";
        Console.Write(AsciiStyles.GetTopCorner() + title + new string('-', Math.Max(0, width - 2 - TextMetrics.WidthOf(title))) +
                      AsciiStyles.GetTopCorner());
        var lines = Layout(names, column, inner, _rows);
        var whole = names.FirstOrDefault(name => name.Equals(typed, StringComparison.OrdinalIgnoreCase));
        for (var row = 0; row < _rows; row++)
        {
            Tools.SetCursorEx(top + 1 + row, 0);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.CommandCorner);
            Console.Write("| ");
            var used = 0;
            if (row < lines.Count)
            {
                foreach (var name in lines[row])
                {
                    // ("+12 more" is not a name)
                    var more = !names.Contains(name);
                    used += WriteName(name, more ? string.Empty : typed, column, name == whole, more, inner - used);
                }
            }
            else if (row == 0 && !text.Contains(' '))
            {
                // Nothing starts with what is typed (after a space: what follows has no list, nothing is said)
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.CommandHint);
                var none = Tools.TruncateString(Lang.T("No command starts with that"), inner, true);
                Console.Write(none);
                used = TextMetrics.WidthOf(none);
            }

            ConsoleColors.SetColors(ConsoleColors.ColorPreset.CommandText);
            Console.Write(new string(' ', Math.Max(0, inner - used)));
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.CommandBorder);
            Console.Write(" |");
        }

        // What the command does (the one typed whole, or the only one left), else the keys
        var described = whole ?? (names.Count == 1 && typed.Length > 0 ? names[0] : null);
        var help = described != null && !text.Contains(' ')
            ? Tools.CommandDescription(Topic(_context), described)
            : null;
        var bottom = help != null ? ":" + described + "  " + help : Lang.T("Tab completes, Enter runs, Esc cancels");
        Tools.SetCursorEx(top + 1 + _rows, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.CommandCorner);
        Console.Write("| ");
        ConsoleColors.SetColors(help != null
            ? ConsoleColors.ColorPreset.CommandText
            : ConsoleColors.ColorPreset.CommandHint);
        var line = Tools.TruncateString(bottom, inner, true);
        Console.Write(line + new string(' ', Math.Max(0, inner - TextMetrics.WidthOf(line))));
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.CommandBorder);
        Console.Write(" |");
        // The bottom, just above the command line
        Tools.SetCursorEx(top + 2 + _rows, 0);
        Console.Write(AsciiStyles.GetBottomCorner() + new string('-', Math.Max(0, width - 2)) + AsciiStyles.GetBottomCorner());
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    // A name in its column: the part typed in the accent color (the whole name highlighted when it is typed whole).
    // The cells written
    private static int WriteName(string name, string typed, int column, bool whole, bool more, int room)
    {
        var cell = Math.Min(column, room);
        if (cell <= 0) return 0;
        var shown = Tools.TruncateString(name, Math.Max(1, cell - 2), true);
        if (whole || more)
        {
            ConsoleColors.SetColors(whole
                ? ConsoleColors.ColorPreset.CommandWholeInverted
                : ConsoleColors.ColorPreset.CommandHint);
            Console.Write(shown);
        }
        else
        {
            var match = Math.Min(typed.Length, shown.Length);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.CommandTyped);
            Console.Write(shown[..match]);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.CommandText);
            Console.Write(shown[match..]);
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.CommandText);
        var width = TextMetrics.WidthOf(shown);
        Console.Write(new string(' ', Math.Max(0, cell - width)));
        return cell;
    }

    // The part of the help that has the commands of a place
    private static Tools.HelpTopic Topic(CommandContext context) => context switch
    {
        CommandContext.Files or CommandContext.FilePicker => Tools.HelpTopic.Files,
        CommandContext.Favorites => Tools.HelpTopic.Favorites,
        CommandContext.History => Tools.HelpTopic.History,
        CommandContext.Hotlist => Tools.HelpTopic.Hotlist,
        CommandContext.Search => Tools.HelpTopic.Search,
        CommandContext.Jump => Tools.HelpTopic.Jump,
        CommandContext.Editor => Tools.HelpTopic.Editor,
        CommandContext.Menu => Tools.HelpTopic.MainMenu,
        CommandContext.List => Tools.HelpTopic.Menus,
        _ => Tools.HelpTopic.Start
    };
}
