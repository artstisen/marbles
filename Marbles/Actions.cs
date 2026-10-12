using System.Globalization;

namespace Marbles;

internal static class Actions
{
    private static ConsoleKeyInfo _c;

    /// <summary>
    /// Handle a key pressed in the main menu, when viewing an entry or in a list of entries.
    /// Called by the main loop for every key (see Navigation).
    /// </summary>
    public static void HandleKey(ConsoleKeyInfo key)
    {
        _c = key;
        if (Mode.CurrentMode == Mode.SelectedMode.Manage)
        {
            #region Manage actions

            if (_c.KeyChar == ':')
            {
                // Command line
                var command = CommandLine.Read(CommandContext.Menu);
                if (command != null)
                {
                    RunManageCommand(command);
                }
            }
            else if (_c.KeyChar == '?')
            {
                // Help
                Tools.WriteEditorHelp(false, Tools.HelpTopic.MainMenu);
                Menu.DrawTopMenu(false, true, false);
            }
            else if (_c.KeyChar == 'G')
            {
                // The last item of the last box
                Menu.SelectLastMenuItem();
                Menu.DrawTopMenu(false, true, false);
            }
            else if (_c.KeyChar == 'g')
            {
                // "gg": The first item of the first box
                if (Console.ReadKey(true).KeyChar == 'g')
                {
                    Menu.SelectFirstMenuItem();
                    Menu.DrawTopMenu(false, true, false);
                }
            }
            else if (_c.Key == ConsoleKey.X)
            {
                // Logout
                Tools.LoginRoutine(true);
            }
            else if (_c.Key == ConsoleKey.Escape)
            {
                // Log out, go to the Select mode page or exit
                ClearSearchQuery();
                switch (Menu.LeaveChoice())
                {
                    case 0:
                        Tools.LoginRoutine(true);
                        break;
                    case 1:
                        StartMenu.Open();
                        Tools.LoginRoutine(true);
                        break;
                    case 2:
                        // The box was the question
                        Tools.ExitApplication();
                        break;
                    default:
                        Menu.DrawTopMenu(false, true, false);
                        break;
                }
            }

            else if (_c.Key == ConsoleKey.N)
            {
                // New entry
                NewEntry();
            }
            else if (_c.Key == ConsoleKey.R)
            {
                // Recall
                RecallEntry();
            }
            else if (_c.Key == ConsoleKey.Tab)
            {
                // The next box (Shift: the previous), on this page or the other
                if (Menu.NextMenuBox((_c.Modifiers & ConsoleModifiers.Shift) != 0))
                {
                    Menu.DrawTopMenu(false, true, false);
                }
            }
            else if (_c.Key is ConsoleKey.J or ConsoleKey.DownArrow)
            {
                if (Menu.MoveMenuSelection(1)) Menu.DrawTopMenu(false, true, false);
            }
            else if (_c.Key is ConsoleKey.K or ConsoleKey.UpArrow)
            {
                if (Menu.MoveMenuSelection(-1)) Menu.DrawTopMenu(false, true, false);
            }
            else if (_c.Key is ConsoleKey.Enter or ConsoleKey.L or ConsoleKey.RightArrow or ConsoleKey.Spacebar)
            {
                // Run the selected item (l and Space as Enter, Space for the settings that are on or off): a command,
                // or the key of the item
                var run = Menu.SelectedMenuItem();
                if (run != null && run.Length > 1 && run[0] == ':')
                {
                    RunManageCommand(run[1..]);
                }
                else if (run != null)
                {
                    var c = run[0];
                    HandleKey(new ConsoleKeyInfo(c, char.IsAsciiLetter(c) ? ConsoleKey.A + (char.ToUpperInvariant(c) - 'A') : 0,
                        false, false, false));
                }
            }
            else if (_c.Key is ConsoleKey.LeftArrow or ConsoleKey.H)
            {
                // The previous page
                Menu.PreviousTopMenuManagePage();
                Menu.DrawTopMenu(false, true, false);
            }
            else if (_c.Key == ConsoleKey.Home)
            {
                // The first item of the first box
                Menu.SelectFirstMenuItem();
                Menu.DrawTopMenu(false, true, false);
            }
            else if (_c.Key == ConsoleKey.End)
            {
                // The last item of the last box
                Menu.SelectLastMenuItem();
                Menu.DrawTopMenu(false, true, false);
            }
            else if (_c.Key == ConsoleKey.D1)
            {
                // Menuitem 1
                Menu.SetTopMenuManagePage(0);
                Menu.DrawTopMenu(false, true, false);
            }
            else if (_c.Key == ConsoleKey.D2)
            {
                // Menuitem 2
                Menu.SetTopMenuManagePage(1);
                Menu.DrawTopMenu(false, true, false);
            }
            else if (_c.Key == ConsoleKey.I)
            {
                // Data file info
                FileActions.ShowDataFileInfo(true);
                Menu.DrawTopMenu(false, true, false);
            }
            else if (_c.Key == ConsoleKey.D)
            {
                // Display timestamp
                Tools.DisplayDateTime();
                Menu.DrawTopMenu(false, true, false);
            }
            else if (_c.Key == ConsoleKey.S || _c.KeyChar == '/')
            {
                // Search
                DefaultSearchSortOption();
                ClearSearchQuery();
                Search();
            }
            else if (_c.Key == ConsoleKey.A)
            {
                // List all
                DefaultListSortOption();
                FindAll(0, string.Empty, false, false);
            }
            else if (_c.Key == ConsoleKey.T)
            {
                // Top 20
                FindAll(1, string.Empty, false, false);
            }
            else if (_c.Key == ConsoleKey.F)
            {
                // Favorites
                FindAll(2, string.Empty, false, false);
            }
            else if (_c.Key is ConsoleKey.M)
            {
                // Session
                FindAll(3, string.Empty, false, false);
            }


            #endregion
        }
        else if (Mode.CurrentMode == Mode.SelectedMode.Search)
        {
            #region Search actions

            if (_isSelectingResult)
            {
                HandleListKey();
            }
            else if (_c.Key == ConsoleKey.Escape || _c.KeyChar == '-')
            {
                Mode.SetMode(Mode.SelectedMode.Manage);
                Menu.DrawTopMenu(false, true, false);
            }

            #endregion
        }
    }

    #region Command line commands

    // Commands available everywhere
    private static bool RunCommonCommand(string command)
    {
        switch (command)
        {
            case "qa":
            case "quitall":
            case "exit":
                Tools.ExitApplicationQuestion();
                return true;
            case "logout":
                Tools.LoginRoutine(true);
                return true;
            default:
                return false;
        }
    }

    // Open the file browser. Opening a file there logs out of the data file, and the login page is shown
    // when the browser is closed. If no file is opened the caller redraws its screen
    private static void BrowseFiles()
    {
        FileExplorer.Run();
        if (FileExplorer.SessionEnded)
        {
            Tools.LoginRoutine(true);
        }
    }

    // Commands of the main menu
    private static void RunManageCommand(string command)
    {
        if (RunCommonCommand(command)) return;
        // A number after the command selects a preset directly, like ":color 5"
        var number = int.TryParse(CommandLine.Argument(command), out var parsedNumber) ? parsedNumber : 0;
        if (CommandLine.Argument(command).Length > 0 &&
            !(CommandLine.Name(command) == "color" && number > 0) && command != "size auto" &&
            CommandLine.Name(command) is not ("new" or "language"))
        {
            CommandLine.ShowUnknown(command);
            Menu.DrawTopMenu(false, true, false);
            return;
        }

        switch (CommandLine.Name(command))
        {
            case "q":
            case "quit":
                // Exit application
                Tools.ExitApplicationQuestion();
                break;
            case "h":
            case "help":
                Tools.WriteEditorHelp(false, Tools.HelpTopic.MainMenu);
                Menu.DrawTopMenu(false, true, false);
                break;
            case "about":
                ShowAbout();
                break;
            case "guide":
                QuickGuide.Show();
                Menu.DrawTopMenu(false, true, false);
                break;
            case "new":
                NewEntry(CommandLine.LastArgument);
                break;
            case "browse":
                BrowseFiles();
                Menu.DrawTopMenu(false, true, false);
                break;
            case "number":
            case "nu":
                // Line numbers in the editor on or off
                ChangeMenuFocus(Menu.PageOfCommand(CommandLine.Name(command)));
                TextArea.LineNumbers = !TextArea.LineNumbers;
                FileIo.SaveSettings();
                Menu.DrawTopMenu(false, true, false);
                break;
            case "import":
                ImportEntry();
                break;
            case "transfer":
                // Switch to settings page if on a different index
                ChangeMenuFocus(Menu.PageOfCommand(CommandLine.Name(command)));
                Entries.TransferEntry();
                Menu.DrawTopMenu(false, true, false);
                break;
            case "undelete":
                UndoDeletion();
                break;
            case "backup":
                ToggleBackup();
                break;
            case "defaults":
                ResetSettings();
                break;
            case "clipboard":
                // The clipboard settings, a page of their own
                ClipboardPage.Show();
                ChangeMenuFocus(Menu.PageOfCommand(CommandLine.Name(command)));
                Menu.DrawTopMenu(false, true, false);
                break;
            case "scheme":
                // The color scheme editor, a page of its own
                SchemeEditor.Show();
                ChangeMenuFocus(Menu.PageOfCommand(CommandLine.Name(command)));
                Menu.DrawTopMenu(false, true, false);
                break;
            case "bw":
                // Black and white on or off
                ChangeMenuFocus(Menu.PageOfCommand(CommandLine.Name(command)));
                ConsoleColors.Monochrome = !ConsoleColors.Monochrome;
                FileIo.SaveSettings();
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                Menu.DrawTopMenu(false, true, false);
                break;
            case "size":
                if (command == "size auto")
                {
                    // Toggle automatic dimensions (following the size of the terminal window)
                    ChangeMenuFocus(Menu.PageOfCommand(CommandLine.Name(command)));
                    Init.AutoSize = !Init.AutoSize;
                    // Turned off: back to the default size
                    if (!Init.AutoSize) Init.SetDefaultOutputDimensions();
                    Init.ApplyAutoSize();
                    FileIo.SaveSettings();
                }
                else
                {
                    DisplayOutputDimensions();
                }

                Menu.DrawTopMenu(false, true, false);
                break;
            case "password":
                Tools.ResetPasswordRoutine();
                break;
            case "language":
            {
                // A code selects the language, and without one it is picked in a box
                var code = CommandLine.Argument(command);
                if (code.Length == 0)
                {
                    PickLanguage();
                    code = Lang.Current;
                }

                if (!Lang.Set(code))
                {
                    Menu.StatusMessage(DialogText.LanguageUnknown + string.Join(", ", Lang.Available.Select(l => l.Code)),
                        true, true, true, DialogText.CaptionsSettings, string.Empty, false);
                }

                ChangeMenuFocus(Menu.PageOfCommand(CommandLine.Name(command)));
                FileIo.SaveSettings();
                Menu.DrawTopMenu(false, true, false);
                break;
            }
            case "color":
                if (number > ColorPresets.Length)
                {
                    CommandLine.ShowUnknown(command);
                    Menu.DrawTopMenu(false, true, false);
                    break;
                }

                // Switch to the page of the colors if on a different one
                ChangeMenuFocus(Menu.PageOfCommand(CommandLine.Name(command)));
                // A number selects the color preset, and without one it is picked in a box
                if (number > 0)
                {
                    SetColorPreset(number - 1);
                    FileIo.SaveSettings();
                }
                else
                {
                    PickColorScheme();
                }

                Menu.DrawTopMenu(false, true, false);
                break;
            case "restart":
                Tools.Restart();
                break;
            default:
                CommandLine.ShowUnknown(command);
                Menu.DrawTopMenu(false, true, false);
                break;
        }
    }

