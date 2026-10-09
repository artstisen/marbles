using System.Text;

namespace Marbles;

/* Console Text Editor: TextArea v2.0 (Lars Brandt Stisen, 2026) */
/* The text area control places an editor in a rectangle of the console window. It reads the keyboard,
   passes the keys to the EditorEngine (which holds the text, the cursor and the Normal/Insert/Visual modes)
   and draws the result. Only the rows that have changed are written to the console.

   The control has three forms:

   * Editor (height above 1): a modal editor in the style of Vim. The bottom line of the window shows the
     mode, messages, the ":" command line and the cursor position. See Tools.WriteEditorHelp for the keys.

   * Single line editor (height of 1): used for titles, file names and search queries. It is always in
     Insert mode. Enter accepts the text and Esc cancels (see the Canceled property).

   * Read only: the text is displayed and the constructor returns right away. The caller can scroll the
     text with PageUp and PageDown.

   Example:
     var textArea = new TextArea(0, 4, Init.LineWidth - 1, Init.LineCount - 5, "Some text...", 0, false, false, false, 0);
     if (!textArea.Canceled)
     {
         var text = textArea.GetText();
     }
 */

public class TextArea
{
    private const char StyleMarker = '\u0001'; // Separates the differently colored parts of a row
    private const int StyleNormal = 0;
    private const int StyleSelection = 1;
    private const int StyleSearchMatch = 2;
    private const int StyleMisspelled = 3;
    private const int StyleMarkdown = 10; // The styles of Markdown follow from this number

    private readonly EditorEngine _engine;
    private readonly int _top;
    private readonly int _left;
    private readonly int _width;
    private readonly int _height;
    private readonly bool _singleLine;
    private readonly bool _readOnly;
    internal const int SaveAsEntry = 0;
    internal const int SaveAsSettings = 1;
    internal const int SaveAsFile = 2; // A plain text file opened from the file browser
    private readonly int _saveMode;
    private readonly string _leftMargin;
    private bool _exit;

    // What is currently shown on screen, used to only write the rows that have changed
    private readonly string?[] _shownRows;
    private readonly int[] _shownCells;
    private string? _shownStatus;

    /// <summary>
    /// True if the editor was closed without accepting or saving the text
    /// </summary>
    public bool Canceled { get; private set; }

    internal bool SaveWasInvoked;

    /// <summary>
    /// Create an instance of the TextArea control
    /// </summary>
    /// <param name="left">Left padding (control placement in console window)</param>
    /// <param name="top">Top padding (control placement in console window)</param>
    /// <param name="width">Text area width</param>
    /// <param name="height">Text area height (a height of 1 creates a single line editor)</param>
    /// <param name="text">Optional text content</param>
    /// <param name="recallCursorIndex">The cursor position to recall</param>
    /// <param name="scrollToEnd">Place the cursor at the end of the supplied text</param>
    /// <param name="readOnly">Limit the control to read only</param>
    /// <param name="openSearchDialog">Automatically open the search prompt</param>
    /// <param name="saveMode">Set which save method to call</param>
    public TextArea(int left, int top, int width, int height, string text, int recallCursorIndex, bool scrollToEnd,
        bool readOnly, bool openSearchDialog, int saveMode) : this(left, top, width, height, text,
        recallCursorIndex, scrollToEnd, readOnly, openSearchDialog, saveMode, false)
    {
    }

