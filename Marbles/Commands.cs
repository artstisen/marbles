namespace Marbles;

/// <summary>
/// The places that have a command line, each with its own commands
/// </summary>
internal enum CommandContext
{
    Menu, // The main menu of a data file
    List, // A list of entries
    Files, // The file browser
    FilePicker, // The file browser when a file or directory is picked for something
    Favorites, // The favorites of the file browser
    History, // The files opened from the file browser
    Search, // The files found by a search in the file browser
    Editor
}

/// <summary>
/// The commands of each place that has a command line. The tables are what Tab completes from on the
/// command line and what the log knows as commands.
/// A command that is added, renamed or removed where the commands are run must be changed here as well:
/// the tests compare the tables with the code that runs the commands (CommandTests).
/// </summary>
internal static class Commands
{
    private sealed class Table
    {
        internal readonly string[] Names; // The commands that are completed to, in the order they are offered
        internal readonly HashSet<string> All; // With the short forms that are not offered

        internal Table(string names, string shortForms)
        {
            Names = Split(names);
            Array.Sort(Names, StringComparer.Ordinal);
            All = new HashSet<string>(Names, StringComparer.Ordinal);
            All.UnionWith(Split(shortForms));
        }

        private static string[] Split(string words)
        {
            return words.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }
    }

    // Leaving Marbles or the data file, from the menus and lists of a data file
    private const string Common = " quitall exit logout";
    private const string CommonShort = " qa";

    private static readonly Dictionary<CommandContext, Table> Tables = new()
    {
        [CommandContext.Menu] = new Table(
            "quit help about guide new browse number import transfer undelete backup defaults scheme " +
            "bw size password color restart language" + Common,
            "q h nu" + CommonShort),
        [CommandContext.List] = new Table(
            "quit close help browse new title delete color transfer size duplicate revert export import info" + Common,
            "q h" + CommonShort),
        [CommandContext.Files] = new Table(
            "quit close home help cd new info mkdir rename copy duplicate move delete favorite favorites history panes find",
            "q h fav favs hist"),
        [CommandContext.FilePicker] = new Table(
            "quit close home help cd",
            "q h"),
        [CommandContext.Favorites] = new Table(
            "quit close help color info remove move undo folder expand collapse rename goto",
            "q h u"),
        [CommandContext.History] = new Table(
            "quit close help info goto favorite remove clear",
            "q h fav"),
        [CommandContext.Search] = new Table(
            "quit close help find info goto favorite",
            "q h fav"),
        [CommandContext.Editor] = new Table(
            "write quit quit! wq help edit edit! reload reload! undo redo delete yank replace nohlsearch " +
            "spell nospell spellnext spellprev suggest spelladd syntax number nonumber timestamp password " +
            "calendar trim clear registers regclear putall vsel vline block ol ul dup duplicate",
            "w q q! wq! x h e e! u red d y s rep noh set spellsuggest spellgood nu nonu ts pw cal reg")
    };

    /// <summary>
    /// The commands of a place that Tab completes to, in alphabetical order (without the short forms)
    /// </summary>
    internal static IReadOnlyList<string> Names(CommandContext context)
    {
        return Tables[context].Names;
    }

    /// <summary>
    /// All the commands of a place, with the short forms
    /// </summary>
    internal static IReadOnlyCollection<string> All(CommandContext context)
    {
        return Tables[context].All;
    }

    /// <summary>
    /// True if the name is a command (or the short form of one) in the place
    /// </summary>
    internal static bool IsKnown(CommandContext context, string name)
    {
        return Tables[context].All.Contains(name);
    }

    /// <summary>
    /// True if the name is a command somewhere in Marbles
    /// </summary>
    internal static bool IsKnown(string name)
    {
        foreach (var table in Tables.Values)
        {
            if (table.All.Contains(name)) return true;
        }

        return false;
    }
}

/// <summary>
/// Completes the command being typed on a command line when Tab is pressed.
/// Pressing Tab again gives the next command that starts with what was typed, and after the last of them
/// what was typed is shown again. With nothing typed, all the commands of the place are gone through.
/// </summary>
internal sealed class CommandCompletion
{
    private readonly CommandContext _context;
    private string _shown = string.Empty; // What the last completion gave: typing something else starts over
    private string _before = string.Empty; // The lines a command works on, typed in front of it ("%", "'<,'>")
    private string _typed = string.Empty;
    private List<string> _matches = new();
    private int _index = -1; // The command shown (-1: what was typed)
    private bool _active;

    internal CommandCompletion(CommandContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Forget the commands being gone through (when a new command line is opened)
    /// </summary>
    internal void Reset()
    {
        _active = false;
        _index = -1;
        _matches = new List<string>();
    }

    /// <summary>
    /// The text of the command line after Tab was pressed
    /// </summary>
    /// <param name="text">The text of the command line, without the ":"</param>
    /// <param name="forward">False to go back to the previous command (Shift+Tab)</param>
    /// <returns>The text with the command completed, or the text as it was if nothing starts with it</returns>
    internal string Next(string text, bool forward)
    {
        if (!_active || !string.Equals(text, _shown, StringComparison.Ordinal))
        {
            Reset();
            if (!Start(text)) return text;
            _active = true;
        }

        // What was typed is one of the places gone through
        var count = _matches.Count + 1;
        _index = (_index + 1 + (forward ? 1 : count - 1)) % count - 1;
        _shown = _before + (_index < 0 ? _typed : _matches[_index]);
        return _shown;
    }

    // Find the commands that start with what is typed
    private bool Start(string text)
    {
        // Only the name of a command is completed, not what follows it
        if (text.Contains(' ')) return false;
        var start = text.Length;
        while (start > 0 && (char.IsLetter(text[start - 1]) || text[start - 1] == '!')) start--;
        _before = text[..start];
        _typed = text[start..];
        _matches = Commands.Names(_context)
            .Where(name => name.StartsWith(_typed, StringComparison.OrdinalIgnoreCase) &&
                           !string.Equals(name, _typed, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return _matches.Count > 0;
    }
}
