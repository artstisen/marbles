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

    private const int MaxManageModePages = 3;

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


    private static void WriteLogoForMenuDisplay()
    {
        Logo.DrawLogo(false);
        Tools.CursorTopLeftDown(6);
    }

    private static int _manageModePage;
    private static int _viewModePage;
    internal static bool BypassAwaitAction { get; set; }
    internal static bool DisplayWelcomeMessage { get; set; }

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
            Tools.ClearScreen(); // Clear screen
        }

        if (DisplayWelcomeMessage)
        {
            DisplayWelcomeMessage = false;
            DisplayFooterMessage(DialogText.WelcomeTo);
        }

        if (Mode.CurrentMode == Mode.SelectedMode.Login)
        {
            Tools.LoginRoutine(true);
        }
        else if (Mode.CurrentMode == Mode.SelectedMode.Manage)
        {
            if (_manageModePage == 0)
            {
                WriteLogoForMenuDisplay();
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                WriteMenuExtension(GetMenuNavigation((_manageModePage + 1) + "/" + (MaxManageModePages + 1)));
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                WriteMenuItem("N", "New", true);
                WriteMenuItem("S", "Search", true);
                WriteMenuItem("R", "Reselect", true);
                WriteMenuItem("F", "Favorites " + Entries.GetFavoritesCount(), true);
                WriteMenuItem("X", "Logout", true);
                Vignette.DisplayVignette();
            }
            else if (_manageModePage == 1)
            {
                WriteLogoForMenuDisplay();
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                WriteMenuExtension(GetMenuNavigation((_manageModePage + 1) + "/" + (MaxManageModePages + 1)));
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                WriteMenuItem("T", "Top 20", true);
                WriteMenuItem("M", "Session history " + Entries.GetSessionCountAsString(), true);
                WriteMenuItem("L", "List all " + Entries.GetEntriesCountAsString(), true);
                WriteMenuItem("I", "File info", true);
                WriteMenuItem("D", "Display timestamp", true);
                WriteMenuItem("H", "Help", true);
                WriteMenuItem("A", "About", true);
            }
            else if (_manageModePage == 2)
            {
                WriteLogoForMenuDisplay();
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                WriteMenuExtension(GetMenuNavigation((_manageModePage + 1) + "/" + (MaxManageModePages + 1)));
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                WriteMenuItem(Keyboard.CmpTrig() + "Z", "Undo deletion", true);
                WriteMenuItem(Keyboard.CmpTrig() + "J", "Transfer " + Entries.GetTransferState(), true);
                WriteMenuItem(Keyboard.CmpTrig() + "I", "Import .txt", true);
                WriteMenuItem(Keyboard.CmpTrig() + "V", "Text vignette", true);
                WriteMenuItem("", "-", true);
                WriteMenuItem(Keyboard.CmpTrig() + "M", "Change dimensions " + Init.GetOutputDimensions(), true);
                WriteMenuItem(Keyboard.CmpTrig() + "P",
                    "[" + (ConsoleColors.GetColorPresetIndex() + 1) + "] Color preset", true);
                WriteMenuItem(Keyboard.CmpTrig() + "L", "[" + (Logo.GetLogoIndex() + 1) + "] Logo preset", true);
                WriteMenuItem(Keyboard.CmpTrig() + "K", FileIo.GetBackupStatus() + " Toggle Backup", true);
                WriteMenuItem(Keyboard.CmpTrig() + Keyboard.GetTriggerAsString(),
                    "[" + Keyboard.GetTriggerAsString() + "] Set shortcuts key", true);
                WriteMenuItem(Keyboard.CmpTrig() + "E", "Reset shortcuts key", true);
                WriteMenuItem(Keyboard.CmpTrig() + "R", "Reset password", true);
                WriteMenuItem(Keyboard.CmpTrig() + "X", "Default settings", true);
                WriteMenuItem(Keyboard.CmpTrig() + "Y", "Restart Marbles", true);
                WriteMenuItem(Keyboard.CmpTrig() + "Q", "Exit", true);
            }
            else if (_manageModePage == 3)
            {
                WriteLogoForMenuDisplay();
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                WriteMenuExtension(GetMenuNavigation((_manageModePage + 1) + "/" + (MaxManageModePages + 1)));
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                //Console.WriteLine(" Change color scheme");
                WriteMenuItem(Keyboard.CmpTrig() + "G", "Unlock color scheme ", false);
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
                Console.Write("[LOCKED]\n");
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                //Console.WriteLine(" (Press Up/Down to select, Left/Right to change)");
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                WriteExtendedMenuItem(new string(' ', 2) + "<- ->", DialogText.ExtExample, "Text",
                    ConsoleColors.ColorPreset.Normal, true, false);
                //WriteExtendedMenuItem("  <- ->", DialogText.ExtExample, "Text", ConsoleColors.ColorPreset.Normal, true);
                WriteExtendedMenuItem(new string(' ', 2) + "<- ->", DialogText.ExtExample,
                    "Logo, login label, directory browser, editor repeat",
                    ConsoleColors.ColorPreset.HeaderColor, true, false);
                WriteExtendedMenuItem(new string(' ', 2) + "<- ->", DialogText.ExtExample, "Dividers and captions",
                    ConsoleColors.ColorPreset.Divider, true, false);
                WriteExtendedMenuItem(new string(' ', 2) + "<- ->", DialogText.ExtExample, "Favorites",
                    ConsoleColors.ColorPreset.Favorites, true, false);
                WriteExtendedMenuItem(new string(' ', 2) + "<- ->", DialogText.ExtExample,
                    "Preview title and save state indicator", ConsoleColors.ColorPreset.PreviewTitle, true, false);
                WriteExtendedMenuItem(new string(' ', 2) + "<- ->", DialogText.ExtExample,
                    "Editor title and scroll indicator", ConsoleColors.ColorPreset.EditorTitle, true, false);
                WriteExtendedMenuItem(new string(' ', 2) + "<- ->", DialogText.ExtExample,
                    "Highlight and selection indicators", ConsoleColors.ColorPreset.HighlightInverted, true, false);
                WriteExtendedMenuItem(new string(' ', 2) + "<- ->", DialogText.ExtExample, "Attention",
                    ConsoleColors.ColorPreset.AttentionInverted, true, false);
                WriteExtendedMenuItem(new string(' ', 2) + "<- ->", DialogText.ExtExample, "Warning",
                    ConsoleColors.ColorPreset.WarningInverted, true, false);
                WriteMenuItem(new string(' ', 2) + "T", "Toggle Text/Background", true);
                //WriteMenuItem(new string(' ', 2) + "P", "Cycle color presets", true);
                WriteMenuItem(new string(' ', 2) + "E", "Reset", true);
                WriteMenuItem(new string(' ', 2) + "Enter", "Save and exit", true);
                WriteMenuItem(new string(' ', 2) + "Esc", "Cancel (exit)", true);
            }


            if (BypassAwaitAction)
            {
                BypassAwaitAction = false;
                return;
            }

            Actions.AwaitAction(false, _c);
        }
        else if (Mode.CurrentMode == Mode.SelectedMode.View)
        {
            if (_viewModePage == 0)
            {
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                WriteMenuExtension(GetMenuNavigation("1/3"));
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                WriteMenuItem("Enter", "Edit", true);
                WriteMenuItem("T", "Title", true);
                WriteExtendedMenuItem("F", GetFavoriteState(), "Favorite",
                    Actions.GetHighlightPresetFromCurrentType(Entries.CurrentEntry,
                        ConsoleColors.ColorPreset.Favorites),
                    true, true);
                //WriteMenuItem("F", "Favorite" + GetFavoriteState(), true);
                WriteMenuItem("D", "Duplicate", true);
                WriteMenuItem("S", "Content search", true);
            }
            else if (_viewModePage == 1)
            {
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                WriteMenuExtension(GetMenuNavigation("2/3"));
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                WriteMenuItem(Keyboard.CmpTrig() + "D", "Delete", true);
                WriteMenuItem(Keyboard.CmpTrig() + "R", "Revert", true);
                WriteMenuItem(Keyboard.CmpTrig() + "J", "Transfer " + Entries.GetTransferState(), true);
                WriteMenuItem(Keyboard.CmpTrig() + "G", "Import", true);
                WriteMenuItem(Keyboard.CmpTrig() + "E", "Export", true);
            }
            else if (_viewModePage == 2)
            {
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                WriteMenuExtension(GetMenuNavigation("3/3"));
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                WriteMenuItem("N", "New", true);
                WriteMenuItem("H", "Help", true);
                WriteMenuItem("X", "Logout", true);
                WriteMenuItem("Esc", "Return", true);
                WriteMenuItem("", "", true);
            }
        }
        else if (Mode.CurrentMode == Mode.SelectedMode.Edit)
        {
            WriteMenuItem(":help", "Help", true);
        }
        else if (Mode.CurrentMode == Mode.SelectedMode.Search)
        {
            if (Mode.CurrentRecallMode != Mode.RecallMode.Favorites)
            {
                WriteMenuItem("Up/Down", "Select", true);
            }
            WriteMenuItem("G or 1-9+0", "Select by number", true);
            WriteMenuItem(DialogText.CtrlPlus + Keyboard.GetTriggerAsString() + ", F", "Toggle favorite/pin", true);
            if (Mode.CurrentRecallMode == Mode.RecallMode.Favorites)
            {
                WriteMenuItem(DialogText.CtrlPlus + Keyboard.GetTriggerAsString() + ", 1-3", "Pinned color", true);
                WriteMenuItem("Tab", "Arrange pinned", true);
            }
            else
            {
                WriteMenuItem("Esc", "Return", true); 
            }
        }
        else
        {
            DrawTopMenu(true, true, false);
        }
    }


    private static string GetMenuNavigation(string pages)
    {
        return "<- " + pages + " ->";
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

    // Secondary menu item used for inserting items at odd locations
    // public static void WriteMenuItem(string key, string description, int leftMargin)
    // {
    //     Tools.CursorReturn();
    //     if (leftMargin > 0)
    //     {
    //         Tools.CursorRight(leftMargin);
    //     }
    //     Console.Write(" " + key);
    //     if (description.Length > 0)
    //     {
    //         ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
    //         Console.Write(" " + new string('.', 13 - key.Length));
    //         ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    //         Console.Write(" " + description);
    //     }
    // }

    private static void WriteExtendedMenuItem(string key, string example, string description,
        ConsoleColors.ColorPreset preset, bool newLine, bool flipExample)
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
                ConsoleColors.SetColors(preset);
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
        //Console.CursorTop = tempCursorMemoryTop;
        //Console.CursorLeft = tempCursorMemoryLeft;
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
        //Console.CursorTop = ;
        //Console.CursorLeft = ;
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
        WriteCenterText("*** " + DialogText.ApplicationName + " ***");
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
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.PreviewTitle);
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

    internal static void WriteStringMemoryAction(string text1, string text2)
    {
        Tools.SaveCursorPosition(8);
        Tools.CursorTopLeft();
        Tools.CursorDown(3);
        Tools.CursorRight(Init.LineWidth - (text1.Length + 5));
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(text1);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Tools.LoadCursorPosition(8);

        if (!string.IsNullOrEmpty(text2))
        {
            Tools.SaveCursorPosition(8);
            Tools.CursorTopLeft();
            Tools.CursorDown(4);
            Tools.CursorRight(Init.LineWidth - (text2.Length + 2));
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
            Console.Write(text2);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Tools.LoadCursorPosition(8);
        }
    }

    internal static void WriteRepeatCommand(string text1, string text2)
    {
        Tools.SaveCursorPosition(8);
        Tools.CursorTopLeft();
        Tools.CursorDown(3);
        Tools.CursorRight(5);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Console.Write(text1);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.HeaderColor);
        Console.Write(text2);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Tools.LoadCursorPosition(8);
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

    internal static void WriteBottomText(string label, bool inverted)
    {
        Tools.SaveCursorPosition(0);
        Tools.CursorTopLeft();
        if (label.Length > 0)
        {
            Tools.CursorReturn();

            Tools.CursorDown(Init.LineCount - 1);
            Tools.CursorRight();
            if (inverted)
            {
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
            }

            Console.Write(label);
            if (inverted)
            {
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            }
        }

        Tools.LoadCursorPosition(0);
    }

    /// <summary>
    /// Creates a 1-line top prompt for text input (search, numeric input etc.)
    /// </summary>
    /// <param name="caption"></param>
    /// <param name="additionalText"></param>
    /// <param name="width"></param>
    /// <param name="colorPreset"></param>
    internal static void WriteInputPrompt(string caption, string additionalText, int width,
        ConsoleColors.ColorPreset colorPreset)
    {
        Tools.CursorTopLeft();
        ConsoleColors.SetColors(colorPreset);
        Tools.CursorRight(1);
        Console.Write(AsciiStyles.GetTopCorner() + new string('-', width) + AsciiStyles.GetTopCorner());
        Tools.CursorReturn();
        Tools.CursorRight(3);
        Console.Write(" " + caption + " ");
        Tools.CursorReturn();
        Tools.CursorDown();
        Tools.CursorRight(1);
        Console.Write("|" + new string(' ', width) + "|");
        Tools.CursorReturn();
        Tools.CursorDown();
        Tools.CursorRight(1);
        Console.Write(AsciiStyles.GetBottomCorner() + new string('-', width) + AsciiStyles.GetBottomCorner());
        Tools.CursorReturn();
        Tools.CursorRight((width - 1) - additionalText.Length);
        Console.Write(" " + additionalText + " ");

        // Place cursor
        Tools.CursorTopLeft();
        Tools.CursorDown();
        Tools.CursorRight(3);
    }

    private static void DisplayFooterMessage(string msg)
    {
        if (Tools.IsValid(msg))
        {
            // Truncate long strings
            msg = Tools.TruncateString(msg, 70, true);
            Tools.CursorTopLeft();
            // Place cursor in bottom right corner
            Tools.CursorDown(Init.LineCount -1);
            Tools.CursorRight(Init.LineWidth - 1);
            // Move cursor left in the length of the message
            Tools.CursorLeft(msg.Length);
            // Write message
            Console.Write(msg);
        }
    }
}