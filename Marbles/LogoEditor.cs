namespace Marbles;

/// <summary>
/// The logos: a page of their own, opened from the Configuration page and with ":logo" in the main menu. It is a
/// frame with two tabs (TabFrame): Logos, where a logo is selected and used (the built-in one and the ones saved
/// by the user), and Editor, where a logo is drawn on a grid of 4 rows of 63 characters, with the modes of the
/// text editor: NORMAL (move, clear, replace one character) and REPLACE (typing draws). A logo is saved as one of
/// the user's own (a file in the directory "logos"), which is then the logo in use.
/// </summary>
internal static class LogoEditor
{
    private const int TabRow = 2; // Below the top bar
    private const int MaxUndo = 100;

    private static LogoArt _editing = LogoArt.BuiltIn; // The logo being edited
    private static bool _changed; // ... is not saved
    private static bool _selectInUse; // The list of logos selects the one in use when it is drawn again
    private static readonly Stack<LogoArt> Undo = new();

    /// <summary>
    /// Select and edit the logos until the page is closed. The page it was opened from must be drawn again
    /// afterwards
    /// </summary>
    internal static void Show()
    {
        _editing = Logo.Current.Clone();
        _changed = false;
        Undo.Clear();
        var frame = new TabFrame(TabRow, () =>
            {
                Menu.WriteModeLabel(DialogText.CaptionsLogos, string.Empty, 0);
                Menu.WriteNameInTopBar(false);
            },
            new TabItem(() => Lang.T("Logos"), new ListPage()),
            new TabItem(() => Lang.T("Editor"), new EditorPage()));
        frame.Run();
        Tools.HideCursor();
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Tools.ClearScreen();
    }

    /// <summary>
    /// Use a logo by its name (":logo Name")
    /// </summary>
    /// <returns>False if there is no logo with the name</returns>
    internal static bool UseByName(string name)
    {
        var logo = Logos.Load(name);
        if (logo == null) return false;
        Use(logo);
        return true;
    }

    // Use a logo: it is shown at once, and its name is saved in the settings
    private static void Use(LogoArt logo)
    {
        Logo.Current = logo.Clone();
        FileIo.SaveSettings();
        _selectInUse = true;
    }

    // Esc or Q: changes that are not saved are asked about
    private static TabResult Close()
    {
        if (!_changed) return TabResult.Close;
        switch (Menu.YesNoCancelPrompt(DialogText.CaptionsLogos, DialogText.SaveChangesQuestion, string.Empty, false))
        {
            case 1:
                return Save() ? TabResult.Close : TabResult.Redraw;
            case 2:
                return TabResult.Close;
            default:
                return TabResult.Redraw;
        }
    }

    // Save the logo being edited as one of the user's own (the name is asked for), and use it
    private static bool Save()
    {
        var suggested = Logos.IsBuiltIn(_editing.Name) ? string.Empty : _editing.Name;
        while (true)
        {
            ChoiceBox.Forget();
            var name = ChoiceBox.AskText(DialogText.LogoSaveCaption, DialogText.LogoSavePrompt, DialogText.NameColon,
                suggested);
            if (name == null) return false;
            suggested = name;
            if (!Logos.IsValidName(name))
            {
                ChoiceBox.Forget();
                ChoiceBox.Message(DialogText.LogoSaveCaption,
                    Logos.IsBuiltIn(name) ? DialogText.LogoNameBuiltIn : DialogText.LogoNameInvalid, string.Empty);
                continue;
            }

            // Another logo of the user's own with the name is replaced, if the user wants that
            if (Logos.Exists(name) && !name.Equals(_editing.Name, StringComparison.OrdinalIgnoreCase))
            {
                ChoiceBox.Forget();
                if (!ChoiceBox.Confirm(DialogText.LogoSaveCaption, Lang.F("Replace the logo \"{0}\"?", name),
                        string.Empty, true))
                {
                    continue;
                }
            }

            _editing.Name = name.Trim();
            var status = Logos.Save(_editing);
            if (status != DialogText.Success)
            {
                ChoiceBox.Forget();
                ChoiceBox.Message(DialogText.LogoSaveCaption, status, string.Empty);
                return false;
            }

            Use(_editing);
            _changed = false;
            return true;
        }
    }