    // The color presets in the order they are numbered (the first is the default colors)
    private static readonly ConsoleColors.ColorPreset[] ColorPresets =
    {
        ConsoleColors.ColorPreset.Reset,
        ConsoleColors.ColorPreset.DeepBlue,
        ConsoleColors.ColorPreset.MarblesQuest,
        ConsoleColors.ColorPreset.NightWriter,
        ConsoleColors.ColorPreset.YellowMonochrome,
        ConsoleColors.ColorPreset.GreenMonochrome,
        ConsoleColors.ColorPreset.IceWarp,
        ConsoleColors.ColorPreset.WaterSport
    };

    internal static int ColorPresetCount => ColorPresets.Length;

    /// <summary>
    /// A color preset by its number (from 0)
    /// </summary>
    internal static ConsoleColors.ColorPreset ColorPresetAt(int index) => ColorPresets[index];

    private static readonly string[] ColorPresetNames =
    {
        "Default", "Deep Blue", "Marbles Quest", "Night Writer", "Yellow Monochrome", "Green Monochrome", "Ice Warp", "Water Sport"
    };

    /// <summary>
    /// The name of a color preset
    /// </summary>
    internal static string ColorPresetName(int index) => index == 0 ? Lang.T("Default") : ColorPresetNames[index];

    /// <summary>
    /// Pick a color scheme in a box on top of the page. The last choice opens the color scheme editor. The choice
    /// is saved in the settings file
    /// </summary>
    internal static void PickColorScheme()
    {
        var count = ColorPresets.Length;
        var choices = Enumerable.Range(0, count).Select(ColorPresetName).Append(DialogText.PickSchemeEditor).ToArray();
        var keys = Enumerable.Range(1, count).Select(n => n.ToString()).Append("E").ToArray();
        var picked = ChoiceBox.Pick(DialogText.PickSchemeCaption, DialogText.PickSchemeQuestion, string.Empty, choices,
            keys, ConsoleColors.GetColorPresetIndex(), false);
        if (picked < 0) return;
        if (picked == count)
        {
            SchemeEditor.Show();
            return;
        }

        SetColorPreset(picked);
        FileIo.SaveSettings();
    }

    /// <summary>
    /// Pick the language in a box on top of the page. The choice is saved in the settings file
    /// </summary>
    internal static void PickLanguage()
    {
        var languages = Lang.Available;
        var choices = languages.Select(language => language.Name + " (" + language.Code + ")").ToArray();
        var keys = Enumerable.Range(1, languages.Length).Select(n => n.ToString()).ToArray();
        var picked = ChoiceBox.Pick(DialogText.PickLanguageCaption, DialogText.PickLanguageQuestion, string.Empty,
            choices, keys, Math.Max(0, Array.FindIndex(languages, language => language.Code == Lang.Current)), false);
        if (picked < 0) return;
        Lang.Set(languages[picked].Code);
        FileIo.SaveSettings();
    }

    // Use a color preset. The index is saved in the settings file
    internal static void SetColorPreset(int index)
    {
        ConsoleColors.SetColors(ColorPresets[index]);
        ConsoleColors.SetColorPresetIndex(index);
    }

    // Commands when selecting from a list of entries. The commands work on the selected entry
    private static void RunListCommand(string command, int pageNumber)
    {
        if (command is "qa" or "quitall" or "exit" or "logout") _isSelectingResult = false;
        if (RunCommonCommand(command)) return;
        // A number selects the entry with that number
        if (int.TryParse(command, out var number))
        {
            if (number >= 1 && number <= _resultPages[pageNumber].Count)
            {
                SelectResultFromNumericValue(pageNumber, _c, true, number);
            }

            return;
        }

        switch (CommandLine.Name(command))
        {
            case "q":
            case "quit":
            case "close":
                // Return to main
                _isSelectingResult = false;
                Mode.SetRecallMode(Mode.RecallMode.None);
                Mode.SetMode(Mode.SelectedMode.Manage);
                Menu.DrawTopMenu(true, true, false);
                break;
            case "h":
            case "help":
                Tools.WriteEditorHelp(false, Tools.HelpTopic.Menus);
                break;
            case "browse":
                BrowseFiles();
                break;
            case "new":
                NewEntry(CommandLine.LastArgument);
                // Return to main
                _isSelectingResult = false;
                Mode.SetMode(Mode.SelectedMode.Manage);
                Menu.DrawTopMenu(true, true, false);
                break;
            case "title":
                SelectResultFromIndex(pageNumber, _resultIndex, 1);
                break;
            case "duplicate":
                SelectResultFromIndex(pageNumber, _resultIndex, 8);
                break;
            case "revert":
                SelectResultFromIndex(pageNumber, _resultIndex, 9);
                break;
            case "export":
                SelectResultFromIndex(pageNumber, _resultIndex, 10);
                break;
            case "info":
                SelectResultFromIndex(pageNumber, _resultIndex, 11);
                break;
            case "import":
                _isSelectingResult = false;
                ImportEntry();
                break;
            case "delete":
                SelectResultFromIndex(pageNumber, _resultIndex, 2);
                break;
            case "color":
                // Set the text color of a pinned favorite: picked from a list (as C), or given by its number in the list
                if (CommandLine.Argument(command).Length == 0)
                {
                    PickPinColor(pageNumber);
                }
                else if (int.TryParse(CommandLine.Argument(command), out var color) &&
                         color >= 1 && color <= ColorPicker.Count)
                {
                    _pinColor = (ConsoleColor)(color - 1);
                    SelectResultFromIndex(pageNumber, _resultIndex, 3);
                    RecallPreviousView();
                }
                else
                {
                    CommandLine.ShowUnknown(command);
                }

                break;
            case "transfer":
                SelectResultFromIndex(pageNumber, _resultIndex, 6);
                RecallPreviousView();
                break;
            case "size":
                DisplayOutputDimensions();
                break;
            default:
                CommandLine.ShowUnknown(command);
                break;
        }
    }

    // Move the selection pointer in a list of entries
    private static void MoveSelection(int index)
    {
        if (index == _resultIndex) return;
        Menu.WriteSelectionPointer(true);
        if (index > _resultIndex)
        {
            Tools.CursorDown(index - _resultIndex);
        }
        else
        {
            Tools.CursorUp(_resultIndex - index);
        }

        Menu.WriteSelectionPointer(false);
        _resultIndex = index;
    }

    #endregion

    private static void ChangeMenuFocus(int page)
    {
        if (Menu.GetTopMenuManagePage() != page)
        {
            Menu.SetTopMenuManagePage(page);
            Menu.BypassAwaitAction = true; // Bypass the ReadKey function within the DrawTopMenu method
            Menu.DrawTopMenu(false, true, false);
        }
    }

    private static bool _changeOutputDimensionsAccelerated;

    private static string GetAcceleratedState()
    {
        return _changeOutputDimensionsAccelerated ? "x2" : "x1";
    }

    internal static void DisplayOutputDimensions()
    {
        var originalLineCount = Init.LineCount;
        var originalLineWidth = Init.LineWidth;
        retry:
        // "<^v>"
        // Display dimensions
        Init.DisplayOutputDimensions();
        // Take input
        string[] instructions =
        {
            Lang.T("Set output dimensions..."),
            "+----+",
            "| " + GetAcceleratedState() + " |",
            "+----+",
            Lang.T("Press the arrow keys (or h/j/k/l) to change dimensions"),
            Lang.T("Press Tab to toggle increment size (x1/x2)"),
            Lang.T("Press E to reset dimensions"),
            Lang.T("Press Enter to save"),
            "",
            Lang.T("Press Esc to cancel")
        };
        var tempC = Keyboard.ToNavigationKey(Keyboard.AwaitAnyKeyWithInstructions(instructions));
        if (tempC.Key == ConsoleKey.Tab)
        {
            _changeOutputDimensionsAccelerated = !_changeOutputDimensionsAccelerated;
            goto retry;
        }
        else if (tempC.Key == ConsoleKey.UpArrow)
        {
            if (_changeOutputDimensionsAccelerated)
            {
                for (int i = 0; i < 3; i++)
                {
                    Init.DecreaseLineCount();
                }
            }
            else
            {
                Init.DecreaseLineCount();
            }

            goto retry;
        }
        else if (tempC.Key == ConsoleKey.DownArrow)
        {
            if (_changeOutputDimensionsAccelerated)
            {
                for (int i = 0; i < 3; i++)
                {
                    Init.IncreaseLineCount();
                }
            }
            else
            {
                Init.IncreaseLineCount();
            }

            goto retry;
        }
        else if (tempC.Key == ConsoleKey.LeftArrow)
        {
            if (_changeOutputDimensionsAccelerated)
            {
                for (int i = 0; i < 6; i++)
                {
                    Init.DecreaseLineWidth();
                }
            }
            else
            {
                Init.DecreaseLineWidth();
            }

            goto retry;
        }
        else if (tempC.Key == ConsoleKey.RightArrow)
        {
            if (_changeOutputDimensionsAccelerated)
            {
                for (int i = 0; i < 6; i++)
                {
                    Init.IncreaseLineWidth();
                }
            }
            else
            {
                Init.IncreaseLineWidth();
            }

            goto retry;
        }
        else if (tempC.Key == ConsoleKey.E)
        {
            _changeOutputDimensionsAccelerated = false;
            Init.SetDefaultOutputDimensions();
            goto retry;
        }
        else if (tempC.Key == ConsoleKey.Enter)
        {
            // Dimensions set by hand take over from the automatic
            Init.AutoSize = false;
            FileIo.SaveSettings();
        }
        else if (tempC.Key == ConsoleKey.Escape)
        {
            Init.SetLineCount(originalLineCount);
            Init.SetLineWidth(originalLineWidth);
        }
        else
        {
            goto retry;
        }
    }

    private static void RecallPreviousView()
    {
        if (Mode.CurrentRecallMode != Mode.RecallMode.None &&
            Entries.GetEntriesCount() >=
            1) // Must be more than one to avoid being sent back to view from search results
        {
            switch (Mode.CurrentRecallMode)
            {
                case Mode.RecallMode.ListAll:
                    Mode.SetRecallMode(Mode.RecallMode.None);
                    FindAll(0, string.Empty, false, true);
                    return;
                case Mode.RecallMode.Top20:
                    Mode.SetRecallMode(Mode.RecallMode.None);
                    FindAll(1, string.Empty, false, true);
                    return;
                case Mode.RecallMode.Favorites:
                    Mode.SetRecallMode(Mode.RecallMode.None);
                    FindAll(2, string.Empty, false, true);
                    return;
                case Mode.RecallMode.Session:
                    Mode.SetRecallMode(Mode.RecallMode.None);
                    FindAll(3, string.Empty, false, true);
                    return;
                case Mode.RecallMode.Search:
                    Mode.SetRecallMode(Mode.RecallMode.None);
                    FindAll(0, _searchQuery, false, true);
                    return;
            }
        }
        else
        {
            Mode.SetMode(Mode.SelectedMode.Manage);
            Menu.DrawTopMenu(true, true, false);
        }
    }


    private static string _tempText = string.Empty;

    // Create an entry and open it in the editor. The title is asked for if it is not given
    private static void NewEntry(string title = "")
    {
        title = Tools.ConstrainTitle(title.Trim());
        if (Tools.IsValid(title))
        {
            CreateEntry(title);
            return;
        }

        // The title is asked for in a box on top of the page
        var asked = ChoiceBox.AskText(DialogText.New, DialogText.EnterTitleAndPressEnter, DialogText.TitleColon,
            string.Empty);
        _tempText = Tools.ConstrainTitle(asked ?? string.Empty);
        if (!Tools.IsValid(_tempText))
        {
            RecallPreviousView();
            return;
        }

        CreateEntry(_tempText);
    }

