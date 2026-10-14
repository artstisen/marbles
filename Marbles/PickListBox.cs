namespace Marbles;

/// <summary>
/// A box on top of the page with a list of texts to pick one from (as the strings of the string memory,
/// ":reglist"): j/k (and gg/G, PgUp/PgDn) select, "/" filters the rows, Enter picks and Esc closes. The first
/// lines of the selected text are shown below the list
/// </summary>
internal static class PickListBox
{
    private const int PreviewRows = 5;

    // Where the parts of the box are
    private static int _top;
    private static int _left;
    private static int _inner;
    private static int _listTop;
    private static int _listRows;
    private static int _previewTop;
    private static int _previewRows;

    /// <summary>
    /// Show the list and let the user pick a row
    /// </summary>
    /// <param name="caption">The title of the box</param>
    /// <param name="rows">The rows (one line each), which the filter looks in</param>
    /// <param name="preview">The text shown below the list for a row</param>
    /// <param name="selected">The row selected from the start</param>
    /// <param name="hints">The keys, on the bottom line</param>
    /// <returns>The number of the row picked, or -1</returns>
    internal static int Show(string caption, IReadOnlyList<string> rows, Func<int, string> preview, int selected,
        string hints)
    {
        // In the color of text in frames and boxes
        using var frameText = ConsoleColors.FrameTextScope();

        var filter = string.Empty;
        var filtering = false;
        var view = Enumerable.Range(0, rows.Count).ToList();
        selected = Math.Max(0, view.IndexOf(selected));
        var scroll = 0;
        var shownScroll = -1;
        var shownSelected = -1;
        DrawBox(caption);
        while (true)
        {
            selected = Math.Clamp(selected, 0, Math.Max(0, view.Count - 1));
            if (selected < scroll) scroll = selected;
            if (selected >= scroll + _listRows) scroll = selected - _listRows + 1;
            scroll = Math.Clamp(scroll, 0, Math.Max(0, view.Count - _listRows));
            if (scroll != shownScroll)
            {
                DrawList(rows, view, scroll, filter);
                shownScroll = scroll;
                shownSelected = -1;
            }

            if (selected != shownSelected)
            {
                DrawPointers(view, scroll, selected);
                DrawPreview(view.Count > 0 ? preview(view[selected]) : string.Empty);
                shownSelected = selected;
            }

            DrawFilter(filter, filtering);
            ChoiceBox.WriteHints(filtering ? DialogText.SnippetFilterKeys : hints);
            if (filtering)
            {
                Tools.SetCursorEx(_top + 2, _left + 3 + TextMetrics.WidthOf(FilterText(filter)));
                Tools.ShowCursor();
            }
            else
            {
                Tools.SetCursorEx(Init.LineCount - 1, 0);
            }

            var key = Console.ReadKey(true);
            Tools.HideCursor();
            if (filtering)
            {
                if (key.Key is ConsoleKey.Tab or ConsoleKey.DownArrow or ConsoleKey.UpArrow)
                {
                    // Between the rows that match while typing (Shift+Tab, Up: back), going round
                    var back = key.Key == ConsoleKey.UpArrow ||
                               (key.Key == ConsoleKey.Tab && (key.Modifiers & ConsoleModifiers.Shift) != 0);
                    selected = PickListBox.NextMatch(selected, view.Count, back);
                    continue;
                }

                var before = filter;
                if (key.Key == ConsoleKey.Enter)
                {
                    filtering = false;
                }
                else if (key.Key == ConsoleKey.Escape)
                {
                    filtering = false;
                    filter = string.Empty;
                }
                else if (key.Key == ConsoleKey.Backspace)
                {
                    if (filter.Length > 0)
                    {
                        filter = filter[..^TextMetrics.PreviousElementLength(filter, filter.Length)];
                    }
                }
                else if (!char.IsControl(key.KeyChar) && filter.Length < 40)
                {
                    filter += key.KeyChar;
                }

                if (filter != before)
                {
                    view = Filter(rows, filter);
                    selected = 0;
                    shownScroll = -1;
                }

                continue;
            }

            var navigation = Keyboard.ToNavigationKey(key).Key;
            var c = key.KeyChar;
            if (key.Key == ConsoleKey.Escape && filter.Length > 0)
            {
                // Esc takes the filter away first
                var current = view.Count > 0 ? view[selected] : -1;
                filter = string.Empty;
                view = Filter(rows, filter);
                selected = Math.Max(0, view.IndexOf(current));
                shownScroll = -1;
                continue;
            }

            if (key.Key == ConsoleKey.Escape || c is 'q' or 'Q' || navigation == ConsoleKey.LeftArrow) return -1;
            if ((key.Key == ConsoleKey.Enter || navigation == ConsoleKey.RightArrow) && view.Count > 0)
            {
                return view[selected];
            }

            if (navigation == ConsoleKey.DownArrow) selected = Math.Min(selected + 1, Math.Max(0, view.Count - 1));
            if (navigation == ConsoleKey.UpArrow) selected = Math.Max(selected - 1, 0);
            if (key.Key == ConsoleKey.PageDown) selected = Math.Min(selected + _listRows, Math.Max(0, view.Count - 1));
            if (key.Key == ConsoleKey.PageUp) selected = Math.Max(selected - _listRows, 0);
            if (c == 'G') selected = Math.Max(0, view.Count - 1);
            if (c == 'g' && Console.ReadKey(true).KeyChar == 'g') selected = 0;
            if (c == '/') filtering = true;
        }
    }

