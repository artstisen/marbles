namespace Marbles;

/// <summary>
/// The commands given on the command lines of a session, for each place (the editor, the file browser, a list ...),
/// the newest first: Up and Down on a command line bring them back. They are kept in memory only, never written to
/// a file, and cleared at logout, as a command can hold the title of a note, a file name or text of a note.
/// It does not touch the console, so it can be tested.
/// </summary>
internal static class CommandHistory
{
    /// <summary>
    /// The most commands kept for a place
    /// </summary>
    internal const int MaxCount = 20;

    private static readonly Dictionary<CommandContext, List<string>> Places = new();

    /// <summary>
    /// Keep a command given in a place. Given again, it is moved to the newest
    /// </summary>
    internal static void Add(CommandContext context, string command)
    {
        command = command.Trim();
        if (command.Length == 0) return;
        if (!Places.TryGetValue(context, out var commands)) Places[context] = commands = new List<string>();
        commands.Remove(command);
        commands.Insert(0, command);
        if (commands.Count > MaxCount) commands.RemoveRange(MaxCount, commands.Count - MaxCount);
    }

    /// <summary>
    /// The commands of a place, the newest first
    /// </summary>
    internal static IReadOnlyList<string> Of(CommandContext context) =>
        Places.TryGetValue(context, out var commands) ? commands.ToArray() : Array.Empty<string>();

    /// <summary>
    /// Forget all the commands (at logout)
    /// </summary>
    internal static void Clear() => Places.Clear();
}

/// <summary>
/// Up and Down on one command line: the commands of its place, the newest first. When something has been typed,
/// only the commands that start with it are brought back, and going down past the newest gives back what was typed
/// </summary>
internal sealed class CommandRecall
{
    private readonly IReadOnlyList<string> _commands; // As they were when the command line was opened
    private int _index = -1; // The command shown (-1: what was typed)
    private string _typed = string.Empty;

    internal CommandRecall(CommandContext context)
    {
        _commands = CommandHistory.Of(context);
    }

    /// <summary>
    /// The command before the one shown (Up), or null if there is none. The text on the line is what the commands
    /// must start with
    /// </summary>
    internal string? Previous(string current)
    {
        if (_index < 0) _typed = current;
        for (var i = _index + 1; i < _commands.Count; i++)
        {
            if (!Matches(_commands[i])) continue;
            _index = i;
            return _commands[i];
        }

        return null;
    }

    /// <summary>
    /// The command after the one shown (Down); after the newest, what was typed. Null when nothing was brought back
    /// </summary>
    internal string? Next()
    {
        if (_index < 0) return null;
        for (var i = _index - 1; i >= 0; i--)
        {
            if (!Matches(_commands[i])) continue;
            _index = i;
            return _commands[i];
        }

        _index = -1;
        return _typed;
    }

    /// <summary>
    /// The line was changed by hand (typed, deleted, completed): Up starts from it again
    /// </summary>
    internal void Reset()
    {
        _index = -1;
    }

    private bool Matches(string command) => command.StartsWith(_typed, StringComparison.OrdinalIgnoreCase);
}
