namespace Marbles;

/// <summary>
/// The page shown when Marbles starts, in a frame with two tabs (TabFrame): Mode, where the user selects what to
/// use Marbles for (notes: the login page, or editing text files: the file browser), and Configuration, with the
/// settings of the page itself and some basic settings on two pages (sub-tabs). Tab goes from page to page.
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

    // The row of the tabs (below the logo)
    private const int TabRow = 6;

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

        var mode = new ModePage();
        var frame = new TabFrame(TabRow, () =>
            {
                Logo.DrawLogo(true, animate, false);
                animate = false;
            },
            new TabItem(() => DialogText.StartModeTab, mode),
            new TabItem(() => DialogText.StartConfiguration, new SettingsPage(0), new SettingsPage(1)));
        // Returns when notes are chosen, or a data file was selected in the file browser
        frame.Run();
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
        Logo,
        LineNumbers,
        WordWrap,
        ScrollOff,
        Clipboard,
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
        settings.Add(Setting.Logo);
        settings.Add(Setting.Language);
        settings.Add(Setting.AutoSize);
        if (!Init.AutoSize) settings.Add(Setting.Size);
        settings.Add(Setting.LineNumbers);
        settings.Add(Setting.WordWrap);
        settings.Add(Setting.ScrollOff);
        settings.Add(Setting.Clipboard);
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
            Setting.ColorScheme => indent + Lang.F("Color scheme: {0} (Enter: Select/Edit)",
                ColorSchemes.DisplayName(ConsoleColors.Current.Name)),
            Setting.Logo => indent + Lang.F("Logo: {0} (Enter: Select/Edit)", Marbles.Logo.Current.Name),
            Setting.AutoSize => Check(Init.AutoSize) + DialogText.ConfigAutoSize,
            Setting.Size => indent + DialogText.ConfigSize + Init.LineWidth + " x " + Init.LineCount +
                            DialogText.ConfigSizeNote,
            Setting.LineNumbers => Check(TextArea.LineNumbers) + DialogText.ConfigLineNumbers,
            Setting.WordWrap => Check(TextArea.WordWrap) + Lang.T("Wrap long lines in the editor"),
            Setting.ScrollOff => Check(TextArea.ScrollOff) +
                                 Lang.F("Keep {0} lines around the cursor in the editor", TextArea.ScrollOffLines),
            Setting.Clipboard => indent + Lang.T("Clipboard and paste bin (Enter: Change)"),
            Setting.Restart => indent + DialogText.ConfigRestart,
            Setting.ViewLog => indent + DialogText.ConfigViewLog,
            _ => indent + DialogText.ConfigReset
        };
    }

    // The settings are shown on two pages: the start page and the display, and the editor and the rest
    private static int PageOf(Setting setting) => setting >= Setting.LineNumbers ? 1 : 0;

    /// <summary>
    /// The Mode tab: what to use Marbles for (notes or text files), the quick guide, and exit
    /// </summary>
    private sealed class ModePage : ITabPage
    {
        private int _selected;

        // In the language in use
        private static string[] Choices => new[]
        {
            DialogText.StartNotes, DialogText.StartEditor, DialogText.QuickGuide, DialogText.StartExit
        };

        public string Title => DialogText.StartModeTab;
        public string Keys => DialogText.StartKeys;
        public Tools.HelpTopic Help => Tools.HelpTopic.Start;

        public void Draw(TabArea area, bool full)
        {
            var choices = Choices;
            if (full)
            {
                for (var i = 0; i < choices.Length; i++)
                {
                    Tools.SetCursorEx(area.Top + i, area.Left + 3);
                    Console.Write(Tools.TruncateString((i + 1) + " .... " + choices[i], area.Width - 4, true));
                }
            }

            for (var i = 0; i < choices.Length; i++)
            {
                Tools.SetCursorEx(area.Top + i, area.Left);
                // The pointer is highlighted as in the lists of entries
                if (i == _selected) ConsoleColors.SetColors(ConsoleColors.ColorPreset.SelectorInverted);
                Console.Write(i == _selected ? "->" : "  ");
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            }
        }

        public TabResult HandleKey(ConsoleKeyInfo key)
        {
            var count = Choices.Length;
            if (key.Key is ConsoleKey.Enter or ConsoleKey.L or ConsoleKey.RightArrow) return Open(_selected);
            if (key.Key is ConsoleKey.Escape or ConsoleKey.Q)
            {
                if (ChoiceBox.Confirm(DialogText.Exit, DialogText.ExitQuestion, string.Empty, false))
                {
                    Tools.ExitApplication();
                }

                return TabResult.Redraw;
            }

            if (key.Key is ConsoleKey.J or ConsoleKey.DownArrow)
            {
                _selected = (_selected + 1) % count;
            }
            else if (key.Key is ConsoleKey.K or ConsoleKey.UpArrow)
            {
                _selected = (_selected + count - 1) % count;
            }
            else if (key.KeyChar >= '1' && key.KeyChar - '1' < count)
            {
                _selected = key.KeyChar - '1';
            }
            else if (key.KeyChar == 'G' || key.Key == ConsoleKey.End)
            {
                _selected = count - 1;
            }
            else if (key.Key == ConsoleKey.Home || (key.KeyChar == 'g' && Console.ReadKey(true).KeyChar == 'g'))
            {
                _selected = 0;
            }

            return TabResult.None;
        }

        // Open a choice: notes (the login page), the file browser, the quick guide, or exit
        private static TabResult Open(int choice)
        {
            switch (choice)
            {
                case 0:
                    return TabResult.Close;
                case 1:
                    return BrowseFiles() ? TabResult.Close : TabResult.Redraw;
                case 2:
                    QuickGuide.Show();
                    return TabResult.Redraw;
                default:
                    // Exit without asking
                    Tools.ExitApplication();
                    return TabResult.Redraw;
            }
        }
    }

    /// <summary>
    /// A page of the Configuration tab. A change is saved in the settings file at once
    /// </summary>
    private sealed class SettingsPage : ITabPage
    {
        private readonly int _page;
        private Setting? _selected; // Kept by the setting, as rows come and go (the size, the color scheme)

        internal SettingsPage(int page)
        {
            _page = page;
        }

        public string Title => Lang.F("Page {0}", _page + 1);
        public string Keys => DialogText.StartConfigKeys;
        public Tools.HelpTopic Help => Tools.HelpTopic.Start;

        private List<Setting> PageSettings => Settings().Where(setting => PageOf(setting) == _page).ToList();

        private int SelectedIndex(List<Setting> settings) =>
            _selected is { } selected ? Math.Max(0, settings.IndexOf(selected)) : 0;

        public void Draw(TabArea area, bool full)
        {
            var settings = PageSettings;
            var selected = SelectedIndex(settings);
            // A blank row between the settings of the start page and the basic settings, when there is room for it
            var gap = settings.Count + 1 <= area.Height ? 1 : 0;
            int Row(int i) => area.Top + i + (settings[i] >= Setting.Monochrome && _page == 0 ? gap : 0);
            if (full)
            {
                for (var i = 0; i < settings.Count; i++)
                {
                    Tools.SetCursorEx(Row(i), area.Left + 2);
                    Console.Write(" " + Tools.TruncateString(SettingText(settings[i]), area.Width - 4, true));
                }
            }

            for (var i = 0; i < settings.Count; i++)
            {
                Tools.SetCursorEx(Row(i), area.Left);
                if (i == selected) ConsoleColors.SetColors(ConsoleColors.ColorPreset.SelectorInverted);
                Console.Write(i == selected ? "->" : "  ");
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            }
        }

        public TabResult HandleKey(ConsoleKeyInfo key)
        {
            var settings = PageSettings;
            var selected = SelectedIndex(settings);
            var count = settings.Count;
            var setting = settings[selected];
            // Back to the Mode tab
            if (key.Key is ConsoleKey.Escape or ConsoleKey.Q) return TabResult.FirstTab;
            if (key.Key is ConsoleKey.J or ConsoleKey.DownArrow)
            {
                _selected = settings[(selected + 1) % count];
            }
            else if (key.Key is ConsoleKey.K or ConsoleKey.UpArrow)
            {
                _selected = settings[(selected + count - 1) % count];
            }
            else if (key.KeyChar >= '1' && key.KeyChar - '1' < count)
            {
                _selected = settings[key.KeyChar - '1'];
            }
            else if (key.KeyChar == 'G' || key.Key == ConsoleKey.End)
            {
                _selected = settings[count - 1];
            }
            else if (key.Key == ConsoleKey.Home || (key.KeyChar == 'g' && Console.ReadKey(true).KeyChar == 'g'))
            {
                _selected = settings[0];
            }
            else if (key.Key is ConsoleKey.Enter or ConsoleKey.Spacebar or ConsoleKey.L or ConsoleKey.RightArrow &&
                     setting == Setting.ColorScheme)
            {
                // The color schemes: select and edit them
                SchemeEditor.Show();
                return TabResult.Redraw;
            }
            else if (key.Key is ConsoleKey.Enter or ConsoleKey.Spacebar or ConsoleKey.L or ConsoleKey.RightArrow &&
                     setting == Setting.Logo)
            {
                // The logos: select and edit them
                LogoEditor.Show();
                return TabResult.Redraw;
            }
            else if (key.Key is ConsoleKey.Enter or ConsoleKey.Spacebar && setting == Setting.Language)
            {
                // Pick the language in a box (l and h select the next and previous)
                Actions.PickLanguage();
                return TabResult.Redraw;
            }
            else if (key.Key is ConsoleKey.Enter or ConsoleKey.Spacebar or ConsoleKey.L or ConsoleKey.RightArrow)
            {
                ChangeSetting(setting, true);
                _selected = setting;
                return TabResult.Redraw;
            }
            else if (key.Key is ConsoleKey.H or ConsoleKey.LeftArrow && setting is Setting.Language)
            {
                // The previous language
                ChangeSetting(setting, false);
                return TabResult.Redraw;
            }

            return TabResult.None;
        }
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
            case Setting.AutoSize:
                Init.AutoSize = !Init.AutoSize;
                // Turned off: back to the default size
                if (!Init.AutoSize) Init.SetDefaultOutputDimensions();
                Init.ApplyAutoSize();
                break;
            case Setting.Size:
                // Saves the settings itself
                Actions.DisplayOutputDimensions();
                return;
            case Setting.LineNumbers:
                TextArea.LineNumbers = !TextArea.LineNumbers;
                break;
            case Setting.ScrollOff:
                TextArea.ScrollOff = !TextArea.ScrollOff;
                break;
            case Setting.WordWrap:
                TextArea.WordWrap = !TextArea.WordWrap;
                break;
            case Setting.ViewLog:
                EventLog.Show();
                return;
            case Setting.Clipboard:
                // Saves the settings itself
                ClipboardPage.Show();
                return;
            case Setting.Restart:
                if (ChoiceBox.Confirm(DialogText.Restart, DialogText.RestartQuestion, string.Empty, false))
                {
                    // Marbles starts over: the settings are read again, and the start page is shown as at startup
                    Tools.RestartFromStartPage();
                }

                return;
            default:
                if (ChoiceBox.Confirm(DialogText.CaptionsSettings, DialogText.ResetSettingsQuestion, string.Empty, true))
                {
                    Actions.ApplyDefaultSettings();
                }

                return;
        }

        FileIo.SaveSettings();
    }
}
