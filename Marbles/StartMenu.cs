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

    // In the language in use
    private static string[] Choices => new[]
        {
            DialogText.StartNotes, DialogText.StartEditor, DialogText.StartConfiguration, DialogText.QuickGuide,
            DialogText.StartExit
        };

    /// <summary>
    /// The name of a start mode in the settings file
    /// </summary>
    /// <summary>
    /// When Marbles starts in the Text Editor: open the favorites list of the file browser first
    /// (setting "startfavorites")
    /// </summary>
    internal static bool StartInFavorites { get; set; }

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
            Logo.DrawLogo(true, animate, false);
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
                case 2:
                    Configure();
                    break;
                case 3:
                    QuickGuide.Show();
                    break;
                default:
                    // Exit without asking
                    Tools.ExitApplication();
                    break;
            }
        }
    }

    // Open the file browser. True if a data file was selected there: the login page is shown for it
    private static bool BrowseFiles()
    {
        FileExplorer.Run(StartInFavorites);
        return FileExplorer.DataFileSelected;
    }

    private enum Setting
    {
        StartSelect,
        StartNotes,
        StartEditor,
        StartFavorites, // Shown below StartEditor when it is chosen
        Monochrome,
        Language,
        ColorScheme,
        AutoSize,
        Size,
        LineNumbers,
        Reset,
        Restart,
        ViewLog
    }

    // The settings shown: some are only shown when they can be used
    private static List<Setting> Settings()
    {
        var settings = new List<Setting>
        {
            Setting.StartSelect, Setting.StartNotes, Setting.StartEditor
        };
        if (Mode == StartMode.Editor) settings.Add(Setting.StartFavorites);
        settings.Add(Setting.Monochrome);
        if (!ConsoleColors.Monochrome) settings.Add(Setting.ColorScheme);
        settings.Add(Setting.Language);
        settings.Add(Setting.AutoSize);
        if (!Init.AutoSize) settings.Add(Setting.Size);
        settings.Add(Setting.LineNumbers);
        settings.Add(Setting.Reset);
        settings.Add(Setting.Restart);
        settings.Add(Setting.ViewLog);
        return settings;
    }

    private static string Check(bool isChecked) => isChecked ? "[x] " : "[ ] ";

    private static string SettingText(Setting setting)
    {
        const string indent = "    ";
        return setting switch
        {
            Setting.StartSelect => Check(Mode == StartMode.Select) + DialogText.StartConfigSelect,
            Setting.StartNotes => Check(Mode == StartMode.Notes) + DialogText.StartConfigNotes,
            Setting.StartEditor => Check(Mode == StartMode.Editor) + DialogText.StartConfigEditor,
            Setting.StartFavorites => indent + Check(StartInFavorites) + Lang.T("Open the favorites list first"),
            Setting.Monochrome => Check(ConsoleColors.Monochrome) + DialogText.ConfigMonochrome,
            Setting.Language => indent + DialogText.ConfigLanguage + Lang.CurrentName,
            Setting.ColorScheme => indent + Lang.F("Color scheme: {0} of {1}", ConsoleColors.GetColorPresetIndex() + 1,
                                       Actions.ColorPresetCount) + " (" +
                                   Actions.ColorPresetName(ConsoleColors.GetColorPresetIndex()) + ")",
            Setting.AutoSize => Check(Init.AutoSize) + DialogText.ConfigAutoSize,
            Setting.Size => indent + DialogText.ConfigSize + Init.LineWidth + " x " + Init.LineCount +
                            DialogText.ConfigSizeNote,
            Setting.LineNumbers => Check(TextArea.LineNumbers) + DialogText.ConfigLineNumbers,
            Setting.Restart => indent + DialogText.ConfigRestart,
            Setting.ViewLog => indent + DialogText.ConfigViewLog,
            _ => indent + DialogText.ConfigReset
        };
    }

    // The settings of the start page and some basic settings. A change is saved in the settings file at once
    private static void Configure()
    {
        var selected = 0;
        // The page is drawn when something has changed. When the selection moves only the pointer is drawn
        var redraw = true;
        while (true)
        {
            var settings = Settings();
            selected = Math.Clamp(selected, 0, settings.Count - 1);
            if (redraw)
            {
                Init.ApplyAutoSize();
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                Tools.ClearScreen();
                Logo.DrawLogo(true, false, false);
                Tools.SetCursorEx(FirstRow - 2, 1);
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.HeaderColor);
                Console.Write(DialogText.StartConfiguration + ":");
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                for (var i = 0; i < settings.Count; i++)
                {
                    // The basic settings follow the ones of the start page after a blank row
                    Tools.SetCursorEx(SettingRow(settings, i), Left + 2);
                    Console.Write(" " + SettingText(settings[i]));
                }

                Tools.SetCursorEx(Init.LineCount - 1, 0);
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                Console.Write(" " + DialogText.StartConfigKeys);
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            }

            Tools.HideCursor();
            for (var i = 0; i < settings.Count; i++)
            {
                Tools.SetCursorEx(SettingRow(settings, i), Left);
                if (i == selected) ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
                Console.Write(i == selected ? "->" : "  ");
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            }

            redraw = true;
            // The page is drawn again if it could not be drawn, or the terminal window has changed size
            if (Tools.TakeRedrawRequest()) continue;
            var key = Console.ReadKey(true);
            if (key.Key is ConsoleKey.Escape or ConsoleKey.Q) return;
            if (Init.AutoSize && Init.ApplyAutoSize()) continue;
            if (key.Key is ConsoleKey.J or ConsoleKey.DownArrow)
            {
                selected = (selected + 1) % settings.Count;
                redraw = false;
            }
            else if (key.Key is ConsoleKey.K or ConsoleKey.UpArrow)
            {
                selected = (selected + settings.Count - 1) % settings.Count;
                redraw = false;
            }
            else if (key.KeyChar >= '1' && key.KeyChar - '1' < settings.Count)
            {
                selected = key.KeyChar - '1';
                redraw = false;
            }
            else if (key.KeyChar == 'G' || key.Key == ConsoleKey.End)
            {
                selected = settings.Count - 1;
                redraw = false;
            }
            else if (key.Key == ConsoleKey.Home || (key.KeyChar == 'g' && Console.ReadKey(true).KeyChar == 'g'))
            {
                selected = 0;
                redraw = false;
            }
            else if (key.KeyChar == '?')
            {
                Tools.WriteEditorHelp(false, Tools.HelpTopic.Start);
            }
            else if (key.Key is ConsoleKey.Enter or ConsoleKey.Spacebar && settings[selected] == Setting.ColorScheme)
            {
                // The color scheme editor (l and h select the next and previous preset)
                SchemeEditor.Show();
            }
            else if (key.Key is ConsoleKey.Enter or ConsoleKey.Spacebar or ConsoleKey.L or ConsoleKey.RightArrow)
            {
                ChangeSetting(settings[selected], true);
            }
            else if (key.Key is ConsoleKey.H or ConsoleKey.LeftArrow &&
                     settings[selected] is Setting.ColorScheme or Setting.Language)
            {
                // The previous color scheme or logo
                ChangeSetting(settings[selected], false);
            }
            else
            {
                // Nothing has changed
                redraw = false;
            }
        }
    }

    private static int SettingRow(List<Setting> settings, int index)
    {
        // A blank row between the settings of the start page and the basic settings, when there is room for it
        var gap = FirstRow + settings.Count + 1 <= Init.LineCount - 1 ? 1 : 0;
        return FirstRow + index + (settings[index] >= Setting.Monochrome ? gap : 0);
    }

    private static void ChangeSetting(Setting setting, bool next)
    {
        switch (setting)
        {
            case Setting.StartSelect:
                Mode = StartMode.Select;
                break;
            case Setting.StartNotes:
                Mode = StartMode.Notes;
                break;
            case Setting.StartEditor:
                Mode = StartMode.Editor;
                break;
            case Setting.StartFavorites:
                StartInFavorites = !StartInFavorites;
                break;
            case Setting.Monochrome:
                ConsoleColors.Monochrome = !ConsoleColors.Monochrome;
                break;
            case Setting.Language:
                Lang.Set(Lang.Next(next));
                break;
            case Setting.ColorScheme:
            {
                var count = Actions.ColorPresetCount;
                Actions.SetColorPreset((ConsoleColors.GetColorPresetIndex() + (next ? 1 : count - 1)) % count);
                break;
            }
            case Setting.AutoSize:
                Init.AutoSize = !Init.AutoSize;
                Init.ApplyAutoSize();
                break;
            case Setting.Size:
                // Saves the settings itself
                Actions.DisplayOutputDimensions();
                return;
            case Setting.LineNumbers:
                TextArea.LineNumbers = !TextArea.LineNumbers;
                break;
            case Setting.ViewLog:
                EventLog.Show();
                return;
            case Setting.Restart:
                if (Menu.YesNoPrompt(DialogText.Restart, DialogText.RestartYn, string.Empty, true))
                {
                    // Marbles starts over: the settings are read again, and the start page is shown as at startup
                    Tools.RestartFromStartPage();
                }

                return;
            default:
                if (Menu.YesNoPrompt(DialogText.CaptionsSettings, DialogText.ResetSettings, string.Empty, true))
                {
                    Actions.ApplyDefaultSettings();
                }

                return;
        }

        FileIo.SaveSettings();
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

            // The page is drawn again if it could not be drawn, or the terminal window has changed size
            if (Tools.TakeRedrawRequest()) return Result.Redraw;
            var key = Console.ReadKey(true);
            if (Init.AutoSize && Init.ApplyAutoSize()) return Result.Redraw;
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
            else if (key.KeyChar == 'G' || key.Key == ConsoleKey.End)
            {
                selected = choices.Length - 1;
            }
            else if (key.Key == ConsoleKey.Home || (key.KeyChar == 'g' && Console.ReadKey(true).KeyChar == 'g'))
            {
                selected = 0;
            }
            else if (key.KeyChar == '?')
            {
                Tools.WriteEditorHelp(false, Tools.HelpTopic.Start);
                return Result.Redraw;
            }
        }
    }
}