    /// <param name="modified">Treat the text as changed from the start (it can be saved without editing it)</param>
    internal TextArea(int left, int top, int width, int height, string text, int recallCursorIndex, bool scrollToEnd,
        bool readOnly, bool openSearchDialog, int saveMode, bool modified)
    {
        _top = top;
        _left = left;
        _width = width;
        _height = Math.Max(1, height);
        _singleLine = height == 1;
        _readOnly = readOnly;
        _saveMode = saveMode;
        _leftMargin = _singleLine ? " " : string.Empty;
        _shownRows = new string?[_height];
        _shownCells = new int[_height];
        Canceled = false;
        SaveWasInvoked = false;

        _engine = new EditorEngine(text ?? string.Empty, width - 1, _height, _singleLine)
        {
            MaxCells = width - 3
        };
        // Markdown files are colored (unless that is turned off)
        _engine.Markdown = saveMode == SaveAsFile && !_singleLine && !readOnly && MarkdownHighlighter.ColorFiles &&
                           MarkdownHighlighter.IsMarkdownFile(FileExplorer.CurrentFileName);
        if (scrollToEnd)
        {
            _engine.CursorToEnd();
        }
        else if (!_singleLine && recallCursorIndex > 0)
        {
            _engine.SetCursorIndex(recallCursorIndex);
        }

        if (_readOnly)
        {
            // Clear the area and show the text with some rows of context below the recalled cursor position
            Array.Fill(_shownCells, width - 1);
            _engine.ShowRowsBelowCursor(5);
            Draw();
            return;
        }

        if (_singleLine) Array.Fill(_shownCells, width - 1);
        DocumentInfo.HasChanged = false;
        Menu.InitializeSaveIndicator();
        if (modified)
        {
            _engine.MarkModified();
            UpdateChangeIndicator();
        }

        if (openSearchDialog && !_engine.Buffer.IsEmpty)
        {
            _engine.OpenSearch();
        }

        Draw();
        MainLoop();
    }

    /// <summary>
    /// Retrieve the text content of the TextArea control
    /// </summary>
    internal string GetText()
    {
        return _engine.GetText();
    }

    internal int GetCursorIndex()
    {
        return _engine.GetCursorIndex();
    }

    public void ClearUndoRedoCache()
    {
        _engine.Buffer.ClearHistory();
    }

    #region Read only scrolling

    /// <summary>
    /// True if the text has more rows than the text area can show
    /// </summary>
    internal bool CanScroll => _engine.CanScroll;

    internal void PageUp()
    {
        ScrollRows(-(_height - 1));
    }

    internal void PageDown()
    {
        ScrollRows(_height - 1);
    }

    internal void ScrollRows(int rows)
    {
        _engine.ScrollView(rows);
        Draw();
    }

    #endregion

    #region Main loop (keyboard input)

    private void MainLoop()
    {
        try
        {
            while (!_exit)
            {
                var key = Console.ReadKey(true);
                var request = _engine.HandleKey(key);
                if (request != EditorRequest.None)
                {
                    HandleRequest(request);
                }

                if (_exit) break;
                UpdateChangeIndicator();
                // When more keys are waiting (as when pasting text) they are all handled before drawing
                if (IsKeyWaiting()) continue;
                Draw();
            }
        }
        catch (Exception ex) when (ex is not NavigationException)
        {
            Tools.HideCursor();
            Tools.ActionUnsupported(ex.Message);
            // Offer to save before closing
            Canceled = true;
            if (!_singleLine && _engine.IsModified &&
                Menu.YesNoCancelPrompt(DialogText.Save, DialogText.SaveChangesYn, string.Empty, 0, string.Empty,
                    true) == 1)
            {
                Save();
                Canceled = false;
            }
        }

        Tools.HideCursor();
    }

    private static bool IsKeyWaiting()
    {
        try
        {
            return Console.KeyAvailable;
        }
        catch
        {
            return false;
        }
    }

