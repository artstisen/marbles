namespace Marbles;

internal static class Menu
{
    private static void ResetTopMenuDisplayIndex()
    {
        _manageModePage = 0;
        ResetMenuSelection();
    }

    public static int GetTopMenuManagePage()
    {
        return _manageModePage;
    }

    private const int MaxManageModePages = 3;

    /// <summary>
    /// The number of tabs of the main menu (Notes, Data file, Settings, Marbles)
    /// </summary>
    internal static int ManagePageCount => MaxManageModePages + 1;

    /// <summary>
    /// The tab of the main menu a command is shown on
    /// </summary>
    internal static int PageOfCommand(string command)
    {
        return command switch
        {
            "undelete" => 0,
            "import" or "transfer" or "backup" or "password" => 1,
            "scheme" or "logo" or "bw" or "language" or "number" or "size" or "clipboard" => 2,
            _ => 3
        };
    }

    public static void NextTopMenuManagePage()
    {
        ResetMenuSelection();
        if (_manageModePage < MaxManageModePages)
        {
            _manageModePage += 1;
        }
        else
        {
            _manageModePage = 0;
        }
    }

    public static void PreviousTopMenuManagePage()
    {
        ResetMenuSelection();
        if (_manageModePage > 0)
        {
            _manageModePage -= 1;
        }
        else
        {
            _manageModePage = MaxManageModePages;
        }
    }

    public static void SetTopMenuManagePage(int index)
    {
        if (index is >= 0 and <= MaxManageModePages)
        {
            if (index != _manageModePage) ResetMenuSelection();
            _manageModePage = index;
        }
    }

    private static int _manageModePage;
    internal static bool BypassAwaitAction { get; set; }

    public static void DrawTopMenu(bool displayMenuFromPageOne, bool clearScreen, bool useEditorColors)
    {
        if (displayMenuFromPageOne)
        {
            ResetTopMenuDisplayIndex(); // Always use page
        }

        // Set cursor at 0x0
        Tools.CursorTopLeft();
        if (clearScreen)
        {
            Init.ApplyAutoSize(); // Follow the size of the terminal window (if automatic dimensions are on)
            Tools.ClearScreen(); // Clear screen
        }

        if (Mode.CurrentMode == Mode.SelectedMode.Login)
        {
            Tools.LoginRoutine(true);
        }
        else if (Mode.CurrentMode == Mode.SelectedMode.Manage)
        {
            DrawMenuPage(_manageModePage);

            if (BypassAwaitAction)
            {
                BypassAwaitAction = false;
                return;
            }

            // The menu is shown: wait for the next key
            Navigation.Wait();
        }
        else if (Mode.CurrentMode == Mode.SelectedMode.Edit)
        {
            // The editor writes its own top row (WriteEditorTop)
        }
        else if (Mode.CurrentMode == Mode.SelectedMode.Search)
        {
            // A list of entries draws its own page (Actions.DisplayResultPages)
        }
        else
        {
            DrawTopMenu(true, true, false);
        }
    }


    private static string GetMenuNavigation(string pages)
    {
        return "<- " + pages + " ->  h/l";
    }

    private static string GetFavoriteState()
    {
        return Actions.IsFavorite(Entries.CurrentEntry) ? "*" : string.Empty;
    }

    public static void StatusMessage(string msg, bool requireKeyPress, bool clearScreen, bool showCaption,
        string caption, string note, bool suppressPause)
    {
        if (string.IsNullOrEmpty(msg)) return;
        if (requireKeyPress)
        {
            // A message that waits for a key is shown in a box on top of the page
            ChoiceBox.Message(showCaption && !string.IsNullOrEmpty(caption) ? caption : DialogText.ApplicationName,
                msg, note);
            return;
        }

        // Clear screen
        if (clearScreen)
        {
            Tools.ClearScreen();
            // Show top info
            if (showCaption && !string.IsNullOrEmpty(caption))
            {
                WriteModeLabel(caption, note, 0);
                WriteNameInTopBar(true);
                Tools.CursorDown(2);
            }
        }

        Tools.CursorReturn();
        // Write message
        Console.Write(" ");
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
        Console.Write(" " + msg + " ");
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        // Require keypress
        if (requireKeyPress)
        {
            Tools.CursorReturn();
            Tools.CursorDown(2);
            Tools.CursorRight(1);
            Keyboard.AwaitAnyKey();
        }
        else
        {
            if (!suppressPause)
            {
                Thread.Sleep(100);
            }
        }
    }


