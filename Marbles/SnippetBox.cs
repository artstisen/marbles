namespace Marbles;

/// <summary>
/// The box of the snippets, on top of the editor (":snip"): a list of their titles, with the first lines of the
/// selected snippet below it. Enter inserts the snippet, "/" filters the titles, S changes the order, A adds a
/// snippet, R renames one, D deletes one and E opens the file of the snippets at the selected one.
/// The snippets themselves are kept by the class Snippets.
/// </summary>
internal static class SnippetBox
{
    internal enum Action
    {
        None,
        Insert, // Insert the text
        Edit // Open the file of the snippets at the line
    }

    internal readonly record struct Result(Action Action, string Text, int Line);

    private const int PreviewRows = 5;

    // True while the box of the snippets is shown: a box shown on top of it (to rename ...) is drawn by itself
    // (else it covers the box before it, as with the boxes of ":snip add")
    private static bool _inBox;

    // Where the parts of the box are
    private static int _top;
    private static int _left;
    private static int _inner;
    private static int _listTop;
    private static int _listRows;
    private static int _previewTop;
    private static int _previewRows;

    /// <summary>
    /// Show the snippets. If there is no file of snippets, it is made if the user wants that
    /// </summary>
    /// <param name="selection">The text selected in the editor (added with A), or empty</param>
    /// <param name="syntax">The kind of text being edited: the snippets for it are shown first</param>
    /// <param name="fromNote">True when a note of the data file is edited (adding its text is asked about)</param>
    internal static Result Show(string selection, SyntaxType syntax, bool fromNote)
    {
        if (!EnsureFile()) return default;
        var status = Snippets.Load();
        if (status != DialogText.Success)
        {
            ShowMessage(status);
            return default;
        }

        _inBox = true;
        try
        {
            return ShowList(selection, syntax, fromNote);
        }
        finally
        {
            _inBox = false;
        }
    }