    private void HandleRequest(EditorRequest request)
    {
        switch (request)
        {
            case EditorRequest.Save:
                if (!_engine.IsModified)
                {
                    _engine.SetMessage(DialogText.EditorNoChanges, false);
                }
                else if (Save())
                {
                    _engine.SetMessage(DialogText.EditorSaved, false);
                    RedrawAll();
                }

                break;
            case EditorRequest.SaveAndQuit:
                // Stay in the editor if the text could not be saved
                if (!_engine.IsModified || Save())
                {
                    Canceled = false;
                    _exit = true;
                }

                break;
            case EditorRequest.Quit:
                Quit();
                break;
            case EditorRequest.ForceQuit:
            case EditorRequest.Cancel:
                Canceled = true;
                _exit = true;
                break;
            case EditorRequest.Accept:
                Canceled = false;
                _exit = true;
                break;
            case EditorRequest.Help:
                Tools.HideCursor();
                Tools.WriteEditorHelp(false, Tools.HelpTopic.Editor);
                RedrawAll();
                break;
            case EditorRequest.InsertCalendar:
                Tools.HideCursor();
                _engine.InsertAtCursor(IsoDateTime.ConfigureCalendar());
                RedrawAll();
                break;
            case EditorRequest.ClearAll:
                Tools.HideCursor();
                if (Menu.YesNoPrompt(DialogText.CaptionsClear, DialogText.ClearTextYn, string.Empty, false))
                {
                    _engine.ClearAll();
                }

                RedrawAll();
                break;
            case EditorRequest.ClearRegisters:
                Tools.HideCursor();
                if (Menu.YesNoPrompt(DialogText.CaptionsStringMemory, DialogText.ClearStringMemory, string.Empty,
                        false))
                {
                    Tools.ClearLineMemory();
                }

                RedrawAll();
                break;
            case EditorRequest.SpellSuggest:
                ShowSpellSuggestions();
                break;
            case EditorRequest.ReplaceDialog:
                ShowReplaceDialog();
                break;
            case EditorRequest.Reload:
            case EditorRequest.ReloadForce:
                ReloadFile(request == EditorRequest.ReloadForce);
                break;
            case EditorRequest.SyntaxChanged:
                // The choice is remembered for Markdown files
                if (_saveMode == SaveAsFile && MarkdownHighlighter.IsMarkdownFile(FileExplorer.CurrentFileName))
                {
                    MarkdownHighlighter.ColorFiles = _engine.Markdown;
                    FileIo.SaveSettings();
                }

                break;
            case EditorRequest.SaveSettings:
                // Not while the settings file itself is being edited
                if (_saveMode != SaveAsSettings) FileIo.SaveSettings();
                break;
        }
    }

    // Read the file from disk again. Changes that are not saved are only given up if that is asked for
    private void ReloadFile(bool force)
    {
        if (_saveMode != SaveAsFile)
        {
            // An entry of a data file cannot be changed by another program
            _engine.SetMessage(DialogText.EditorReloadFilesOnly, true);
            return;
        }

        if (_engine.IsModified && !force)
        {
            _engine.SetMessage(DialogText.EditorReloadUnsaved, true);
            return;
        }

        var status = FileExplorer.ReloadCurrentFile(out var text);
        if (status != DialogText.Success)
        {
            _engine.SetMessage(status, true);
            return;
        }

        _engine.ReloadText(text);
        UpdateChangeIndicator();
        _engine.SetMessage(DialogText.EditorReloaded, false);
    }

    // Show the find-and-replace box on top of the text, and replace as asked for
    private void ShowReplaceDialog()
    {
        const int firstRow = 1;
        DrawStatusLine(); // Remove the command from the bottom line
        var left = Math.Max(0, (Init.LineWidth - ReplaceDialog.Width) / 2 - _left);
        var lines = _engine.ReplaceLines;
        if (ReplaceDialog.Show(_top + firstRow, _left + left, lines != null, _engine.ReplaceFind, out var scope))
        {
            var cursorLine = _engine.Cursor.Line;
            var (first, last) = scope switch
            {
                ReplaceDialog.Scope.SelectedLines when lines != null => lines.Value,
                ReplaceDialog.Scope.CurrentLine => (cursorLine, cursorLine),
                _ => (0, _engine.Buffer.LineCount - 1)
            };
            _engine.Replace(ReplaceDialog.Find, ReplaceDialog.Replacement, ReplaceDialog.MatchCase,
                ReplaceDialog.IsRegex, first, last);
        }

        // Write the rows that were covered again
        for (var r = firstRow; r < firstRow + ReplaceDialog.Height && r < _height; r++)
        {
            _shownRows[r] = null;
            _shownCells[r] = Math.Max(_shownCells[r], left + ReplaceDialog.Width);
        }

        UpdateChangeIndicator();
        Draw();
    }