    /// <summary>
    /// Ask yes or no in a box on top of the page (ChoiceBox)
    /// </summary>
    /// <param name="invertColors">True for something that cannot be undone: the question is shown as a warning
    /// and No is selected first</param>
    public static bool YesNoPrompt(string caption, string question, string note, bool invertColors)
    {
        return ChoiceBox.Confirm(caption, question, note, invertColors);
    }

    // Returns:
    // 1 = Yes
    // 2 = Option
    // 3 = No
    /// <summary>
    /// Ask yes, an option or no in a box on top of the page
    /// </summary>
    /// <returns>1 = Yes, 2 = the option, 3 = No (also Esc)</returns>
    public static byte YesOptionNoPrompt(string caption, string question, string note, bool invertColors,
        ConsoleKey optionKey, string option)
    {
        var choice = ChoiceBox.Pick(caption, question, note,
            new[] { DialogText.ChoiceYes, option, DialogText.ChoiceNo },
            new[] { "Y", optionKey.ToString(), "N" }, 0, invertColors);
        return choice switch
        {
            0 => 1,
            1 => 2,
            _ => 3
        };
    }

    // Returns:
    // 1 = Yes
    // 2 = No
    // 3 = Cancel
    /// <summary>
    /// Ask yes, no or cancel in a box on top of the page
    /// </summary>
    /// <returns>1 = Yes, 2 = No, 3 = Cancel (also Esc)</returns>
    public static byte YesNoCancelPrompt(string caption, string question, string note, bool invertColors)
    {
        var choice = ChoiceBox.Pick(caption, question, note,
            new[] { DialogText.ChoiceYes, DialogText.ChoiceNo, DialogText.ChoiceCancel },
            new[] { "Y", "N", "Esc" }, 0, invertColors);
        return choice switch
        {
            0 => 1,
            1 => 2,
            _ => 3
        };
    }





    // Primary menu item method
    public static bool DimMenuItem { get; set; }

    #region The main menu of a data file

    // An item of the main menu: its key (or command), what it does, and what selecting it runs: a key ("N") or a
    // command (":scheme")
    private readonly record struct MenuItem(string Key, string Text, string Run);

    private const int MenuTabRow = 2; // The row of the tabs (below the top bar)
    private const int MenuKeyWidth = 11; // The room for the key of an item (the dots fill up to it)

    // The items of the tab shown, the selected one, and where they are
    private static MenuItem[] _menuItems = Array.Empty<MenuItem>();
    private static int _menuRow;
    private static TabArea _menuArea;

    // The names of the tabs (in the language in use)
    private static string[] MenuTabTitles => new[]
    {
        Lang.T("Notes"), Lang.T("Data file"), DialogText.CaptionsSettings, DialogText.ApplicationName
    };