    private static void CreateEntry(string title)
    {
        Mode.SetRecallMode(Mode.RecallMode.Top20); // Return to top20 on Esc
        Entries.NewEntry(title, string.Empty);
        Entries.SaveEntriesToFile();
        EditEntry(false);
    }


    internal static void DrawEditMenu()
    {
        Menu.DrawTopMenu(false, false, false);
        Menu.WriteEditorTop();
        Tools.CursorTopLeft();
        Tools.CursorDown(1);
        WriteHorizontalRule(false, "", false, ConsoleColors.ColorPreset.Normal);
        Menu.WriteTitleLabel(); // Insert title
        Tools.CursorReturn();
        Tools.CursorDown();
        // The editor writes its mode on the divider
        WriteHorizontalRule(false, "", false, ConsoleColors.ColorPreset.EditorTitle);
        Tools.SaveCursorPosition(1);
        Tools.CursorTopLeft();
        Tools.CursorDown(Init.LineCount - 1); // Insert bottom line
        WriteHorizontalRule(false, "", false, ConsoleColors.ColorPreset.Normal);
        Tools.LoadCursorPosition(1);
    }

    // The editor frame for a plain text file opened from the file browser
    internal static void DrawEditFileMenu()
    {
        Menu.DrawTopMenu(false, false, false);
        Menu.WriteEditorTop();
        Tools.CursorTopLeft();
        Tools.CursorDown(1);
        WriteHorizontalRule(false, "", false, ConsoleColors.ColorPreset.Normal);
        Console.Write(" " + DialogText.FileColon + " " +
                      Tools.TruncateString(FileExplorer.CurrentFileName, Init.LineWidth - 40, true) + " ");
        if (FileExplorer.SessionEnded)
        {
            // Coming from the encrypted notes of a data file: a reminder that this file is not
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write(DialogText.NotEncrypted);
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Tools.CursorReturn();
        Tools.CursorDown();
        // The editor writes its mode on the divider
        WriteHorizontalRule(false, "", false, ConsoleColors.ColorPreset.EditorTitle);
        Tools.SaveCursorPosition(1);
        Tools.CursorTopLeft();
        Tools.CursorDown(Init.LineCount - 1); // Insert bottom line
        WriteHorizontalRule(false, "", false, ConsoleColors.ColorPreset.Normal);
        Tools.LoadCursorPosition(1);
    }

    internal static void DrawEditSettingsMenu()
    {
        Menu.DrawTopMenu(false, false, false);
        Menu.WriteEditorTop();
        Tools.CursorTopLeft();
        Tools.CursorDown(1);
        WriteHorizontalRule(false, "", false, ConsoleColors.ColorPreset.Normal);
        Console.Write(" " + DialogText.FileColon + " " + FileIo.SettingsFileName);
        Tools.CursorReturn();
        Tools.CursorDown();
        // The editor writes its mode on the divider
        WriteHorizontalRule(false, "", false, ConsoleColors.ColorPreset.EditorTitle);
        Tools.SaveCursorPosition(1);
        Tools.CursorTopLeft();
        Tools.CursorDown(Init.LineCount - 1); // Insert bottom line
        WriteHorizontalRule(false, "", false, ConsoleColors.ColorPreset.Normal);
        Tools.LoadCursorPosition(1);
    }

    private static void EditEntry(bool search)
    {
        if (Entries.CurrentEntryExists())
        {
            // Add entry ID to session ID list
            Entries.RegisterSessionId((Entries.CurrentEntry.Id));
            
            var cursorIndex = Entries.CurrentEntry.CursorIndex;
            while (true)
            {
                Mode.SetMode(Mode.SelectedMode.Edit);
                Tools.ClearScreen();
                DrawEditMenu();
                // Insert editor at current cursor position (colored as set for the entry)
                TextArea.NextEntrySyntax = EntrySyntax.Get(Entries.CurrentEntry.Options);
                TextArea textArea = new TextArea(Console.CursorLeft, Console.CursorTop, Init.LineWidth - 1,
                    Init.LineCount - 5, Entries.CurrentEntry.Text, cursorIndex, false, false, search, 0);
                // Cleanup
                textArea.ClearUndoRedoCache();
                // ":snip edit": the file of snippets, and then the entry again
                if (TextArea.TakeSnippetsRequest() is not { } line) break;
                cursorIndex = textArea.GetCursorIndex();
                search = false;
                FileExplorer.EditSnippets(line);
            }

            ReturnFromEntry();
        }
        else
        {
            Menu.StatusMessage(DialogText.NoEntrySelected, true, true, true, DialogText.CaptionsEdit, string.Empty,
                false);
        }
    }

    /// <summary>
    /// Keep the syntax highlighting of the entry being edited (set with :syntax in the editor) in the entry, and
    /// save the data file
    /// </summary>
    internal static void SetEntrySyntax(SyntaxType type)
    {
        if (!Entries.CurrentEntryExists()) return;
        var options = EntrySyntax.Set(Entries.CurrentEntry.Options, type);
        if (options == Entries.CurrentEntry.Options) return;
        Entries.CurrentEntry.Options = options;
        Entries.UpdateEntry(Entries.CurrentEntry, false);
        Entries.SaveEntriesToFile();
    }

    internal static bool LatestEntryWasEdited;
    internal static void SaveContentDuringEdit(TextArea textArea)
    {
        if (!DocumentInfo.HasChanged) return;
        SaveBackup();
        // Fetch data from editor
        _tempText = textArea.GetText();
        LatestEntryWasEdited = true; // Used when recalling the TOP 20 list to reset the selection index (ResetLastSelectedIndex())
        //ResetLastSelectedIndex(); // <- Do not reset index here as this will prevent reselecting the entry when returning to the lists
        Entries.CurrentEntry.Text = _tempText;
        Entries.CurrentEntry.CursorIndex = textArea.GetCursorIndex();
        Entries.UpdateEntry(Entries.CurrentEntry, true);
        Entries.SaveEntriesToFile();
    }


    /// <param name="fromList">True when changed from a list of entries: the list is shown again afterwards
    /// (else the entry)</param>
    private static void ChangeTitle(bool fromList = false)
    {
        if (Entries.CurrentEntryExists())
        {
            // The title is changed in a box on top of the page
            var asked = ChoiceBox.AskText(DialogText.EditTitle, DialogText.EditTitleAndPressEnter,
                DialogText.TitleColon, Entries.CurrentEntry.Title);
            _tempText = Tools.ConstrainTitle(asked ?? string.Empty);
            if (Tools.IsValid(_tempText) && _tempText != Entries.CurrentEntry.Title)
            {
                if (!fromList) ResetLastSelectedIndex();
                Entries.CurrentEntry.Title = _tempText;
                Entries.UpdateEntry(Entries.CurrentEntry, true);
                Entries.SaveEntriesToFile();
            }

            if (fromList)
            {
                ExitNumericSelect(_lastSelectedPage, _lastSelectedIndex);
            }
            else
            {
                ReturnFromEntry();
            }
        }
        else
        {
            Menu.StatusMessage(DialogText.NoEntrySelected, true, true, true, DialogText.CaptionsEdit, string.Empty,
                false);
        }
    }

    private static void RecallEntry()
    {
        if (Tools.IsValid(Entries.RecallEntryId))
        {
            if (Entries.HasEntries())
            {
                if (Entries.SelectEntryById(Entries.RecallEntryId))
                {
                    ResetLastSelectedIndex();
                    EditEntry(false);
                }
                else
                {
                    Menu.StatusMessage(DialogText.NoEntrySelected, true, true, true, DialogText.CaptionsRecall,
                        string.Empty, false);
                    Menu.DrawTopMenu(false, true, false);
                }
            }
            else
            {
                Menu.StatusMessage(DialogText.NoEntriesFound, true, true, true, DialogText.CaptionsRecall, string.Empty,
                    false);
                Menu.DrawTopMenu(false, true, false);
            }
        }
        else
        {
            Menu.StatusMessage(DialogText.NoRecallHistoryFound, true, true, true, DialogText.CaptionsRecall,
                string.Empty, false);
            Menu.DrawTopMenu(false, true, false);
        }
    }


    // After editing an entry (or an action on it): back to where it was opened from, the list (with the entry
    // selected) or the main menu
    private static void ReturnFromEntry()
    {
        RecallPreviousView();
    }

    // The dates and the size of an entry, in a box on top of the page
    private static void ShowEntryInfo()
    {
        if (!Entries.CurrentEntryExists()) return;
        var entry = Entries.CurrentEntry;
        ChoiceBox.ShowRows(DialogText.InspectInfoCaption, new List<(string Label, string Value)>
        {
            (DialogText.TitleColon, entry.Title),
            (DialogText.InfoCreated, entry.DateCreated),
            (DialogText.InfoChanged, entry.DateChanged),
            (Lang.T("Words:"), Tools.WordCount(entry.Text).ToString()),
            (Lang.T("Characters:"), Tools.CharacterCount(entry.Text).ToString()),
            (Lang.T("Lines:"), Tools.LineCount(entry.Text).ToString()),
            (Lang.T("Syntax:"), EntrySyntax.Get(entry.Options) == SyntaxType.None
                ? Lang.T("None (:syntax in the editor)")
                : SyntaxHighlighter.Name(EntrySyntax.Get(entry.Options)))
        }, 60);
    }


    // A label and a path on one line of the window: the start of a long path is left out
    private static string FitPath(string label, string path)
    {
        var room = Init.LineWidth - 2 - label.Length;
        return label + (path.Length <= room ? path : "..." + path[^Math.Max(1, room - 3)..]);
    }

    // Export the text of the current entry as a text file. The directory is selected in the file browser,
    // and then the name of the file is entered
    private static void ExportCurrentEntry()
    {
        if (!Entries.CurrentEntryExists()) return;
        var directory = FileExplorer.PickDirectory(DialogText.CaptionsExport);
        if (directory != null)
        {
            Tools.ClearScreen();
            Menu.WriteModeLabel(DialogText.CaptionsExport,
                DialogText.EnterFileNameAndPressEnter + " " + DialogText.PressEscToExitP, 1);
            Tools.CursorDown(3);
            Console.Write(" " + DialogText.NameColon);
            var textArea = new TextArea(Console.CursorLeft, Console.CursorTop, 56, 1,
                FileIo.GetExportFileName(Entries.CurrentEntry.Title), 0, true, false, false, 0);
            var name = textArea.GetText().Trim();
            if (!textArea.Canceled && name.Length > 0)
            {
                var path = Tools.IsValidFileName(name) ? Path.Combine(directory, name) : string.Empty;
                if (path.Length == 0 || Directory.Exists(path))
                {
                    Menu.StatusMessage(DialogText.InvalidFileName, true, true, true, DialogText.CaptionsExport,
                        string.Empty, false);
                }
                else if (!File.Exists(path) ||
                         Menu.YesNoPrompt(DialogText.CaptionsExport, DialogText.ReplaceFileYn, FitPath(string.Empty, path),
                             false))
                {
                    var status = FileIo.ExportCurrentEntryAsTxt(path);
                    Menu.StatusMessage(status == DialogText.Success ? DialogText.ExportCompleted : status, true,
                        true, true, DialogText.CaptionsExport,
                        status == DialogText.Success ? FitPath(DialogText.ExportedTo, path) : string.Empty, false);
                }
            }
        }

        ReturnFromEntry();
    }

    private static void ToggleBackup()
    {
        if (!FileIo.BackupEnabled())
        {
            if (ChoiceBox.Confirm(DialogText.CaptionsBackup, DialogText.BackupQuestion, string.Empty, false))
            {
                // Select the directory in the file browser
                var backupPath = FileExplorer.PickDirectory(DialogText.CaptionsBackup);
                if (backupPath != null)
                {
                    var valid = Tools.IsValidPath(backupPath) && FileIo.CheckBackupDirectory(backupPath);
                    if (valid)
                    {
                        FileIo.SetBackup(true, backupPath, true);
                        Entries.SaveEntriesToFile();
                    }

                    // The message on top of the menu
                    Mode.SetMode(Mode.SelectedMode.Manage);
                    Menu.DrawTopMenu(false, true, false);
                    ChoiceBox.Message(DialogText.CaptionsBackup,
                        valid ? DialogText.BackupEnabled : DialogText.InvalidBackupPath,
                        Lang.T("Path: ") + backupPath);
                }
            }
        }
        else if (FileIo.BackupEnabled())
        {
            if (ChoiceBox.Confirm(DialogText.CaptionsBackup, DialogText.BackupDisableQuestion,
                    FileIo.GetBackupPathForDisplay(true), false))
            {
                // Set to false and delete path
                FileIo.SetBackup(false, "", true);
                ChoiceBox.Message(DialogText.CaptionsBackup, DialogText.BackupDisabled, string.Empty);
            }
        }

        Mode.SetMode(Mode.SelectedMode.Manage);
        Menu.DrawTopMenu(false, true, false);
    }

    // Import a text file, selected in the file browser, as a new entry
    private static void ImportEntry()
    {
        if (ChoiceBox.Confirm(DialogText.CaptionsImport, DialogText.ImportQuestion, DialogText.ImportYnNote, false))
        {
            var path = FileExplorer.PickFile(DialogText.CaptionsImport);
            if (path != null)
            {
                // Clear screen and indicate activity while reading file...
                Tools.ClearScreen();
                Console.WriteLine(DialogText.Reading);
                string text;
                try
                {
                    text = TextFile.Load(path).Text;
                }
                catch (Exception e)
                {
                    // The message on top of the menu
                    Mode.SetMode(Mode.SelectedMode.Manage);
                    Menu.DrawTopMenu(false, true, false);
                    ChoiceBox.Message(DialogText.CaptionsImport, e.Message, string.Empty);
                    Menu.DrawTopMenu(false, true, false);
                    return;
                }

                // The entry gets the name of the file as its title
                var title = Tools.ConstrainTitle(Path.GetFileNameWithoutExtension(path));
                if (!Tools.IsValid(title)) title = DialogText.CaptionsImport + " " + IsoDateTime.GetDateTimeNow();
                Entries.NewEntry(title, text);
                Entries.SaveEntriesToFile();
                // The new entry is opened in the editor
                EditEntry(false);
                return;
            }
        }

        Mode.SetMode(Mode.SelectedMode.Manage);
        Menu.DrawTopMenu(false, true, false);
    }

    /// <summary>
    /// Set all settings back to their defaults and save them
    /// </summary>
    internal static void ApplyDefaultSettings()
    {
        Tools.ClearEditorHelpData();
        AsciiStyles.ResetAsciiStyle();
        FileIo.SetBackup(false, string.Empty, false);
        FileBrowser.ResetSubfolderAndIndex();
        ConsoleColors.Monochrome = false;
        SyntaxHighlighter.ColorFiles = true;
        FileExplorer.DualPane = true;
        TextArea.LineNumbers = false;
        TextArea.WordWrap = true;
        Snippets.Sort = Snippets.SortOrder.File;
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Reset);
        ConsoleColors.DefaultColorPresetIndex();
        Logo.AnimationEnabled = true;
        StartMenu.Mode = StartMenu.StartMode.Select;
        StartMenu.StartInFavorites = false;
        SystemClipboard.SetDefaults();
        Init.AutoSize = false;
        Init.SetDefaultOutputDimensions();
        FileIo.SaveSettings();
    }