    private static Result ShowList(string selection, SyntaxType syntax, bool fromNote)
    {
        var filter = string.Empty;
        var filtering = false;
        var view = Snippets.View(filter, Snippets.Sort, syntax);
        var selected = 0;
        var scroll = 0;
        var drawBox = true;
        var shownScroll = -1;
        var shownSelected = -1;
        while (true)
        {
            if (drawBox)
            {
                DrawBox();
                drawBox = false;
                shownScroll = -1;
            }

            selected = Math.Clamp(selected, 0, Math.Max(0, view.Count - 1));
            if (selected < scroll) scroll = selected;
            if (selected >= scroll + _listRows) scroll = selected - _listRows + 1;
            scroll = Math.Clamp(scroll, 0, Math.Max(0, view.Count - _listRows));
            if (scroll != shownScroll)
            {
                DrawList(view, scroll, filter);
                shownScroll = scroll;
                shownSelected = -1;
            }

            if (selected != shownSelected)
            {
                DrawPointers(view, scroll, selected);
                DrawPreview(view.Count > 0 ? Snippets.Items[view[selected]].Text : string.Empty);
                shownSelected = selected;
            }

            DrawStatus(filter, filtering);
            ChoiceBox.WriteHints(filtering ? DialogText.SnippetFilterKeys : DialogText.SnippetKeys);
            if (filtering)
            {
                // The cursor after the filter
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
                    view = Snippets.View(filter, Snippets.Sort, syntax);
                    selected = 0;
                    scroll = 0;
                    shownScroll = -1;
                }

                continue;
            }

            var navigation = Keyboard.ToNavigationKey(key).Key;
            var c = key.KeyChar;
            if (key.Key == ConsoleKey.Escape && filter.Length > 0)
            {
                // Esc takes the filter away first
                filter = string.Empty;
                view = Snippets.View(filter, Snippets.Sort, syntax);
                selected = 0;
                shownScroll = -1;
                continue;
            }

            if (key.Key == ConsoleKey.Escape || c is 'q' or 'Q' || navigation == ConsoleKey.LeftArrow)
            {
                return default;
            }

            if ((key.Key == ConsoleKey.Enter || navigation == ConsoleKey.RightArrow) && view.Count > 0)
            {
                var snippet = Snippets.Items[view[selected]];
                Snippets.MarkUsed(snippet.Title);
                return new Result(Action.Insert, snippet.Text, snippet.Line);
            }

            if (navigation == ConsoleKey.DownArrow) selected = Math.Min(selected + 1, Math.Max(0, view.Count - 1));
            if (navigation == ConsoleKey.UpArrow) selected = Math.Max(selected - 1, 0);
            if (key.Key == ConsoleKey.PageDown) selected = Math.Min(selected + _listRows, Math.Max(0, view.Count - 1));
            if (key.Key == ConsoleKey.PageUp) selected = Math.Max(selected - _listRows, 0);
            if (c == 'G') selected = Math.Max(0, view.Count - 1);
            if (c == 'g' && Console.ReadKey(true).KeyChar == 'g') selected = 0;
            switch (c)
            {
                case '/':
                    filtering = true;
                    break;
                case 's' or 'S':
                {
                    // The next order, which is remembered
                    Snippets.Sort = Snippets.Sort switch
                    {
                        Snippets.SortOrder.File => Snippets.SortOrder.Title,
                        Snippets.SortOrder.Title => Snippets.SortOrder.Used,
                        _ => Snippets.SortOrder.File
                    };
                    FileIo.SaveSettings();
                    var current = view.Count > 0 ? view[selected] : -1;
                    view = Snippets.View(filter, Snippets.Sort, syntax);
                    selected = Math.Max(0, view.IndexOf(current));
                    shownScroll = -1;
                    break;
                }
                case 'a' or 'A':
                {
                    var title = Add(selection, fromNote);
                    drawBox = true;
                    if (title == null) break;
                    var index = Snippets.IndexOf(title);
                    // A snippet without text is written in the file
                    if (Snippets.Items[index].Text.Length == 0)
                    {
                        return new Result(Action.Edit, string.Empty, Snippets.Items[index].Line);
                    }

                    filter = string.Empty;
                    view = Snippets.View(filter, Snippets.Sort, syntax);
                    selected = Math.Max(0, view.IndexOf(index));
                    break;
                }
                case 'r' or 'R' when view.Count > 0:
                {
                    var index = view[selected];
                    Rename(index);
                    drawBox = true;
                    view = Snippets.View(filter, Snippets.Sort, syntax);
                    selected = Math.Max(0, view.IndexOf(index));
                    break;
                }
                case 'd' or 'D' when view.Count > 0:
                    Delete(view[selected]);
                    drawBox = true;
                    view = Snippets.View(filter, Snippets.Sort, syntax);
                    break;
                case 'e' or 'E':
                    return new Result(Action.Edit, string.Empty, view.Count > 0 ? Snippets.Items[view[selected]].Line : 0);
            }
        }
    }

    /// <summary>
    /// Add a text as a snippet (":snip add"): the title is asked for. If the text is from a note of the data file,
    /// the user is asked first, as the file of the snippets is not encrypted
    /// </summary>
    /// <returns>The title of the snippet added, or null</returns>
    internal static string? Add(string text, bool fromNote)
    {
        if (!EnsureFile()) return null;
        var status = Snippets.Load();
        if (status != DialogText.Success)
        {
            ShowMessage(status);
            return null;
        }

        if (fromNote && text.Trim().Length > 0)
        {
            Cover();
            if (!ChoiceBox.Confirm(DialogText.CaptionsSnippets, DialogText.SnippetFromNote,
                    DialogText.SnippetFromNoteNote, true))
            {
                return null;
            }
        }

        var title = AskTitle(DialogText.SnippetAdd, DialogText.SnippetAddPrompt, string.Empty, -1);
        if (title == null || !Snippets.Add(title, text)) return null;
        status = Snippets.Save();
        if (status == DialogText.Success) return title;
        ShowMessage(status);
        return null;
    }

    /// <summary>
    /// Make the file of the snippets if there is none, if the user wants that
    /// </summary>
    /// <returns>True if there is a file</returns>
    internal static bool EnsureFile()
    {
        if (Snippets.Exists) return true;
        Cover();
        if (!ChoiceBox.Confirm(DialogText.CaptionsSnippets, DialogText.SnippetCreate,
                Lang.F("It is made in the data directory as {0}, with two examples.", Snippets.FileName), false))
        {
            return false;
        }

        var status = Snippets.Create();
        if (status == DialogText.Success) return true;
        ShowMessage(status);
        return false;
    }

    private static void Rename(int index)
    {
        var title = AskTitle(DialogText.SnippetRename, DialogText.SnippetRenamePrompt, Snippets.Items[index].Title,
            index);
        if (title == null || title == Snippets.Items[index].Title || !Snippets.Rename(index, title)) return;
        var status = Snippets.Save();
        if (status != DialogText.Success) ShowMessage(status);
    }

    private static void Delete(int index)
    {
        Cover();
        if (!ChoiceBox.Confirm(DialogText.CaptionsSnippets,
                Lang.F("Delete the snippet \"{0}\"?", Snippets.Items[index].Title), DialogText.SnippetDeleteNote,
                true))
        {
            return;
        }

        Snippets.Delete(index);
        var status = Snippets.Save();
        if (status != DialogText.Success) ShowMessage(status);
    }

    // Ask for a title until one is given that is not taken (null: canceled)
    private static string? AskTitle(string caption, string prompt, string text, int index)
    {
        while (true)
        {
            Cover();
            var title = ChoiceBox.AskText(caption, prompt, DialogText.TitleColon, text);
            if (title == null) return null;
            if (!Snippets.TitleExists(title, index)) return title;
            Cover();
            ChoiceBox.Message(caption, DialogText.SnippetTitleTaken, string.Empty);
            text = title;
        }
    }

    // A box on top of the box of the snippets is drawn by itself
    private static void Cover()
    {
        if (_inBox) ChoiceBox.Forget();
    }

    private static void ShowMessage(string message)
    {
        Cover();
        ChoiceBox.Message(DialogText.CaptionsSnippets, message, string.Empty);
    }

    #region Drawing

    // The frame, and where the parts of the box go: a row for the order and the filter, the list, and the
    // first lines of the selected snippet
    private static void DrawBox()
    {
        ChoiceBox.Forget();
        var inner = Init.LineWidth - 6;
        // (and the tab of the title above it)
        var height = Math.Max(8, Init.LineCount - 5);
        _previewRows = Math.Clamp(height - 13, 1, PreviewRows);
        // A blank row, the order and filter, a blank row, the list, a blank row, the preview and a blank row
        _listRows = height - 5 - _previewRows;
        var (top, left, width, _) = ChoiceBox.DrawFrame(DialogText.CaptionsSnippets, inner, height);
        _top = top;
        _left = left;
        _inner = width;
        _listTop = top + 4;
        _previewTop = _listTop + _listRows + 1;
        // A line above the preview
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Tools.SetCursorEx(_previewTop - 1, left + 3);
        Console.Write(new string('-', width - 4));
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    private static string FilterText(string filter) => Lang.F("Filter: {0}", filter);

    // The order of the list and the filter
    private static void DrawStatus(string filter, bool filtering)
    {
        var order = Snippets.Sort switch
        {
            Snippets.SortOrder.Title => DialogText.SnippetSortTitle,
            Snippets.SortOrder.Used => DialogText.SnippetSortUsed,
            _ => DialogText.SnippetSortFile
        };
        var text = filtering || filter.Length > 0 ? FilterText(filter) + "    " + order : order;
        Tools.SetCursorEx(_top + 2, _left + 3);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(Tools.TruncateString(text, _inner - 4, true).PadRight(_inner - 4));
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    private static void DrawList(List<int> view, int scroll, string filter)
    {
        for (var row = 0; row < _listRows; row++)
        {
            Tools.SetCursorEx(_listTop + row, _left + 1);
            var i = scroll + row;
            string text;
            if (i < view.Count)
            {
                var title = Snippets.Items[view[i]].Title;
                text = "     " + TextMetrics.Printable(Tools.TruncateString(title.Length == 0 ? "-" : title, _inner - 8, true));
            }
            else if (i == 0)
            {
                text = "     " + (filter.Length > 0 ? DialogText.SnippetNoMatch : DialogText.SnippetNone);
            }
            else
            {
                text = string.Empty;
            }

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

    // The pointer at the selected title (the others are blank)
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

    // The first lines of the selected snippet
    private static void DrawPreview(string text)
    {
        var lines = text.Length == 0 ? Array.Empty<string>() : text.Split('\n');
        for (var row = 0; row < _previewRows; row++)
        {
            Tools.SetCursorEx(_previewTop + row, _left + 1);
            var line = row < lines.Length
                ? "  " + TextMetrics.Printable(Tools.TruncateString(lines[row].Replace("\t", "    "), _inner - 4, true))
                : string.Empty;
            // The rows of a long snippet that are not shown
            if (row == _previewRows - 1 && lines.Length > _previewRows) line = "  ...";
            Console.Write(line + new string(' ', Math.Max(0, _inner - TextMetrics.WidthOf(line))));
        }
    }

    #endregion
}