    // The items of a tab
    private static MenuItem[] MenuItems(int tab)
    {
        static string Check(bool on) => on ? DialogText.CheckedSetting : DialogText.UnCheckedSetting;
        return tab switch
        {
            0 => new MenuItem[]
            {
                new("N", Lang.T("New"), "N"),
                new("S /", Lang.T("Search"), "S"),
                new("R", Lang.T("Reselect"), "R"),
                new("F", Lang.T("Favorites") + " " + Entries.GetFavoritesCount(), "F"),
                new("T", Lang.T("Top 20"), "T"),
                new("M", Lang.T("Session history") + " " + Entries.GetSessionCountAsString(), "M"),
                new("A", Lang.T("List all") + " " + Entries.GetEntriesCountAsString(), "A"),
                new(":undelete", Lang.T("Undo deletion"), ":undelete")
            },
            1 => new MenuItem[]
            {
                new("I", Lang.T("File info"), "I"),
                new(":import", Lang.T("Import text file"), ":import"),
                new(":transfer", Lang.T("Transfer") + " " + Entries.GetTransferState(), ":transfer"),
                new(":backup", FileIo.GetBackupStatus() + " " + Lang.T("Backup"), ":backup"),
                new(":password", Lang.T("Reset password"), ":password"),
                new(":", Lang.T("Command line"), ":"),
                new("?", Lang.T("Help"), "?"),
                new("X", Lang.T("Logout"), "X")
            },
            2 => new MenuItem[]
            {
                new(":scheme", "[" + ColorSchemes.DisplayName(ConsoleColors.Current.Name) + "] " +
                               DialogText.CaptionsColorScheme, ":scheme"),
                new(":logo", "[" + Logo.Current.Name + "] " + DialogText.CaptionsLogos, ":logo"),
                new(":bw", Check(ConsoleColors.Monochrome) + " " + Lang.T("Black and white"), ":bw"),
                new(":language", "[" + Lang.Current + "] " + Lang.T("Language"), ":language"),
                new(":number", Check(TextArea.LineNumbers) + " " + Lang.T("Line numbers"), ":number"),
                new(":size", Lang.T("Size") + " " + Init.GetOutputDimensions(), ":size"),
                new(":size auto", Check(Init.AutoSize) + " " + Lang.T("Follow the window"), ":size auto"),
                new(":clipboard", Lang.T("Clipboard"), ":clipboard")
            },
            _ => new MenuItem[]
            {
                new(":browse", Lang.T("Browse files"), ":browse"),
                new("D", Lang.T("Display timestamp"), "D"),
                new(":guide", DialogText.QuickGuide, ":guide"),
                new(":defaults", Lang.T("Default settings"), ":defaults"),
                new(":jump", DialogText.StartJump, ":jump"),
                new(":config", Lang.T("More configurations"), ":config"),
                new(":logout", Lang.T("Logout"), ":logout"),
                new(":q", Lang.T("Exit"), ":q")
            }
        };
    }