    // Draw a logo (in the color of the logo) from a row and column
    private static void DrawLogo(LogoArt logo, int top, int left)
    {
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.HeaderColor);
        for (var row = 0; row < LogoArt.Rows; row++)
        {
            Tools.SetCursorEx(top + row, left);
            Console.Write(logo.Row(row));
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    /// <summary>
    /// The Logos tab: the selected logo at the top, and the list of logos below it, the one in use marked
    /// </summary>
    private sealed class ListPage : ITabPage
    {
        private int _selected = -1;
        private int _scroll;
        private int _shown = -1;

        public string Title => Lang.T("Logos");
        public string Keys => DialogText.LogoListKeys;
        public Tools.HelpTopic Help => Tools.HelpTopic.Logos;

        private const int ListTop = LogoArt.Rows + 2; // Below the logo and a line

        private static int Rows(TabArea area) => Math.Max(1, area.Height - ListTop - 2);

        public void Draw(TabArea area, bool full)
        {
            var names = Logos.AllNames();
            if (_selectInUse)
            {
                _selected = -1;
                _selectInUse = false;
            }

            if (_selected < 0)
            {
                // The logo in use is selected from the start
                _selected = Math.Max(0, names.FindIndex(name =>
                    name.Equals(Logo.Current.Name, StringComparison.OrdinalIgnoreCase)));
            }

            _selected = Math.Clamp(_selected, 0, Math.Max(0, names.Count - 1));
            var rows = Rows(area);
            var scroll = Math.Clamp(_scroll, Math.Max(0, _selected - rows + 1), _selected);
            if (full || scroll != _scroll)
            {
                _scroll = scroll;
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                Tools.SetCursorEx(area.Top + LogoArt.Rows + 1, area.Left + 2);
                Console.Write(new string('-', Math.Min(LogoArt.Width, area.Width - 2)));
                for (var row = 0; row < rows; row++)
                {
                    var i = _scroll + row;
                    Tools.SetCursorEx(area.Top + ListTop + row, area.Left);
                    ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                    Console.Write(new string(' ', area.Width));
                    if (i >= names.Count) continue;
                    Tools.SetCursorEx(area.Top + ListTop + row, area.Left + 3);
                    var inUse = names[i].Equals(Logo.Current.Name, StringComparison.OrdinalIgnoreCase);
                    Console.Write((inUse ? "[x] " : "[ ] ") + names[i]);
                    if (i == 0)
                    {
                        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                        Console.Write("  " + Lang.T("(built-in)"));
                        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                    }
                }

                // Where the logos of the user's own are kept
                Tools.SetCursorEx(area.Top + area.Height - 1, area.Left + 3);
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                Console.Write(Tools.TruncateString(Lang.F("Your own logos are saved in {0}",
                    Path.Combine(FileIo.GetDataDirectoryName(), Logos.DirectoryName)), area.Width - 3, true));
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                _shown = -1;
            }

            if (_shown == _selected) return;

            // The selected logo, and the pointer
            DrawLogo(Logos.Load(names[_selected]) ?? LogoArt.BuiltIn, area.Top, area.Left + 2);
            if (_shown >= 0)
            {
                Tools.SetCursorEx(area.Top + ListTop + _shown - _scroll, area.Left);
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                Console.Write("  ");
            }

            Tools.SetCursorEx(area.Top + ListTop + _selected - _scroll, area.Left);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
            Console.Write("->");
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            _shown = _selected;
        }

        public TabResult HandleKey(ConsoleKeyInfo key)
        {
            var names = Logos.AllNames();
            var navigation = Keyboard.ToNavigationKey(key).Key;
            if (key.Key is ConsoleKey.Escape or ConsoleKey.Q) return Close();
            if (key.Key == ConsoleKey.S)
            {
                if (_changed) Save();
                return TabResult.Redraw;
            }

            if (navigation == ConsoleKey.DownArrow)
            {
                _selected = Math.Min(_selected + 1, names.Count - 1);
            }
            else if (navigation == ConsoleKey.UpArrow)
            {
                _selected = Math.Max(_selected - 1, 0);
            }
            else if (key.KeyChar == 'G')
            {
                _selected = names.Count - 1;
            }
            else if (key.KeyChar == 'g')
            {
                if (Console.ReadKey(true).KeyChar == 'g') _selected = 0;
            }
            else if (key.Key is ConsoleKey.Enter or ConsoleKey.Spacebar || navigation == ConsoleKey.RightArrow)
            {
                return UseSelected(names[_selected], false);
            }
            else if (key.Key == ConsoleKey.E)
            {
                return UseSelected(names[_selected], true);
            }
            else if (key.Key == ConsoleKey.D)
            {
                return Delete(names[_selected]);
            }

            return TabResult.None;
        }

        // Use the selected logo (Enter), or edit it (E). Changes that are not saved are given up if the user
        // wants that
        private static TabResult UseSelected(string name, bool edit)
        {
            if (_changed)
            {
                ChoiceBox.Forget();
                if (!ChoiceBox.Confirm(DialogText.CaptionsLogos, DialogText.LogoDiscardQuestion, string.Empty, true))
                {
                    return TabResult.Redraw;
                }
            }

            var logo = Logos.Load(name);
            if (logo == null) return TabResult.Redraw;
            if (!edit) Use(logo);
            _editing = logo.Clone();
            _changed = false;
            Undo.Clear();
            return edit ? TabResult.NextPage : TabResult.Redraw;
        }

        // Delete a logo of the user's own (the built-in one cannot be deleted)
        private TabResult Delete(string name)
        {
            ChoiceBox.Forget();
            if (Logos.IsBuiltIn(name))
            {
                ChoiceBox.Message(DialogText.CaptionsLogos, DialogText.LogoBuiltInDelete, string.Empty);
                return TabResult.Redraw;
            }

            if (!ChoiceBox.Confirm(DialogText.CaptionsLogos, Lang.F("Delete the logo \"{0}\"?", name),
                    DialogText.SchemeDeleteNote, true))
            {
                return TabResult.Redraw;
            }

            var status = Logos.Delete(name);
            if (status != DialogText.Success)
            {
                ChoiceBox.Forget();
                ChoiceBox.Message(DialogText.CaptionsLogos, status, string.Empty);
                return TabResult.Redraw;
            }

            // The logo in use was deleted: the built-in one is used
            if (name.Equals(Logo.Current.Name, StringComparison.OrdinalIgnoreCase))
            {
                Use(LogoArt.BuiltIn);
                if (!_changed) _editing = LogoArt.BuiltIn;
            }

            _selected = Math.Max(0, _selected - 1);
            return TabResult.Redraw;
        }
    }

    /// <summary>
    /// The Editor tab: the logo on a grid with a ruler above it, edited in the modes NORMAL and REPLACE
    /// </summary>
    private sealed class EditorPage : ITabPage
    {
        private int _row;
        private int _column;
        private bool _replace; // REPLACE mode: typing draws
        private char _pending; // The first key of a command of two keys ("r", "d", "g")
        private int _gridTop;
        private int _gridLeft;

        public string Title => Lang.T("Editor");
        public string Keys => _replace ? DialogText.LogoReplaceKeys : DialogText.LogoEditorKeys;
        public Tools.HelpTopic Help => Tools.HelpTopic.Logos;

        // All keys are characters to draw in REPLACE mode, and after "r"
        public bool CapturesKeys => _replace || _pending == 'r';

        /// <summary>
        /// The ruler above the grid: a dot for each column, ":" for every 5th and the tens as digits
        /// </summary>
        internal static string Ruler()
        {
            var ruler = new char[LogoArt.Width];
            for (var column = 1; column <= LogoArt.Width; column++)
            {
                ruler[column - 1] = column % 10 == 0 ? (char)('0' + column / 10 % 10) : column % 5 == 0 ? ':' : '.';
            }

            return new string(ruler);
        }

        public void Draw(TabArea area, bool full)
        {
            _gridLeft = area.Left + 2;
            _gridTop = area.Top + 4;
            if (full)
            {
                // The logo being edited
                Tools.SetCursorEx(area.Top, area.Left + 1);
                Console.Write(Tools.TruncateString(Lang.F("Logo: {0}", _editing.Name) +
                                                   (_changed ? "  " + Lang.T("(changed, S: save)") : string.Empty),
                    area.Width - 2, true));

                // The ruler and the frame of the grid
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                Tools.SetCursorEx(_gridTop - 2, _gridLeft);
                Console.Write(Ruler());
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.BoxBorder);
                Tools.SetCursorEx(_gridTop - 1, _gridLeft - 1);
                Console.Write(AsciiStyles.GetTopCorner() + new string('-', LogoArt.Width) + AsciiStyles.GetTopCorner());
                for (var row = 0; row < LogoArt.Rows; row++)
                {
                    Tools.SetCursorEx(_gridTop + row, _gridLeft - 1);
                    Console.Write('|');
                    Tools.SetCursorEx(_gridTop + row, _gridLeft + LogoArt.Width);
                    Console.Write('|');
                }

                Tools.SetCursorEx(_gridTop + LogoArt.Rows, _gridLeft - 1);
                Console.Write(AsciiStyles.GetBottomCorner() + new string('-', LogoArt.Width) +
                              AsciiStyles.GetBottomCorner());
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            }

            DrawLogo(_editing, _gridTop, _gridLeft);

            // The mode and where the cursor is (the names of the modes are as in the text editor)
            Tools.SetCursorEx(_gridTop + LogoArt.Rows + 2, area.Left + 1);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.EditorTitle);
            var mode = _replace ? "-- REPLACE --" : "-- NORMAL --"; // English
            Console.Write(mode);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            var position = "  " + Lang.F("Row {0}, column {1}", _row + 1, _column + 1);
            Console.Write(position.PadRight(Math.Max(0, area.Width - 1 - mode.Length)));
        }

