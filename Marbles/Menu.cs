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


    /// <summary>
    /// The row of the page navigation of the main menu. The menu items follow below it
    /// </summary>
    internal const int MenuTop = 2;

    // The top of the main menu: the name of the program and of the data file (the logo is for the login page)
    private static void WriteLogoForMenuDisplay()
    {
        WriteNameInTopBar(true);
        Tools.CursorTopLeftDown(MenuTop);
    }

    /// <summary>
    /// The column of the "[LOCKED]" label of the color scheme on the fourth page of the menu
    /// </summary>
    internal static int SchemeLabelColumn { get; private set; }

    /// <summary>
    /// The label of the color scheme: locked or being edited (as wide as the longer of the two)
    /// </summary>
    internal static string SchemeLabel(bool editing)
    {
        var width = Math.Max(DialogText.SchemeLocked.Length, DialogText.SchemeEditing.Length);
        return (editing ? DialogText.SchemeEditing : DialogText.SchemeLocked).PadRight(width);
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
            if (_manageModePage == 0)
            {
                WriteLogoForMenuDisplay();
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                WriteMenuExtension(GetMenuNavigation((_manageModePage + 1) + "/" + (MaxManageModePages + 1)));
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                WriteMenuItem("N", Lang.T("New"), true);
                WriteMenuItem("S /", Lang.T("Search"), true);
                WriteMenuItem("R", Lang.T("Reselect"), true);
                WriteMenuItem("F", Lang.T("Favorites") + " " + Entries.GetFavoritesCount(), true);
                WriteMenuItem("P",
                    (Actions.PreviewEnabled ? DialogText.CheckedSetting : DialogText.UnCheckedSetting) + " " + Lang.T("Preview"),
                    true);
                WriteMenuItem(":", Lang.T("Command line"), true);
                WriteMenuItem(":guide", DialogText.QuickGuide, true);
                WriteMenuItem("X", Lang.T("Logout"), true);
                Actions.DrawPreview();
                Vignette.DisplayVignette();
            }
            else if (_manageModePage == 1)
            {
                WriteLogoForMenuDisplay();
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                WriteMenuExtension(GetMenuNavigation((_manageModePage + 1) + "/" + (MaxManageModePages + 1)));
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                WriteMenuItem("T", Lang.T("Top 20"), true);
                WriteMenuItem("M", Lang.T("Session history") + " " + Entries.GetSessionCountAsString(), true);
                WriteMenuItem("A", Lang.T("List all") + " " + Entries.GetEntriesCountAsString(), true);
                WriteMenuItem("I", Lang.T("File info"), true);
                WriteMenuItem(":browse", Lang.T("Browse files"), true);
                WriteMenuItem(":number",
                    (TextArea.LineNumbers ? DialogText.CheckedSetting : DialogText.UnCheckedSetting) +
                    " " + Lang.T("Line numbers in the editor"), true);
                WriteMenuItem("D", Lang.T("Display timestamp"), true);
                WriteMenuItem("?", Lang.T("Help"), true);
                WriteMenuItem(":about", Lang.T("About"), true);
            }
            else if (_manageModePage == 2)
            {
                WriteLogoForMenuDisplay();
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                WriteMenuExtension(GetMenuNavigation((_manageModePage + 1) + "/" + (MaxManageModePages + 1)));
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                WriteMenuItem(":undelete", Lang.T("Undo deletion"), true);
                WriteMenuItem(":transfer", Lang.T("Transfer") + " " + Entries.GetTransferState(), true);
                WriteMenuItem(":import", Lang.T("Import text file"), true);
                WriteMenuItem(":vignette", Lang.T("Text vignette"), true);
                WriteMenuItem("", "-", true);
                WriteMenuItem(":size", Lang.T("Change dimensions") + " " + Init.GetOutputDimensions(), true);
                WriteMenuItem(":size auto",
                    (Init.AutoSize ? DialogText.CheckedSetting : DialogText.UnCheckedSetting) +
                    " " + Lang.T("Follow the size of the window"), true);
                WriteMenuItem(":color 1-" + Actions.ColorPresetCount,
                    "[" + (ConsoleColors.GetColorPresetIndex() + 1) + "] " + Lang.T("Color preset (no number: next)"), true);
                WriteMenuItem(":logo 1-" + Actions.LogoPresetCount,
                    "[" + (Logo.GetLogoIndex() + 1) + "] " + Lang.T("Logo preset (no number: next)"), true);
                WriteMenuItem(":language", "[" + Lang.Current + "] " + Lang.T("Language: da, de ... (none: next)"), true);
                WriteMenuItem(":backup", FileIo.GetBackupStatus() + " " + Lang.T("Toggle Backup"), true);
                WriteMenuItem(":password", Lang.T("Reset password"), true);
                WriteMenuItem(":defaults", Lang.T("Default settings"), true);
                WriteMenuItem(":restart", Lang.T("Restart Marbles"), true);
                WriteMenuItem(":logout", Lang.T("Logout"), true);
                WriteMenuItem(":q", Lang.T("Exit"), true);
            }
            else if (_manageModePage == 3)
            {
                WriteLogoForMenuDisplay();
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                WriteMenuExtension(GetMenuNavigation((_manageModePage + 1) + "/" + (MaxManageModePages + 1)));
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                WriteMenuItem(":scheme", Lang.T("Unlock color scheme") + " ", false);
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
                SchemeLabelColumn = Console.CursorLeft;
                Console.Write(SchemeLabel(false) + "\n");
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                WriteExtendedMenuItem(new string(' ', 2) + "l", DialogText.ExtExample, Lang.T("Text"),
                    ConsoleColors.ColorPreset.Normal, true, false);
                WriteExtendedMenuItem(new string(' ', 2) + "l", DialogText.ExtExample,
                    Lang.T("Logo, login label, directory browser, editor repeat"),
                    ConsoleColors.ColorPreset.HeaderColor, true, false);
                WriteExtendedMenuItem(new string(' ', 2) + "l", DialogText.ExtExample, Lang.T("Dividers and captions"),
                    ConsoleColors.ColorPreset.Divider, true, false);
                WriteExtendedMenuItem(new string(' ', 2) + "l", DialogText.ExtExample, Lang.T("Favorites"),
                    ConsoleColors.ColorPreset.Favorites, true, false);
                WriteExtendedMenuItem(new string(' ', 2) + "l", DialogText.ExtExample,
                    Lang.T("Preview title and save state indicator"), ConsoleColors.ColorPreset.PreviewTitle, true, false);
                WriteExtendedMenuItem(new string(' ', 2) + "l", DialogText.ExtExample,
                    Lang.T("Editor title"), ConsoleColors.ColorPreset.EditorTitle, true, false);
                WriteExtendedMenuItem(new string(' ', 2) + "l / T", DialogText.ExtExample,
                    Lang.T("Highlight and selection indicators"), ConsoleColors.ColorPreset.HighlightInverted, true, false);
                WriteExtendedMenuItem(new string(' ', 2) + "l / T", DialogText.ExtExample, Lang.T("Attention"),
                    ConsoleColors.ColorPreset.AttentionInverted, true, false);
                WriteExtendedMenuItem(new string(' ', 2) + "l / T", DialogText.ExtExample, Lang.T("Warning"),
                    ConsoleColors.ColorPreset.WarningInverted, true, false);
                WriteExtendedMenuItem(new string(' ', 2) + "l", DialogText.ExtExample,
                    Lang.T("Background ([Default]: the terminal's)"), ConsoleColors.ColorPreset.Normal, true,
                    false);
                WriteMenuItem(new string(' ', 2) + "j k / l", Lang.T("Select/Pick color from a list"), true);
                WriteMenuItem(new string(' ', 2) + "T", Lang.T("Pick text color (the last three)"), true);
                WriteMenuItem(new string(' ', 2) + "E", Lang.T("Reset"), true);
                WriteMenuItem(new string(' ', 2) + "Enter/Esc", Lang.T("Save and exit/Cancel"), true);
                WriteMenuItem(":bw",
                    (ConsoleColors.Monochrome ? DialogText.CheckedSetting : DialogText.UnCheckedSetting) +
                    " " + Lang.T("Black and white (no colors)"), true);
            }


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
        foreach (var hint in hints.Split(" | "))
        {
            var space = hint.IndexOf(' ');
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.NormalInverted);
            Console.Write(" " + (space < 0 ? hint : hint[..space]) + " ");
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            // One blank space on each side of the description
            Console.Write(space < 0 ? " " : " " + hint[(space + 1)..] + " ");
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