    // The main menu of a data file: a frame with four tabs (Notes, Data file, Settings, Marbles), the items of
    // the tab shown in it
    private static void DrawMenuPage(int tab)
    {
        WriteModeLabel(DialogText.CaptionsMenu, string.Empty, 0);
        WriteNameInTopBar(true);
        Tools.SetCursorEx(1, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(new string('-', Init.LineWidth));
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        _menuItems = MenuItems(tab);
        ClampMenuSelection();
        _menuArea = TabFrame.DrawChrome(MenuTabRow, MenuTabTitles, tab, null, 0);
        // The items in the color of text in frames
        var frameText = ConsoleColors.FrameTextScope();
        for (var row = 0; row < _menuArea.Height; row++)
        {
            Tools.SetCursorEx(_menuArea.Top + row, _menuArea.Left);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Console.Write(new string(' ', _menuArea.Width));
            if (row >= _menuItems.Length) continue;
            var item = _menuItems[row];
            Tools.SetCursorEx(_menuArea.Top + row, _menuArea.Left + 3);
            Console.Write(item.Key);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write(" " + new string('.', Math.Max(1, MenuKeyWidth - item.Key.Length)) + " ");
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            // A long text (in another language) is cut at the side of the frame
            var room = _menuArea.Left + _menuArea.Width - Console.CursorLeft;
            Console.Write(Tools.TruncateString(item.Text, Math.Max(0, room), true));
        }

        frameText.Dispose();

        // The keys of the page
        Tools.SetCursorEx(Init.LineCount - 1, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        WriteKeyHints(DialogText.MenuKeys);
        DrawMenuPointer(true);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Tools.SetCursorEx(Init.LineCount - 1, 0);
    }

    private static void ClampMenuSelection()
    {
        _menuRow = Math.Clamp(_menuRow, 0, Math.Max(0, _menuItems.Length - 1));
    }

    // Draw (or remove) the selection pointer at the selected item
    private static void DrawMenuPointer(bool show)
    {
        if (_menuItems.Length == 0) return;
        Tools.SetCursorEx(_menuArea.Top + _menuRow, _menuArea.Left);
        ConsoleColors.SetColors(show ? ConsoleColors.ColorPreset.SelectorInverted : ConsoleColors.ColorPreset.Normal);
        Console.Write(show ? "->" : "  ");
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Tools.SetCursorEx(Init.LineCount - 1, 0);
    }

    /// <summary>
    /// Move the selection of the main menu up or down in its tab (only the pointer is drawn again)
    /// </summary>
    /// <returns>True if the selection has gone to another tab, which must be drawn</returns>
    internal static bool MoveMenuSelection(int rows)
    {
        var row = _menuRow + rows;
        if (row >= 0 && row < _menuItems.Length)
        {
            DrawMenuPointer(false);
            _menuRow = row;
            DrawMenuPointer(true);
            return false;
        }

        // Past the last item: the first of the next tab. Before the first: the last of the previous tab
        var forward = rows > 0;
        NextMenuBox(!forward);
        _menuRow = forward ? 0 : int.MaxValue; // The last of the tab, when it is drawn
        return true;
    }

    /// <summary>
    /// Go to the next tab of the main menu (or the previous), and round from the last to the first
    /// </summary>
    /// <returns>True: the tab must be drawn</returns>
    internal static bool NextMenuBox(bool back)
    {
        _manageModePage = TabLayout.Next(_manageModePage, ManagePageCount, back);
        _menuRow = 0;
        return true;
    }

    /// <summary>
    /// Select the first item of the first tab of the main menu (gg)
    /// </summary>
    internal static void SelectFirstMenuItem()
    {
        _manageModePage = 0;
        _menuRow = 0;
    }

    /// <summary>
    /// Select the last item of the last tab of the main menu (G)
    /// </summary>
    internal static void SelectLastMenuItem()
    {
        _manageModePage = ManagePageCount - 1;
        _menuRow = int.MaxValue; // The last of the tab, when it is drawn
    }

    /// <summary>
    /// The choices shown with Esc in the main menu: the Jump menu, the file browser, the Configuration page, log
    /// out, or exit Marbles
    /// </summary>
    /// <returns>The number of the choice (0 to 4, in that order), or -1 if the box was closed</returns>
    internal static int LeaveChoice()
    {
        // In the color of text in frames and boxes
        using var frameText = ConsoleColors.FrameTextScope();

        // The order is the one of the switch in Actions (the Options of the main menu)
        var choices = new[]
        {
            DialogText.StartJump, DialogText.HelpSectionFiles, DialogText.StartConfiguration, Lang.T("Logout"),
            DialogText.Exit
        };
        // The keys that open a choice at once, the same in all languages and as on the start page and in the
        // options of the login page: O the Jump menu (as Ctrl+O), F files, S Configuration, X Logout, Q Exit
        var keys = new[] { ConsoleKey.O, ConsoleKey.F, ConsoleKey.S, ConsoleKey.X, ConsoleKey.Q };
        // Room for "   1 O .... " in front of the longest choice, and three blank spaces after it
        const int textStart = 12;
        var inner = Math.Max(TextMetrics.WidthOf(DialogText.CaptionsOptions) + 4,
            choices.Max(c => TextMetrics.WidthOf(c)) + textStart + 3);
        var left = Math.Max(0, (Init.LineWidth - inner - 2) / 2);
        var top = MenuTabRow + 6;
        // The box, on top of the menu, with the tab of its title above it
        ChoiceBox.DrawTitleTab(top - 1, left, inner, DialogText.CaptionsOptions);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.BoxBorder);
        for (var row = 0; row < choices.Length + 2; row++)
        {
            Tools.SetCursorEx(top + 1 + row, left);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.BoxBorder);
            Console.Write('|');
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            // The number, the key, and the choice
            var text = row >= 1 && row <= choices.Length
                ? "   " + row + " " + keys[row - 1] + " .... " + choices[row - 1]
                : string.Empty;
            Console.Write(text + new string(' ', Math.Max(0, inner - TextMetrics.WidthOf(text))));
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.BoxBorder);
            Console.Write('|');
        }

