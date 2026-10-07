using System.Text;

namespace Marbles;

/* Console Text Editor: TextArea v2.0 (LBS, 2026) */
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

    private readonly EditorEngine _engine;
    private readonly int _top;
    private readonly int _left;
    private readonly int _width;
    private readonly int _height;
    private readonly bool _singleLine;
    private readonly bool _readOnly;
    private readonly int _saveMode; // 0 = save as entry, 1 = save as settings
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
        bool readOnly, bool openSearchDialog, int saveMode)
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
        catch (Exception ex)
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
                if (Save())
                {
                    _engine.SetMessage(DialogText.EditorSaved);
                    RedrawAll();
                }
                else
                {
                    _engine.SetMessage(DialogText.EditorNoChanges);
                }

                break;
            case EditorRequest.SaveAndQuit:
                Save();
                Canceled = false;
                _exit = true;
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
                Tools.WriteEditorHelp(false);
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
        }
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
        var result = Menu.YesNoCancelPrompt(DialogText.Save, DialogText.SaveChangesYnCancel, DialogText.YesContinue,
            15, string.Empty, true);
        switch (result)
        {
            case 1: // Yes, save
                Save();
                Canceled = false;
                _exit = true;
                break;
            case 2: // No, dont save
                Canceled = true;
                _exit = true;
                break;
            case 4: // Save, return to edit
                Save();
                RedrawAll();
                break;
            default: // Cancel, return to edit
                RedrawAll();
                break;
        }
    }

    /// <returns>False if there were no changes to save</returns>
    private bool Save()
    {
        if (!_engine.IsModified) return false;
        Tools.HideCursor();
        DocumentInfo.HasChanged = true;
        SaveWasInvoked = true;
        if (_saveMode == 1)
        {
            // Save as settings
            FileIo.WriteSettingsFile(GetText());
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
        if (_saveMode == 1)
        {
            Actions.DrawEditSettingsMenu();
        }
        else
        {
            Actions.DrawEditMenu();
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
        if (selection == null && !hasMatches)
        {
            row.Append(TextMetrics.Printable(s, start, end));
            return cells;
        }

        var styles = new int[end - start];
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

    private void WriteRow(int r, string content, int cells)
    {
        Tools.SetCursorEx(_top + r, _left);
        if (cells > 0 || _shownCells[r] > 0) Console.Write(_leftMargin);
        if (content.Contains(StyleMarker))
        {
            foreach (var run in content.Split(StyleMarker, StringSplitOptions.RemoveEmptyEntries))
            {
                ConsoleColors.SetColors((run[0] - '0') switch
                {
                    StyleSelection => ConsoleColors.ColorPreset.HighlightInverted,
                    StyleSearchMatch => ConsoleColors.ColorPreset.AttentionInverted,
                    _ => ConsoleColors.ColorPreset.Normal
                });
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
        right += (cursor.Line + 1) + ":" + column + " | " + percent + "% ";

        // Make it fit
        var available = width - label.Length - right.Length;
        while (text.Length > 0 && TextMetrics.WidthOf(text) > available)
        {
            text = text[..^TextMetrics.PreviousElementLength(text, text.Length)];
        }

        var filler = new string('-', Math.Max(0, available - TextMetrics.WidthOf(text)));
        var status = label + StyleMarker + text + StyleMarker + filler + right + _engine.MessageIsError;
        if (status == _shownStatus) return;
        _shownStatus = status;

        Tools.SetCursorEx(Init.LineCount - 1, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
        Console.Write(label);
        ConsoleColors.SetColors(_engine.MessageIsError
            ? ConsoleColors.ColorPreset.AttentionInverted
            : ConsoleColors.ColorPreset.Normal);
        Console.Write(text);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(filler + right);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    private void PlaceCursor()
    {
        if (_engine.Mode is EditorMode.Command or EditorMode.Search)
        {
            // The cursor is on the command line
            Tools.SetCursorEx(Init.LineCount - 1, TextMetrics.WidthOf(_engine.CommandPrefix + _engine.CommandText));
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