    private static void ResetSettings()
    {
        if (ChoiceBox.Confirm(DialogText.CaptionsSettings, DialogText.ResetSettingsQuestion, string.Empty, true))
        {
            ApplyDefaultSettings();
            Menu.DrawTopMenu(false, true, false);
        }
        else
        {
            Menu.DrawTopMenu(false, true, false);
        }
    }


    private static void DeleteCurrentEntry()
    {
        if (Entries.CurrentEntryExists())
        {
            // Ask to delete entry
            if (Menu.YesNoPrompt(DialogText.CaptionsDelete, DialogText.DeleteEntryYn,
                    DialogText.TitleColon + " \"" + Entries.CurrentEntry.Title + "\"", true))
            {
                // Save deletion backup for undo (the entry can be brought back with :undelete)
                SaveDeletionBackup();
                if (Entries.DeleteEntry(Entries.CurrentEntry))
                {
                    ResetLastSelectedIndex();
                    Menu.StatusMessage(DialogText.EntryDeleted, true, true, true, DialogText.CaptionsDelete,
                        string.Empty, false);
                    Entries.SaveEntriesToFile();
                }

                Mode.SetMode(Mode.SelectedMode.Manage);
                Menu.DrawTopMenu(false, true, false);
            }
            else
            {
                ReturnFromEntry();
            }
        }
    }


    private static string _duplicateEntryId = string.Empty;

    private static void DuplicateCurrentEntry()
    {
        _duplicateEntryId = "";
        if (Menu.YesNoPrompt(DialogText.CaptionsDuplicate, DialogText.DuplicateEntryYn, string.Empty, false))
        {
            if (Entries.CurrentEntryExists())
            {
                _duplicateEntryId = Entries.DuplicateEntry(Entries.CurrentEntry);
                if (Tools.IsValid(_duplicateEntryId) && Entries.EntryExists(_duplicateEntryId))
                {
                    ResetLastSelectedIndex();
                    Entries.SaveEntriesToFile();
                    Entries.SelectEntryById(_duplicateEntryId); // Select and show entry
                    ReturnFromEntry();
                }
                else
                {
                    Menu.StatusMessage(DialogText.ErrorDuplicatingEntry, true, true, true, DialogText.CaptionsDuplicate,
                        string.Empty, false);
                    ReturnFromEntry();
                }
            }
            else
            {
                Menu.StatusMessage(DialogText.NoEntriesSelected, true, true, true, DialogText.CaptionsDuplicate,
                    string.Empty, false);
                Mode.SetMode(Mode.SelectedMode.Manage);
                Menu.DrawTopMenu(false, true, false);
            }
        }
        else
        {
            ReturnFromEntry();
        }
    }

    private static void RestoreBackup()
    {
        if (Tools.IsValid(Entries.BackupEntry.Id))
        {
            if (Menu.YesNoPrompt(DialogText.CaptionsRevert, DialogText.RevertEditYn, string.Empty, true))
            {
                Entries.SetAsCurrentEntry(Entries.BackupEntry);
                Entries.UpdateEntry(Entries.CurrentEntry, true);
                Menu.StatusMessage(DialogText.EntryRestored, true, true, true, DialogText.CaptionsRevert, string.Empty,
                    false);
                Entries.SaveEntriesToFile();
                ReturnFromEntry();
            }
            else
            {
                ReturnFromEntry();
            }
        }
        else
        {
            Menu.StatusMessage(DialogText.NoBackupFound, true, true, true, DialogText.CaptionsRevert, string.Empty,
                false);
            ReturnFromEntry();
        }
    }

    // Used for reverting last edit
    private static void SaveBackup()
    {
        if (!Tools.IsValid(Entries.CurrentEntry.Id)) return;
        // Create new entry
        Entries.BackupEntry = new Entry
        {
            // Store data from the current entry
            Id = Entries.CurrentEntry.Id,
            DateCreated = Entries.CurrentEntry.DateCreated,
            DateChanged = Entries.CurrentEntry.DateChanged,
            Title = Entries.CurrentEntry.Title,
            Text = Entries.CurrentEntry.Text,
            Options = Entries.CurrentEntry.Options
        };
    }

    // Used for restoring last deleted entry
    private static void SaveDeletionBackup()
    {
        if (!Tools.IsValid(Entries.CurrentEntry.Id)) return;
        // Clone current entry to backup
        Entries.DeletedEntry = Entries.CloneEntry(Entries.CurrentEntry, false);
    }

    private static void UndoDeletion()
    {
        if (!string.IsNullOrEmpty(Entries.DeletedEntry.Id))
        {
            if (Menu.YesNoPrompt(DialogText.CaptionsRestore, DialogText.RestoreDeletedYn, string.Empty, true))
            {
                ResetLastSelectedIndex();
                try
                {
                    Entries.AddEntryToEntries(Entries.DeletedEntry);
                    Entries.SetAsCurrentEntry(Entries.DeletedEntry);
                    Entries.SaveEntriesToFile();
                    Menu.StatusMessage(DialogText.DeletionRestored, true, true, true, DialogText.CaptionsRestore,
                        string.Empty, false);
                    // Remove deleted entry after the restore
                    Entries.ResetDeletedEntry();
                    ReturnFromEntry();
                }
                catch (Exception e) when (e is not NavigationException)
                {
                    Menu.StatusMessage(DialogText.UnableToCompleteRestore, true, true, true, DialogText.CaptionsRestore,
                        string.Empty, false);
                    Menu.DrawTopMenu(false, true, false);
                }
            }
            else
            {
                Menu.DrawTopMenu(false, true, false);
            }
        }
        else
        {
            Menu.StatusMessage(DialogText.NoDeletionBackupFound, true, true, true, DialogText.CaptionsRestore,
                string.Empty, false);
            Menu.DrawTopMenu(false, true, false);
        }
    }

    private static string _tempAboutText = string.Empty;
    private static string TempPageNumber => Lang.F("Page {0}/{1}", 1, 2);

