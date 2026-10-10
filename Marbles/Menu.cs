namespace Marbles;

internal static class Menu
{
    private static void ResetTopMenuDisplayIndex()
    {
        _manageModePage = 0;
        _viewModePage = 0;
    }

    public static int GetTopMenuManagePage()
    {
        return _manageModePage;
    }

    private const int MaxManageModePages = 1;

    /// <summary>
    /// The number of pages of the main menu
    /// </summary>
    internal static int ManagePageCount => MaxManageModePages + 1;

    /// <summary>
    /// The page of the main menu a command is shown on
    /// </summary>
    internal static int PageOfCommand(string command)
    {
        return command is "undelete" or "import" or "transfer" or "backup" or "password" ? 0 : 1;
    }

    public static void NextTopMenuManagePage()
    {
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
            _manageModePage = index;
        }
    }

    public static void SetTopMenuViewPage(int index)
    {
        if (index is >= 0 and <= 2)
        {
            _viewModePage = index;
        }
    }


    public static void NextTopMenuViewPage()
    {
        if (_viewModePage < 2)
        {
            _viewModePage += 1;
        }
        else
        {
            _viewModePage = 0;
        }
    }

    public static void PreviousTopMenuViewPage()
    {
        if (_viewModePage > 0)
        {
            _viewModePage -= 1;
        }
        else
        {
            _viewModePage = 2;
        }
    }


    private static int _manageModePage;
    private static int _viewModePage;
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
        else if (Mode.CurrentMode == Mode.SelectedMode.View)
        {
            if (_viewModePage == 0)
            {
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                WriteMenuExtension(GetMenuNavigation("1/3"));
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                WriteMenuItem("Enter I", Lang.T("Edit"), true);
                WriteMenuItem("T", Lang.T("Title"), true);
                WriteExtendedMenuItem("F", GetFavoriteState(), Lang.T("Favorite"), ConsoleColors.ColorPreset.Favorites,
                    true, true, Entries.CurrentEntry);
                WriteMenuItem("D", Lang.T("Duplicate"), true);
                WriteMenuItem("S /", Lang.T("Content search"), true);
            }
            else if (_viewModePage == 1)
            {
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                WriteMenuExtension(GetMenuNavigation("2/3"));
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                WriteMenuItem(":delete", Lang.T("Delete"), true);
                WriteMenuItem(":revert", Lang.T("Revert"), true);
                WriteMenuItem(":transfer", Lang.T("Transfer") + " " + Entries.GetTransferState(), true);
                WriteMenuItem(":import", Lang.T("Import"), true);
                WriteMenuItem(":export", Lang.T("Export"), true);
            }
            else if (_viewModePage == 2)
            {
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                WriteMenuExtension(GetMenuNavigation("3/3"));
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                WriteMenuItem("N", Lang.T("New"), true);
                WriteMenuItem("?", Lang.T("Help"), true);
                WriteMenuItem("X", Lang.T("Logout"), true);
                WriteMenuItem("Esc :q", Lang.T("Return"), true);
                WriteMenuItem("", "", true);
            }
        }
        else if (Mode.CurrentMode == Mode.SelectedMode.Edit)
        {
            WriteMenuItem(":help", Lang.T("Help"), true);
        }
        else if (Mode.CurrentMode == Mode.SelectedMode.Search)
        {
            if (Mode.CurrentRecallMode != Mode.RecallMode.Favorites)
            {
                WriteMenuItem("j k", Lang.T("Select"), true);
            }
            WriteMenuItem("1-9+0 :12", Lang.T("Select by number"), true);
            WriteMenuItem("P", Lang.T("Toggle favorite/pin"), true);
            if (Mode.CurrentRecallMode == Mode.RecallMode.Favorites)
            {
                WriteMenuItem(":pin", Lang.T("Pinned color"), true);
                WriteMenuItem("Tab V", Lang.T("Move pinned (j/k, Esc)"), true);
            }
            else
            {
                WriteMenuItem(":title", Lang.T("Change title (:delete Delete)"), true);
            }
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


    private static ConsoleKeyInfo _c;
    private static bool _waiting;

    public static bool YesNoPrompt(string caption, string question, string note, bool invertColors)
    {
        // Hide cursor, set label and name in top bar
        Tools.HideCursor();
        Tools.ClearScreen();
        WriteModeLabel(caption, string.Empty, 0);
        WriteNameInTopBar(true);
        Tools.CursorDown(1);
        if (!string.IsNullOrEmpty(note))
        {
            Console.WriteLine(" " + note);
        }

        Tools.CursorDown(1);
        Console.Write(" ");
        ConsoleColors.SetColors(invertColors
            ? ConsoleColors.ColorPreset.WarningInverted
            : ConsoleColors.ColorPreset.AttentionInverted);
        Console.WriteLine(" " + question + " ");
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        // Keep asking until Y or N is pressed
        _waiting = true;
        do
        {
            _c = Console.ReadKey(true);
            switch (_c.Key)
            {
                case ConsoleKey.Y:
                    _waiting = false;
                    return true;
                case ConsoleKey.N:
                    _waiting = false;
                    return false;
            }
        } while (_waiting);

        return false;
    }

    // Returns:
    // 1 = Yes
    // 2 = Option
    // 3 = No
    public static byte YesOptionNoPrompt(string caption, string question, string note, bool invertColors,
        ConsoleKey optionKey)
    {
        // Hide cursor, set label and name in top bar
        Tools.ClearScreen();
        WriteModeLabel(caption, string.Empty, 0);
        WriteNameInTopBar(true);
        Tools.CursorDown(1);
        if (!string.IsNullOrEmpty(note))
        {
            Console.WriteLine(" " + note);
        }

        Tools.CursorDown(1);
        Console.Write(" ");
        ConsoleColors.SetColors(invertColors
            ? ConsoleColors.ColorPreset.WarningInverted
            : ConsoleColors.ColorPreset.AttentionInverted);
        Console.WriteLine(" " + question + " ");
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        // Keep asking until Y, option or N is pressed
        _waiting = true;
        do
        {
            _c = Console.ReadKey(true);
            if (_c.Key == ConsoleKey.Y)
            {
                _waiting = false;
                return 1;
            }
            else if (_c.Key == optionKey)
            {
                _waiting = false;
                return 2;
            }
            else if (_c.Key == ConsoleKey.N)
            {
                _waiting = false;
                return 3;
            }
        } while (_waiting);

        return 0;
    }

    // Returns:
    // 1 = Yes
    // 2 = No
    // 3 = Cancel
    public static byte YesNoCancelPrompt(string caption, string question, string question2, int question2margin, string note, bool invertColors)
    {
        // Hide cursor, set label and name in top bar
        Tools.ClearScreen();
        WriteModeLabel(caption, string.Empty, 0);
        WriteNameInTopBar(true);
        Tools.CursorDown(1);
        if (!string.IsNullOrEmpty(note))
        {
            Console.WriteLine(" " + note);
        }

        Tools.CursorDown(1);
        Console.Write(" ");
        ConsoleColors.SetColors(invertColors
            ? ConsoleColors.ColorPreset.WarningInverted
            : ConsoleColors.ColorPreset.AttentionInverted);
        Console.WriteLine(" " + question + " ");
        if (!string.IsNullOrEmpty(question2))
        {
            Tools.CursorDown(1);
            // Insert margin
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Console.Write(new string(' ', question2margin));
            // Write question2
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
            Console.Write(" " + question2 + " ");
        }
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        // Keep asking until Y or N is pressed
        _waiting = true;
        do
        {
            _c = Console.ReadKey(true);
            switch (_c.Key)
            {
                case ConsoleKey.Y:
                    _waiting = false;
                    return 1;
                case ConsoleKey.N:
                    _waiting = false;
                    return 2;
                case ConsoleKey.Escape:
                    _waiting = false;
                    return 3;
                case ConsoleKey.E:
                    _waiting = false;
                    return 4;
            }
        } while (_waiting);

        return 0;
    }


    public static byte SelectionPrompt(string caption, string questionA, string questionB, string questionC,
        string note, bool invertColors, ConsoleKey a, ConsoleKey b, ConsoleKey c)
    {
        // Hide cursor, set label and name in top bar
        Tools.ClearScreen();
        WriteModeLabel(caption, string.Empty, 0);
        WriteNameInTopBar(true);
        Tools.CursorDown(1);
        if (!string.IsNullOrEmpty(note))
        {
            Console.WriteLine(" " + note);
        }

        Tools.CursorDown(1);
        if (invertColors)
        {
            Console.Write(" ");
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
        }

        Console.WriteLine(" " + questionA + " ");
        if (invertColors)
        {
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        }

        Tools.CursorDown(1);
        if (invertColors)
        {
            Console.Write(" ");
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
        }

        Console.WriteLine(" " + questionB + " ");
        if (invertColors)
        {
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        }

        Tools.CursorDown(1);
        if (invertColors)
        {
            Console.Write(" ");
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
        }

        Console.WriteLine(" " + questionC + " ");
        if (invertColors)
        {
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        }

        // Keep asking until keys are pressed
        _waiting = true;
        do
        {
            var selection = Console.ReadKey(true).Key;
            if (selection == a || selection == ConsoleKey.Enter)
            {
                _waiting = false;
                return 0;
            }

            if (selection == b)
            {
                _waiting = false;
                return 1;
            }

            if (selection == c)
            {
                _waiting = false;
                return 2;
            }

            if (selection == ConsoleKey.Escape)
            {
                _waiting = false;
                return 3;
            }
        } while (_waiting);

        return 0;
    }


    // Primary menu item method
    public static bool DimMenuItem { get; set; }

    #region The main menu of a data file

    // An item of the main menu: its key (or command) and what it does
    private readonly record struct MenuItem(string Key, string Text);

    private const int MenuBoxTop = 2; // The row of the top of the boxes
    private const int MenuKeyWidth = 11; // The room for the key of an item (the dots fill up to it)

    // The main menu of a data file: two pages, each with two boxes of items
    private static void DrawMenuPage(int page)
    {
        WriteModeLabel(DialogText.CaptionsMenu, string.Empty, 0);
        WriteNameInTopBar(true);
        Tools.SetCursorEx(1, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(" " + GetMenuNavigation((page + 1) + "/" + ManagePageCount));
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        static string Check(bool on) => on ? DialogText.CheckedSetting : DialogText.UnCheckedSetting;
        int rows;
        if (page == 0)
        {
            rows = DrawMenuBoxes(Lang.T("Notes"), new MenuItem[]
            {
                new("N", Lang.T("New")),
                new("S /", Lang.T("Search")),
                new("R", Lang.T("Reselect")),
                new("F", Lang.T("Favorites") + " " + Entries.GetFavoritesCount()),
                new("T", Lang.T("Top 20")),
                new("M", Lang.T("Session history") + " " + Entries.GetSessionCountAsString()),
                new("A", Lang.T("List all") + " " + Entries.GetEntriesCountAsString()),
                new(":undelete", Lang.T("Undo deletion"))
            }, Lang.T("Data file"), new MenuItem[]
            {
                new("I", Lang.T("File info")),
                new(":import", Lang.T("Import text file")),
                new(":transfer", Lang.T("Transfer") + " " + Entries.GetTransferState()),
                new(":backup", FileIo.GetBackupStatus() + " " + Lang.T("Backup")),
                new(":password", Lang.T("Reset password")),
                new(":", Lang.T("Command line")),
                new("?", Lang.T("Help")),
                new("X", Lang.T("Logout"))
            });
            // The vignette is shown below the boxes, where there is room for it
            Vignette.DisplayVignette(MenuBoxTop + rows + 3);
        }
        else
        {
            DrawMenuBoxes(DialogText.CaptionsSettings, new MenuItem[]
            {
                new(":scheme", Lang.T("Edit color scheme")),
                new(":color 1-" + Actions.ColorPresetCount,
                    "[" + (ConsoleColors.GetColorPresetIndex() + 1) + "] " + Lang.T("Color preset")),
                new(":bw", Check(ConsoleColors.Monochrome) + " " + Lang.T("Black and white")),
                new(":language", "[" + Lang.Current + "] " + Lang.T("Language")),
                new(":number", Check(TextArea.LineNumbers) + " " + Lang.T("Line numbers")),
                new(":size", Lang.T("Size") + " " + Init.GetOutputDimensions()),
                new(":size auto", Check(Init.AutoSize) + " " + Lang.T("Follow the window")),
                new(":vignette", Lang.T("Text vignette"))
            }, DialogText.ApplicationName, new MenuItem[]
            {
                new(":browse", Lang.T("Browse files")),
                new("D", Lang.T("Display timestamp")),
                new(":guide", DialogText.QuickGuide),
                new(":about", Lang.T("About")),
                new(":defaults", Lang.T("Default settings")),
                new(":restart", Lang.T("Restart Marbles")),
                new(":logout", Lang.T("Logout")),
                new(":q", Lang.T("Exit"))
            });
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Tools.SetCursorEx(Init.LineCount - 1, 0);
    }

    // Two boxes side by side, each with a title and its items
    // <returns>The number of rows of items</returns>
    private static int DrawMenuBoxes(string leftTitle, MenuItem[] left, string rightTitle, MenuItem[] right)
    {
        var width = (Init.LineWidth - 1) / 2;
        var rows = Math.Max(left.Length, right.Length);
        DrawMenuBox(0, width, rows, leftTitle, left);
        DrawMenuBox(width + 1, width, rows, rightTitle, right);
        return rows;
    }

    private static void DrawMenuBox(int left, int width, int rows, string title, MenuItem[] items)
    {
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Tools.SetCursorEx(MenuBoxTop, left);
        Console.Write(AsciiStyles.GetTopCorner() + new string('-', width - 2) + AsciiStyles.GetTopCorner());
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.HeaderColor);
        Tools.SetCursorEx(MenuBoxTop, left + 2);
        Console.Write(Tools.FitLine(" " + title + " ", Init.LineWidth - (width - 4)));
        for (var row = 0; row < rows; row++)
        {
            var y = MenuBoxTop + 1 + row;
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Tools.SetCursorEx(y, left);
            Console.Write('|');
            Tools.SetCursorEx(y, left + width - 1);
            Console.Write('|');
            if (row >= items.Length) continue;
            var item = items[row];
            Tools.SetCursorEx(y, left + 2);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Console.Write(item.Key);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write(" " + new string('.', Math.Max(1, MenuKeyWidth - item.Key.Length)) + " ");
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            // A long text (in another language) is cut at the side of the box
            var room = left + width - 2 - Console.CursorLeft;
            Console.Write(Tools.FitLine(item.Text, Init.LineWidth - 1 - room));
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Tools.SetCursorEx(MenuBoxTop + rows + 1, left);
        Console.Write(AsciiStyles.GetBottomCorner() + new string('-', width - 2) + AsciiStyles.GetBottomCorner());
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
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

        Tools.CursorRight(left);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        // Write top line
        Console.Write(AsciiStyles.GetTopCorner() + new string('-', menuWidth) + AsciiStyles.GetTopCorner());
        // Write header label
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.HeaderColor);
        Tools.CursorReturn();
        Tools.CursorRight(left + 2);
        Console.Write(" " + caption + " ");


        // Iterate and draw menu and menu items
        foreach (var t in shortcuts)
        {
            Tools.CursorReturn();
            Tools.CursorDown();
            Tools.CursorRight(left);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
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
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
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
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
            Console.Write("->");
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

    public static void WriteModeLabel(string title, string note, int recallMode)
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
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
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
            _tempTitleLabel = Tools.TruncateString(Entries.CurrentEntry.Title, Tools.GetTitlePreviewWidth(), true);
            Console.Write(" " + DialogText.TitleColon + " ");
            Console.Write(_tempTitleLabel);
        }
    }

    public static void WriteDocumentScrollLabel()
    {
        Console.SetCursorPosition((Init.LineWidth / 2) - (DialogText.DocumentScroll.Length / 2), Init.LineCount - 1);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.PreviewTitle);
        Console.Write(DialogText.DocumentScroll);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    internal static void WriteNameInTopBar(bool displayDataFileName)
    {
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.HeaderColor);
        WriteCenterText("-- " + DialogText.ApplicationName + " --");
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
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.NormalInverted);
            Console.Write(key);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
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
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.NormalInverted);
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