        Tools.SetCursorEx(top + choices.Length + 3, left);
        Console.Write(AsciiStyles.GetBottomCorner() + new string('-', inner) + AsciiStyles.GetBottomCorner());
        // The keys of the box
        Tools.SetCursorEx(Init.LineCount - 1, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Console.Write(new string(' ', Init.LineWidth - 1));
        Tools.SetCursorEx(Init.LineCount - 1, 0);
        WriteKeyHints(DialogText.LeaveKeys);

        var selected = 0;
        while (true)
        {
            for (var i = 0; i < choices.Length; i++)
            {
                Tools.SetCursorEx(top + 2 + i, left + 1);
                ConsoleColors.SetColors(i == selected
                    ? ConsoleColors.ColorPreset.SelectorInverted
                    : ConsoleColors.ColorPreset.Normal);
                Console.Write(i == selected ? "->" : "  ");
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            }

            Tools.SetCursorEx(Init.LineCount - 1, 0);
            var key = Console.ReadKey(true);
            var navigation = Keyboard.ToNavigationKey(key).Key;
            // The key of a choice opens it at once
            var direct = Array.IndexOf(keys, key.Key);
            if (direct >= 0 && (key.Modifiers & (ConsoleModifiers.Control | ConsoleModifiers.Alt)) == 0) return direct;
            if (key.Key is ConsoleKey.Escape || navigation == ConsoleKey.LeftArrow) return -1;
            if (key.Key is ConsoleKey.Enter or ConsoleKey.Spacebar || navigation == ConsoleKey.RightArrow) return selected;
            if (navigation == ConsoleKey.DownArrow) selected = (selected + 1) % choices.Length;
            if (navigation == ConsoleKey.UpArrow) selected = (selected + choices.Length - 1) % choices.Length;
            if (key.KeyChar >= '1' && key.KeyChar < '1' + choices.Length) selected = key.KeyChar - '1';
            // gg and G: the first and the last choice
            if (key.KeyChar == 'G') selected = choices.Length - 1;
            if (key.KeyChar == 'g' && Console.ReadKey(true).KeyChar == 'g') selected = 0;
        }
    }

    /// <summary>
    /// Select the first item (when another tab is shown)
    /// </summary>
    internal static void ResetMenuSelection()
    {
        _menuRow = 0;
    }

    /// <summary>
    /// What the selected item of the main menu runs: a key ("N") or a command (":scheme"), or null if none
    /// </summary>
    internal static string? SelectedMenuItem()
    {
        return _menuRow < _menuItems.Length ? _menuItems[_menuRow].Run : null;
    }

    #endregion

    public static void WriteMenuItem(string key, string description, bool newLine)
    {
        // Clear line 45 chars
        Console.Write(new string(' ', 45));
        Tools.CursorReturn();
        // Change color if DimMenuItem
        if (DimMenuItem)
        {
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        }

        Console.Write(" " + key);
        if (description.Length > 0)
        {
            if (description == "-")
            {
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                Console.Write(new string('-', 30));
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            }
            else
            {
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                Console.Write(" " + new string('.', 13 - key.Length));
                if (!DimMenuItem)
                {
                    ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                }

                Console.Write(" " + description);
            }
        }

        // Reset DimMenuItem
        if (DimMenuItem) DimMenuItem = false;
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);

