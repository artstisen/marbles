namespace Marbles;

/// <summary>
/// The page shown when Marbles starts, in a frame with three tabs (TabFrame): Mode, where the user selects what
/// to use Marbles for (notes: the login page, or editing text files: the file browser), Configuration, with the
/// settings on four pages (the sub-tabs General, Layout, Editor and System), and Help (the help and the about
/// page). Tab goes from page to page.
/// The setting "startmode" tells if the page is shown at startup, or if Marbles goes directly to one of the two.
/// The setting "onlymode" makes Marbles a text editor only or a note application only (for running one of each):
/// the other mode is not shown in the Mode tab, and as a text editor Marbles does not open data files.
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

    internal enum OnlyMode
    {
        None, // Both modes
        Editor, // Text Editor only: no Note Management, and data files are not opened
        Notes // Note Management only: no Text Editor in the Mode tab
    }

    /// <summary>
    /// Use Marbles for one of the modes only (setting "onlymode")
    /// </summary>
    internal static OnlyMode Only { get; set; } = OnlyMode.None;

    /// <summary>
    /// What happens when Marbles starts: the start mode, unless it is the mode that is not used
    /// </summary>
    internal static StartMode EffectiveMode =>
        (Mode == StartMode.Notes && Only == OnlyMode.Editor) || (Mode == StartMode.Editor && Only == OnlyMode.Notes)
            ? StartMode.Select
            : Mode;

    /// <summary>
    /// Use one mode only, or both again. A start mode that is no longer used goes back to showing the choices
    /// </summary>
    internal static void SetOnly(OnlyMode only)
    {
        Only = only;
        Mode = EffectiveMode;
    }

    internal static string SettingName(OnlyMode only)
    {
        return only switch
        {
            OnlyMode.Editor => "editor",
            OnlyMode.Notes => "notes",
            _ => "none"
        };
    }

    internal static OnlyMode ParseOnly(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "editor" => OnlyMode.Editor,
            "notes" => OnlyMode.Notes,
            _ => OnlyMode.None
        };
    }

    /// <summary>
    /// True when the page is to be shown (or passed, depending on the setting) before the login page
    /// </summary>
    internal static bool Pending { get; private set; }

    // True to show the choices whatever the setting is
    private static bool _showChoices;

    // The page of the Configuration tab to show first (0 to 3), or -1 for the Mode tab
    private static int _showConfiguration = -1;

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
        _showConfiguration = -1;
    }

    /// <summary>
    /// Show the choices before the login page is shown again
    /// </summary>
    /// <param name="configurationPage">Show a page of the Configuration tab first (0 General, 1 Layout, 2 Editor,
    /// 3 System), or -1 for the Mode tab</param>
    internal static void Open(int configurationPage = -1)
    {
        Pending = true;
        _showChoices = true;
        _showConfiguration = Math.Clamp(configurationPage, -1, 3);
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
            if (EffectiveMode == StartMode.Notes)
            {
                // The login page sweeps the logo
                if (animate) Logo.AnimateNext();
                return;
            }

            if (EffectiveMode == StartMode.Editor && BrowseFiles()) return;
        }

        var mode = new ModePage();
        var frame = _frame = new TabFrame(TabRow, () =>
            {
                Logo.DrawLogo(true, animate, false);
                animate = false;
            },
            new TabItem(() => DialogText.StartModeTab, mode),
            new TabItem(() => DialogText.StartConfiguration, new SettingsPage(0), new SettingsPage(1),
                new SettingsPage(2), new SettingsPage(3)),
            new TabItem(() => Lang.T("Help"), new HelpPage())) { Commands = ReadCommand };
        if (_showConfiguration >= 0) frame.Selected = 1 + _showConfiguration;
        _showConfiguration = -1;
        // Returns when notes are chosen, or a data file was selected in the file browser
        frame.Run();
    }

    // The frame of the page while it is shown (for the commands that go to a tab)
    private static TabFrame? _frame;

    // ":" on any tab: a command of the start page
    private static TabResult ReadCommand()
    {
        var command = CommandLine.Read(CommandContext.Start);
        return command == null ? TabResult.Redraw : RunCommand(command);
    }

    // The commands of the start page: go to a tab or a page of Configuration, open the file browser, the login
    // page, the Jump menu or a page of Marbles, show the help, or exit
    private static TabResult RunCommand(string command)
    {
        switch (CommandLine.Name(command))
        {
            case "note":
            case "notes":
                // The login page (not with Text Editor only)
                if (Only != OnlyMode.Editor) return TabResult.Close;
                Refused(DialogText.NotesNotUsed);
                break;
            case "file":
            case "browse":
                // The file browser (not with Note Management only)
                if (Only != OnlyMode.Notes) return BrowseFiles() ? TabResult.Close : TabResult.Redraw;
                Refused(DialogText.EditorNotUsed);
                break;
            case "mode":
                ShowPage(0);
                break;
            case "config":
            case "general":
                ShowPage(1);
                break;
            case "layout":
                ShowPage(2);
                break;
            case "editor":
                ShowPage(3);
                break;
            case "system":
                ShowPage(4);
                break;
            case "jump":
            case "ju":
                Marbles.Jump.Open();
                break;
            case "book":
                // ":book 3": jump to bookmark 3
                Marbles.Jump.ToBookmark(CommandLine.Argument(command));
                break;
            case "help":
            case "h":
                // The keys of the start page, without going to the Help tab
                Tools.WriteEditorHelp(false, Tools.HelpTopic.Start);
                break;
            case "about":
                Actions.ShowAboutPages();
                break;
            case "quick":
            case "guide":
                QuickGuide.Show();
                break;
            case "scheme":
                SchemeEditor.Show();
                break;
            case "logo":
                LogoEditor.Show();
                break;
            case "snippet":
            case "snip":
                ChoiceBox.Forget();
                if (SnippetBox.EnsureFile()) FileExplorer.EditSnippets(0);
                break;
            case "completions":
            case "comp":
                ChoiceBox.Forget();
                FileExplorer.EditCompletions();
                break;
            case "clipboard":
                ClipboardPage.Show();
                break;
            case "log":
                EventLog.Show();
                break;
            case "restart":
                ChangeSetting(Setting.Restart, true);
                break;
            case "reset":
                // As Reset all settings on the System page: asked first
                ChangeSetting(Setting.Reset, true);
                break;
            case "quit":
            case "exit":
            case "q":
                // As Q on the Mode tab: asked first
                if (ChoiceBox.Confirm(DialogText.Exit, DialogText.ExitQuestion, string.Empty, false))
                {
                    Tools.ExitApplication();
                }

                break;
            default:
                CommandLine.ShowUnknown(command);
                break;
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Tools.ClearScreen();
        return TabResult.Redraw;
    }

    // Go to a page of the frame (0 Mode, 1 to 4 the pages of Configuration)
    private static void ShowPage(int page)
    {
        if (_frame != null) _frame.Selected = page;
    }

    // A mode that is not used
    private static void Refused(string message)
    {
        ChoiceBox.Forget();
        ChoiceBox.Message(DialogText.StartSelectMode, message, string.Empty);
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
        StayLoggedIn,
        Monochrome,
        Language,
        ColorScheme,
        AutoSize,
        Size,
        Logo,
        LogoAnimation,
        LineNumbers,
        WordWrap,
        ScrollOff,
        Clipboard,
        EditorOnly,
        NotesOnly,
        SyntaxColors,
        Spell,
        Snippets,
        AutoComplete,
        Completions,
        Reset,
        EditSettings,
        Restart,
        Info,
        ViewLog
    }

    // The settings shown, in the order of the pages: some are only shown when they can be used
    private static List<Setting> Settings()
    {
        // General: what happens at startup (starting in a mode that is not used cannot be chosen), using one
        // mode only, and the language
        var settings = new List<Setting> { Setting.StartSelect };
        if (Only != OnlyMode.Editor) settings.Add(Setting.StartNotes);
        if (Only != OnlyMode.Notes) settings.Add(Setting.StartEditor);
        if (EffectiveMode == StartMode.Editor) settings.Add(Setting.StartFavorites);
        if (Only != OnlyMode.Editor) settings.Add(Setting.StayLoggedIn);
        settings.Add(Setting.EditorOnly);
        settings.Add(Setting.NotesOnly);
        settings.Add(Setting.Language);
        // Layout
        settings.Add(Setting.AutoSize);
        if (!Init.AutoSize) settings.Add(Setting.Size);
        settings.Add(Setting.Monochrome);
        if (!ConsoleColors.Monochrome) settings.Add(Setting.ColorScheme);
        settings.Add(Setting.Logo);
        settings.Add(Setting.LogoAnimation);
        // Editor
        settings.Add(Setting.LineNumbers);
        settings.Add(Setting.WordWrap);
        settings.Add(Setting.ScrollOff);
        settings.Add(Setting.Clipboard);
        settings.Add(Setting.SyntaxColors);
        settings.Add(Setting.Spell);
        settings.Add(Setting.Snippets);
        settings.Add(Setting.AutoComplete);
        settings.Add(Setting.Completions);
        // System
        settings.Add(Setting.Reset);
        settings.Add(Setting.EditSettings);
        settings.Add(Setting.Restart);
        settings.Add(Setting.Info);
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
            Setting.StayLoggedIn => Check(DataSession.StayLoggedIn) + Lang.T("Stay logged into data file (Session)"),
            Setting.Monochrome => Check(ConsoleColors.Monochrome) + DialogText.ConfigMonochrome,
            Setting.Language => indent + DialogText.ConfigLanguage + Lang.CurrentName,
            Setting.ColorScheme => indent + Lang.F("Color scheme: {0} (Enter: Select/Edit)",
                ColorSchemes.DisplayName(ConsoleColors.Current.Name)),
            Setting.Logo => indent + Lang.F("Logo: {0} (Enter: Select/Edit)", Marbles.Logo.Current.Name),
            Setting.LogoAnimation => indent + Lang.F("Logo animation: {0}", AnimationName(Marbles.Logo.Animation)),
            Setting.AutoSize => Check(Init.AutoSize) + DialogText.ConfigAutoSize,
            Setting.Size => indent + DialogText.ConfigSize + Init.LineWidth + " x " + Init.LineCount +
                            DialogText.ConfigSizeNote,
            Setting.LineNumbers => Check(TextArea.LineNumbers) + DialogText.ConfigLineNumbers,
            Setting.WordWrap => Check(TextArea.WordWrap) + Lang.T("Wrap long lines"),
            Setting.ScrollOff => Check(TextArea.ScrollOff) +
                                 Lang.F("Keep {0} lines around the cursor", TextArea.ScrollOffLines),
            Setting.Clipboard => indent + Lang.T("Clipboard and paste bin (Enter: Change)"),
            Setting.SyntaxColors => Check(SyntaxHighlighter.ColorFiles) + Lang.T("Syntax highlighting"),
            Setting.Spell => indent + SpellText(),
            Setting.Snippets => indent + Lang.T("Snippet editor (Enter: Open)"),
            Setting.AutoComplete => Check(Marbles.Completions.Enabled) + Lang.T("Complete words while typing"),
            Setting.Completions => indent + Lang.T("Completions editor (Enter: Open)"),
            Setting.Info => indent + DialogText.FileInfo,
            Setting.EditorOnly => Check(Only == OnlyMode.Editor) + Lang.T("Text Editor only"),
            Setting.NotesOnly => Check(Only == OnlyMode.Notes) + Lang.T("Note Management only"),
            Setting.EditSettings => indent + Lang.F("Edit {0}", FileIo.SettingsFileName),
            Setting.Restart => indent + DialogText.ConfigRestart,
            Setting.ViewLog => indent + DialogText.ConfigViewLog,
            _ => indent + DialogText.ConfigReset
        };
    }

    // The languages of the spell checker (":spell" turns it on in the editor), or that there are no spell files
    private static string SpellText()
    {
        return SpellChecker.AvailableLanguages().Count == 0
            ? Lang.T("Spell checker: no spell files")
            : Lang.F("Spell checker: {0}", SpellChecker.Languages);
    }

    private const int PageCount = 4;

    // The settings are shown on four pages: General, Layout, Editor and System
    private static int PageOf(Setting setting)
    {
        return setting switch
        {
            Setting.StartSelect or Setting.StartNotes or Setting.StartEditor or Setting.StartFavorites
                or Setting.StayLoggedIn or Setting.EditorOnly or Setting.NotesOnly or Setting.Language => 0,
            Setting.AutoSize or Setting.Size or Setting.Monochrome or Setting.ColorScheme or Setting.Logo
                or Setting.LogoAnimation => 1,
            Setting.LineNumbers or Setting.WordWrap or Setting.ScrollOff or Setting.Clipboard
                or Setting.SyntaxColors or Setting.Spell or Setting.Snippets or Setting.AutoComplete
                or Setting.Completions => 2,
            _ => 3
        };
    }

    // In the language in use
    private static string PageTitle(int page)
    {
        return page switch
        {
            0 => Lang.T("General"),
            1 => Lang.T("Layout"),
            2 => Lang.T("Editor"),
            _ => Lang.T("System")
        };
    }

    // A blank row is shown above these settings when there is room: they start a group of their page
    private static bool StartsGroup(Setting setting) => setting is Setting.EditorOnly or Setting.Language;

    // Pick the languages of the spell checker from the ones there is a spell file for: Enter turns one on or
    // off, until the box is closed with Esc
    private static void PickSpellLanguages()
    {
        var available = SpellChecker.AvailableLanguages();
        ChoiceBox.Forget();
        if (available.Count == 0)
        {
            ChoiceBox.Message(DialogText.CaptionsSpell,
                DialogText.SpellFileNotFound(Path.GetFullPath(SpellChecker.DirectoryName)), string.Empty);
            return;
        }

        var keys = Enumerable.Range(1, available.Count).Select(n => n <= 9 ? n.ToString() : " ").ToArray();
        var selected = 0;
        while (true)
        {
            var used = SpellChecker.Languages.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var choices = available.Select(language =>
                Check(used.Contains(language, StringComparer.OrdinalIgnoreCase)) + language).ToArray();
            var picked = ChoiceBox.Pick(DialogText.CaptionsSpell, Lang.T("The languages of the spell checker"),
                Lang.T("Enter turns a language on or off. Esc closes."), choices, keys, selected, false);
            if (picked < 0) return;
            selected = picked;
            SpellChecker.Languages = SpellChecker.ToggleLanguage(available, SpellChecker.Languages, available[picked]);
            FileIo.SaveSettings();
        }
    }

    // The speeds of the sweep of the logo, as they are listed
    private static readonly Logo.AnimationSpeed[] AnimationSpeeds =
    {
        Marbles.Logo.AnimationSpeed.Slow, Marbles.Logo.AnimationSpeed.Fast, Marbles.Logo.AnimationSpeed.VeryFast,
        Marbles.Logo.AnimationSpeed.Off
    };

    // In the language in use
    private static string AnimationName(Logo.AnimationSpeed speed)
    {
        return speed switch
        {
            Marbles.Logo.AnimationSpeed.Slow => Lang.T("Slow"),
            Marbles.Logo.AnimationSpeed.VeryFast => Lang.T("Very fast"),
            Marbles.Logo.AnimationSpeed.Off => Lang.T("Off"),
            _ => Lang.T("Fast")
        };
    }

    // Pick how fast the logo is swept in (or that it is not) from a list in a box
    private static void PickLogoAnimation()
    {
        ChoiceBox.Forget();
        var picked = ChoiceBox.Pick(Lang.T("Logo animation"), Lang.T("How fast the logo is swept in on the login page"),
            string.Empty, AnimationSpeeds.Select(AnimationName).ToArray(), new[] { "1", "2", "3", "4" },
            Math.Max(0, Array.IndexOf(AnimationSpeeds, Marbles.Logo.Animation)), false);
        if (picked < 0) return;
        Marbles.Logo.Animation = AnimationSpeeds[picked];
        FileIo.SaveSettings();
    }

    // Where Marbles is: its version, and the directories of the program and of the data
    private static void ShowInfo()
    {
        ChoiceBox.Forget();
        ChoiceBox.ShowRows(DialogText.FileInfo, new List<(string Label, string Value)>
        {
            (Lang.T("Version:"), DialogText.ApplicationName + " " + DialogText.ApplicationVersion),
            (Lang.T("Program:"), Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppContext.BaseDirectory))),
            (Lang.T("Data:"), Path.GetFullPath(FileIo.GetDataDirectoryName())),
            (Lang.T("Settings:"), Path.Combine(Path.GetFullPath(FileIo.GetDataDirectoryName()), FileIo.SettingsFileName))
        });
    }

    /// <summary>
    /// The Mode tab: what to use Marbles for (notes or text files), the quick guide, and exit
    /// </summary>
    private sealed class ModePage : ITabPage
    {
        private int _selected;

        private enum Choice
        {
            Notes,
            Editor,
            Jump,
            QuickGuide,
            Exit
        }

        // The choices shown: a mode that is not used (Text Editor only, Note Management only) is left out
        private static List<Choice> ChoicesShown()
        {
            var choices = new List<Choice>();
            if (Only != OnlyMode.Editor) choices.Add(Choice.Notes);
            if (Only != OnlyMode.Notes) choices.Add(Choice.Editor);
            choices.Add(Choice.Jump);
            choices.Add(Choice.QuickGuide);
            choices.Add(Choice.Exit);
            return choices;
        }

        // The keys that open a choice at once (the same in all languages; the numbers only select). Notes: N,
        // the file browser: F, the Jump menu: O (as Ctrl+O; j moves down), the quick guide: U, exit: Q (asks)
        private static string KeysOf(Choice choice) => choice switch
        {
            Choice.Notes => "N",
            Choice.Editor => "F",
            Choice.Jump => "O",
            Choice.QuickGuide => "U",
            _ => "Q"
        };

        // In the language in use
        private static string[] Choices => ChoicesShown().Select(choice => choice switch
        {
            Choice.Notes => DialogText.StartNotes,
            Choice.Editor => DialogText.StartEditor,
            Choice.Jump => DialogText.StartJump,
            Choice.QuickGuide => DialogText.QuickGuide,
            _ => DialogText.StartExit
        }).ToArray();

        public string Title => DialogText.StartModeTab;
        public string Keys => DialogText.StartKeys;
        public Tools.HelpTopic Help => Tools.HelpTopic.Start;

        public void Draw(TabArea area, bool full)
        {
            var choices = Choices;
            var shown = ChoicesShown();
            _selected = Math.Min(_selected, choices.Length - 1);
            if (full)
            {
                for (var i = 0; i < choices.Length; i++)
                {
                    // The number, the keys that open the choice at once, and the choice
                    Tools.SetCursorEx(area.Top + i, area.Left + 3);
                    Console.Write(Tools.TruncateString((i + 1) + " " + KeysOf(shown[i]) + " .... " +
                                                       choices[i], area.Width - 4, true));
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
            var shown = ChoicesShown();
            var count = shown.Count;
            _selected = Math.Min(_selected, count - 1);
            // Ctrl+O: the Jump menu, as on the other pages
            if (Marbles.Jump.IsKey(key)) return Open(Choice.Jump);
            if (key.Key is ConsoleKey.Enter or ConsoleKey.L or ConsoleKey.RightArrow) return Open(shown[_selected]);
            // The keys of the choices open them at once
            var direct = key.Key switch
            {
                ConsoleKey.N => Choice.Notes,
                ConsoleKey.F => Choice.Editor,
                ConsoleKey.O => Choice.Jump,
                ConsoleKey.U => Choice.QuickGuide,
                _ => (Choice?)null
            };
            if (direct is { } choice && (key.Modifiers & (ConsoleModifiers.Control | ConsoleModifiers.Alt)) == 0)
            {
                // A mode that is not used (Text Editor only, Note Management only) is not opened
                if (!shown.Contains(choice)) return TabResult.None;
                _selected = shown.IndexOf(choice);
                return Open(choice);
            }

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

        // Open a choice: notes (the login page), the file browser, the Jump menu, the quick guide, or exit
        private static TabResult Open(Choice choice)
        {
            switch (choice)
            {
                case Choice.Notes:
                    return TabResult.Close;
                case Choice.Editor:
                    return BrowseFiles() ? TabResult.Close : TabResult.Redraw;
                case Choice.Jump:
                    // A place that is opened on top of the page returns here; the others leave it
                    Marbles.Jump.Open();
                    return TabResult.Redraw;
                case Choice.QuickGuide:
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
    /// The Help tab: the help (all the keys) and the about page
    /// </summary>
    private sealed class HelpPage : ITabPage
    {
        private int _selected;

        // In the language in use
        private static string[] Choices => new[] { Lang.T("Help"), Lang.T("About") };

        public string Title => Lang.T("Help");
        public string Keys => DialogText.StartHelpKeys;
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
                if (i == _selected) ConsoleColors.SetColors(ConsoleColors.ColorPreset.SelectorInverted);
                Console.Write(i == _selected ? "->" : "  ");
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            }
        }

        public TabResult HandleKey(ConsoleKeyInfo key)
        {
            var count = Choices.Length;
            // Back to the Mode tab
            if (key.Key is ConsoleKey.Escape or ConsoleKey.Q) return TabResult.FirstTab;
            if (key.Key is ConsoleKey.Enter or ConsoleKey.Spacebar or ConsoleKey.L or ConsoleKey.RightArrow)
            {
                if (_selected == 0)
                {
                    // As ":help": all the keys
                    Tools.WriteEditorHelp(false);
                }
                else
                {
                    Actions.ShowAboutPages();
                }

                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                Tools.ClearScreen();
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

        public string Title => PageTitle(_page);
        public string Keys => DialogText.StartConfigKeys;
        public Tools.HelpTopic Help => Tools.HelpTopic.Start;

        private List<Setting> PageSettings => Settings().Where(setting => PageOf(setting) == _page).ToList();

        private int SelectedIndex(List<Setting> settings) =>
            _selected is { } selected ? Math.Max(0, settings.IndexOf(selected)) : 0;

        public void Draw(TabArea area, bool full)
        {
            var settings = PageSettings;
            var selected = SelectedIndex(settings);
            // A blank row above the groups of the page, when there is room for them
            var groups = settings.Count(StartsGroup);
            var gaps = settings.Count + groups <= area.Height;
            int Row(int i) => area.Top + i + (gaps ? settings.Take(i + 1).Count(StartsGroup) : 0);
            if (full)
            {
                // The settings without a check box are indented to line up with the names of the ones with a
                // check box. On a page without check boxes they line up with the pointer
                var texts = settings.Select(SettingText).ToList();
                var checkBoxes = texts.Any(text => text.StartsWith('['));
                for (var i = 0; i < settings.Count; i++)
                {
                    Tools.SetCursorEx(Row(i), area.Left + 2);
                    var text = checkBoxes ? texts[i] : texts[i].TrimStart();
                    Console.Write(" " + Tools.TruncateString(text, area.Width - 4, true));
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
            else if (key.Key is ConsoleKey.Enter or ConsoleKey.Spacebar &&
                     setting == Setting.ColorScheme)
            {
                // The color schemes: select and edit them
                SchemeEditor.Show();
                return TabResult.Redraw;
            }
            else if (key.Key is ConsoleKey.Enter or ConsoleKey.Spacebar &&
                     setting == Setting.Logo)
            {
                // The logos: select and edit them
                LogoEditor.Show();
                return TabResult.Redraw;
            }
            else if (key.Key is ConsoleKey.Enter or ConsoleKey.Spacebar && setting == Setting.Spell)
            {
                // Pick the languages of the spell checker in a box
                PickSpellLanguages();
                return TabResult.Redraw;
            }
            else if (key.Key is ConsoleKey.Enter or ConsoleKey.Spacebar && setting == Setting.Language)
            {
                // Pick the language in a box
                Actions.PickLanguage();
                return TabResult.Redraw;
            }
            else if (key.Key is ConsoleKey.Enter or ConsoleKey.Spacebar)
            {
                ChangeSetting(setting, true);
                _selected = setting;
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
            case Setting.StayLoggedIn:
                DataSession.StayLoggedIn = !DataSession.StayLoggedIn;
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
            case Setting.SyntaxColors:
                SyntaxHighlighter.ColorFiles = !SyntaxHighlighter.ColorFiles;
                break;
            case Setting.Snippets:
                // The file of snippets in the editor (it is made first, if the user wants that)
                ChoiceBox.Forget();
                if (SnippetBox.EnsureFile()) FileExplorer.EditSnippets(0);
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                Tools.ClearScreen();
                return;
            case Setting.AutoComplete:
                // Turned on: the file of completions is made if there is none
                Marbles.Completions.Enabled = !Marbles.Completions.Enabled;
                if (Marbles.Completions.Enabled) Marbles.Completions.EnsureFile();
                break;
            case Setting.Completions:
                // The file of completions in the editor (made first if there is none)
                ChoiceBox.Forget();
                FileExplorer.EditCompletions();
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                Tools.ClearScreen();
                return;
            case Setting.Info:
                ShowInfo();
                return;
            case Setting.LogoAnimation:
                // Saves the settings itself
                PickLogoAnimation();
                return;
            case Setting.EditorOnly:
                // Only one of the two can be set
                SetOnly(Only == OnlyMode.Editor ? OnlyMode.None : OnlyMode.Editor);
                break;
            case Setting.NotesOnly:
                SetOnly(Only == OnlyMode.Notes ? OnlyMode.None : OnlyMode.Notes);
                break;
            case Setting.ViewLog:
                EventLog.Show();
                return;
            case Setting.Clipboard:
                // Saves the settings itself
                ClipboardPage.Show();
                return;
            case Setting.EditSettings:
                // The settings file in the editor. Saved: Marbles starts over and reads the settings again
                if (Tools.EditSettingsFile()) Tools.RestartFromStartPage();
                Tools.ClearScreen();
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
