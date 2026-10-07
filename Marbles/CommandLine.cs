namespace Marbles;

/// <summary>
/// The ":" command line used in the menus and lists (the editor has its own, see EditorEngine).
/// The command is typed on the bottom line of the window.
/// </summary>
internal static class CommandLine
{
    private const int MaxLength = 40;
    private const char Prefix = ':';

    /// <summary>
    /// Let the user type a command
    /// </summary>
    /// <returns>The command in lowercase without the ":", or null if canceled</returns>
    internal static string? Read()
    {
        var command = string.Empty;
        WriteLine(Prefix.ToString(), ConsoleColors.ColorPreset.Normal);
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
                    command = command.Trim().ToLowerInvariant();
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
                else if (!char.IsControl(key.KeyChar) && key.KeyChar != '\0' && command.Length < MaxLength)
                {
                    command += key.KeyChar;
                    Console.Write(key.KeyChar);
                }
            }
        }
        finally
        {
            Tools.HideCursor();
        }
    }

    /// <summary>
    /// Tell the user that a command is unknown and wait for a key press
    /// </summary>
    internal static void ShowUnknown(string command)
    {
        WriteLine(" " + DialogText.NotACommand + command + " ", ConsoleColors.ColorPreset.AttentionInverted);
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

    // Clear the bottom line and write a text on it
    private static void WriteLine(string text, ConsoleColors.ColorPreset preset)
    {
        Tools.SetCursorEx(Init.LineCount - 1, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Console.Write(new string(' ', Init.LineWidth - 1));
        Tools.SetCursorEx(Init.LineCount - 1, 0);
        ConsoleColors.SetColors(preset);
        Console.Write(text);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }
}