        /// <summary>
        /// The cursor is shown in the grid
        /// </summary>
        public (int Row, int Column)? Cursor => (_gridTop + _row, _gridLeft + _column);

        // Keep a copy of the logo before a change, to undo it
        private static void Remember()
        {
            Undo.Push(_editing.Clone());
            if (Undo.Count <= MaxUndo) return;
            var kept = Undo.Take(MaxUndo).Reverse().ToList();
            Undo.Clear();
            foreach (var logo in kept) Undo.Push(logo);
        }

        private void Set(char c)
        {
            _editing[_row, _column] = c;
            _changed = true;
        }

        private void Move(int rows, int columns)
        {
            _row = Math.Clamp(_row + rows, 0, LogoArt.Rows - 1);
            _column = Math.Clamp(_column + columns, 0, LogoArt.Width - 1);
        }

        public TabResult HandleKey(ConsoleKeyInfo key)
        {
            return _replace ? HandleReplace(key) : HandleNormal(key);
        }

        // REPLACE mode: what is typed is drawn over the grid, and the cursor moves on
        private TabResult HandleReplace(ConsoleKeyInfo key)
        {
            switch (key.Key)
            {
                case ConsoleKey.Escape:
                    _replace = false;
                    return TabResult.Redraw;
                case ConsoleKey.LeftArrow:
                    Move(0, -1);
                    return TabResult.None;
                case ConsoleKey.RightArrow:
                    Move(0, 1);
                    return TabResult.None;
                case ConsoleKey.UpArrow:
                    Move(-1, 0);
                    return TabResult.None;
                case ConsoleKey.DownArrow:
                    Move(1, 0);
                    return TabResult.None;
                case ConsoleKey.Enter:
                    // The start of the next row
                    _column = 0;
                    Move(1, 0);
                    return TabResult.None;
                case ConsoleKey.Backspace:
                    if (_column == 0) return TabResult.None;
                    _column--;
                    Set(' ');
                    return TabResult.None;
                case ConsoleKey.Delete:
                    Set(' ');
                    return TabResult.None;
            }

            if (!LogoArt.Typeable(key.KeyChar)) return TabResult.None;
            Set(key.KeyChar);
            Move(0, 1);
            return TabResult.None;
        }