    // Show suggestions for the word at the cursor in a box on top of the text. The word is replaced with the
    // suggestion picked (with its number, or with j/k and Enter), or added to the word list with "a"
    private void ShowSpellSuggestions()
    {
        Tools.HideCursor();
        DrawStatusLine(); // Remove the command or pending key from the bottom line
        var word = _engine.SpellWord();
        var suggestions = _engine.SpellSuggestions();
        if (suggestions.Count > _height - 3) suggestions = suggestions.Take(Math.Max(0, _height - 3)).ToList();
        var rows = new List<string>();
        for (var i = 0; i < suggestions.Count; i++)
        {
            rows.Add((i + 1) + "  " + suggestions[i]);
        }

        if (rows.Count == 0) rows.Add("-  " + DialogText.SpellNoSuggestions.TrimEnd(' ', ':'));
        rows.Add("a  " + DialogText.SpellAddWord);
        var caption = " " + DialogText.SpellSuggestions + ": " + (word.Length > 24 ? word[..24] : word) + " ";
        var inner = Math.Max(rows.Max(TextMetrics.WidthOf), TextMetrics.WidthOf(caption)) + 4;

        // Below the word if there is room for it, else above
        var cursor = _engine.Cursor;
        var cursorRow = _engine.Layout.RowOf(cursor) - _engine.TopRow;
        var boxHeight = rows.Count + 2;
        var firstRow = cursorRow + 1;
        if (firstRow + boxHeight > _height) firstRow = cursorRow - boxHeight;
        firstRow = Math.Clamp(firstRow, 0, Math.Max(0, _height - boxHeight));
        var left = Math.Max(0,
            Math.Min(_leftMargin.Length + _engine.Layout.CellOf(cursor), Init.LineWidth - 1 - _left - inner - 2));
        var selected = 0;
        var picked = -1;
        var add = false;
        while (true)
        {
            DrawBox(firstRow, left, inner, caption, rows, selected);
            var key = Console.ReadKey(true);
            var c = key.KeyChar;
            if (key.Key == ConsoleKey.Escape || c == 'q') break;
            if (key.Key == ConsoleKey.DownArrow || c == 'j')
            {
                selected = (selected + 1) % rows.Count;
            }
            else if (key.Key == ConsoleKey.UpArrow || c == 'k')
            {
                selected = (selected + rows.Count - 1) % rows.Count;
            }
            else if (key.Key == ConsoleKey.Enter)
            {
                if (selected < suggestions.Count) picked = selected;
                add = selected == rows.Count - 1;
                if (picked >= 0 || add) break;
            }
            else if (c == 'a')
            {
                add = true;
                break;
            }
            else if (c >= '1' && c - '1' < suggestions.Count)
            {
                picked = c - '1';
                break;
            }
        }

        if (picked >= 0) _engine.ReplaceSpellWord(suggestions[picked]);
        if (add) _engine.AddSpellWord();

        // Write the rows that were covered again
        for (var r = firstRow; r < firstRow + boxHeight && r < _height; r++)
        {
            _shownRows[r] = null;
            _shownCells[r] = Math.Max(_shownCells[r], left + inner + 2);
        }

        UpdateChangeIndicator();
        Draw();
    }

    private void DrawBox(int firstRow, int left, int inner, string caption, List<string> rows, int selected)
    {
        var column = _left + left;
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Tools.SetCursorEx(_top + firstRow, column);
        Console.Write(AsciiStyles.GetTopCorner() + new string('-', inner) + AsciiStyles.GetTopCorner());
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.HeaderColor);
        Tools.SetCursorEx(_top + firstRow, column + 2);
        Console.Write(caption);
        for (var i = 0; i < rows.Count; i++)
        {
            Tools.SetCursorEx(_top + firstRow + 1 + i, column);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write('|');
            ConsoleColors.SetColors(i == selected
                ? ConsoleColors.ColorPreset.HighlightInverted
                : ConsoleColors.ColorPreset.Normal);
            Console.Write(" " + rows[i] + new string(' ', inner - 1 - TextMetrics.WidthOf(rows[i])));
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write('|');
        }

