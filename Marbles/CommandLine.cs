namespace Marbles;

/// <summary>
/// The ":" command line used in the menus and lists (the editor has its own, see EditorEngine).
/// The command is typed on the bottom line of the window.
/// </summary>
internal static class CommandLine
{
    private const char Prefix = ':';

    /// <summary>
    /// The text after the first word of the last command, as it was typed (commands are returned in lowercase)
    /// </summary>
    internal static string LastArgument { get; private set; } = string.Empty;

    /// <summary>
    /// Let the user type a command
    /// </summary>
    /// <param name="context">Where the command is typed: Tab completes to the commands of that place</param>
    /// <returns>The command in lowercase without the ":", or null if canceled</returns>
    internal static string? Read(CommandContext context)
    {
        return Read(Prefix, true, context);
    }

    // The columns of the window: the text stays on its line also when the window is narrower than the pages
    private static int VisibleWidth()
    {
        try
        {
            return Math.Min(Init.LineWidth, Console.WindowWidth);
        }
        catch (Exception e) when (e is IOException or PlatformNotSupportedException)
        {
            return Init.LineWidth;
        }
    }

    /// <summary>
    /// Let the user type a text on the bottom line
    /// </summary>
    /// <param name="prefix">The character shown before the text</param>
    /// <param name="isCommand">True to return the text trimmed and in lowercase</param>
    /// <param name="context">For a command: where it is typed, to have Tab complete it</param>
    /// <returns>The text, or null if canceled or empty</returns>
    internal static string? Read(char prefix, bool isCommand, CommandContext? context = null)
    {
        var command = string.Empty;
        var completion = context == null ? null : new CommandCompletion(context.Value);
        // Up and Down: the commands given here before
        var recall = context == null ? null : new CommandRecall(context.Value);
        // The whole line is highlighted while typing
        WriteLine(prefix == Prefix ? DialogText.CommandPrompt : prefix.ToString(),
            ConsoleColors.ColorPreset.HighlightInverted, true);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
        Tools.ShowCursor();
        var left = Console.CursorLeft; // Where the text starts

        // Show another text on the line instead of the one typed (counted in columns, and only when it fits)
        void Replace(string text)
        {
            if (text == command || TextMetrics.WidthOf(text) + 2 >= VisibleWidth() - left) return;
            var shown = TextMetrics.WidthOf(command);
            var width = TextMetrics.WidthOf(text);
            Tools.SetCursorEx(Console.CursorTop, left);
            Console.Write(text + new string(' ', Math.Max(0, shown - width)));
            Tools.SetCursorEx(Console.CursorTop, left + width);
            command = text;
        }

        try
        {
            while (true)
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Escape)
                {
                    return null;
                }

                if (key.Key == ConsoleKey.Tab)
                {
                    // Complete the command. Tab again: the next command that starts the same (Shift+Tab: the previous)
                    Replace(completion?.Next(command, (key.Modifiers & ConsoleModifiers.Shift) == 0) ?? command);
                    recall?.Reset();
                    continue;
                }

                if (key.Key is ConsoleKey.UpArrow or ConsoleKey.DownArrow)
                {
                    var recalled = key.Key == ConsoleKey.UpArrow ? recall?.Previous(command) : recall?.Next();
                    if (recalled != null)
                    {
                        Replace(recalled);
                        completion?.Reset();
                    }

                    continue;
                }

                if (key.Key == ConsoleKey.Enter)
                {
                    // A command line of a place (also one read as typed, as the file browser's) keeps its commands
                    if (context != null) CommandHistory.Add(context.Value, command);
                    if (isCommand)
                    {
                        LastArgument = Argument(command.Trim());
                        // Only the name: a title or other text after it is not logged
                        EventLog.AddCommand(DialogText.LogCommand, command);
                    }

                    command = isCommand ? command.Trim().ToLowerInvariant() : command.Trim();
                    return command.Length > 0 ? command : null;
                }

                if (key.Key == ConsoleKey.Backspace)
                {
                    if (command.Length == 0) return null;
                    // A whole character (an emoji is two chars, a wide one two columns)
                    var length = TextMetrics.PreviousElementLength(command, command.Length);
                    var cells = TextMetrics.WidthOf(command[^length..]);
                    command = command[..^length];
                    recall?.Reset();
                    Tools.SetCursorEx(Console.CursorTop, Math.Max(left, Console.CursorLeft - cells));
                    Console.Write(new string(' ', cells));
                    Tools.SetCursorEx(Console.CursorTop, Math.Max(left, Console.CursorLeft - cells));
                }
                else if (!char.IsControl(key.KeyChar) && key.KeyChar != '\0' &&
                         TextMetrics.WidthOf(command) + 2 < VisibleWidth() - left)
                {
                    command += key.KeyChar;
                    recall?.Reset();
                    Console.Write(key.KeyChar);
                }
            }
        }
        finally
        {
            Tools.HideCursor();
            // Remove the highlighted line
            WriteLine(string.Empty, ConsoleColors.ColorPreset.Normal, false);
        }
    }

    /// <summary>
    /// Tell the user that a command is unknown and wait for a key press
    /// </summary>
    internal static void ShowUnknown(string command)
    {
        WriteLine(" " + DialogText.NotACommand(command) + " ", ConsoleColors.ColorPreset.AttentionInverted, false);
        Console.Write(" ");
        Keyboard.AwaitAnyKey();
    }

    /// <summary>
    /// The first word of a command ("pin" for "pin 2")
    /// </summary>
    internal static string Name(string command)
    {
        var space = command.IndexOf(' ');
        return space < 0 ? command : command[..space];
    }

    /// <summary>
    /// The text after the first word of a command ("2" for "pin 2")
    /// </summary>
    internal static string Argument(string command)
    {
        var space = command.IndexOf(' ');
        return space < 0 ? string.Empty : command[(space + 1)..].Trim();
    }

    /// <summary>
    /// Show a message on the bottom line and wait for a key press
    /// </summary>
    internal static void ShowMessage(string message)
    {
        WriteLine(" " + message + " ", ConsoleColors.ColorPreset.AttentionInverted, false);
        Console.Write(" ");
        Keyboard.AwaitAnyKey();
        WriteLine(string.Empty, ConsoleColors.ColorPreset.Normal, false);
    }

    // Clear the bottom line and write a text on it
    private static void WriteLine(string text, ConsoleColors.ColorPreset preset, bool fillLine)
    {
        Tools.SetCursorEx(Init.LineCount - 1, 0);
        ConsoleColors.SetColors(fillLine ? preset : ConsoleColors.ColorPreset.Normal);
        Console.Write(new string(' ', Init.LineWidth - 1));
        Tools.SetCursorEx(Init.LineCount - 1, 0);
        ConsoleColors.SetColors(preset);
        Console.Write(text);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }
}
