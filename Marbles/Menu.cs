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
        else if (Mode.CurrentMode == Mode.SelectedMode.View)
        {
            // The entry view draws its own page (Actions.DrawViewMenu)
        }
        else if (Mode.CurrentMode == Mode.SelectedMode.Edit)
        {
            // The editor writes its own top row (WriteEditorTop)
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

    private const int MenuBoxTop = 2; // The row of the top of the boxes
    private const int MenuKeyWidth = 11; // The room for the key of an item (the dots fill up to it)
    private const int MenuBoxRows = 8; // The rows of items in a box (a box with fewer items has blank rows)

    // The items of the two boxes of the page shown, and where the boxes are
    private static MenuItem[][] _menuBoxes = { Array.Empty<MenuItem>(), Array.Empty<MenuItem>() };
    private static readonly int[] MenuBoxLeft = new int[2];
    private static readonly string[] MenuBoxTitles = { string.Empty, string.Empty };
    private static int _menuBoxWidth;

    // The selected item: the box (0 left, 1 right) and its row
    private static int _menuBox;
    private static int _menuRow;

    // The main menu of a data file: two pages, each with two boxes of items
    private static void DrawMenuPage(int page)
    {
        WriteModeLabel(DialogText.CaptionsMenu, string.Empty, 0);
        WriteNameInTopBar(true);
        Tools.SetCursorEx(1, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(new string('-', Init.LineWidth));
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        static string Check(bool on) => on ? DialogText.CheckedSetting : DialogText.UnCheckedSetting;
        if (page == 0)
        {
            DrawMenuBoxes(Lang.T("Notes"), new MenuItem[]
            {
                new("N", Lang.T("New"), "N"),
                new("S /", Lang.T("Search"), "S"),
                new("R", Lang.T("Reselect"), "R"),
                new("F", Lang.T("Favorites") + " " + Entries.GetFavoritesCount(), "F"),
                new("T", Lang.T("Top 20"), "T"),
                new("M", Lang.T("Session history") + " " + Entries.GetSessionCountAsString(), "M"),
                new("A", Lang.T("List all") + " " + Entries.GetEntriesCountAsString(), "A"),
                new(":undelete", Lang.T("Undo deletion"), ":undelete")
            }, Lang.T("Data file"), new MenuItem[]
            {
                new("I", Lang.T("File info"), "I"),
                new(":import", Lang.T("Import text file"), ":import"),
                new(":transfer", Lang.T("Transfer") + " " + Entries.GetTransferState(), ":transfer"),
                new(":backup", FileIo.GetBackupStatus() + " " + Lang.T("Backup"), ":backup"),
                new(":password", Lang.T("Reset password"), ":password"),
                new(":", Lang.T("Command line"), ":"),
                new("?", Lang.T("Help"), "?"),
                new("X", Lang.T("Logout"), "X")
            });
        }
        else
        {
            DrawMenuBoxes(DialogText.CaptionsSettings, new MenuItem[]
            {
                new(":scheme", Lang.T("Edit color scheme"), ":scheme"),
                new(":color 1-" + Actions.ColorPresetCount,
                    "[" + (ConsoleColors.GetColorPresetIndex() + 1) + "] " + Lang.T("Color preset"), ":color"),
                new(":bw", Check(ConsoleColors.Monochrome) + " " + Lang.T("Black and white"), ":bw"),
                new(":language", "[" + Lang.Current + "] " + Lang.T("Language"), ":language"),
                new(":number", Check(TextArea.LineNumbers) + " " + Lang.T("Line numbers"), ":number"),
                new(":size", Lang.T("Size") + " " + Init.GetOutputDimensions(), ":size"),
                new(":size auto", Check(Init.AutoSize) + " " + Lang.T("Follow the window"), ":size auto")
            }, DialogText.ApplicationName, new MenuItem[]
            {
                new(":browse", Lang.T("Browse files"), ":browse"),
                new("D", Lang.T("Display timestamp"), "D"),
                new(":guide", DialogText.QuickGuide, ":guide"),
                new(":about", Lang.T("About"), ":about"),
                new(":defaults", Lang.T("Default settings"), ":defaults"),
                new(":restart", Lang.T("Restart Marbles"), ":restart"),
                new(":logout", Lang.T("Logout"), ":logout"),
                new(":q", Lang.T("Exit"), ":q")
            });
        }

        // The keys of the page
        Tools.SetCursorEx(Init.LineCount - 1, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        WriteKeyHints(DialogText.MenuKeys);
        DrawMenuBoxNumbers();
        DrawMenuPointer(true);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Tools.SetCursorEx(Init.LineCount - 1, 0);
    }

    // Two boxes side by side, each with a title and its items
    private static void DrawMenuBoxes(string leftTitle, MenuItem[] left, string rightTitle, MenuItem[] right)
    {
        var width = (Init.LineWidth - 1) / 2;
        _menuBoxWidth = width;
        _menuBoxes = new[] { left, right };
        MenuBoxLeft[0] = 0;
        MenuBoxLeft[1] = width + 1;
        MenuBoxTitles[0] = leftTitle;
        MenuBoxTitles[1] = rightTitle;
        ClampMenuSelection();
        DrawMenuBox(MenuBoxLeft[0], width, left);
        DrawMenuBox(MenuBoxLeft[1], width, right);
        DrawMenuTitle(0);
        DrawMenuTitle(1);
    }

    // The title of a box: highlighted on the box with the selection, as the panes of the file browser
    private static void DrawMenuTitle(int box)
    {
        Tools.SetCursorEx(MenuBoxTop, MenuBoxLeft[box] + 2);
        ConsoleColors.SetColors(box == _menuBox
            ? ConsoleColors.ColorPreset.HighlightInverted
            : ConsoleColors.ColorPreset.HeaderColor);
        Console.Write(Tools.FitLine(" " + MenuBoxTitles[box] + " ", Init.LineWidth - (_menuBoxWidth - 4)));
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    private static void DrawMenuBox(int left, int width, MenuItem[] items)
    {
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Tools.SetCursorEx(MenuBoxTop, left);
        Console.Write(AsciiStyles.GetTopCorner() + new string('-', width - 2) + AsciiStyles.GetTopCorner());
        for (var row = 0; row < MenuBoxRows; row++)
        {
            var y = MenuBoxTop + 1 + row;
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Tools.SetCursorEx(y, left);
            Console.Write('|');
            Tools.SetCursorEx(y, left + width - 1);
            Console.Write('|');
            if (row >= items.Length) continue;
            var item = items[row];
            Tools.SetCursorEx(y, left + 3);
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
        Tools.SetCursorEx(MenuBoxTop + MenuBoxRows + 1, left);
        Console.Write(AsciiStyles.GetBottomCorner() + new string('-', width - 2) + AsciiStyles.GetBottomCorner());
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    private static void ClampMenuSelection()
    {
        _menuBox = Math.Clamp(_menuBox, 0, 1);
        _menuRow = Math.Clamp(_menuRow, 0, Math.Max(0, _menuBoxes[_menuBox].Length - 1));
    }

    // Draw (or remove) the selection pointer at the selected item
    private static void DrawMenuPointer(bool show)
    {
        if (_menuBoxes[_menuBox].Length == 0) return;
        Tools.SetCursorEx(MenuBoxTop + 1 + _menuRow, MenuBoxLeft[_menuBox] + 1);
        ConsoleColors.SetColors(show ? ConsoleColors.ColorPreset.HighlightInverted : ConsoleColors.ColorPreset.Normal);
        Console.Write(show ? "->" : "  ");
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Tools.SetCursorEx(Init.LineCount - 1, 0);
    }

    /// <summary>
    /// Move the selection of the main menu up or down in its box (only the pointer is drawn again)
    /// </summary>
    /// <returns>True if the selection has gone to the other page, which must be drawn</returns>
    internal static bool MoveMenuSelection(int rows)
    {
        var count = _menuBoxes[_menuBox].Length;
        var row = _menuRow + rows;
        if (row >= 0 && row < count)
        {
            DrawMenuPointer(false);
            _menuRow = row;
            DrawMenuPointer(true);
            return false;
        }

        // Past the last item: the first of the next box. Before the first: the last of the previous box
        var forward = rows > 0;
        var changedPage = NextMenuBox(!forward);
        if (changedPage)
        {
            _menuRow = forward ? 0 : int.MaxValue; // The last of the box, when it is drawn
            return true;
        }

        DrawMenuPointer(false);
        _menuRow = forward ? 0 : Math.Max(0, _menuBoxes[_menuBox].Length - 1);
        DrawMenuPointer(true);
        return false;
    }

    /// <summary>
    /// Go to the next box of the main menu (or the previous): the two boxes of the first page, then the two of the
    /// second, and back to the first. In a box of the same page the row stays (or is the last of a shorter box)
    /// </summary>
    /// <returns>True if the next box is on the other page, which must be drawn</returns>
    internal static bool NextMenuBox(bool back)
    {
        var count = ManagePageCount * 2;
        var next = (_manageModePage * 2 + _menuBox + (back ? count - 1 : 1)) % count;
        if (next / 2 != _manageModePage)
        {
            _manageModePage = next / 2;
            _menuBox = next % 2;
            _menuRow = 0;
            return true;
        }

        DrawMenuPointer(false);
        _menuBox = next % 2;
        ClampMenuSelection();
        DrawMenuTitle(0);
        DrawMenuTitle(1);
        DrawMenuBoxNumbers();
        DrawMenuPointer(true);
        return false;
    }

    // The numbers of the four boxes below them, with the one of the box with the selection highlighted
    private static void DrawMenuBoxNumbers()
    {
        Tools.SetCursorEx(MenuBoxTop + MenuBoxRows + 2, 1);
        var active = _manageModePage * 2 + _menuBox;
        for (var i = 0; i < ManagePageCount * 2; i++)
        {
            ConsoleColors.SetColors(i == active
                ? ConsoleColors.ColorPreset.HighlightInverted
                : ConsoleColors.ColorPreset.Divider);
            // Each number has a blank space on each side: " 1  2  3  4  (Tab)"
            Console.Write(" " + (i + 1) + " ");
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(" " + DialogText.TabBrackets);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    /// <summary>
    /// Select the first item of the first box of the main menu (gg)
    /// </summary>
    internal static void SelectFirstMenuItem()
    {
        _manageModePage = 0;
        _menuBox = 0;
        _menuRow = 0;
    }

    /// <summary>
    /// Select the last item of the last box of the main menu (G)
    /// </summary>
    internal static void SelectLastMenuItem()
    {
        _manageModePage = ManagePageCount - 1;
        _menuBox = 1;
        _menuRow = int.MaxValue; // The last of the box, when it is drawn
    }

    /// <summary>
    /// The choices shown with Esc in the main menu: log out, the Select mode page, or exit Marbles
    /// </summary>
    /// <returns>The number of the choice (0, 1, 2), or -1 if the box was closed</returns>
    internal static int LeaveChoice()
    {
        var choices = new[] { Lang.T("Logout"), DialogText.StartSelectMode, DialogText.Exit };
        var inner = Math.Max(TextMetrics.WidthOf(DialogText.CaptionsOptions) + 4,
            choices.Max(c => TextMetrics.WidthOf(c)) + 12);
        var left = Math.Max(0, (Init.LineWidth - inner - 2) / 2);
        var top = MenuBoxTop + 2;
        // The box, on top of the menu
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Tools.SetCursorEx(top, left);
        Console.Write(AsciiStyles.GetTopCorner() + new string('-', inner) + AsciiStyles.GetTopCorner());
        for (var row = 0; row < choices.Length + 2; row++)
        {
            Tools.SetCursorEx(top + 1 + row, left);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write('|');
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            var text = row >= 1 && row <= choices.Length ? "   " + row + " .... " + choices[row - 1] : string.Empty;
            Console.Write(text + new string(' ', Math.Max(0, inner - TextMetrics.WidthOf(text))));
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write('|');
        }

        Tools.SetCursorEx(top + choices.Length + 3, left);
        Console.Write(AsciiStyles.GetBottomCorner() + new string('-', inner) + AsciiStyles.GetBottomCorner());
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.HeaderColor);
        Tools.SetCursorEx(top, left + 2);
        Console.Write(" " + DialogText.CaptionsOptions + " ");
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
                    ? ConsoleColors.ColorPreset.HighlightInverted
                    : ConsoleColors.ColorPreset.Normal);
                Console.Write(i == selected ? "->" : "  ");
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            }

            Tools.SetCursorEx(Init.LineCount - 1, 0);
            var key = Console.ReadKey(true);
            var navigation = Keyboard.ToNavigationKey(key).Key;
            if (key.Key is ConsoleKey.Escape or ConsoleKey.Q || navigation == ConsoleKey.LeftArrow) return -1;
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
    /// Select the first item of the first box (when another page is shown)
    /// </summary>
    internal static void ResetMenuSelection()
    {
        _menuBox = 0;
        _menuRow = 0;
    }

    /// <summary>
    /// What the selected item of the main menu runs: a key ("N") or a command (":scheme"), or null if none
    /// </summary>
    internal static string? SelectedMenuItem()
    {
        var items = _menuBoxes[_menuBox];
        return _menuRow < items.Length ? items[_menuRow].Run : null;
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
            _tempTitleLabel = Tools.TruncateString(Entries.CurrentEntry.Title, Tools.GetTitlePreviewWidth(), true);
            Console.Write(" " + DialogText.TitleColon + " ");
            Console.Write(_tempTitleLabel);
        }
    }

    public static void WriteDocumentScrollLabel()
    {
        Console.SetCursorPosition(Math.Max(0, (Init.LineWidth / 2) - (DialogText.DocumentScroll.Length / 2)),
            Init.LineCount - 2);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.PreviewTitle);
        Console.Write(DialogText.DocumentScroll);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    /// <summary>
    /// The top row of the editor: the label EDITOR at the left, the name of the program in the middle, and
    /// ":help" at the right
    /// </summary>
    internal static void WriteEditorTop()
    {
        WriteModeLabel(DialogText.CaptionsEditor, string.Empty, 0);
        WriteNameInTopBar(false);
        var key = " :help ";
        var text = " " + Lang.T("Help") + " ";
        Tools.SetCursorEx(0, Math.Max(0, Init.LineWidth - 1 - TextMetrics.WidthOf(key + text)));
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.NormalInverted);
        Console.Write(key);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Console.Write(text);
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