        if (newLine)
        {
            Console.Write(Entries.NewLineCharString);
        }
    }

    private static void WriteExtendedMenuItem(string key, string example, string description,
        ConsoleColors.ColorPreset preset, bool newLine, bool flipExample, Entry? colorOf = null)
    {
        // Clear line 40 chars
        Console.Write(new string(' ', 40));
        Tools.CursorReturn();
        Console.Write(" " + key);
        if (description.Length > 0)
        {
            Console.Write(" ");
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write(new string('.', 13 - key.Length));
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Console.Write(" ");
            if (flipExample)
            {
                Console.Write(description);
                // An entry can have a color of its own
                if (colorOf != null)
                {
                    PinColors.Apply(colorOf, preset);
                }
                else
                {
                    ConsoleColors.SetColors(preset);
                }

                Console.Write(example);
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            }
            else
            {
                ConsoleColors.SetColors(preset);
                Console.Write(example);
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                Console.Write(" " + description);
            }
        }

        if (newLine)
        {
            Console.Write(Entries.NewLineCharString);
        }
    }

    #region Floating menu

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

    public static void FloatingMenu(string caption, List<Shortcut> shortcuts, int top, int left, bool dockRight)
    {
        // In the color of text in frames and boxes
        using var frameText = ConsoleColors.FrameTextScope();

        // Return if list is null or empty
        if (shortcuts.Count == 0)
        {
            return;
        }

        var menuWidth = 0;
        // Get the widest item from the list
        foreach (Shortcut s in shortcuts)
        {
            if (s.GetLength() > menuWidth)
            {
                menuWidth = s.GetLength();
            }
        }

        menuWidth = menuWidth + 6;
        SaveCursorPosition();
        Tools.CursorTopLeft();
        Tools.CursorDown(top);
        if (dockRight)
        {
            left = Init.LineWidth - (menuWidth + 2);
        }

        // The title in a tab above the top line (on the row above the one given)
        ChoiceBox.DrawTitleTab(top - 1, left, menuWidth, caption);
        Tools.SetCursorEx(top, left);


        // Iterate and draw menu and menu items
        foreach (var t in shortcuts)
        {
            Tools.CursorReturn();
            Tools.CursorDown();
            Tools.CursorRight(left);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.BoxBorder);
            Console.Write("|" + new string(' ', menuWidth) + "|");
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Tools.CursorReturn();
            Tools.CursorRight(left + 2);
            if (t.GetDescription() == "-")
            {
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                Console.Write(new string('-', menuWidth - 2));
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            }
            else
            {
                t.WriteShortcut(false, false);
            }
        }

        // Write menu bottom
        Tools.CursorReturn();
        Tools.CursorDown();
        Tools.CursorRight(left);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.BoxBorder);
        Console.Write(AsciiStyles.GetBottomCorner() + new string('-', menuWidth) + AsciiStyles.GetBottomCorner());
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        LoadCursorPosition();
    }

    #endregion

    internal static void WriteSelectionPointer(bool delete)
    {
        if (delete)
        {
            Tools.CursorReturn();
            Console.Write(new String(' ', 2));
            Tools.CursorReturn();
        }
        else
        {
            Tools.CursorReturn();
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.SelectorInverted);
            // "<>" while the entry is moved (as in all the lists where items are moved)
            Console.Write(Actions.MovingPinned ? "<>" : "->");
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Tools.CursorReturn();
        }
    }

    private static void WriteMenuExtension(string extension)
    {
        if (string.IsNullOrEmpty(extension)) return;
        Tools.CursorReturn();
        Console.Write(" " + extension);
        Tools.CursorReturn();
        Tools.CursorDown();
    }

    #region Mode Label

    private static int _tempModeLabelCursorMemoryTop;
    private static int _tempModeLabelCursorMemoryLeft;

    private static void ModeLabelSaveCursor()
    {
        _tempModeLabelCursorMemoryTop = Console.CursorTop;
        _tempModeLabelCursorMemoryLeft = Console.CursorLeft;
    }

    private static void ModeLabelLoadCursor()
    {
        Tools.SetCursorEx(_tempModeLabelCursorMemoryTop, _tempModeLabelCursorMemoryLeft);
    }

    private static string _modeTitleMemory = string.Empty;
    private static string _modeNoteMemory = string.Empty;

    /// <param name="attention">True to show the label in the attention colors (something is in progress,
    /// as copying to a directory being selected)</param>
    public static void WriteModeLabel(string title, string note, int recallMode, bool attention = false)
    {
        // 0 = do nothing
        // 1 = store in memory
        // 2 = recall memory
        if (recallMode == 2 && !string.IsNullOrEmpty(_modeTitleMemory))
        {
            // Recall (used to recall the mode label after using compound/option key)
            title = _modeTitleMemory;
            note = _modeNoteMemory;
        }
        else if (recallMode == 1)
        {
            // Save for recall
            _modeTitleMemory = title;
            _modeNoteMemory = note;
        }

        ModeLabelSaveCursor();
        Tools.CursorTopLeft();
        ConsoleColors.SetColors(attention
            ? ConsoleColors.ColorPreset.AttentionInverted
            : ConsoleColors.ColorPreset.HighlightInverted);
        Console.WriteLine(" " + title.ToUpper() + " ");
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        if (!string.IsNullOrEmpty(note))
        {
            Console.WriteLine(" " + note);
        }

        ModeLabelLoadCursor();
    }

    #endregion

    private static string _tempTitleLabel = string.Empty;

    public static void WriteTitleLabel()
    {
        if (Entries.CurrentEntryExists() && Entries.CurrentEntry.Title.Length > 0)
        {
            _tempTitleLabel = TextMetrics.Printable(
                Tools.TruncateString(Entries.CurrentEntry.Title, Tools.GetTitlePreviewWidth(), true));
            Console.Write(" " + DialogText.TitleColon + " ");
            Console.Write(_tempTitleLabel);
        }
    }

    /// <summary>
    /// The top row of the editor: the label EDITOR at the left, the name of the program in the middle, and
    /// ":help" at the right
    /// </summary>
    internal static void WriteEditorTop()
    {
        WriteEditorTop(DialogText.CaptionsEditor);
    }

    // The top line of the editor, with another caption (a comparison)
    internal static void WriteEditorTop(string caption)
    {
        WriteModeLabel(caption, string.Empty, 0);
        WriteNameInTopBar(false);
        var key = " :help ";
        var text = " " + Lang.T("Help") + " ";
        Tools.SetCursorEx(0, Math.Max(0, Init.LineWidth - 1 - TextMetrics.WidthOf(key + text)));
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.KeyHintInverted);
        Console.Write(key);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.KeyHint);
        Console.Write(text);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    internal static void WriteNameInTopBar(bool displayDataFileName)
    {
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.HeaderColor);
        WriteCenterText(DialogText.ApplicationName);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        if (displayDataFileName)
        {
            WriteDataFileName();
        }
    }

    internal static void WriteDataFileName()
    {
        Tools.SaveCursorPosition(0);
        Tools.CursorTopLeft();

        Tools.CursorRight(Init.LineWidth - 26);
        Console.Write(new string(' ', 26));
        Tools.CursorReturn();


        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        if (FileIo.GetFileNameWithoutExtension().Length > 0)
        {
            Tools.CursorRight(Init.LineWidth -
                              (Tools.TruncateString(FileIo.GetFileNameWithoutExtension(), 24, true).Length + 2));
            Console.Write("[" + Tools.TruncateString(FileIo.GetFileNameWithoutExtension(), 24, true) + "]");
        }
        else
        {
            Tools.CursorRight(Init.LineWidth -
                              (DialogText.NotAvailable.Length + 2));
            Console.Write("[" + DialogText.NotAvailable + "]");
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);

        Tools.LoadCursorPosition(0);
    }

    private static string _tempCurrentSaveIndicator = string.Empty;

    /// <summary>
    /// Write a line of keys and what they do ("j/k Select | Esc Close"), with the keys highlighted
    /// </summary>
    internal static void WriteKeyHints(string hints)
    {
        Console.Write(" ");
        // The hints that fit on the line are shown (a longer text in another language can need more room)
        var room = Init.LineWidth - 1 - Console.CursorLeft;
        foreach (var hint in hints.Split(" | "))
        {
            var space = hint.IndexOf(' ');
            var key = " " + (space < 0 ? hint : hint[..space]) + " ";
            // One blank space on each side of the description
            var description = space < 0 ? " " : " " + hint[(space + 1)..] + " ";
            room -= TextMetrics.WidthOf(key + description);
            if (room < 0) break;
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.KeyHintInverted);
            Console.Write(key);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.KeyHint);
            Console.Write(description);
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    internal static void InitializeSaveIndicator()
    {
        _tempCurrentSaveIndicator = "";
    }

    internal static void IndicateChanges(bool forceDisplay)
    {
        if (DocumentInfo.HasChanged && (_tempCurrentSaveIndicator == "" || forceDisplay))
        {
            Tools.CursorTopLeft();
            Tools.CursorDown(2);
            Tools.CursorRight(Init.LineWidth - (DialogText.SavIndicator.Length));
            _tempCurrentSaveIndicator = DialogText.SavIndicator;
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.UnsavedInverted);
            Console.Write(_tempCurrentSaveIndicator);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        }
        else if (!DocumentInfo.HasChanged && _tempCurrentSaveIndicator == DialogText.SavIndicator)
        {
            _tempCurrentSaveIndicator = "";
        }
    }

    internal static void ClearChangeIndicator()
    {
        Tools.CursorTopLeft();
        Tools.CursorDown(2);
        Tools.CursorRight(Init.LineWidth - (DialogText.SavIndicator.Length));
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Console.Write(new string(' ', DialogText.SavIndicator.Length));
    }

    private static void WriteCenterText(string label)
    {
        Tools.SaveCursorPosition(0);
        Tools.CursorTopLeft();
        if (label.Length > 0)
        {
            Tools.CursorReturn();
            Tools.CursorRight(Init.LineWidth / 2);
            Tools.CursorLeft((label.Length / 2) + 1);
            Console.Write(" " + label + " ");
        }

        Tools.LoadCursorPosition(0);
    }
}