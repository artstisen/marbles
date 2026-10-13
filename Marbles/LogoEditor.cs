namespace Marbles;

/// <summary>
/// The logos: a page of their own, opened from the Configuration page and with ":logo" in the main menu. It is a
/// frame with three tabs (TabFrame): Logos, where a logo is selected and used (the built-in one and the ones saved
/// by the user), Editor, where a logo is drawn on a grid of 4 rows of 63 characters, with the modes of the text
/// editor: NORMAL (move, clear, replace one character) and REPLACE (typing draws), and Colors, with the sub-tabs
/// Rows, where each row gets a color of its own (or the color of the logo), and Cells, where cells are painted in
/// colors of their own on the grid. A logo is saved as one of the user's own (a file in the directory "logos"),
/// which is then the logo in use.
/// </summary>
internal static class LogoEditor
{
    private const int TabRow = 2; // Below the top bar
    private const int MaxUndo = 100;
    private const int PickerLeft = 52;

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
            new TabItem(() => Lang.T("Editor"), new EditorPage()),
            new TabItem(() => Lang.T("Colors"), new ColorsPage(), new CellsPage()));
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

    // Draw a logo from a row and column: each cell in its color, the one of its row, or the color of the logo
    private static void DrawLogo(LogoArt logo, int top, int left)
    {
        for (var row = 0; row < LogoArt.Rows; row++)
        {
            Tools.SetCursorEx(top + row, left);
            foreach (var run in logo.Runs(row))
            {
                Logo.SetLogoColor(run.Color);
                Console.Write(run.Text);
            }
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    // The name of the logo being edited, on the first line of a page with the grid
    private static void DrawName(TabArea area)
    {
        Tools.SetCursorEx(area.Top, area.Left + 1);
        Console.Write(Tools.TruncateString(Lang.F("Logo: {0}", _editing.Name) +
                                           (_changed ? "  " + Lang.T("(changed, S: save)") : string.Empty),
            area.Width - 2, true));
    }

    // The ruler and the frame of the grid, around the logo drawn from a row and column
    private static void DrawGridFrame(int gridTop, int gridLeft)
    {
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Tools.SetCursorEx(gridTop - 2, gridLeft);
        Console.Write(EditorPage.Ruler());
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.BoxBorder);
        Tools.SetCursorEx(gridTop - 1, gridLeft - 1);
        Console.Write(AsciiStyles.GetTopCorner() + new string('-', LogoArt.Width) + AsciiStyles.GetTopCorner());
        for (var row = 0; row < LogoArt.Rows; row++)
        {
            Tools.SetCursorEx(gridTop + row, gridLeft - 1);
            Console.Write('|');
            Tools.SetCursorEx(gridTop + row, gridLeft + LogoArt.Width);
            Console.Write('|');
        }

        Tools.SetCursorEx(gridTop + LogoArt.Rows, gridLeft - 1);
        Console.Write(AsciiStyles.GetBottomCorner() + new string('-', LogoArt.Width) +
                      AsciiStyles.GetBottomCorner());
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    // Keep a copy of the logo before a change, to undo it
    private static void Remember()
    {
        Undo.Push(_editing.Clone());
        if (Undo.Count <= MaxUndo) return;
        var kept = Undo.Take(MaxUndo).Reverse().ToList();
        Undo.Clear();
        foreach (var logo in kept) Undo.Push(logo);
    }

    // Take back the last change (the name of the logo stays)
    private static bool UndoLast()
    {
        if (Undo.Count == 0) return false;
        var name = _editing.Name;
        _editing = Undo.Pop();
        _editing.Name = name;
        _changed = true;
        return true;
    }

    /// <summary>
    /// The Cells page of the Colors tab: the logo on the grid of the editor, where cells are painted in a color
    /// of their own (c picks the color to paint with, e takes the color of the cell of the cursor, Space paints,
    /// x gives a cell the color of its row again).
    /// "v" selects a rectangle to paint or clear in one go
    /// </summary>
    private sealed class CellsPage : ITabPage
    {
        private int _row;
        private int _column;
        private bool _visual; // A rectangle is being selected, from the anchor to the cursor
        private int _anchorRow;
        private int _anchorColumn;
        private char _pending; // The first key of "gg"
        private ConsoleColor? _paint; // The color to paint with (null: not picked yet)
        private int _gridTop;
        private int _gridLeft;

        public string Title => Lang.T("Cells");
        public string Keys => _visual ? DialogText.LogoCellVisualKeys : DialogText.LogoCellKeys;
        public Tools.HelpTopic Help => Tools.HelpTopic.Logos;

        /// <summary>
        /// The cursor is shown in the grid
        /// </summary>
        public (int Row, int Column)? Cursor => (_gridTop + _row, _gridLeft + _column);

        // The cells a key works on: the selected rectangle, or the cell of the cursor
        private (int Top, int Left, int Bottom, int Right) Selection => _visual
            ? (Math.Min(_row, _anchorRow), Math.Min(_column, _anchorColumn), Math.Max(_row, _anchorRow),
                Math.Max(_column, _anchorColumn))
            : (_row, _column, _row, _column);

        public void Draw(TabArea area, bool full)
        {
            _gridLeft = area.Left + 2;
            _gridTop = area.Top + 4;
            if (full)
            {
                DrawName(area);
                DrawGridFrame(_gridTop, _gridLeft);
            }

            DrawLogo(_editing, _gridTop, _gridLeft);
            if (_visual)
            {
                // The selected cells
                var (top, left, bottom, right) = Selection;
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
                for (var row = top; row <= bottom; row++)
                {
                    Tools.SetCursorEx(_gridTop + row, _gridLeft + left);
                    Console.Write(_editing.Row(row).Substring(left, right - left + 1));
                }

                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            }

            // The mode and where the cursor is (the names of the modes are as in the text editor)
            Tools.SetCursorEx(_gridTop + LogoArt.Rows + 2, area.Left + 1);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.EditorTitle);
            var mode = _visual ? "-- VISUAL --" : "-- NORMAL --"; // English
            Console.Write(mode);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            var position = "  " + Lang.F("Row {0}, column {1}", _row + 1, _column + 1);
            Console.Write(position.PadRight(Math.Max(0, area.Width - 1 - mode.Length)));

            // The color to paint with, and the color of the cell of the cursor
            if (_paint is { } paint) ColorLine(area, 3, Lang.T("Paint color:"), paint, ColorPicker.Name(paint));
            else ColorLine(area, 3, Lang.T("Paint color:"), null, Lang.T("none (c: pick a color)"));
            if (_editing.CellColor(_row, _column) is { } cell)
            {
                // A cell with a color of its own: "e" paints with that color
                ColorLine(area, 4, Lang.T("Color of the cell:"), cell, ColorPicker.Name(cell),
                    cell == _paint ? string.Empty : Lang.T("(e: pick this color)"));
            }
            else
            {
                ColorLine(area, 4, Lang.T("Color of the cell:"), null, Lang.T("[Default]: the row color"));
            }
        }

        // A line below the grid: a label, and the name of a color in the color (or a note, in the color of the
        // dividers), and a tip after it
        private void ColorLine(TabArea area, int line, string label, ConsoleColor? color, string name,
            string tip = "")
        {
            Tools.SetCursorEx(_gridTop + LogoArt.Rows + line, area.Left + 1);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Console.Write(label + " ");
            if (color is { } own) ConsoleColors.SetTextColor(own);
            else ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            var room = Math.Max(0, area.Left + area.Width - 1 - Console.CursorLeft);
            name = Tools.TruncateString(name, room, true);
            Console.Write(name);
            room -= TextMetrics.WidthOf(name);
            if (tip.Length > 0)
            {
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                tip = Tools.TruncateString("  " + tip, room, true);
                Console.Write(tip);
                room -= TextMetrics.WidthOf(tip);
            }

            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Console.Write(new string(' ', Math.Max(0, room)));
        }

        private void Move(int rows, int columns)
        {
            _row = Math.Clamp(_row + rows, 0, LogoArt.Rows - 1);
            _column = Math.Clamp(_column + columns, 0, LogoArt.Width - 1);
        }

        // Pick the color to paint with from the list of colors. The colors of the background cannot be picked
        private bool PickPaint()
        {
            if (!ColorPicker.Pick(DialogText.ColorCaption, PickerLeft, false, _paint,
                    color => !ConsoleColors.IsBackgroundColor(color), color =>
                    {
                        Console.BackgroundColor = ConsoleColors.Current.BackgroundColor;
                        Console.ForegroundColor = color;
                    }, out var picked) || picked == null)
            {
                return false;
            }

            _paint = picked;
            return true;
        }

        // Give the selected cells (or the cell of the cursor) a color of their own, or with null the color of
        // their rows again. A selection ends with it
        private TabResult Apply(ConsoleColor? color)
        {
            var (top, left, bottom, right) = Selection;
            Remember();
            for (var row = top; row <= bottom; row++)
            {
                for (var column = left; column <= right; column++) _editing.SetCellColor(row, column, color);
            }

            _changed = true;
            if (!_visual) return TabResult.None;
            _visual = false;
            return TabResult.Redraw;
        }

        public TabResult HandleKey(ConsoleKeyInfo key)
        {
            var pending = _pending;
            _pending = '\0';
            if (pending == 'g')
            {
                if (key.KeyChar == 'g') _row = 0;
                return TabResult.None;
            }

            if (key.Key == ConsoleKey.Spacebar)
            {
                // Paint: the color is asked for the first time
                var asked = _paint == null;
                if (asked && !PickPaint()) return TabResult.Redraw;
                var single = !_visual;
                var result = Apply(_paint);
                if (single) Move(0, 1);
                return asked ? TabResult.Redraw : result;
            }

            switch (Keyboard.ToNavigationKey(key).Key)
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
                    // The last character of the row (that is not blank space)
                    _column = Math.Max(0, _editing.Row(_row).TrimEnd().Length - 1);
                    return TabResult.None;
                case 'G':
                    _row = LogoArt.Rows - 1;
                    return TabResult.None;
                case 'g':
                    _pending = key.KeyChar;
                    return TabResult.None;
                case 'c':
                    PickPaint();
                    return TabResult.Redraw;
                case 'e':
                    // Paint with the color the cell of the cursor has of its own
                    if (_editing.CellColor(_row, _column) is { } own) _paint = own;
                    return TabResult.None;
                case 'x':
                    return Apply(null);
                case 'X':
                    // All the cells have the colors of their rows again
                    if (!_editing.HasCellColors) return TabResult.None;
                    Remember();
                    _editing.ClearCellColors();
                    _changed = true;
                    _visual = false;
                    return TabResult.Redraw;
                case 'v':
                    _visual = !_visual;
                    _anchorRow = _row;
                    _anchorColumn = _column;
                    return TabResult.Redraw;
                case 'u':
                    _visual = false;
                    UndoLast();
                    return TabResult.Redraw;
                case 'S':
                case 's':
                    Save();
                    return TabResult.Redraw;
            }

            if (key.Key == ConsoleKey.Escape && _visual)
            {
                _visual = false;
                return TabResult.Redraw;
            }

            if (key.Key is ConsoleKey.Escape or ConsoleKey.Q) return Close();
            return TabResult.None;
        }
    }

    /// <summary>
    /// The Rows page of the Colors tab: the logo at the top, and the color of each row below it, and a row to
    /// give them all one color (or the color of the logo again)
    /// </summary>
    private sealed class ColorsPage : ITabPage
    {
        private int _selected;
        private int _shown = -1;

        public string Title => Lang.T("Rows");
        public string Keys => DialogText.LogoColorKeys;
        public Tools.HelpTopic Help => Tools.HelpTopic.Logos;

        private const int ListTop = LogoArt.Rows + 2; // Below the logo and a line
        private const int Count = LogoArt.Rows + 1; // The rows, and all of them

        private static int RowOf(TabArea area, int selected) =>
            area.Top + ListTop + selected + (selected == LogoArt.Rows ? 1 : 0);

        public void Draw(TabArea area, bool full)
        {
            if (full)
            {
                DrawLogo(_editing, area.Top, area.Left + 2);
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                Tools.SetCursorEx(area.Top + LogoArt.Rows + 1, area.Left + 2);
                Console.Write(new string('-', Math.Min(LogoArt.Width, area.Width - 2)));
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                var sample = " " + Lang.T("Sample") + " ";
                var labels = Enumerable.Range(1, LogoArt.Rows).Select(row => Lang.F("Row {0}", row))
                    .Append(Lang.T("All rows")).ToList();
                var width = labels.Max(TextMetrics.WidthOf) + 2;
                for (var i = 0; i < Count; i++)
                {
                    Tools.SetCursorEx(RowOf(area, i), area.Left + 3);
                    Console.Write(labels[i] + new string(' ', width - TextMetrics.WidthOf(labels[i])));
                    if (i == LogoArt.Rows)
                    {
                        // All the rows: one color for all of them, or [Default] for the color of the logo
                        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                        Console.Write(Tools.TruncateString(Lang.T("Pick one color for all the rows ([Default]: reset)"),
                            Math.Max(0, area.Left + area.Width - Console.CursorLeft), true));
                        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                        continue;
                    }

                    var color = _editing.RowColor(i);
                    if (color is { } own) ConsoleColors.SetTextColor(own);
                    else ConsoleColors.SetColors(ConsoleColors.ColorPreset.HeaderColor);
                    Console.Write(sample);
                    ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                    Console.Write("  " + (color is { } named ? ColorPicker.Name(named) : Lang.T("[Default]: the logo color")));
                }

                _shown = -1;
            }

            // The pointer
            if (_shown >= 0 && _shown != _selected)
            {
                Tools.SetCursorEx(RowOf(area, _shown), area.Left);
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                Console.Write("  ");
            }

            Tools.SetCursorEx(RowOf(area, _selected), area.Left);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.SelectorInverted);
            Console.Write("->");
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            _shown = _selected;
        }

        public TabResult HandleKey(ConsoleKeyInfo key)
        {
            var navigation = Keyboard.ToNavigationKey(key).Key;
            if (key.Key is ConsoleKey.Escape or ConsoleKey.Q) return Close();
            if (key.Key == ConsoleKey.S)
            {
                Save();
                return TabResult.Redraw;
            }

            if (navigation == ConsoleKey.DownArrow)
            {
                _selected = (_selected + 1) % Count;
            }
            else if (navigation == ConsoleKey.UpArrow)
            {
                _selected = (_selected + Count - 1) % Count;
            }
            else if (key.KeyChar == 'G')
            {
                _selected = Count - 1;
            }
            else if (key.KeyChar == 'g')
            {
                if (Console.ReadKey(true).KeyChar == 'g') _selected = 0;
            }
            else if (key.Key is ConsoleKey.Enter or ConsoleKey.Spacebar || navigation == ConsoleKey.RightArrow)
            {
                Pick(_selected);
                return TabResult.Redraw;
            }

            return TabResult.None;
        }

        // Pick the color of a row (or of all of them) from the list of colors. [Default] is the color of the logo.
        // The colors of the background cannot be picked
        private static void Pick(int row)
        {
            var all = row == LogoArt.Rows;
            var current = all ? null : _editing.RowColor(row);
            if (!ColorPicker.Pick(DialogText.ColorCaption, PickerLeft, true, current,
                    color => !ConsoleColors.IsBackgroundColor(color), color =>
                    {
                        Console.BackgroundColor = ConsoleColors.Current.BackgroundColor;
                        Console.ForegroundColor = color;
                    }, out var picked))
            {
                return;
            }

            Remember();
            for (var i = 0; i < LogoArt.Rows; i++)
            {
                if (all || i == row) _editing.SetRowColor(i, picked);
            }

            _changed = true;
        }
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
                    if (i < LogoArt.BuiltInNames.Count)
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
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.SelectorInverted);
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
            else if (key.Key == ConsoleKey.R)
            {
                return Rename(names[_selected]);
            }
            else if (key.Key == ConsoleKey.D)
            {
                return Delete(names[_selected]);
            }

            return TabResult.None;
        }

        // Give a logo of the user's own another name (a built-in one cannot be renamed). The logo in use and
        // the one being edited follow their name
        private TabResult Rename(string name)
        {
            ChoiceBox.Forget();
            if (Logos.IsBuiltIn(name))
            {
                ChoiceBox.Message(DialogText.CaptionsLogos, DialogText.LogoBuiltInRename, string.Empty);
                return TabResult.Redraw;
            }

            var suggested = name;
            while (true)
            {
                ChoiceBox.Forget();
                var typed = ChoiceBox.AskText(DialogText.LogoRenameCaption, DialogText.LogoSavePrompt,
                    DialogText.NameColon, suggested);
                if (typed == null) return TabResult.Redraw;
                suggested = typed;
                var status = Logos.Rename(name, typed);
                if (status != DialogText.Success)
                {
                    ChoiceBox.Forget();
                    ChoiceBox.Message(DialogText.LogoRenameCaption, status, string.Empty);
                    continue;
                }

                var renamed = typed.Trim();
                if (name.Equals(_editing.Name, StringComparison.OrdinalIgnoreCase)) _editing.Name = renamed;
                if (name.Equals(Logo.Current.Name, StringComparison.OrdinalIgnoreCase))
                {
                    Logo.Current.Name = renamed;
                    FileIo.SaveSettings();
                }

                _selected = Math.Max(0, Logos.AllNames().FindIndex(logo =>
                    logo.Equals(renamed, StringComparison.OrdinalIgnoreCase)));
                return TabResult.Redraw;
            }
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

        // Delete a logo of the user's own (a built-in one cannot be deleted)
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
                // The logo being edited, and the ruler and the frame of the grid
                DrawName(area);
                DrawGridFrame(_gridTop, _gridLeft);
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
                    return UndoLast() ? TabResult.Redraw : TabResult.None;
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
