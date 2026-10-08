namespace Marbles;

/// <summary>
/// The page shown when Marbles starts, where the user selects what to use it for: notes (the login page),
/// editing text files (the file browser) or the configuration of the page itself.
/// The setting "startmode" tells if the page is shown at startup, or if Marbles goes directly to one of the two.
/// </summary>
internal static class StartMenu
{
    internal enum StartMode
    {
        Select, // Show the choices
        Notes, // Go to the login page
        Editor // Open the file browser
    }

    /// <summary>
    /// What happens when Marbles starts (setting "startmode")
    /// </summary>
    internal static StartMode Mode { get; set; } = StartMode.Select;

    /// <summary>
    /// True when the page is to be shown (or passed, depending on the setting) before the login page
    /// </summary>
    internal static bool Pending { get; private set; }

    // True to show the choices whatever the setting is
    private static bool _showChoices;

    private const int FirstRow = 8;
    private const int Left = 3;

    private static readonly string[] Choices =
        { DialogText.StartNotes, DialogText.StartEditor, DialogText.StartConfiguration };

    private static readonly string[] ConfigurationChoices =
        { DialogText.StartConfigSelect, DialogText.StartConfigNotes, DialogText.StartConfigEditor };

    /// <summary>
    /// The name of a start mode in the settings file
    /// </summary>
    internal static string SettingName(StartMode mode)
    {
        return mode switch
        {
            StartMode.Notes => "notes",
            StartMode.Editor => "editor",
            _ => "select"
        };
    }

    internal static StartMode ParseSetting(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "notes" => StartMode.Notes,
            "editor" => StartMode.Editor,
            _ => StartMode.Select
        };
    }

    /// <summary>
    /// Marbles has started: follow the setting before the login page is shown
    /// </summary>
    internal static void Start()
    {
        Pending = true;
        _showChoices = false;
    }

    /// <summary>
    /// Show the choices before the login page is shown again
    /// </summary>
    internal static void Open()
    {
        Pending = true;
        _showChoices = true;
    }

    /// <summary>
    /// Show the page. Returns when the login page is to be shown
    /// </summary>
    internal static void Run()
    {
        Pending = false;
        var animate = Logo.TakeAnimation();
        if (!_showChoices)
        {
            if (Mode == StartMode.Notes)
            {
                // The login page sweeps the logo
                if (animate) Logo.AnimateNext();
                return;
            }

            if (Mode == StartMode.Editor && BrowseFiles()) return;
        }

        var selected = 0;
        while (true)
        {
            Init.ApplyAutoSize();
            Tools.ClearScreen();
            Logo.DrawLogo(true, animate);
            animate = false;
            var result = Select(DialogText.StartSelectMode, Choices, ref selected, false, DialogText.StartKeys);
            if (result == Result.Redraw) continue;
            if (result == Result.Close)
            {
                if (Menu.YesNoPrompt(DialogText.Exit, DialogText.ExitYn, string.Empty, true))
                {
                    Tools.ExitApplication();
                }

                continue;
            }

            switch (selected)
            {
                case 0:
                    return;
                case 1:
                    if (BrowseFiles()) return;
                    break;
                default:
                    Configure();
                    break;
            }
        }
    }

    // Open the file browser. True if a data file was selected there: the login page is shown for it
    private static bool BrowseFiles()
    {
        FileExplorer.Run();
        return FileExplorer.DataFileSelected;
    }

    // Select what happens at startup. The choice is saved in the settings file
    private static void Configure()
    {
        var selected = (int)Mode;
        while (true)
        {
            Tools.ClearScreen();
            Logo.DrawLogo(true, false);
            var result = Select(DialogText.StartConfiguration, ConfigurationChoices, ref selected, true,
                DialogText.StartConfigKeys);
            if (result == Result.Close) return;
            if (result == Result.Redraw || Mode == (StartMode)selected) continue;
            Mode = (StartMode)selected;
            FileIo.SaveSettings();
        }
    }

    // Show a list of choices, and let the user move the selection (j/k or a number) and accept it (Enter)
    private enum Result
    {
        Accept,
        Close, // Esc or Q
        Redraw // Something was shown on top of the page
    }

    private static Result Select(string caption, string[] choices, ref int selected, bool showState, string keys)
    {
        Tools.HideCursor();
        Tools.SetCursorEx(FirstRow - 2, 1);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.HeaderColor);
        Console.Write(caption + ":");
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        for (var i = 0; i < choices.Length; i++)
        {
            Tools.SetCursorEx(FirstRow + i, Left + 3);
            Console.Write(showState
                ? ((int)Mode == i ? "[x] " : "[ ] ") + choices[i]
                : (i + 1) + " .... " + choices[i]);
        }

        Tools.SetCursorEx(Init.LineCount - 1, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(" " + keys);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        while (true)
        {
            for (var i = 0; i < choices.Length; i++)
            {
                Tools.SetCursorEx(FirstRow + i, Left);
                // The pointer is highlighted as in the lists of entries
                if (i == selected) ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
                Console.Write(i == selected ? "->" : "  ");
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            }

            var key = Console.ReadKey(true);
            if (key.Key is ConsoleKey.Enter or ConsoleKey.L or ConsoleKey.RightArrow ||
                (showState && key.Key == ConsoleKey.Spacebar))
            {
                return Result.Accept;
            }

            if (key.Key is ConsoleKey.Escape or ConsoleKey.Q ||
                (showState && key.Key is ConsoleKey.H or ConsoleKey.LeftArrow))
            {
                return Result.Close;
            }

            if (key.Key is ConsoleKey.J or ConsoleKey.DownArrow)
            {
                selected = (selected + 1) % choices.Length;
            }
            else if (key.Key is ConsoleKey.K or ConsoleKey.UpArrow)
            {
                selected = (selected + choices.Length - 1) % choices.Length;
            }
            else if (key.KeyChar >= '1' && key.KeyChar - '1' < choices.Length)
            {
                selected = key.KeyChar - '1';
            }
            else if (key.KeyChar == '?')
            {
                Tools.WriteEditorHelp(false);
                return Result.Redraw;
            }
        }
    }
}