    /// <summary>
    /// The next (or the one before) of a number of rows, going round from the last to the first
    /// </summary>
    internal static int NextMatch(int selected, int count, bool back)
    {
        if (count <= 0) return 0;
        return (selected + (back ? count - 1 : 1)) % count;
    }

    /// <summary>
    /// The rows that have the filter in them (upper and lower case alike)
    /// </summary>
    internal static List<int> Filter(IReadOnlyList<string> rows, string filter)
    {
        return Enumerable.Range(0, rows.Count)
            .Where(i => filter.Length == 0 || rows[i].Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    #region Drawing

    private static void DrawBox(string caption)
    {
        ChoiceBox.Forget();
        var inner = Init.LineWidth - 6;
        // (and the tab of the title above it)
        var height = Math.Max(8, Init.LineCount - 5);
        _previewRows = Math.Clamp(height - 13, 1, PreviewRows);
        // A blank row, the filter, a blank row, the list, a blank row, the preview and a blank row
        _listRows = height - 5 - _previewRows;
        var (top, left, width, _) = ChoiceBox.DrawFrame(caption, inner, height);
        _top = top;
        _left = left;
        _inner = width;
        _listTop = top + 4;
        _previewTop = _listTop + _listRows + 1;
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Tools.SetCursorEx(_previewTop - 1, left + 3);
        Console.Write(new string('-', width - 4));
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    private static string FilterText(string filter) => Lang.F("Filter: {0}", filter);

    private static void DrawFilter(string filter, bool filtering)
    {
        var text = filtering || filter.Length > 0 ? FilterText(filter) : string.Empty;
        Tools.SetCursorEx(_top + 2, _left + 3);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(Tools.TruncateString(text, _inner - 4, true).PadRight(_inner - 4));
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    private static void DrawList(IReadOnlyList<string> rows, List<int> view, int scroll, string filter)
    {
        for (var row = 0; row < _listRows; row++)
        {
            Tools.SetCursorEx(_listTop + row, _left + 1);
            var i = scroll + row;
            var text = i < view.Count
                ? "     " + Tools.TruncateString(rows[view[i]], _inner - 8, true)
                : i == 0 && filter.Length > 0
                    ? "     " + DialogText.PickListNoMatch
                    : string.Empty;
            Console.Write(text + new string(' ', Math.Max(0, _inner - TextMetrics.WidthOf(text))));
        }

        // More above or below
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Tools.SetCursorEx(_listTop, _left + _inner - 2);
        Console.Write(scroll > 0 ? "^" : " ");
        Tools.SetCursorEx(_listTop + _listRows - 1, _left + _inner - 2);
        Console.Write(scroll + _listRows < view.Count ? "v" : " ");
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    private static void DrawPointers(List<int> view, int scroll, int selected)
    {
        for (var row = 0; row < _listRows && scroll + row < view.Count; row++)
        {
            Tools.SetCursorEx(_listTop + row, _left + 2);
            var isSelected = scroll + row == selected;
            ConsoleColors.SetColors(isSelected
                ? ConsoleColors.ColorPreset.SelectorInverted
                : ConsoleColors.ColorPreset.Normal);
            Console.Write(isSelected ? "->" : "  ");
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    private static void DrawPreview(string text)
    {
        var lines = text.Length == 0 ? Array.Empty<string>() : text.Split('\n');
        for (var row = 0; row < _previewRows; row++)
        {
            Tools.SetCursorEx(_previewTop + row, _left + 1);
            var line = row < lines.Length
                ? "  " + Tools.TruncateString(lines[row].Replace("\t", "    "), _inner - 4, true)
                : string.Empty;
            if (row == _previewRows - 1 && lines.Length > _previewRows) line = "  ...";
            Console.Write(line + new string(' ', Math.Max(0, _inner - TextMetrics.WidthOf(line))));
        }
    }

    #endregion
}