        // NORMAL mode: move, and change one character or a row
        private TabResult HandleNormal(ConsoleKeyInfo key)
        {
            var pending = _pending;
            _pending = '\0';
            if (pending == 'r')
            {
                // "r" and a character: replace the character under the cursor
                if (!LogoArt.Typeable(key.KeyChar)) return TabResult.None;
                Remember();
                Set(key.KeyChar);
                return TabResult.None;
            }

            if (pending == 'd')
            {
                // "dd": clear the row
                if (key.KeyChar != 'd') return TabResult.None;
                Remember();
                for (var column = 0; column < LogoArt.Width; column++) _editing[_row, column] = ' ';
                _changed = true;
                return TabResult.None;
            }

            if (pending == 'g')
            {
                if (key.KeyChar == 'g') _row = 0;
                return TabResult.None;
            }

            var navigation = Keyboard.ToNavigationKey(key).Key;
            switch (navigation)
            {
                case ConsoleKey.LeftArrow:
                    Move(0, -1);
                    return TabResult.None;
                case ConsoleKey.RightArrow:
                    Move(0, 1);
                    return TabResult.None;
                case ConsoleKey.UpArrow:
                    Move(-1, 0);
                    return TabResult.None;
                case ConsoleKey.DownArrow:
                    Move(1, 0);
                    return TabResult.None;
            }

            switch (key.KeyChar)
            {
                case '0':
                    _column = 0;
                    return TabResult.None;
                case '$':
                {
                    // The last character of the row (that is not blank space)
                    var row = _editing.Row(_row);
                    _column = Math.Max(0, row.TrimEnd().Length - 1);
                    return TabResult.None;
                }
                case 'G':
                    _row = LogoArt.Rows - 1;
                    return TabResult.None;
                case 'g':
                case 'r':
                case 'd':
                    _pending = key.KeyChar;
                    return TabResult.None;
                case 'x':
                    Remember();
                    Set(' ');
                    return TabResult.None;
                case 'i':
                case 'R':
                    Remember();
                    _replace = true;
                    return TabResult.Redraw;
                case 'a':
                    Remember();
                    Move(0, 1);
                    _replace = true;
                    return TabResult.Redraw;
                case 'u':
                    if (Undo.Count == 0) return TabResult.None;
                    var name = _editing.Name;
                    _editing = Undo.Pop();
                    _editing.Name = name;
                    _changed = true;
                    return TabResult.Redraw;
                case 'S':
                case 's':
                    Save();
                    return TabResult.Redraw;
            }

            if (key.Key is ConsoleKey.Escape or ConsoleKey.Q) return Close();
            return TabResult.None;
        }
    }
}