    private static void ShowAbout()
    {
        Tools.ClearScreen();
        Tools.CursorTopLeft();

        // Show help
        // Copyright
        _tempAboutText = DialogText.ApplicationName + " " + DialogText.ApplicationVersion + " " +
                         DialogText.ApplicationBy;
        _tempAboutText += Environment.NewLine + Environment.NewLine;
        _tempAboutText += DialogText.About;
        _tempAboutText += Environment.NewLine + Environment.NewLine;
        _tempAboutText += DialogText.MultipleInstances;
        _tempAboutText += Environment.NewLine + Environment.NewLine;
        // Show in read only editor (page 1)
        var dummy = new TextArea(Console.CursorLeft, Console.CursorTop, Init.LineWidth - 1, 22, _tempAboutText, 0, false,
            true, false, 0);
        Tools.CursorTopLeft();
        Tools.CursorDown(Init.LineCount - 2);
        Tools.CursorRight(Init.LineWidth - (TempPageNumber.Length + 1));
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(TempPageNumber);
        Tools.CursorReturn();
        Tools.CursorRight();
        if (Keyboard.AwaitAnyKeyOrEscape())
        {
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Menu.DrawTopMenu(false, true, false);
            return;
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Tools.ClearScreen();
        _tempAboutText = string.Empty;
        _tempAboutText = SoftwareLicense.GetLicenseText();
        // Show in read only editor (page 2)
        var dummy1 = new TextArea(Console.CursorLeft, Console.CursorTop, Init.LineWidth - 1, 22, _tempAboutText, 0, false,
            true, false, 0);
        Tools.CursorTopLeft();
        Tools.CursorDown(Init.LineCount - 2);
        Tools.CursorRight(Init.LineWidth - (TempPageNumber.Length + 1));
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(Lang.F("Page {0}/{1}", 2, 2));
        Tools.CursorReturn();
        Tools.CursorRight();
        Keyboard.AwaitAnyKey();
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Menu.DrawTopMenu(false, true, false);
    }

    internal static bool IsFavorite(Entry e)
    {
        return !string.IsNullOrEmpty(e.Options) && e.Options.Contains(Entries.FavoritesTag);
    }

    private static bool IsPinned(Entry e)
    {
        return !string.IsNullOrEmpty(e.Options) && e.Options.Contains(Entries.PinnedTag);
    }


    private static void ToggleFavorite()
    {
        if (Entries.CurrentEntryExists() && !IsFavorite(Entries.CurrentEntry))
        {
            var result = Menu.YesOptionNoPrompt(DialogText.Favorites, DialogText.AddFavoriteYn, string.Empty, false,
                ConsoleKey.P, DialogText.FavoriteChoicePinned);
            if (result != 3) // If not No, continue
            {
                if (result == 2) // If option set pinned
                {
                    SetOptionsTag(Entries.CurrentEntry, Entries.FavoritesTag.ToString());
                    SetOptionsTag(Entries.CurrentEntry, Entries.PinnedTag.ToString());
                    SetOptionsTag(Entries.CurrentEntry, Entries.DefaultRank.ToString());
                    Entries.UpdateEntry(Entries.CurrentEntry, false); // Add the updates to the collection
                    Entries.SetRanking(Entries.CurrentEntry); // Ensure that the pinned entry is ranked topmost
                }
                else // Else ad to favorites
                {
                    SetOptionsTag(Entries.CurrentEntry, Entries.FavoritesTag.ToString());
                    Entries.UpdateEntry(Entries.CurrentEntry, false);
                }

                // Update collection and write to file
                // Entries.UpdateEntry(Entries.CurrentEntry, false);
                Entries.SaveEntriesToFile();
                _transferCurrentEntryResult = true;
                FindAll(2, string.Empty, false, true);
            }
            else
            {
                ReturnFromEntry();
            }
        }
        else if (Entries.CurrentEntryExists())
        {
            byte result;
            if (IsPinned(Entries.CurrentEntry))
            {
                // Pinned: remove it from the favorites, or only unpin it (it stays a favorite)
                result = Menu.YesOptionNoPrompt(DialogText.Favorites, DialogText.RemoveFavoriteYn, string.Empty,
                    false, ConsoleKey.U, DialogText.FavoriteChoiceUnpin);
                if (result == 2)
                {
                    Entries.RemovePinnedRanking(Entries.CurrentEntry);
                    RemoveOptionsTag(Entries.CurrentEntry, Entries.PinnedTag.ToString());
                    // (the color is kept for when it is pinned again)
                    Entries.UpdateEntry(Entries.CurrentEntry, false);
                    Entries.SaveEntriesToFile();
                    _transferCurrentEntryResult = true;
                    FindAll(2, string.Empty, false, true);
                    return;
                }
            }
            else
            {
                result = Menu.YesOptionNoPrompt(DialogText.Favorites, DialogText.RemoveFavoriteOrPinYn, string.Empty,
                    false, ConsoleKey.P, DialogText.FavoriteChoicePin);
            }

            if (result == 1) // Remove from favorites
            {
                // Just replace the tags and leave anything else
                Entries.RemovePinnedRanking(Entries.CurrentEntry);
                RemoveOptionsTag(Entries.CurrentEntry, Entries.FavoritesTag.ToString());
                RemoveOptionsTag(Entries.CurrentEntry, Entries.PinnedTag.ToString());
                Entries.CurrentEntry.Options = PinColors.Set(Entries.CurrentEntry.Options, null);
                // Update collection and write to file
                Entries.UpdateEntry(Entries.CurrentEntry, false);
                Entries.SaveEntriesToFile();
                FindAll(2, string.Empty, false, false);
                return;
            }
            if (result == 2) // Pin to highlight
            {
                if (!IsPinned(Entries.CurrentEntry))
                {
                    SetOptionsTag(Entries.CurrentEntry, Entries.PinnedTag.ToString());
                    SetOptionsTag(Entries.CurrentEntry, Entries.DefaultRank.ToString());
                    Entries.UpdateEntry(Entries.CurrentEntry, false); // Add the updates to the collection
                    Entries.SetRanking(Entries.CurrentEntry); // Ensure that the pinned entry is ranked topmost
                    // Update collection and write to file
                    Entries.UpdateEntry(Entries.CurrentEntry, false);
                    Entries.SaveEntriesToFile();
                    _transferCurrentEntryResult = true;
                    FindAll(2, string.Empty, false, true);
                    return;
                }
            }
            
            ReturnFromEntry();
            
        }
    }

    // Info about options content
    // f, p (Favorites, Pinned)
    // A-P (Text color of pinned entries; h, i, j in files from earlier versions, see PinColors)
    // 0, 1, 2, 3 etc. (Pinned ranking (sorting))
    
    #region Moving a pinned entry (favorites list)

    // True while a pinned entry is being moved up and down among the pinned entries: the entry is highlighted
    // and follows j/k until Esc is pressed (like moving selected lines in the editor)
    private static bool _movingPinned;

    private static void StartMovingPinned(int pageNumber)
    {
        if (!ValidateNumericSelectionInput(pageNumber, _resultIndex)) return;
        _lastSelectedPage = pageNumber;
        _lastSelectedIndex = _resultIndex;
        if (IsPinned(_resultPages[pageNumber][_resultIndex]))
        {
            _movingPinned = true;
        }
        else
        {
            // Inform that only pinned entries can be moved
            Menu.StatusMessage(DialogText.PinnedEntryPosition, true, true, true, DialogText.CaptionsRanking,
                string.Empty, false);
        }

        // Show the favorites again with the entry selected
        FindAll(2, string.Empty, false, true);
    }

    // A key pressed while a pinned entry is being moved
    private static void HandleMovePinnedKey(int pageNumber)
    {
        var key = Keyboard.ToNavigationKey(_c).Key;
        if (key is ConsoleKey.Escape or ConsoleKey.Enter or ConsoleKey.Tab or ConsoleKey.V or ConsoleKey.Q)
        {
            _movingPinned = false;
        }
        else if (key is ConsoleKey.UpArrow or ConsoleKey.K or ConsoleKey.DownArrow or ConsoleKey.J)
        {
            var up = key is ConsoleKey.UpArrow or ConsoleKey.K;
            if (!MovePinned(pageNumber, _resultIndex, up)) return;
            // The selection follows the entry (also to another page of the list)
            var pageSize = _resultPages[0].Count;
            var position = pageNumber * pageSize + _resultIndex + (up ? -1 : 1);
            _lastSelectedPage = position / pageSize;
            _lastSelectedIndex = position % pageSize;
        }
        else
        {
            return;
        }

        FindAll(2, string.Empty, false, true);
    }

    // Move a pinned entry one place up or down among the pinned entries, as they are shown in the list
    private static bool MovePinned(int pageNumber, int index, bool up)
    {
        var pinned = _resultPages.SelectMany(page => page).Where(IsPinned).Select(e => e.Id).ToList();
        var position = pinned.IndexOf(_resultPages[pageNumber][index].Id);
        var target = position + (up ? -1 : 1);
        if (position < 0 || target < 0 || target >= pinned.Count) return false;
        (pinned[position], pinned[target]) = (pinned[target], pinned[position]);
        Entries.SetPinnedOrder(pinned);
        Entries.SaveEntriesToFile();
        return true;
    }

    #endregion

    private static void SetOptionsTag(Entry e, string value)
    {
        if (e.Options.Contains(value)) return;
        e.Options += value;
    }

    private static void RemoveOptionsTag(Entry e, string value)
    {
        if (!e.Options.Contains(value)) return;
        e.Options = e.Options.Replace(value, string.Empty);
    }

    // The color to set by the action "set color" of SelectResultFromIndex (null = the default color)
    private static ConsoleColor? _pinColor;

    // Set the text color of the current entry (pinned entries only)
    private static void SetPinColor(ConsoleColor? color)
    {
        if (Entries.CurrentEntryExists() && IsPinned(Entries.CurrentEntry))
        {
            Entries.CurrentEntry.Options = PinColors.Set(Entries.CurrentEntry.Options, color);
            Entries.UpdateEntry(Entries.CurrentEntry, false);
            Entries.SaveEntriesToFile();
        }
        else
        {
            // Inform that only pinned entries can change color
            Menu.StatusMessage(DialogText.PinnedEntryColors, true, true, true, DialogText.CaptionsColorize, string.Empty, false);
        }
    }


    internal static void ClearSearchQuery()
    {
        _searchQuery = "";
    }


    private static string _searchQuery = string.Empty;

    private static void Search()
    {
        if (Entries.HasEntries())
        {
            // The search text in a box on top of the page (starting with the last search)
            var query = ChoiceBox.AskText(DialogText.TitleSearch, DialogText.EnterQueryForSearch,
                DialogText.SearchColon, _searchQuery ?? string.Empty);
            if (query == null)
            {
                // Canceled (or nothing to search for)
                ClearSearchQuery();
                ResetLastSelectedIndex();
                RecallPreviousView();
                return;
            }

            _searchQuery = query;
            FindAll(0, _searchQuery, true, false);
        }
        else
        {
            ChoiceBox.Message(DialogText.CaptionsSearch, DialogText.NoEntriesFound, string.Empty);
            Mode.SetMode(Mode.SelectedMode.Manage);
            Menu.DrawTopMenu(false, true, false);
        }
    }

    // Search logic
    private static string[]? _queryWords;
    private static int _countQueryMatches;

    private static bool MatchQuery(string title, string text, string query)
    {
        if (Tools.IsValid(query))
        {
            _countQueryMatches = 0;
            // Trim to lower
            text = Tools.TrimToLower(text);
            title = Tools.TrimToLower(title);
            query = Tools.TrimToLower(query);

            // Split query if more words
            _queryWords = query.Split(' ');

            // Match all query words to text source
            foreach (var t in _queryWords)
            {
                if (title.Contains(t) || title.Replace("'", string.Empty).Contains(t) || text.Contains(t))
                {
                    _countQueryMatches += 1;
                }
            }

            // Check that all words where matched
            return _countQueryMatches == _queryWords.Length;
        }
        else
        {
            return false;
        }
    }

    private const int SearchLimit = 19;
    private static bool _searchSuccess;
    private static bool _queryMatch;
    private static List<Entry> _foundEntries = new();
    private static List<Entry> _foundEntriesLimited = new();
    private static List<List<Entry>> _resultPages = new();
    private static int _currentEntryResultIndex;
    private static int _currentEntryResultPage;
    private static int _sortOption;

    private static void DefaultListSortOption()
    {
        _sortOption = 0;
    }

    private static void DefaultSearchSortOption()
    {
        _sortOption = 1;
    }


    private static void NextSortOption()
    {
        _sortOption += 1;
        if (_sortOption >= 3)
        {
            _sortOption = 0;
        }
    }

    private static string GetSortCaption()
    {
        if (_sortOption == 1)
        {
            return DialogText.SortColon + " " + Lang.T("Date changed") + " " + DialogText.TabBrackets;
        }
        else if (_sortOption == 2)
        {
            return DialogText.SortColon + " " + Lang.T("Title") + " " + DialogText.TabBrackets;
        }
        else
        {
            return DialogText.SortColon + " " + Lang.T("Date created") + " " + DialogText.TabBrackets;
        }
    }

    // search code: 0 = search by query
    //              1 = limit (TOP 20)
    //              2 = favorites
    //              3 = Session history
    // OLD layout: FindAll(bool limit, bool favorites, string query, bool reOpenSearchBox, bool recallSelection)
    private static void
        FindAll(int searchCode, string query, bool reOpenSearchBox,
            bool recallSelection) //bool skipSingleResultDisplay
    {
        if (Entries.HasEntries())
        {
            _searchSuccess = false;
            _queryMatch = false;
            // Prepare a list to hold search result
            _foundEntries.Clear();
            _foundEntriesLimited.Clear();
            _resultPages.Clear();

            // Do search on query (multiple word search in title and content)
            if (Tools.IsValid(query))
            {
                Mode.SetRecallMode(Mode.RecallMode.Search);
                // Collect entries
                for (int i = 0; i < Entries.GetEntries().Count; i++)
                {
                    if (!MatchQuery(Entries.GetEntries()[i].Title, Entries.GetEntries()[i].Text, query)) continue;
                    _foundEntries.Add(Entries.GetEntries()[i]);
                    _searchSuccess = true;
                    _queryMatch = true;
                }

                if (_searchSuccess)
                {
                    try
                    {
                        // Sort by DateChanged desc
                        if (_sortOption == 1)
                        {
                            // Sort by DateChanged desc
                            _foundEntries.Sort((x, y) => DateTime.Compare(Convert.ToDateTime(y.DateChanged),
                                Convert.ToDateTime(x.DateChanged)));
                        }
                        else if (_sortOption == 2)
                        {
                            // Sort by Title (alphabetically)
                            _foundEntries.Sort((x, y) =>
                                string.Compare(x.Title, y.Title, true, CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            // Sort by DateCreated desc
                            _foundEntries.Sort((x, y) => DateTime.Compare(Convert.ToDateTime(y.DateCreated),
                                Convert.ToDateTime(x.DateCreated)));
                        }
                    }
                    catch
                    {
                        Menu.StatusMessage(DialogText.ErrorSortingList, true, true, true, DialogText.CaptionsSearch,
                            string.Empty, false);
                    }
                }
            }
            else
            {
                Mode.SetRecallMode(Mode.RecallMode.ListAll);
                // Collect entries
                for (int i = 0; i < Entries.GetEntries().Count; i++)
                {
                    _foundEntries.Add(Entries.GetEntries()[i]);
                    _searchSuccess = true;
                }

                if (_searchSuccess)
                {
                    try
                    {
                        if (_sortOption == 1)
                        {
                            // Sort by DateChanged desc
                            _foundEntries.Sort((x, y) => DateTime.Compare(Convert.ToDateTime(y.DateChanged),
                                Convert.ToDateTime(x.DateChanged)));
                        }
                        else if (_sortOption == 2)
                        {
                            // Sort by Title (alphabetically)
                            _foundEntries.Sort((x, y) =>
                                string.Compare(x.Title, y.Title, true, CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            // Sort by DateCreated desc
                            _foundEntries.Sort((x, y) => DateTime.Compare(Convert.ToDateTime(y.DateCreated),
                                Convert.ToDateTime(x.DateCreated)));
                        }
                    }
                    catch
                    {
                        Menu.StatusMessage(DialogText.ErrorSortingList, true, true, true, DialogText.CaptionsSearch,
                            string.Empty, false);
                    }
                }
            }

            // Print found entries
            if (_searchSuccess)
            {
                //ClearSearchQuery(); <- DO NOT CLEAR SEARCH QUERY: THIS WILL CRIPPLE THE RETURN TO SEARCH FUNCTION
                // Select directly if only one result
                //if (foundEntries.Count == 1 && !skipSingleResultDisplay)
                //{
                //    if (Entries.EntryExists(foundEntries[0].ID))
                //    {
                //        Entries.SelectEntryByID(foundEntries[0].ID);
                //        EditEntry(false);
                //        return;
                //    }
                //}

                // Limit sorted result to searchLimit
                if (searchCode == 1) // TOP 20
                {
                    Mode.SetRecallMode(Mode.RecallMode.Top20);
                    // Reset selection index to ensure the top index is marked upon return to this list
                    if (LatestEntryWasEdited)
                    {
                        LatestEntryWasEdited = false;
                        ResetLastSelectedIndex();
                    }

                    // Start by sorting to get the changed entries in the top of the list
                    try
                    {
                        // Sort by DateChanged desc
                        _foundEntries.Sort(delegate(Entry x, Entry y)
                        {
                            return DateTime.Compare(Convert.ToDateTime(y.DateChanged),
                                Convert.ToDateTime(x.DateChanged));
                        });
                    }
                    catch
                    {
                        Menu.StatusMessage(DialogText.ErrorSortingList, true, true, true, DialogText.CaptionsSearch,
                            string.Empty, false);
                    }

                    // Grab the top of the list
                    for (int i = 0; i < _foundEntries.Count; i++)
                    {
                        _foundEntriesLimited.Add(_foundEntries[i]);
                        // Clear the original list and add the grabbed entries back to the list
                        if (i >= SearchLimit)
                        {
                            _foundEntries.Clear();
                            _foundEntries.AddRange(_foundEntriesLimited);
                            break;
                        }
                    }
                }
                else if (searchCode == 2) // Favorites
                {
                    Mode.SetRecallMode(Mode.RecallMode.Favorites);
                    // Start by sorting date changed desc
                    try
                    {
                        // Sort by DateChanged desc
                        _foundEntries.Sort(delegate(Entry x, Entry y)
                        {
                            return DateTime.Compare(Convert.ToDateTime(y.DateChanged),
                                Convert.ToDateTime(x.DateChanged));
                        });
                    }
                    catch
                    {
                        Menu.StatusMessage(DialogText.ErrorSortingList, true, true, true, DialogText.CaptionsSearch,
                            string.Empty, false);
                    }

                    // Add pinned favorites to foundEntriesLimited first
                    for (int i = 0; i < _foundEntries.Count; i++)
                    {
                        if (IsFavorite(_foundEntries[i]) && IsPinned(_foundEntries[i]))
                        {
                            _foundEntriesLimited.Add(_foundEntries[i]);
                        }
                    }

                    // Sort pinned entries now in the foundEntriesLimited by Ranking
                    try
                    {
                        // Sort by Ranking
                        _foundEntriesLimited.Sort((x, y) => Entries.ExtractRank(x.Options).CompareTo(Entries.ExtractRank(y.Options)));
                    }
                    catch
                    {
                        Menu.StatusMessage(DialogText.ErrorSortingList, true, true, true, DialogText.CaptionsSearch,
                            string.Empty, false);
                    }

                    // Add normal favorites that arent pinned afterwards
                    foreach (var t in _foundEntries)
                    {
                        if (IsFavorite(t) && !IsPinned(t))
                        {
                            _foundEntriesLimited.Add(t);
                        }
                    }

                    if (_foundEntriesLimited.Count > 0)
                    {
                        _foundEntries.Clear();
                        _foundEntries.AddRange(_foundEntriesLimited);
                    }
                    else
                    {
                        _foundEntries.Clear();
                        Menu.StatusMessage(DialogText.NoFavoritesFound, true, true, true, DialogText.Favorites,
                            string.Empty, false);
                        Mode.SetMode(Mode.SelectedMode.Manage);
                        Menu.DrawTopMenu(true, true, false);
                        return;
                    }
                }
                else if (searchCode == 3) // Session
                {
                    Mode.SetRecallMode(Mode.RecallMode.Session);

                    // Match session entries and add them to the foundEntriesLimited in the order added (starting from the end of the list)
                    for (int i = 0; i < Entries.GetSessionIdList().Count; i++)
                    {
                        for (int j = 0; j < _foundEntries.Count; j++)
                        {
                            if (_foundEntries[j].Id == Entries.GetSessionIdList()[i])
                            {
                                _foundEntriesLimited.Add(_foundEntries[j]);
                                break; // Break when found and continue with the next Session ID...
                            }
                        }
                    }

                    if (_foundEntriesLimited.Count > 0)
                    {
                        _foundEntries.Clear();
                        _foundEntries.AddRange(_foundEntriesLimited);
                    }
                    else
                    {
                        _foundEntries.Clear();
                        Menu.StatusMessage(DialogText.NoSessionData, true, true, true, DialogText.Session,
                            string.Empty, false);
                        Mode.SetMode(Mode.SelectedMode.Manage);
                        Menu.DrawTopMenu(true, true, false);
                        return;
                    }
                }

                // Split the result into pages
                // A page has the rows between the column names and the line above the keys, less the page number
                if (_foundEntries.Count > Init.LineCount - 7)
                {
                    _resultPages = Tools.SplitIntoChunks(_foundEntries, Init.LineCount - 7);
                }
                else
                {
                    _resultPages.Add(_foundEntries);
                }

                _currentEntryResultPage = 0;
                _currentEntryResultIndex = 0;
                // Save current entry page and index in result pages for eventual use
                for (int i = 0; i < _resultPages.Count; i++)
                {
                    for (int j = 0; j < _resultPages[i].Count; j++)
                    {
                        if (_resultPages[i][j].Id == Entries.CurrentEntry.Id)
                        {
                            _currentEntryResultPage += i;
                            _currentEntryResultIndex += j;
                            break;
                        }
                    }
                }


                if (searchCode == 1)
                {
                    Mode.SetSearchMode(Mode.SearchMode.Top20);
                }
                else if (searchCode == 2)
                {
                    Mode.SetSearchMode(Mode.SearchMode.Favorites);
                }
                else if (searchCode == 3)
                {
                    Mode.SetSearchMode(Mode.SearchMode.Session);
                }
                else if (_queryMatch)
                {
                    Mode.SetSearchMode(Mode.SearchMode.Search);
                }
                else
                {
                    Mode.SetSearchMode(Mode.SearchMode.ListAll);
                }

                if (recallSelection)
                {
                    RecallSelectionInSearchResult();
                }
                else
                {
                    DisplayResultPages(0, false);
                }
            }
            else
            {
                Menu.StatusMessage(DialogText.NoEntriesFound, true, true, true, DialogText.CaptionsSearch, string.Empty,
                    false);
                if (reOpenSearchBox)
                {
                    Search();
                }
                else
                {
                    Menu.DrawTopMenu(true, true, false);
                }
            }
        }
        else
        {
            Menu.StatusMessage(DialogText.NoEntriesFound, true, true, true, DialogText.CaptionsSearch, string.Empty,
                false);
            Menu.DrawTopMenu(true, true, false);
        }
    }


    private static void WriteHorizontalRule(bool jaggedLine, string label, bool colorizeLabel,
        ConsoleColors.ColorPreset preset)
    {
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        if (jaggedLine)
        {
            Console.Write(Tools.WriteJaggedLine(Init.LineWidth));
        }
        else
        {
            Console.Write(new string('-', Init.LineWidth));
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        if (label.Length > 0)
        {
            Tools.CursorReturn();
            Tools.CursorRight(Init.LineWidth / 2);
            Tools.CursorLeft((label.Length / 2) + 1);
            if (colorizeLabel)
            {
                ConsoleColors.SetColors(preset);
                Console.Write(" " + label + " ");
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            }
            else
            {
                Console.Write(" " + label + " ");
            }
        }

        Tools.CursorReturn();
        Tools.CursorDown();
    }


    private static string _tempPaginationText = string.Empty;
    private const string TempPaginationText2 = " " + DialogText.PaginationLeftRight + " ";
    private static string _tempTruncatedTitle = string.Empty;
    private static string _tempDateChanged = string.Empty;

    private static void DisplayResultPages(int page, bool usingRecall)
    {
        if (_resultPages.Count > 0 && (page >= 0 && page < _resultPages.Count))
        {
            Mode.SetMode(Mode.SelectedMode.Search);
            Menu.DrawTopMenu(true, true, false);
            // The kind of list at the top left (as MENU, INSPECT and EDITOR on their pages)
            Menu.WriteModeLabel(Mode.CurrentSearchMode switch
            {
                Mode.SearchMode.Search => DialogText.CaptionsSearch,
                Mode.SearchMode.Favorites => DialogText.CaptionsFavorites,
                Mode.SearchMode.Top20 => DialogText.CaptionsTop20,
                Mode.SearchMode.Session => DialogText.CaptionsSession,
                _ => DialogText.CaptionsAll
            }, string.Empty, 0);
            Menu.WriteNameInTopBar(true);
            Tools.SetCursorEx(1, 0);
            WriteHorizontalRule(false, "", false, ConsoleColors.ColorPreset.Normal);
            Tools.SetCursorEx(ListHeaderRow, 0);

            // Write column names
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            if (Mode.CurrentSearchMode == Mode.SearchMode.ListAll || Mode.CurrentSearchMode == Mode.SearchMode.Search)
            {
                Console.Write(" " + DialogText.Select + new string(' ', 4) + DialogText.Title + new string(' ',
                    Math.Max(1, Init.LineWidth - ((DialogText.Select.Length + 1) + DialogText.Title.Length +
                                                  GetSortCaption().Length + 4))));
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.EditorTitle);
                Console.Write(GetSortCaption());
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            }
            else if
                (Mode.CurrentSearchMode ==
                 Mode.SearchMode.Favorites) // Favorites uses "date changes" but without changing the header color
            {
                Console.Write(" " + DialogText.Select + new string(' ', 4) + DialogText.Title + new string(' ',
                    Math.Max(1, Init.LineWidth - ((DialogText.Select.Length + 1) + DialogText.Title.Length +
                                                  DialogText.FavoritesSortColumn.Length + 5))));
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                Console.Write(DialogText.FavoritesSortColumn);
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            }
            else // All other: dynamic search, top 20 are "Date changed" with header color to indicate the sorting
            {
                // The column name ends 4 columns from the right side, however long it is
                Console.Write(" " + DialogText.Select + new string(' ', 4) + DialogText.Title +
                              new string(' ', Math.Max(1, Init.LineWidth - 4 - (1 + DialogText.Select.Length + 4 +
                                  DialogText.Title.Length) - DialogText.SortOptionDateChanged.Length)));
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.EditorTitle);
                Console.Write(DialogText.SortOptionDateChanged);
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            }

            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Tools.CursorReturn();
            Tools.CursorDown();
            WriteHorizontalRule(false, "", false, ConsoleColors.ColorPreset.Normal);
            // The line above the keys, and the keys on the bottom line
            Tools.SetCursorEx(Init.LineCount - 2, 0);
            WriteHorizontalRule(false, "", false, ConsoleColors.ColorPreset.Normal);
            Tools.SetCursorEx(Init.LineCount - 1, 0);
            // (while a pinned entry is moved, the keys for moving it are shown instead)
            if (!_movingPinned) Menu.WriteKeyHints(DialogText.ListKeys);
            Tools.SetCursorEx(ListFirstRow, 0);

            // Draw content
            for (int j = 0; j < _resultPages[page].Count; j++)
            {
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                // Space
                Console.Write(new string(' ', 3));
                // Number
                Console.Write((j + 1).ToString());
                // Indicate favorite
                if (IsFavorite(_resultPages[page][j]))
                {
                    // This will ensure that highlights on the star (*) are only shown in other lists than the favourites list
                    if (Mode.CurrentSearchMode != Mode.SearchMode.Favorites && IsPinned(_resultPages[page][j]))
                    {
                        PinColors.Apply(_resultPages[page][j], ConsoleColors.ColorPreset.Favorites);
                    }
                    else
                    {
                        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Favorites);
                    }

                    Console.Write("*");
                    ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);

                    // Dots
                    ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                    Console.Write(" " + new string('.', (5 - (j + 1).ToString().Length)));
                    ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                }
                else
                {
                    // Dots
                    ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                    Console.Write(" " + new string('.', (6 - (j + 1).ToString().Length)));
                    ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                }

                // Get title and date
                _tempTruncatedTitle =
                    Tools.TruncateString(_resultPages[page][j].Title, Tools.GetTitleListWidth(), true);
                if (Mode.CurrentSearchMode == Mode.SearchMode.ListAll)
                {
                    // Match the dates used for the sorting
                    if (_sortOption == 1)
                    {
                        _tempDateChanged = _resultPages[page][j].DateChanged;
                    }
                    else
                    {
                        _tempDateChanged = _resultPages[page][j].DateCreated;
                    }
                }
                else
                {
                    _tempDateChanged = _resultPages[page][j].DateChanged;
                }

                Console.Write(" ");
                // Title
                var moving = _movingPinned && page == _lastSelectedPage && j == _lastSelectedIndex;
                if (moving)
                {
                    ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
                }
                else if (Mode.CurrentSearchMode == Mode.SearchMode.Favorites && IsPinned(_resultPages[page][j]))
                {
                    PinColors.Apply(_resultPages[page][j], ConsoleColors.ColorPreset.Favorites);
                }
                else
                {
                    ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                }

                Console.Write(_tempTruncatedTitle);
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                // Dots, ending with the syntax of the entry if it has one ("MD")
                var dots = Init.LineWidth - (_tempTruncatedTitle.Length + 29);
                var syntax = EntrySyntax.ShortName(EntrySyntax.Get(_resultPages[page][j].Options));
                if (syntax.Length > 0 && dots > syntax.Length + 2)
                {
                    Console.Write(" " + new string('.', dots - syntax.Length - 1) + " ");
                    ConsoleColors.SetColors(ConsoleColors.ColorPreset.EditorTitle);
                    Console.Write(syntax);
                    ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                }
                else
                {
                    Console.Write(" " + new string('.', dots));
                }
                // Changed date
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                //  [    PINNED    ]
                if (Mode.CurrentSearchMode == Mode.SearchMode.Favorites && IsPinned(_resultPages[page][j]))
                {
                    ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                    Console.Write(" ");
                    if (moving) ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
                    Console.Write("[" + new string(' ', 4) + (moving ? "MOVING" : "PINNED") + new string(' ', 4) + "]");
                    ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                }
                else
                {
                    if (_sortOption == 2) // If sort by title dim the dates
                    {
                        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                    }

                    // Normal (2022-01-02 14:53)
                    Console.Write(string.Concat(" ",
                        _tempDateChanged.AsSpan(0,
                            (_tempDateChanged.Length - 3)))); // Show the timestamp without seconds to save width
                }

                Console.Write(Environment.NewLine);
            }

            // Write page-number
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            _tempPaginationText = Lang.F("Page {0} of {1}", page + 1, _resultPages.Count);
            if (_resultPages.Count > 1)
            {
                Tools.CursorRight(Init.LineWidth - ((_tempPaginationText.Length + TempPaginationText2.Length)));
            }
            else
            {
                Tools.CursorRight(Init.LineWidth - (_tempPaginationText.Length));
            }

            Console.Write(_tempPaginationText);
            if (_resultPages.Count > 1)
            {
                Console.Write(" ");
                Console.Write(TempPaginationText2);
            }

            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            if (_movingPinned)
            {
                // The keys for moving the entry on the bottom line
                Tools.SetCursorEx(Init.LineCount - 1, 0);
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
                Console.Write(" " + DialogText.MovePinnedKeys + " ");
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            }

            if (!usingRecall)
            {
                SelectFromSearchResult(page, false);
            }
        }
    }

    private static void TransferCurrentEntryToLastSelected()
    {
        _lastSelectedIndex = _currentEntryResultIndex;
        _lastSelectedPage = _currentEntryResultPage;
    }

    private static void ResetLastSelectedIndex()
    {
        _lastSelectedIndex = 0;
        _lastSelectedPage = 0;
    }

    private static int _lastSelectedIndex;
    private static int _lastSelectedPage;
    private static bool _transferCurrentEntryResult;

    private static void RecallSelectionInSearchResult()
    {
        if (_transferCurrentEntryResult)
        {
            _transferCurrentEntryResult = false;
            TransferCurrentEntryToLastSelected();
        }

        DisplayResultPages(_lastSelectedPage, true);
        if (_resultPages[_lastSelectedPage].Count > 0)
        {
            // Place selection cursor:
            _resultBottom = _resultPages[_lastSelectedPage].Count - 1;
            Tools.SetCursorEx(ListFirstRow, 0);
            _resultIndex = _lastSelectedIndex;
            if (_resultIndex <= _resultBottom)
            {
                Tools.CursorDown(_lastSelectedIndex);
                Menu.WriteSelectionPointer(false);
            }
            else
            {
                _lastSelectedIndex = 0;
                _resultIndex = _lastSelectedIndex;
                Menu.WriteSelectionPointer(false);
            }

            SelectFromSearchResult(_lastSelectedPage, true);
        }
        else
        {
            SelectFromSearchResult(0, false);
        }
    }

    #region Select from search result

    // Local cursor memory for the result selection
    private static int _tempCursorMemoryTop;
    private static int _tempCursorMemoryLeft;

    private static void SaveCursorPosition()
    {
        _tempCursorMemoryTop = Console.CursorTop;
        _tempCursorMemoryLeft = Console.CursorLeft;
    }

    private static void LoadCursorPosition()
    {
        Tools.SetCursorEx(_tempCursorMemoryTop, _tempCursorMemoryLeft);
    }

    private static bool _isSelectingResult;
    private const int ResultTop = 0;
    private static int _resultIndex;
    private static int _resultBottom;

    private const int ListHeaderRow = 2; // The names of the columns of a list of entries
    private const int ListFirstRow = 4; // The first entry of a list

    private static void SelectFromSearchResult(int pageNumber, bool isRecall)
    {
        _isSelectingResult = false;
        if (_resultPages[pageNumber].Count > 0)
        {
            _isSelectingResult = true;
            if (!isRecall)
            {
                // Place selection cursor:
                _resultBottom = _resultPages[pageNumber].Count - 1;
                Tools.SetCursorEx(ListFirstRow, 0);
                Menu.WriteSelectionPointer(false);
                _resultIndex = 0;
            }


            // The list is shown: wait for the next key
            _selectionPage = pageNumber;
            Navigation.Wait();
        }
    }

    private static int _selectionPage; // The page of the list where an entry is being selected

    // Handle a key pressed in a list of entries
    private static void HandleListKey()
    {
        var pageNumber = _selectionPage;
        SaveCursorPosition();
        if (_movingPinned)
        {
            HandleMovePinnedKey(pageNumber);
            return;
        }

        if (_c.KeyChar == ':')
        {
            // Command line
            var command = CommandLine.Read(CommandContext.List);
            if (command == null)
            {
                ExitNumericSelect(pageNumber, _resultIndex);
                return;
            }

            RunListCommand(command, pageNumber);
            if (_isSelectingResult)
            {
                // The command did not lead anywhere else: show the list again
                ExitNumericSelect(pageNumber, _resultIndex);
            }

            return;
        }
        else if (_c.KeyChar == '?')
        {
            // Help
            Tools.WriteEditorHelp(false, Tools.HelpTopic.Menus);
            ExitNumericSelect(pageNumber, _resultIndex);
            return;
        }
        else if (_c.KeyChar == 'G')
        {
            // The last entry of the list (on the last page)
            if (pageNumber == _resultPages.Count - 1)
            {
                MoveSelection(_resultBottom);
            }
            else
            {
                ShowListAt(_resultPages.Count - 1, _resultPages[^1].Count - 1);
            }
        }
        else if (_c.KeyChar == 'g')
        {
            // "gg": The first entry of the list (on the first page)
            if (Console.ReadKey(true).KeyChar == 'g')
            {
                if (pageNumber == 0)
                {
                    MoveSelection(ResultTop);
                }
                else
                {
                    ShowListAt(0, 0);
                }
            }
        }
        else if (_c.Key == ConsoleKey.P)
        {
            // Favorite/pin
            SelectResultFromIndex(pageNumber, _resultIndex, 7);
            LoadCursorPosition();
        }
        else if (_c.Key == ConsoleKey.C)
        {
            // The text color of a pinned favorite (as C in the favorites of the file browser)
            PickPinColor(pageNumber);
            return;
        }
        // Date time
        else if (_c.Key == ConsoleKey.D)
        {
            // Display timestamp
            Tools.DisplayDateTime();
            ExitNumericSelect(pageNumber, _resultIndex);
            return;
        }
        // Search
        else if (_c.Key == ConsoleKey.S || _c.KeyChar == '/')
        {
            DefaultSearchSortOption();
            Search();
        }
        // List all
        else if (_c.Key == ConsoleKey.A)
        {
            DefaultListSortOption();
            FindAll(0, string.Empty, false, false);
        }
        else if (Mode.CurrentRecallMode == Mode.RecallMode.Favorites &&
                 _c.Key is ConsoleKey.Tab or ConsoleKey.V)
        {
            // Move a pinned entry
            StartMovingPinned(pageNumber);
        }
        // List all/Search (Sort)
        else if (_c.Key == ConsoleKey.Tab)
        {
            // Only cycle sorting options if already in ListAll mode
            if (Mode.CurrentRecallMode == Mode.RecallMode.ListAll)
            {
                NextSortOption();
                FindAll(0, string.Empty, false, false);
            }
            else if (Mode.CurrentRecallMode == Mode.RecallMode.Search && Tools.IsValid(_searchQuery))
            {
                NextSortOption();
                FindAll(0, _searchQuery, true, false);
            }
            // Else do nothing as the other lists do not have the option to toggle sort
        }
        // Top 20
        else if (_c.Key == ConsoleKey.T)
        {
            FindAll(1, string.Empty, false, false);
        }
        // Favorites
        else if (_c.Key == ConsoleKey.F)
        {
            FindAll(2, string.Empty, false, false);
        }
        // Session
        else if (_c.Key == ConsoleKey.M)
        {
            FindAll(3, string.Empty, false, false);
        }
        // New entry
        else if (_c.Key == ConsoleKey.N)
        {
            NewEntry();
            // Return to main
            _isSelectingResult = false;
            Mode.SetMode(Mode.SelectedMode.Manage);
            Menu.DrawTopMenu(true, true, false);
        }
        // Recall
        else if (_c.Key == ConsoleKey.R)
        {
            RecallEntry();
            // Return to main
            _isSelectingResult = false;
            Mode.SetMode(Mode.SelectedMode.Manage);
            Menu.DrawTopMenu(true, true, false);
        }
        else if (_c.Key == ConsoleKey.I)
        {
            // The dates and the size of the selected entry
            SelectResultFromIndex(pageNumber, _resultIndex, 11);
        }
        // Logout
        else if (_c.Key == ConsoleKey.X)
        {
            Tools.LoginRoutine(true);
        }
        else if (_c.Key is ConsoleKey.UpArrow or ConsoleKey.K)
        {
            if (_resultIndex > ResultTop)
            {
                _resultIndex -= 1;
                Menu.WriteSelectionPointer(true);
                Tools.CursorUp(1);
                Menu.WriteSelectionPointer(false);
            }
            else
            {
                // Else jump to last item
                _resultIndex = _resultBottom;
                Menu.WriteSelectionPointer(true);
                Tools.CursorDown(_resultIndex);
                Menu.WriteSelectionPointer(false);
            }
        }
        else if (_c.Key is ConsoleKey.DownArrow or ConsoleKey.J)
        {
            if (_resultIndex < _resultBottom)
            {
                _resultIndex += 1;
                Menu.WriteSelectionPointer(true);
                Tools.CursorDown();
                Menu.WriteSelectionPointer(false);
            }
            else
            {
                // Else jump to first item
                _resultIndex = ResultTop;
                Menu.WriteSelectionPointer(true);
                Tools.CursorUp(_resultBottom);
                Menu.WriteSelectionPointer(false);
            }
        }
        else if (_c.Key == ConsoleKey.PageUp)
        {
            // Move by 3
            if (_resultIndex >= 3 && ((_resultIndex - 3) >= ResultTop))
            {
                _resultIndex -= 3;
                Menu.WriteSelectionPointer(true);
                Tools.CursorUp(3);
                Menu.WriteSelectionPointer(false);
            }
            else
            {
                if ((_resultIndex - 2) >= ResultTop)
                {
                    _resultIndex -= 2;
                    Menu.WriteSelectionPointer(true);
                    Tools.CursorUp(2);
                    Menu.WriteSelectionPointer(false);
                }
                else if ((_resultIndex - 1) >= ResultTop)
                {
                    _resultIndex -= 1;
                    Menu.WriteSelectionPointer(true);
                    Tools.CursorUp(1);
                    Menu.WriteSelectionPointer(false);
                }
            }
        }
        else if (_c.Key == ConsoleKey.PageDown)
        {
            // Move by 3
            if ((_resultIndex + 3) < _resultBottom)
            {
                _resultIndex += 3;
                Menu.WriteSelectionPointer(true);
                Tools.CursorDown(3);
                Menu.WriteSelectionPointer(false);
            }
            else
            {
                if ((_resultIndex + 2) <= _resultBottom)
                {
                    _resultIndex += 2;
                    Menu.WriteSelectionPointer(true);
                    Tools.CursorDown(2);
                    Menu.WriteSelectionPointer(false);
                }
                else if ((_resultIndex + 1) <= _resultBottom)
                {
                    _resultIndex += 1;
                    Menu.WriteSelectionPointer(true);
                    Tools.CursorDown(1);
                    Menu.WriteSelectionPointer(false);
                }
            }
        }
        else if (_c.Key is ConsoleKey.LeftArrow or ConsoleKey.H)
        {
            DisplayResultPages(pageNumber - 1, false);
        }
        else if (_c.Key == ConsoleKey.Home)
        {
            DisplayResultPages(0, false);
        }
        else if (_c.Key is ConsoleKey.RightArrow or ConsoleKey.L)
        {
            DisplayResultPages(pageNumber + 1, false);
        }
        else if (_c.Key == ConsoleKey.End)
        {
            DisplayResultPages(_resultPages.Count - 1, false);
        }
        else if (_c.Key == ConsoleKey.Escape || _c.KeyChar == '-')
        {
            // Return to main (Esc, or "-" as in the file browser)
            _isSelectingResult = false;
            Mode.SetRecallMode(Mode.RecallMode.None);
            Mode.SetMode(Mode.SelectedMode.Manage);
            Menu.DrawTopMenu(true, true, false);
        }
        else if (_c.Key is ConsoleKey.Enter or ConsoleKey.Spacebar)
        {
            SelectResultFromIndex(pageNumber, _resultIndex, 0);
        }
        else if (Tools.IsNumeric(_c.KeyChar.ToString())) // Select from 1-9
        {
            SelectResultFromNumericValue(pageNumber, _c, false, 0);
        }
    }

    // Show a page of the list with an entry selected
    private static void ShowListAt(int page, int index)
    {
        _lastSelectedPage = page;
        _lastSelectedIndex = index;
        RecallSelectionInSearchResult();
    }

    // Pick the text color of the selected entry (a pinned favorite) from the list of colors
    private static void PickPinColor(int pageNumber)
    {
        if (!ValidateNumericSelectionInput(pageNumber, _resultIndex)) return;
        var entry = _resultPages[pageNumber][_resultIndex];
        if (!IsPinned(entry) || PinColors.Pick(PinColors.Get(entry.Options), out _pinColor))
        {
            SelectResultFromIndex(pageNumber, _resultIndex, 3);
        }

        RecallPreviousView();
    }

    private static void ExitNumericSelect(int page, int index)
    {
        _lastSelectedIndex = index;
        _lastSelectedPage = page;
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        RecallPreviousView();
    }

    private static bool ValidateNumericSelectionInput(int pageNumber, int resultIndex)
    {
        if (_resultPages.Count > 0 && pageNumber < _resultPages.Count)
        {
            if (resultIndex <= _resultPages[pageNumber].Count)
            {
                return true;
            }
        }

        return false;
    }

    private static void SelectResultFromIndex(int pageNumber, int resultIndex, int action)
    {
        try
        {
            // Only continue if valid entry index
            if (!ValidateNumericSelectionInput(pageNumber, resultIndex)) return;


            // Take action
            _isSelectingResult = false;
            if (Entries.EntryExists(_resultPages[pageNumber][resultIndex].Id))
            {
                // Save index and page
                _lastSelectedIndex = resultIndex;
                _lastSelectedPage = pageNumber;
                Entries.SelectEntryById(_resultPages[pageNumber][resultIndex].Id);
                if (action == 0)
                {
                    EditEntry(false);
                }
                else if (action == 1)
                {
                    ChangeTitle(true);
                }
                else if (action == 2)
                {
                    DeleteCurrentEntry();
                }
                else if (action == 3)
                {
                    SetPinColor(_pinColor);
                }
                else if (action == 6)
                {
                    Entries.TransferEntry();
                }
                else if (action == 8)
                {
                    DuplicateCurrentEntry();
                }
                else if (action == 9)
                {
                    // Only the entry whose last edit was kept can be reverted
                    if (Entries.BackupEntry.Id == Entries.CurrentEntry.Id)
                    {
                        RestoreBackup();
                    }
                    else
                    {
                        Menu.StatusMessage(DialogText.NoBackupFound, true, true, true, DialogText.CaptionsRevert,
                            string.Empty, false);
                        ReturnFromEntry();
                    }
                }
                else if (action == 10)
                {
                    ExportCurrentEntry();
                }
                else if (action == 11)
                {
                    ShowEntryInfo();
                    ReturnFromEntry();
                }
                else if (action == 7)
                {
                    ToggleFavorite();
                    // Display favorites
                    FindAll(2, string.Empty, false, false);
                }
            }
        }
        catch (Exception e) when (e is not NavigationException)
        {
            ChoiceBox.Message(DialogText.ApplicationName, e.Message, string.Empty);
        }
    }

    private static void SelectResultFromNumericValue(int pageNumber, ConsoleKeyInfo ck, bool useFreeValue, int value)
    {
        // If numeric between 1-9 and 0 (10) was selected
        // or using "free value" from typing in dialog
        try
        {
            int tempNumber;
            if (useFreeValue)
            {
                tempNumber = value;
            }
            else
            {
                tempNumber = Convert.ToInt32(ck.KeyChar.ToString());
            }

            if (tempNumber >= 0 && tempNumber <= _resultPages[pageNumber].Count)
            {
                _isSelectingResult = false;
                // Convert zero to 10
                if (tempNumber == 0)
                    tempNumber = 10;
                // Account for zero indexing
                tempNumber = tempNumber - 1;
                if (Entries.EntryExists(_resultPages[pageNumber][tempNumber].Id))
                {
                    // Save index and page
                    _lastSelectedIndex = tempNumber;
                    _lastSelectedPage = pageNumber;
                    Entries.SelectEntryById(_resultPages[pageNumber][tempNumber].Id);
                    EditEntry(false);
                }
            }
        }
        catch (Exception e) when (e is not NavigationException)
        {
            ChoiceBox.Message(DialogText.ApplicationName, e.Message, string.Empty);
        }
    }

    #endregion
}