        Tools.SetCursorEx(_top + firstRow + 1 + rows.Count, column);
        Console.Write(AsciiStyles.GetBottomCorner() + new string('-', inner) + AsciiStyles.GetBottomCorner());
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    // Close the editor, asking what to do with unsaved changes
    private void Quit()
    {
        if (!_engine.IsModified)
        {
            Canceled = true;
            _exit = true;
            return;
        }

        Tools.HideCursor();
        var result = AskToSave();
        switch (result)
        {
            case 1: // Yes, save
                if (Save())
                {
                    Canceled = false;
                    _exit = true;
                }
                else
                {
                    RedrawAll();
                }

                break;
            case 2: // No, dont save
                Canceled = true;
                _exit = true;
                break;
            case 4: // Save, return to edit
                Save();
                RedrawAll();
                break;
            default: // Cancel, return to edit (the box is removed by writing the rows it covered again)
                Draw();
                break;
        }
    }

    // Ask what to do with unsaved changes, in a box on top of the text.
    // Returns 1 (save and close), 2 (close without saving), 4 (save and continue) or 0 (cancel)
    private int AskToSave()
    {
        string[] choices =
        {
            DialogText.SaveChoiceYes, DialogText.SaveChoiceNo, DialogText.SaveChoiceContinue,
            DialogText.SaveChoiceCancel
        };
        int[] results = { 1, 2, 4, 0 };
        var inner = choices.Max(choice => choice.Length) + 8;
        var boxHeight = choices.Length + 4;
        var firstRow = Math.Max(0, (_height - boxHeight) / 2);
        var left = Math.Max(0, (Init.LineWidth - inner - 2) / 2 - _left);
        var column = _left + left;
        DrawStatusLine(); // Remove the command from the bottom line

        // The frame and the question
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Tools.SetCursorEx(_top + firstRow, column);
        Console.Write(AsciiStyles.GetTopCorner() + new string('-', inner) + AsciiStyles.GetTopCorner());
        for (var row = 1; row < boxHeight - 1; row++)
        {
            Tools.SetCursorEx(_top + firstRow + row, column);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write('|');
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Console.Write(new string(' ', inner));
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write('|');
        }

        Tools.SetCursorEx(_top + firstRow + boxHeight - 1, column);
        Console.Write(AsciiStyles.GetBottomCorner() + new string('-', inner) + AsciiStyles.GetBottomCorner());
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.HeaderColor);
        Tools.SetCursorEx(_top + firstRow, column + 2);
        Console.Write(" " + DialogText.Save + " ");
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
        Tools.SetCursorEx(_top + firstRow + 1, column + 2);
        Console.Write(" " + DialogText.SaveQuestion + " ");
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);

        var selected = 0;
        var result = 0;
        while (true)
        {
            for (var i = 0; i < choices.Length; i++)
            {
                Tools.SetCursorEx(_top + firstRow + 3 + i, column + 2);
                if (i == selected) ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
                Console.Write(i == selected ? "->" : "  ");
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                Console.Write(" " + choices[i]);
            }

            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Escape) break;
            if (key.Key == ConsoleKey.Y) result = 1;
            if (key.Key == ConsoleKey.N) result = 2;
            if (key.Key == ConsoleKey.E) result = 4;
            if (key.Key == ConsoleKey.Enter) result = results[selected];
            if (result != 0 || key.Key == ConsoleKey.Enter) break;
            if (key.Key is ConsoleKey.J or ConsoleKey.DownArrow) selected = (selected + 1) % choices.Length;
            if (key.Key is ConsoleKey.K or ConsoleKey.UpArrow)
            {
                selected = (selected + choices.Length - 1) % choices.Length;
            }
        }

        // The rows that were covered are written again when the editor is drawn
        for (var r = firstRow; r < firstRow + boxHeight && r < _height; r++)
        {
            _shownRows[r] = null;
            _shownCells[r] = Math.Max(_shownCells[r], left + inner + 2);
        }

        return result;
    }

    /// <returns>False if the text could not be saved (the reason is shown as a message)</returns>
    private bool Save()
    {
        if (!_engine.IsModified) return true;
        Tools.HideCursor();
        DocumentInfo.HasChanged = true;
        SaveWasInvoked = true;
        if (_saveMode == SaveAsSettings)
        {
            // Save as settings
            FileIo.WriteSettingsFile(GetText());
        }
        else if (_saveMode == SaveAsFile)
        {
            // Save as a plain text file
            var status = FileExplorer.SaveCurrentFile(GetText());
            if (status != DialogText.Success)
            {
                _engine.SetMessage(status, true);
                return false;
            }
        }
        else
        {
            // Save entry
            Actions.SaveContentDuringEdit(this);
        }

        _engine.MarkSaved();
        DocumentInfo.HasChanged = false;
        Menu.InitializeSaveIndicator();
        return true;
    }

    // Show or remove the "unsaved" indicator when the state changes
    private void UpdateChangeIndicator()
    {
        var modified = _engine.IsModified;
        if (modified == DocumentInfo.HasChanged) return;
        DocumentInfo.HasChanged = modified;
        if (_singleLine) return;
        if (modified)
        {
            Menu.IndicateChanges(true);
        }
        else
        {
            Menu.ClearChangeIndicator();
            Menu.InitializeSaveIndicator();
        }
    }

    #endregion

    #region Drawing

    // Redraw the whole screen (after a dialog or message has been shown on top of the editor)
    private void RedrawAll()
    {
        Tools.HideCursor();
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Tools.ClearScreen();
        switch (_saveMode)
        {
            case SaveAsSettings:
                Actions.DrawEditSettingsMenu();
                break;
            case SaveAsFile:
                Actions.DrawEditFileMenu();
                break;
            default:
                Actions.DrawEditMenu();
                break;
        }

        Menu.IndicateChanges(true);
        Array.Fill(_shownRows, null);
        Array.Fill(_shownCells, 0);
        _shownStatus = null;
        Draw();
    }

    private void Draw()
    {
        Tools.HideCursor();
        var buffer = _engine.Buffer;
        var layout = _engine.Layout;
        var (line, segment) = layout.LineAtRow(_engine.TopRow);
        var row = new StringBuilder();
        for (var r = 0; r < _height; r++)
        {
            row.Clear();
            var cells = 0;
            if (line < buffer.LineCount)
            {
                cells = BuildRow(row, line, segment);
                segment++;
                if (segment >= layout.RowCount(line))
                {
                    line++;
                    segment = 0;
                }
            }

            var content = row.ToString();
            if (content == _shownRows[r]) continue;
            WriteRow(r, content, cells);
        }

        if (_readOnly) return;
        if (!_singleLine) DrawStatusLine();
        PlaceCursor();
    }

    // Build the content of a row: the text of a segment of a line, split where the color changes
    private int BuildRow(StringBuilder row, int line, int segment)
    {
        var layout = _engine.Layout;
        var s = _engine.Buffer[line];
        var start = layout.SegmentStart(line, segment);
        var end = layout.SegmentEnd(line, segment);
        var cells = TextMetrics.WidthOfRange(s, start, end);
        var selection = _engine.Selection(line);
        var hasMatches = false;
        foreach (var _ in _engine.SearchMatches(line))
        {
            hasMatches = true;
            break;
        }

        // The common case is a row in a single color
        var misspelled = _engine.Misspelled(line);
        var markdown = _engine.MarkdownStyles(line);
        if (selection == null && !hasMatches && misspelled.Count == 0 && markdown == null)
        {
            row.Append(TextMetrics.Printable(s, start, end));
            return cells;
        }

        var styles = new int[end - start];
        if (markdown != null)
        {
            // The colors of Markdown are below the marks: misspelled words, search matches and the selection
            for (var i = start; i < end && i < markdown.Length; i++)
            {
                if (markdown[i] != MarkdownStyle.None) styles[i - start] = StyleMarkdown + (int)markdown[i];
            }
        }

        foreach (var (wordStart, wordEnd) in misspelled)
        {
            for (var i = Math.Max(start, wordStart); i < Math.Min(end, wordEnd); i++)
            {
                styles[i - start] = StyleMisspelled;
            }
        }

        foreach (var (matchStart, matchEnd) in _engine.SearchMatches(line))
        {
            for (var i = Math.Max(start, matchStart); i < Math.Min(end, matchEnd); i++)
            {
                styles[i - start] = StyleSearchMatch;
            }
        }

        if (selection != null)
        {
            for (var i = Math.Max(start, selection.Value.Start); i < Math.Min(end, selection.Value.End); i++)
            {
                styles[i - start] = StyleSelection;
            }
        }

        var runStart = start;
        for (var i = start; i <= end; i++)
        {
            if (i < end && styles[i - start] == styles[runStart - start]) continue;
            if (i > runStart)
            {
                row.Append(StyleMarker).Append((char)('0' + styles[runStart - start]));
                row.Append(TextMetrics.Printable(s, runStart, i));
            }

            runStart = i;
        }

        // Show that the end of the line is part of the selection (this also makes selected empty lines visible)
        if (selection != null && end == s.Length && selection.Value.End == s.Length &&
            (s.Length == 0 || _engine.Mode == EditorMode.VisualLine))
        {
            row.Append(StyleMarker).Append((char)('0' + StyleSelection)).Append(' ');
            cells++;
        }

        return cells;
    }

    // The colors of a part of a row
    private static void SetStyleColors(int style)
    {
        if (style < StyleMarkdown)
        {
            ConsoleColors.SetColors(style switch
            {
                StyleSelection => ConsoleColors.ColorPreset.HighlightInverted,
                StyleSearchMatch => ConsoleColors.ColorPreset.AttentionInverted,
                StyleMisspelled => ConsoleColors.ColorPreset.WarningInverted,
                _ => ConsoleColors.ColorPreset.Normal
            });
            return;
        }

        // Markdown: the headings have colors of their own, and the rest use the colors of the color scheme
        switch ((MarkdownStyle)(style - StyleMarkdown))
        {
            case MarkdownStyle.Heading1:
                ConsoleColors.SetTextColor(ConsoleColor.Red);
                break;
            case MarkdownStyle.Heading2:
                ConsoleColors.SetTextColor(ConsoleColor.Magenta);
                break;
            case MarkdownStyle.Heading3:
                ConsoleColors.SetTextColor(ConsoleColor.Yellow);
                break;
            case MarkdownStyle.Heading4:
                ConsoleColors.SetTextColor(ConsoleColor.Cyan);
                break;
            case MarkdownStyle.Heading5:
                ConsoleColors.SetTextColor(ConsoleColor.Green);
                break;
            case MarkdownStyle.Heading6:
                ConsoleColors.SetTextColor(ConsoleColor.Blue);
                break;
            case MarkdownStyle.Bold:
            case MarkdownStyle.ListMarker:
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.HeaderColor);
                break;
            case MarkdownStyle.Italic:
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.PreviewTitle);
                break;
            case MarkdownStyle.Code:
            case MarkdownStyle.CodeBlock:
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Favorites);
                break;
            case MarkdownStyle.Link:
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.EditorTitle);
                break;
            default:
                // Quotes, web addresses and rules
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
                break;
        }
    }

    private void WriteRow(int r, string content, int cells)
    {
        Tools.SetCursorEx(_top + r, _left);
        if (cells > 0 || _shownCells[r] > 0) Console.Write(_leftMargin);
        if (content.Contains(StyleMarker))
        {
            foreach (var run in content.Split(StyleMarker, StringSplitOptions.RemoveEmptyEntries))
            {
                SetStyleColors(run[0] - '0');
                Console.Write(run.AsSpan(1).ToString());
            }

            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        }
        else
        {
            Console.Write(content);
        }

        // Only overwrite what was there before. The rows are not padded with blank spaces, as these would
        // be included when copying text from the terminal window
        if (_shownCells[r] > cells)
        {
            Console.Write(new string(' ', _shownCells[r] - cells));
        }

        _shownRows[r] = content;
        _shownCells[r] = cells;
    }

    // The bottom line of the window: mode, messages or command line to the left and cursor position to the right
    private void DrawStatusLine()
    {
        var width = Init.LineWidth - 1;
        var label = string.Empty;
        var text = string.Empty;
        switch (_engine.Mode)
        {
            case EditorMode.Command:
            case EditorMode.Search:
                text = _engine.CommandPrefix + _engine.CommandText + " ";
                break;
            case EditorMode.Insert:
                label = DialogText.EditorModeInsert;
                break;
            case EditorMode.Visual:
                label = DialogText.EditorModeVisual;
                break;
            case EditorMode.VisualLine:
                label = DialogText.EditorModeVisualLine;
                break;
            default:
                label = DialogText.EditorModeNormal;
                break;
        }

        if (text.Length == 0 && _engine.Message.Length > 0)
        {
            text = " " + _engine.Message + " ";
        }

        // Pending keys, string memory, line:column and position in the document
        var cursor = _engine.Cursor;
        var rows = _engine.Layout.TotalRows();
        var percent = rows <= 1 ? 100 : _engine.Layout.RowOf(cursor) * 100 / (rows - 1);
        var column = TextMetrics.WidthOfRange(_engine.Buffer[cursor.Line], 0, cursor.Col) + 1;
        var right = " ";
        if (_engine.PendingKeys.Length > 0) right += _engine.PendingKeys + " | ";
        if (Tools.HasLineMemory()) right += DialogText.EditorMemory + " " + Tools.GetLineMemoryPosition() + " | ";
        if (_engine.IsSpellOn) right += DialogText.SpellStatus + " " + _engine.SpellLanguages + " | ";
        // A text file: its type and encoding
        if (_saveMode == SaveAsFile) right += FileExplorer.CurrentFileLabel + " | ";
        right += (cursor.Line + 1) + ":" + column + " | " + percent + "% ";

        // Make it fit
        var available = width - label.Length - right.Length;
        while (text.Length > 0 && TextMetrics.WidthOf(text) > available)
        {
            text = text[..^TextMetrics.PreviousElementLength(text, text.Length)];
        }

        var commandLine = _engine.Mode is EditorMode.Command or EditorMode.Search;
        if (commandLine)
        {
            // The whole line is highlighted while typing a command
            right = string.Empty;
            text = CommandLinePrefix + _engine.CommandText;
            while (TextMetrics.WidthOf(text) > width - 1)
            {
                text = text[..^TextMetrics.PreviousElementLength(text, text.Length)];
            }

            text += new string(' ', width - TextMetrics.WidthOf(text));
            available = width;
        }

        var filler = new string(commandLine ? ' ' : '-', Math.Max(0, available - TextMetrics.WidthOf(text)));
        var status = label + StyleMarker + text + StyleMarker + filler + right + _engine.MessageIsError;
        if (status == _shownStatus) return;
        _shownStatus = status;

        Tools.SetCursorEx(Init.LineCount - 1, 0);
        // The mode label has a color of its own for each mode
        ConsoleColors.SetColors(_engine.Mode switch
        {
            EditorMode.Insert => ConsoleColors.ColorPreset.AttentionInverted,
            EditorMode.Visual or EditorMode.VisualLine => ConsoleColors.ColorPreset.WarningInverted,
            _ => ConsoleColors.ColorPreset.HighlightInverted
        });
        Console.Write(label);
        ConsoleColors.SetColors(commandLine
            ? ConsoleColors.ColorPreset.HighlightInverted
            : _engine.MessageIsError
                ? ConsoleColors.ColorPreset.AttentionInverted
                : ConsoleColors.ColorPreset.Normal);
        Console.Write(text);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(filler + right);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    // What is shown in front of the text on the command line: "COMMAND:" for a command, "/" or "?" for a search
    private string CommandLinePrefix =>
        _engine.CommandPrefix == ':' ? DialogText.CommandPrompt : _engine.CommandPrefix.ToString();

    private void PlaceCursor()
    {
        if (_engine.Mode is EditorMode.Command or EditorMode.Search)
        {
            // The cursor is on the command line
            Tools.SetCursorEx(Init.LineCount - 1,
                Math.Min(Init.LineWidth - 2, TextMetrics.WidthOf(CommandLinePrefix + _engine.CommandText)));
        }
        else
        {
            var cursor = _engine.Cursor;
            Tools.SetCursorEx(_top + _engine.Layout.RowOf(cursor) - _engine.TopRow,
                _left + _leftMargin.Length + _engine.Layout.CellOf(cursor));
        }

        Tools.ShowCursor();
    }

    #endregion
}

internal static class DocumentInfo
{
    public static bool HasChanged;
}
