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
    /// <returns>The command in lowercase without the ":", or null if canceled</returns>
    internal static string? Read()
    {
        return Read(Prefix, true);
    }

    /// <summary>
    /// Let the user type a text on the bottom line
    /// </summary>
    /// <param name="prefix">The character shown before the text</param>
    /// <param name="isCommand">True to return the text trimmed and in lowercase</param>
    /// <returns>The text, or null if canceled or empty</returns>
    internal static string? Read(char prefix, bool isCommand)
    {
        var command = string.Empty;
        // The whole line is highlighted while typing
        WriteLine(prefix == Prefix ? DialogText.CommandPrompt : prefix.ToString(),
            ConsoleColors.ColorPreset.HighlightInverted, true);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
        Tools.ShowCursor();
        try
        {
            while (true)
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Escape)
                {
                    return null;
                }

                if (key.Key == ConsoleKey.Enter)
                {
                    if (isCommand) LastArgument = Argument(command.Trim());
                    command = isCommand ? command.Trim().ToLowerInvariant() : command.Trim();
                    return command.Length > 0 ? command : null;
                }

                if (key.Key == ConsoleKey.Backspace)
                {
                    if (command.Length == 0) return null;
                    command = command[..^1];
                    Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
                    Console.Write(" ");
                    Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
                }
                else if (!char.IsControl(key.KeyChar) && key.KeyChar != '\0' && command.Length < Init.LineWidth - DialogText.CommandPrompt.Length - 3)
                {
                    command += key.KeyChar;
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
        WriteLine(" " + DialogText.NotACommand + command + " ", ConsoleColors.ColorPreset.AttentionInverted, false);
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
