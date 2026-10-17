using System.Text;
using System.Text.RegularExpressions;

namespace Marbles;

internal enum EditorMode
{
    Normal,
    Insert,
    Visual,
    VisualLine,
    VisualBlock, // A rectangle of text (see EditorEngine.Block.cs)
    Command, // Typing a ":" command
    Search // Typing a "/" or "?" search
}

/// <summary>
/// What the editor asks its host (the TextArea control) to do after a key press
/// </summary>
internal enum EditorRequest
{
    None,
    Save,
    Quit,
    ForceQuit,
    SaveAndQuit,
    Accept, // Single line editor: Enter was pressed
    Cancel, // Single line editor: Esc was pressed
    Help,
    ClearAll,
    ClearRegisters,
    SpellSuggest, // Show suggestions for the word at the cursor
    ReplaceDialog, // Show the find-and-replace box
    SyntaxChanged, // The colors of the text were turned on or off
    LineNumbersChanged, // The line numbers were turned on or off
    Reload, // Read the file from disk again
    ReloadForce, // ... also when the text has changes that are not saved
    SaveSettings,
    Clipboard, // Text was yanked to go to the system clipboard and/or the paste bin (ClipboardText)
    PasteBinWrite, // ":pb": write PasteBinText to the paste bin (PasteBinAppend: add it to the end)
    PasteBinRead, // ":pbread": insert the text of the paste bin at the cursor
    PasteBinClear, // ":pbclear": empty the paste bin
    ClipboardSettings, // ":clipboard": show the clipboard settings
    DocumentInfo, // ":info": show the size, the dates and the path of the text being edited
    Jump, // ":jump", ":ju" or Ctrl+O: show the Jump menu
    Rename, // ":rename" or ":title": rename the file (RenameText: the new name, or empty to ask), or the title
            // of the entry
    WrapChanged, // Wrapping of long lines was turned on or off
    ScrollOffChanged, // The lines kept around the cursor (ScrollOff) were changed
    CopyPath, // ":path": the path of the file being edited to the clipboard
    Snippets, // ":snip": show the snippets (SnippetText: the selected text, which can be added)
    SnippetAdd, // ":snip add": add SnippetText as a snippet
    SnippetsEdit, // ":snip edit": open the file of the snippets
    CompleteChanged, // Completing words while typing was turned on or off (":complete")
    CompletionsEdit, // ":complete edit": open the file of the completions
    Diff, // ":diff": compare the text with the file on disk (or the saved entry)
    WriteCopy, // ":w name": save the text as a new file (a comparison)
    Bookmark, // ":bookmark": keep the file as a bookmark of the Jump menu
    JumpToBookmark, // ":book 3": jump to bookmark 3 (BookArgument)
    StringMemory, // ":reglist": show the strings of the string memory, to pick one to insert
    MacroRecord, // ":record": start recording a macro (the host warns first in a note of a data file)
    MacroStop, // The Esc that would close the editor, while recording: ask whether to stop, save or go on
    MacroList // ":macro": show the macros, to pick one to play
}

/// <summary>
/// The modal (Normal/Insert/Visual) text editor. It holds the text, the cursor and the mode, and turns
/// key presses into edits. It never touches the console: the TextArea control feeds it keys and draws its state.
/// This file holds the state and the Insert and command line modes. Normal and Visual mode are found in
/// EditorEngine.Normal.cs (Visual Block mode in EditorEngine.Block.cs) and cursor movement in
/// EditorEngine.Motions.cs.
/// </summary>
internal sealed partial class EditorEngine
{
    internal const int TabWidth = 4;
    private const int MaxCommandLength = 64;

    internal TextBuffer Buffer { get; }
    internal TextLayout Layout { get; }
    internal TextPos Cursor { get; private set; }
    internal EditorMode Mode { get; private set; }

    /// <summary>
    /// The row (counted from the top of the document) shown at the top of the view
    /// </summary>
    internal int TopRow { get; private set; }

    /// <summary>
    /// Long lines are wrapped into several rows (on unless turned off). Without wrapping each line is one row,
    /// and the view scrolls sideways to follow the cursor (LeftCell)
    /// </summary>
    internal bool Wrap
    {
        get => Layout.Wrap;
        set
        {
            if (SingleLine) return;
            Layout.Wrap = value;
            LeftCell = 0;
            TopRow = Math.Clamp(TopRow, 0, Math.Max(0, Layout.TotalRows() - 1));
            EnsureCursorVisible();
        }
    }

    /// <summary>
    /// The first cell of the lines shown, when lines are not wrapped
    /// </summary>
    internal int LeftCell { get; private set; }

    /// <summary>
    /// The number of rows in the view
    /// </summary>
    internal int Height { get; }

    /// <summary>
    /// A single line editor is always in Insert mode. Enter accepts the text and Esc cancels.
    /// </summary>
    internal bool SingleLine { get; }

    /// <summary>
    /// The text typed so far when in Command or Search mode
    /// </summary>
    internal string CommandText { get; private set; } = string.Empty;

    /// <summary>
    /// The char starting the command line (":", "/" or "?")
    /// </summary>
    internal char CommandPrefix { get; private set; }

    /// <summary>
    /// A message for the user set by the last key press
    /// </summary>
    internal string Message { get; private set; } = string.Empty;

    /// <summary>
    /// Show a message from the host on the status line (an error is highlighted)
    /// </summary>
    internal void ShowMessage(string message, bool isError)
    {
        Message = message;
        MessageIsError = isError;
    }

    /// <summary>
    /// Every yank goes to the clipboard (else only the ones with "+ in front, as "+y)
    /// </summary>
    internal bool ClipboardEveryYank { get; set; }

    /// <summary>
    /// The text yanked for the clipboard (with EditorRequest.Clipboard)
    /// </summary>
    internal string ClipboardText { get; private set; } = string.Empty;

    /// <summary>
    /// The text for the paste bin (with EditorRequest.PasteBinWrite), and if it is added to the end of it (null:
    /// as set in the clipboard settings)
    /// </summary>
    internal string PasteBinText { get; private set; } = string.Empty;

    internal bool? PasteBinAppend { get; private set; }

    /// <summary>
    /// The text for EditorRequest.Snippets and EditorRequest.SnippetAdd (the selection, or the line)
    /// </summary>
    internal string SnippetText { get; private set; } = string.Empty;


    internal bool MessageIsError { get; private set; }

    /// <summary>
    /// True when the matches of the last search are highlighted
    /// </summary>
    internal bool HighlightSearch { get; private set; }

    private EditorRequest _request;
    private TextPos _stepCursor; // Where the cursor was before the current change
    private EditorMode _commandReturnMode;
    private string _lastSearch = string.Empty;
    private bool _searchForward = true;
    private int _selectionFirstLine; // The lines of the last selection a command was started from ("'<" and "'>")
    private int _selectionLastLine;
    private string _selectionText = string.Empty; // The text of that selection, if it was within a line

    // The cell the cursor tries to stay on when moving up and down (-1 = the cell it is on)
    private int _desiredCell = -1;
    private bool _keepDesiredCell;

    // Repeat (".") works by replaying the keys of the last change
    private readonly List<ConsoleKeyInfo> _recordedKeys = new();
    private ConsoleKeyInfo[] _lastChange = Array.Empty<ConsoleKeyInfo>();
    private int _recordedVersion;
    private bool _recordedInsert;
    private bool _doNotRecord;
    private bool _replaying;

    /// <summary>
    /// Create an editor
    /// </summary>
    /// <param name="text">The text to edit</param>
    /// <param name="width">The number of terminal cells available for text on each row</param>
    /// <param name="height">The number of rows in the view</param>
    /// <param name="singleLine">Create a single line editor (see SingleLine)</param>
    internal EditorEngine(string text, int width, int height, bool singleLine)
    {
        SingleLine = singleLine;
        if (singleLine)
        {
            text = TextBuffer.Normalize(text).Replace(TextBuffer.NewLine, ' ');
        }

        Buffer = new TextBuffer(text);
        Layout = new TextLayout(Buffer, width, !singleLine);
        Height = Math.Max(1, height);
        // Start typing right away in a single line editor and in an empty document
        Mode = singleLine || Buffer.IsEmpty ? EditorMode.Insert : EditorMode.Normal;
    }

    internal bool IsVisual => Mode is EditorMode.Visual or EditorMode.VisualLine or EditorMode.VisualBlock;

    /// <summary>
    /// True to show the number of each line in front of it (the host draws them, and gives the text the
    /// width that is left)
    /// </summary>
    internal bool LineNumbers { get; set; }

    /// <summary>
    /// Change the width the text is wrapped in
    /// </summary>
    internal void SetLayoutWidth(int width)
    {
        Layout.SetWidth(width);
        TopRow = Math.Clamp(TopRow, 0, Math.Max(0, Layout.TotalRows() - 1));
        ClampCursor();
        EnsureCursorVisible();
    }

    // The commands ":wrap", ":set wrap" and ":set nowrap"
    /// <summary>
    /// The rows kept above and below the cursor when the view scrolls ("scrolloff", 0: none). At most half the
    /// view is kept, so a large number keeps the cursor in the middle
    /// </summary>
    internal int ScrollOff { get; set; }

    /// <summary>
    /// The number of rows used when ":scrolloff" turns it on (the last one set)
    /// </summary>
    internal int ScrollOffLines { get; set; } = 12;

    // ":scrolloff" (on/off), ":scrolloff 8" and ":set scrolloff=8" / ":set so=8" (0: off)
    private void SetScrollOff(int? lines)
    {
        if (SingleLine) return;
        if (lines is { } value)
        {
            value = Math.Clamp(value, 0, 999);
            if (value > 0) ScrollOffLines = value;
            ScrollOff = value;
        }
        else
        {
            ScrollOff = ScrollOff > 0 ? 0 : ScrollOffLines;
        }

        Message = ScrollOff > 0
            ? Lang.F("Scrolling keeps {0} lines around the cursor", ScrollOff)
            : Lang.T("Scrolling keeps no lines around the cursor");
        _request = EditorRequest.ScrollOffChanged;
        EnsureCursorVisible();
    }

    private void SetWrap(bool on)
    {
        if (SingleLine) return;
        Wrap = on;
        Message = on ? DialogText.WrapOn : DialogText.WrapOff;
        // The host remembers the choice
        _request = EditorRequest.WrapChanged;
    }

    /// <summary>
    /// "zh" "zl" (a cell left/right), "zH" "zL" (half the width), "zs" "ze" (the cursor at the left/right side):
    /// scroll the view sideways when lines are not wrapped. The cursor is moved if the view leaves it
    /// </summary>
    private void ScrollSideways(char cmd, int count)
    {
        if (Layout.Wrap || SingleLine) return;
        var width = Layout.Width;
        var cell = Layout.CellOf(Cursor);
        LeftCell = cmd switch
        {
            'h' => Math.Max(0, LeftCell - count),
            'l' => LeftCell + count,
            'H' => Math.Max(0, LeftCell - width / 2),
            'L' => LeftCell + width / 2,
            's' => Math.Max(0, cell - 1),
            'e' => Math.Max(0, cell - width + 2),
            _ => LeftCell
        };
        var first = LeftCell + (LeftCell > 0 ? 1 : 0);
        var last = LeftCell + width - 2;
        if (cell < first || cell > last)
        {
            Cursor = new TextPos(Cursor.Line,
                Layout.ColAtCell(Cursor.Line, 0, Math.Clamp(cell, first, last), Mode == EditorMode.Insert));
        }
    }

    // The commands ":number", ":set number" and ":set nonumber"
    private void SetLineNumbers(bool on)
    {
        if (SingleLine) return;
        LineNumbers = on;
        Message = on ? DialogText.LineNumbersOn : DialogText.LineNumbersOff;
        // The host remembers the choice
        _request = EditorRequest.LineNumbersChanged;
    }
    internal bool IsModified => Buffer.IsModified;
    internal string GetText() => Buffer.GetText();

    /// <summary>
    /// The cursor position as an index in the complete text
    /// </summary>
    internal int GetCursorIndex() => Buffer.ToIndex(Cursor);

    /// <summary>
    /// Place the cursor at an index in the complete text
    /// </summary>
    internal void SetCursorIndex(int index)
    {
        Cursor = Buffer.FromIndex(index);
        ClampCursor();
        EnsureCursorVisible();
    }

    internal void MarkSaved() => Buffer.MarkSaved();

    /// <summary>
    /// Set the message shown until the next key press
    /// </summary>
    internal void SetMessage(string message, bool isError)
    {
        Message = message;
        MessageIsError = isError;
    }

    /// <summary>
    /// Treat the text as changed, also when it has not been edited
    /// </summary>
    internal void MarkModified() => Buffer.MarkModified();

    internal void CursorToEnd()
    {
        Cursor = Buffer.End;
        ClampCursor();
        EnsureCursorVisible();
    }

    /// <summary>
    /// Start with the search prompt open
    /// </summary>
    internal void OpenSearch()
    {
        if (!SingleLine) OpenCommandLine('/');
    }

    #region Key handling

    /// <summary>
    /// The new name given with ":rename" or ":title" (empty: it is asked for)
    /// </summary>
    internal string RenameText { get; private set; } = string.Empty;

    /// <summary>
    /// True if the command was ":title" (in a file, the name is changed with ":rename")
    /// </summary>
    internal bool RenameIsTitle { get; private set; }

    /// <summary>
    /// The text cannot be changed (an entry that is read only): commands that would change it are refused with
    /// a message, and INSERT mode is not entered. Moving, searching and yanking work as usual
    /// </summary>
    internal bool ReadOnly
    {
        get => Buffer.ReadOnly;
        set => Buffer.ReadOnly = value;
    }

    /// <summary>
    /// What is said when a change of a read-only text is refused (null: the text of a read-only entry)
    /// </summary>
    internal string? ReadOnlyMessage { get; set; }

    /// <summary>
    /// Tell that the text is read only, if a change of it was refused
    /// </summary>
    /// <returns>True if a change was refused</returns>
    internal bool ReportReadOnly()
    {
        if (!Buffer.TakeChangeRefused()) return false;
        SetError(ReadOnlyMessage ?? DialogText.EditorReadOnly);
        return true;
    }

    /// <summary>
    /// Handle a key press
    /// </summary>
    /// <returns>What the host must do as a result of the key press</returns>
    internal EditorRequest HandleKey(ConsoleKeyInfo key)
    {
        Message = string.Empty;
        MessageIsError = false;
        _request = EditorRequest.None;
        _keepDesiredCell = false;
        if (!Buffer.IsStepOpen) _stepCursor = Cursor;

        // Record the keys of each command for repeating with "."
        var startsCommand = Mode == EditorMode.Normal && !HasPending;
        if (!_replaying)
        {
            if (startsCommand)
            {
                _recordedKeys.Clear();
                _recordedVersion = Buffer.Version;
                _recordedInsert = false;
                _doNotRecord = false;
            }

            _recordedKeys.Add(key);
        }

        RecordMacroKey(key, startsCommand);

        // Ctrl+O: the Jump menu (INSERT mode is left first, as with Esc)
        if (!SingleLine && IsControl(key, ConsoleKey.O) && Mode is EditorMode.Normal or EditorMode.Insert)
        {
            if (Mode == EditorMode.Insert) HandleInsert(new ConsoleKeyInfo('\u001b', ConsoleKey.Escape, false, false, false));
            Buffer.EndStep(Cursor);
            _request = EditorRequest.Jump;
            RecordMacroCommandDone(true);
            ClampCursor();
            EnsureCursorVisible();
            return _request;
        }

        // The selection is remembered when Visual mode is left, for "gv"
        var selectionBefore = IsVisual ? CurrentSelection() : null;
        var insertKey = Mode == EditorMode.Insert;
        switch (Mode)
        {
            case EditorMode.Insert:
                // Tab, Enter and Esc can be for the completions shown; else the word typed is looked up
                if (!HandleCompletionKey(key))
                {
                    HandleInsert(key);
                    UpdateCompletions(key);
                }

                break;
            case EditorMode.Command:
            case EditorMode.Search:
                HandleCommandLine(key);
                break;
            default:
                HandleNormal(key);
                break;
        }

        if (Mode != EditorMode.Insert) ClearCompletions();

        if (selectionBefore != null && !IsVisual) _lastSelection = selectionBefore;

        // A command is complete when back in Normal mode with nothing pending
        if ((Mode == EditorMode.Normal || IsVisual) && !HasPending)
        {
            Buffer.EndStep(Cursor);
            // "+ is for the next command only (it is kept while that command is typed)
            if (_clipboardJustAsked)
            {
                _clipboardJustAsked = false;
            }
            else
            {
                _clipboardAsked = false;
            }
        }

        if (!_replaying && Mode == EditorMode.Normal && !HasPending && !_doNotRecord &&
            (_recordedInsert || Buffer.Version != _recordedVersion))
        {
            _lastChange = _recordedKeys.ToArray();
        }

        // Read only: INSERT mode is left at once, and a change that was refused is told about
        if (ReadOnly && Mode == EditorMode.Insert)
        {
            Mode = EditorMode.Normal;
            Buffer.EndStep(Cursor);
            Buffer.TakeChangeRefused();
            SetError(ReadOnlyMessage ?? DialogText.EditorReadOnly);
        }
        else
        {
            ReportReadOnly();
        }

        // While recording, :q, :wq and ZZ ask about the macro first (as Esc does)
        if (IsRecording && !IsPlayingMacro &&
            _request is EditorRequest.Quit or EditorRequest.ForceQuit or EditorRequest.SaveAndQuit)
        {
            _request = EditorRequest.MacroStop;
        }

        RecordMacroCommandDone(insertKey);
        ClampCursor();
        if (!_keepDesiredCell) _desiredCell = -1;
        EnsureCursorVisible();
        return _request;
    }

    private static bool IsEscape(ConsoleKeyInfo key)
    {
        return key.Key == ConsoleKey.Escape || key.KeyChar == '\u001b';
    }

    private static bool IsEnter(ConsoleKeyInfo key)
    {
        return key.Key == ConsoleKey.Enter || key.KeyChar is '\r' or '\n';
    }

    // Keys that produce a character are always text, also when typed with AltGr (reported as CTRL+ALT)
    private static bool IsText(ConsoleKeyInfo key)
    {
        return key.KeyChar != '\0' && !char.IsControl(key.KeyChar);
    }

    private static bool IsControl(ConsoleKeyInfo key, ConsoleKey letter)
    {
        return !IsText(key) && key.Key == letter && (key.Modifiers & ConsoleModifiers.Control) != 0 &&
               (key.Modifiers & ConsoleModifiers.Alt) == 0;
    }

    private void SetError(string message)
    {
        Message = message;
        MessageIsError = true;
    }

    #endregion

    #region Editing helpers

    private string Line(int line) => Buffer[line];
    private string CurrentLine => Buffer[Cursor.Line];

    private TextPos Insert(TextPos at, string text)
    {
        Buffer.BeginStep(_stepCursor);
        return Buffer.Insert(at, text);
    }

    private string Delete(TextPos from, TextPos to)
    {
        Buffer.BeginStep(_stepCursor);
        return Buffer.Delete(from, to);
    }

    private void ClampCursor()
    {
        var pos = Buffer.Clamp(Cursor);
        // On the first char of a character made of more than one (an emoji), never inside it
        if (pos.Col > 0 && pos.Col < Buffer.LineLength(pos.Line))
        {
            pos = pos with { Col = TextMetrics.ElementStart(Buffer[pos.Line], pos.Col) };
        }

        // Outside Insert mode the cursor is always on a character (if the line has any)
        if (Mode != EditorMode.Insert && pos.Col > 0 && pos.Col >= Buffer.LineLength(pos.Line))
        {
            var line = Buffer[pos.Line];
            pos = pos with { Col = line.Length - TextMetrics.PreviousElementLength(line, line.Length) };
        }

        Cursor = pos;
    }

    private int FirstNonBlank(int line)
    {
        var s = Buffer[line];
        var col = 0;
        while (col < s.Length && char.IsWhiteSpace(s[col]))
        {
            col++;
        }

        return col;
    }

    /// <summary>
    /// The maximum width (in terminal cells) of the text in a single line editor
    /// </summary>
    internal int MaxCells { get; init; } = int.MaxValue;

    private bool HasRoomFor(string text)
    {
        return !SingleLine || TextMetrics.WidthOf(Buffer[0]) + TextMetrics.WidthOf(text) <= MaxCells;
    }

    /// <summary>
    /// Insert text at the cursor
    /// </summary>
    internal void InsertAtCursor(string text)
    {
        text = TextBuffer.Normalize(text).Replace("\t", new string(' ', TabWidth));
        if (SingleLine) text = text.Replace(TextBuffer.NewLine, ' ');
        if (text.Length == 0 || !HasRoomFor(text)) return;
        if (!Buffer.IsStepOpen) _stepCursor = Cursor;
        Cursor = Insert(Buffer.Clamp(Cursor), text);
        if (Mode != EditorMode.Insert) Buffer.EndStep(Cursor);
        ClampCursor();
        EnsureCursorVisible();
    }

    /// <summary>
    /// Insert a snippet after the cursor (as "p"), as one change. The lines after the first are indented as the line of the
    /// cursor. If the snippet has the cursor mark ("$0"), the cursor is placed there in Insert mode. If ":snip" was
    /// typed with a selection (or lines), a snippet with the mark is put around it instead (see WrapInSnippet).
    /// In Insert mode (CTRL+K) the snippet is inserted at the cursor, and the editor stays in Insert mode
    /// </summary>
    internal void InsertSnippet(string text)
    {
        if (SingleLine) return;
        ClearCompletions();
        var insertMode = Mode == EditorMode.Insert;
        ResetPending();
        if (!insertMode) Mode = EditorMode.Normal;
        text = TextBuffer.Normalize(text).Replace("\t", new string(' ', TabWidth));
        var selection = _snippetSelection;
        _snippetSelection = null;
        if (selection != null && text.Contains(Snippets.CursorMark, StringComparison.Ordinal))
        {
            WrapInSnippet(text, selection.Value);
            return;
        }

        var at = Buffer.Clamp(Cursor);
        if (!insertMode && at.Col < Buffer.LineLength(at.Line))
        {
            at = at with { Col = at.Col + TextMetrics.ElementLength(CurrentLine, at.Col) };
        }

        var (inserted, mark) = Snippets.Prepare(text, CurrentLine[..FirstNonBlank(at.Line)]);
        if (inserted.Length == 0) return;
        // The snippet is a change of its own (also after text typed in Insert mode)
        if (Buffer.IsStepOpen) Buffer.EndStep(Cursor);
        _stepCursor = Cursor;
        var end = Insert(at, inserted);
        Cursor = mark < 0 ? end : Buffer.FromIndex(Buffer.ToIndex(at) + mark);
        Buffer.EndStep(Cursor);
        _stepCursor = Cursor;
        if (mark >= 0 || insertMode) Mode = EditorMode.Insert;
        ClampCursor();
        EnsureCursorVisible();
    }

    /// <summary>
    /// Replace the text with the text of the file as it is on disk now. The text is not changed as far as
    /// saving goes, and what was done before cannot be undone
    /// </summary>
    internal void ReloadText(string text)
    {
        var cursor = Cursor;
        ResetPending();
        Mode = EditorMode.Normal;
        _stepCursor = cursor;
        Delete(new TextPos(0, 0), Buffer.End);
        Insert(new TextPos(0, 0), TextBuffer.Normalize(text));
        Buffer.EndStep(cursor);
        Buffer.ClearHistory();
        Buffer.MarkSaved();
        // The cursor stays where it was, if the text still reaches that far
        Cursor = Buffer.Clamp(cursor);
        ClampCursor();
        EnsureCursorVisible();
    }

    /// <summary>
    /// Remove all text
    /// </summary>
    internal void ClearAll()
    {
        _stepCursor = Cursor;
        Delete(new TextPos(0, 0), Buffer.End);
        Buffer.EndStep(new TextPos(0, 0));
        Cursor = new TextPos(0, 0);
        TopRow = 0;
    }

    #endregion

    #region Insert mode

    private void EnterInsert()
    {
        Mode = EditorMode.Insert;
        _recordedInsert = true;
    }

    private void HandleInsert(ConsoleKeyInfo key)
    {
        var line = CurrentLine;
        var control = (key.Modifiers & ConsoleModifiers.Control) != 0 && !IsText(key);
        if (IsEscape(key))
        {
            if (SingleLine)
            {
                _request = EditorRequest.Cancel;
                return;
            }

            // Text typed after "I" or "A" on a block goes on all its lines
            FinishBlockInsert();
            // Back to Normal mode with the cursor on the last typed character
            Mode = EditorMode.Normal;
            if (Cursor.Col > 0)
            {
                Cursor = Cursor with { Col = Cursor.Col - TextMetrics.PreviousElementLength(line, Cursor.Col) };
            }
        }
        else if (IsEnter(key))
        {
            if (SingleLine)
            {
                _request = EditorRequest.Accept;
            }
            else
            {
                Cursor = Insert(Cursor, TextBuffer.NewLine.ToString());
            }
        }
        else if (IsText(key))
        {
            var text = key.KeyChar.ToString();
            if (HasRoomFor(text)) Cursor = Insert(Cursor, text);
        }
        else if (key.Key == ConsoleKey.Tab)
        {
            var text = new string(' ', TabWidth);
            if (HasRoomFor(text)) Cursor = Insert(Cursor, text);
        }
        else if (key.Key == ConsoleKey.Backspace)
        {
            if (Cursor.Col > 0)
            {
                var from = Cursor with { Col = Cursor.Col - TextMetrics.PreviousElementLength(line, Cursor.Col) };
                Delete(from, Cursor);
                Cursor = from;
            }
            else if (Cursor.Line > 0)
            {
                // Join with the line above
                var from = new TextPos(Cursor.Line - 1, Buffer.LineLength(Cursor.Line - 1));
                Delete(from, Cursor);
                Cursor = from;
            }
        }
        else if (key.Key == ConsoleKey.Delete)
        {
            if (Cursor.Col < line.Length)
            {
                Delete(Cursor, Cursor with { Col = Cursor.Col + TextMetrics.ElementLength(line, Cursor.Col) });
            }
            else if (Cursor.Line < Buffer.LineCount - 1)
            {
                // Join with the line below
                Delete(Cursor, new TextPos(Cursor.Line + 1, 0));
            }
        }
        else if (key.Key == ConsoleKey.LeftArrow)
        {
            Cursor = control ? WordBackward(Cursor, false) : Left(Cursor, 1);
        }
        else if (key.Key == ConsoleKey.RightArrow)
        {
            Cursor = control ? WordForward(Cursor, false) : Right(Cursor, 1, true);
        }
        else if (key.Key == ConsoleKey.UpArrow)
        {
            MoveRows(-1);
        }
        else if (key.Key == ConsoleKey.DownArrow)
        {
            MoveRows(1);
        }
        else if (key.Key == ConsoleKey.PageUp)
        {
            MoveRows(-Height);
        }
        else if (key.Key == ConsoleKey.PageDown)
        {
            MoveRows(Height);
        }
        else if (key.Key == ConsoleKey.Home)
        {
            Cursor = Cursor with { Col = 0 };
        }
        else if (key.Key == ConsoleKey.End)
        {
            Cursor = Cursor with { Col = line.Length };
        }
        else if (IsControl(key, ConsoleKey.W))
        {
            // Delete the word before the cursor
            if (Cursor.Col > 0)
            {
                var from = WordBackward(Cursor, false);
                if (from.Line != Cursor.Line) from = Cursor with { Col = 0 };
                Delete(from, Cursor);
                Cursor = from;
            }
        }
        else if (IsControl(key, ConsoleKey.U))
        {
            // Delete everything before the cursor on the line
            Delete(Cursor with { Col = 0 }, Cursor);
            Cursor = Cursor with { Col = 0 };
        }
        else if (IsControl(key, ConsoleKey.K))
        {
            // The snippets, inserted at the cursor (not when "." repeats what was typed)
            if (!SingleLine && !_replaying)
            {
                SnippetText = string.Empty;
                _snippetSelection = null;
                _request = EditorRequest.Snippets;
            }
        }
        else if (IsControl(key, ConsoleKey.R))
        {
            // Insert the current register
            var text = GetRegister(out _).TrimEnd(TextBuffer.NewLine);
            if (SingleLine) text = text.Replace(TextBuffer.NewLine, ' ');
            if (text.Length > 0 && HasRoomFor(text)) Cursor = Insert(Cursor, text);
        }
    }

    #endregion

    #region Command line (":" commands and "/" search)

    private readonly CommandCompletion _completion = new(CommandContext.Editor);

    private void OpenCommandLine(char prefix)
    {
        ResetPending();
        _completion.Reset();
        _commandReturnMode = Mode is EditorMode.Command or EditorMode.Search ? EditorMode.Normal : Mode;
        CommandPrefix = prefix;
        CommandText = string.Empty;
        Mode = prefix == ':' ? EditorMode.Command : EditorMode.Search;
    }

    private void HandleCommandLine(ConsoleKeyInfo key)
    {
        if (IsEscape(key))
        {
            Mode = _commandReturnMode;
        }
        else if (IsEnter(key))
        {
            var text = CommandText;
            var wasSearch = Mode == EditorMode.Search;
            Mode = _commandReturnMode;
            if (wasSearch)
            {
                StartSearch(text, CommandPrefix == '/');
            }
            else
            {
                Mode = EditorMode.Normal;
                ExecuteCommand(text.Trim());
            }
        }
        else if (key.Key == ConsoleKey.Tab)
        {
            // Complete the command. Tab again: the next command that starts the same (Shift+Tab: the previous)
            if (Mode == EditorMode.Command)
            {
                CommandText = _completion.Next(CommandText, (key.Modifiers & ConsoleModifiers.Shift) == 0);
            }
        }
        else if (key.Key == ConsoleKey.Backspace)
        {
            if (CommandText.Length == 0)
            {
                Mode = _commandReturnMode;
            }
            else
            {
                CommandText = CommandText[..^TextMetrics.PreviousElementLength(CommandText, CommandText.Length)];
            }
        }
        else if (IsText(key) && CommandText.Length < MaxCommandLength)
        {
            CommandText += key.KeyChar;
        }
    }

    private void ExecuteCommand(string command)
    {
        if (command.Length == 0) return;
        // Logged by name only: the text of the document (as in a replace command) is not
        if (!SingleLine && !IsPlayingMacro) EventLog.AddCommand(DialogText.LogEditorCommand, command);
        // The range of a selection ("'<,'>", given when ":" is pressed in a Visual mode)
        var fromSelection = command.StartsWith("'<,'>", StringComparison.Ordinal) &&
                            _commandSelectionMode != EditorMode.Normal;
        // Commands may start with the lines to work on, like ":%s/a/b/" or ":2,5d"
        if (!ParseRange(ref command, out var range))
        {
            SetError(Lang.T("Invalid range"));
            return;
        }

        // ":sn" is short for ":snip"
        if (command == "sn" || command.StartsWith("sn ", StringComparison.Ordinal)) command = "snip" + command[2..];
        // ":diff" and ":diff -w"
        var diff = Regex.Match(command, "^diff(?: +(-w))?$");
        if (diff.Success)
        {
            RequestDiff(diff.Groups[1].Success);
            return;
        }

        // ":play name 3" (or ":macro name 3"): play a macro, a number of times. ":macro" alone: the list of macros
        var play = Regex.Match(command, @"^(play|macro)(?: +(.+?))?(?: +(\d+))?$");
        if (play.Success && !SingleLine)
        {
            if (!play.Groups[2].Success)
            {
                if (play.Groups[1].Value == "macro") _request = EditorRequest.MacroList;
                else SetError(Lang.T("Type the name of the macro: :play name"));
                return;
            }

            var times = !play.Groups[3].Success ? 1 : int.TryParse(play.Groups[3].Value, out var count) ? count : 1000;
            PlayMacro(play.Groups[2].Value.Trim(), Math.Clamp(times, 1, 1000));
            return;
        }

        // ":book 3": jump to bookmark 3 (":book": the Bookmarks tab of the Jump menu)
        var book = Regex.Match(command, "^book(?: +(.*))?$");
        if (book.Success)
        {
            BookArgument = book.Groups[1].Value.Trim();
            if (!SingleLine) _request = EditorRequest.JumpToBookmark;
            return;
        }

        // ":w name": the text as a new file (the host tells where this can be done)
        var writeTo = Regex.Match(command, "^(?:w|write) +(.+)$");
        if (writeTo.Success && !SingleLine)
        {
            WriteName = writeTo.Groups[1].Value.Trim();
            _request = EditorRequest.WriteCopy;
            return;
        }

        // ":rename name" and ":title text": the new name (as typed) follows the command
        var renameTo = Regex.Match(command, "^(rename|title)(?: +(.*))?$");
        if (renameTo.Success)
        {
            RenameText = renameTo.Groups[2].Value.Trim();
            command = renameTo.Groups[1].Value;
        }

        // ":dup 5" makes five copies
        var copies = 1;
        var dupCount = Regex.Match(command, "^(dup|duplicate) +([0-9]+)$");
        if (dupCount.Success)
        {
            copies = int.TryParse(dupCount.Groups[2].Value, out var n) ? Math.Clamp(n, 1, MaxCopies) : MaxCopies;
            command = "dup";
        }

        if (range.HasValue)
        {
            var (first, last) = range.Value;
            if (command.Length == 0)
            {
                // Only a line number: go to it
                GoToLine(last);
            }
            else if (command[0] == 's' && command.Length > 1 && !char.IsLetterOrDigit(command[1]))
            {
                Substitute(command[1..], first, last);
            }
            else if (command is "d" or "delete")
            {
                Cursor = new TextPos(first, 0);
                ApplyOperatorToLines('d', first, last);
            }
            else if (command is "y" or "yank")
            {
                ApplyOperatorToLines('y', first, last);
            }
            else if (command is "ol" or "ul")
            {
                MakeList(first, last, command == "ol");
            }
            else if (command is "dup" or "duplicate")
            {
                Duplicate(first, last, fromSelection, copies);
            }
            else if (command is "sort" or "sort!" or "sort i" or "sort! i")
            {
                SortLines(first, last, command.StartsWith("sort!", StringComparison.Ordinal), command.EndsWith(" i"));
            }
            else if (command is "pb" or "pbappend")
            {
                WriteToPasteBin(SelectedText(first, last, fromSelection), command == "pbappend");
            }
            else if (command is "snip" or "snip add" && !SingleLine)
            {
                _snippetSelection = command == "snip" ? SelectionRange(first, last, fromSelection) : null;
                SnippetText = SelectedText(first, last, fromSelection);
                _request = command == "snip" ? EditorRequest.Snippets : EditorRequest.SnippetAdd;
            }
            else if (command is "replace" or "rep" && !SingleLine)
            {
                // Text selected within a line is what to find. Else the selected lines are where to replace
                ReplaceFind = _selectionText;
                ReplaceLines = _selectionText.Length > 0 ? null : range;
                _request = EditorRequest.ReplaceDialog;
            }
            else
            {
                SetError(Lang.T("Not an editor command: ") + command);
            }

            return;
        }

        if (command[0] == 's' && command.Length > 1 && !char.IsLetterOrDigit(command[1]))
        {
            Substitute(command[1..], Cursor.Line, Cursor.Line);
            return;
        }

        if (command == "syntax" || command.StartsWith("syntax ", StringComparison.Ordinal))
        {
            SetSyntax(command[6..].Trim().ToLowerInvariant());
            return;
        }

        var scrollOff = Regex.Match(command, "^(?:scrolloff +|set +(?:scrolloff|so) *= *)([0-9]+)$");
        if (scrollOff.Success)
        {
            SetScrollOff(int.TryParse(scrollOff.Groups[1].Value, out var lines) ? lines : 999);
            return;
        }

        if (command.StartsWith("spell ", StringComparison.Ordinal))
        {
            TurnSpellOn(command[6..].Trim());
            return;
        }

        switch (command)
        {
            case "spell":
                // On or off (":set spell" only turns it on, and ":spell da" checks with other languages)
                if (IsSpellOn) TurnSpellOff();
                else TurnSpellOn(string.Empty);
                break;
            case "set spell":
                TurnSpellOn(string.Empty);
                break;
            case "nospell":
            case "set nospell":
                TurnSpellOff();
                break;
            case "number":
            case "nu":
                SetLineNumbers(!LineNumbers);
                break;
            case "wrap":
                SetWrap(!Wrap);
                break;
            case "scrolloff":
                SetScrollOff(null);
                break;
            case "set wrap":
                SetWrap(true);
                break;
            case "nowrap":
            case "set nowrap":
                SetWrap(false);
                break;
            case "set number":
            case "set nu":
                SetLineNumbers(true);
                break;
            case "nonumber":
            case "nonu":
            case "set nonumber":
            case "set nonu":
                SetLineNumbers(false);
                break;
            case "spellnext":
                JumpToMisspelled(true, 1);
                break;
            case "spellprev":
                JumpToMisspelled(false, 1);
                break;
            case "spellsuggest":
            case "suggest":
                RequestSuggestions();
                break;
            case "spellgood":
            case "spelladd":
                AddSpellWord();
                break;
            case "replace":
            case "rep":
                if (SingleLine) break;
                ReplaceFind = string.Empty;
                ReplaceLines = null;
                _request = EditorRequest.ReplaceDialog;
                break;
            case "w":
            case "write":
                _request = EditorRequest.Save;
                break;
            case "q":
            case "quit":
                _request = EditorRequest.Quit;
                break;
            case "q!":
            case "quit!":
                _request = EditorRequest.ForceQuit;
                break;
            case "wq":
            case "wq!":
            case "x":
                _request = EditorRequest.SaveAndQuit;
                break;
            case "h":
            case "help":
                _request = EditorRequest.Help;
                break;
            case "noh":
            case "nohlsearch":
                HighlightSearch = false;
                break;
            case "u":
            case "undo":
                Undo(1);
                break;
            case "red":
            case "redo":
                Redo(1);
                break;
            case "ts":
            case "timestamp":
                InsertAtCursor(IsoDateTime.GetDateTimeNow());
                break;
            case "pw":
            case "password":
                InsertAtCursor(new PasswordGenerator().GeneratePassword());
                break;
            case "trim":
                TrimLines();
                break;
            case "dup":
            case "duplicate":
                Duplicate(Cursor.Line, Cursor.Line, false, copies);
                break;
            case "sort":
            case "sort!":
            case "sort i":
            case "sort! i":
                // Without a selection: all the lines
                SortLines(0, Buffer.LineCount - 1, command.StartsWith("sort!", StringComparison.Ordinal),
                    command.EndsWith(" i"));
                break;
            case "ol":
            case "ul":
                MakeList(Cursor.Line, Cursor.Line, command == "ol");
                break;
            case "e":
            case "edit":
            case "reload":
                _request = EditorRequest.Reload;
                break;
            case "e!":
            case "edit!":
            case "reload!":
                _request = EditorRequest.ReloadForce;
                break;
            case "clear":
                if (!Buffer.IsEmpty) _request = EditorRequest.ClearAll;
                break;
            case "reg":
            case "registers":
                ShowRegister();
                break;
            case "record":
                if (!SingleLine) _request = EditorRequest.MacroRecord;
                break;
            case "reglist":
                if (!Tools.HasLineMemory()) Message = Lang.T("String memory is empty");
                else if (!SingleLine) _request = EditorRequest.StringMemory;
                break;
            case "regclear":
                if (Tools.HasLineMemory()) _request = EditorRequest.ClearRegisters;
                break;
            case "putall":
                PutAllRegisters();
                break;
            case "pb":
            case "pbappend":
                WriteToPasteBin(Buffer.GetText(), command == "pbappend");
                break;
            case "pbread":
                if (!SingleLine) _request = EditorRequest.PasteBinRead;
                break;
            case "pbclear":
                _request = EditorRequest.PasteBinClear;
                break;
            case "clipboard":
                _request = EditorRequest.ClipboardSettings;
                break;
            case "info":
                if (!SingleLine) _request = EditorRequest.DocumentInfo;
                break;
            case "jump":
            case "ju":
            case "jmp":
                if (!SingleLine) _request = EditorRequest.Jump;
                break;
            case "rename":
            case "title":
                if (SingleLine) break;
                RenameIsTitle = command == "title";
                _request = EditorRequest.Rename;
                break;
            case "path":
                if (!SingleLine) _request = EditorRequest.CopyPath;
                break;
            case "bookmark":
                if (!SingleLine) _request = EditorRequest.Bookmark;
                break;
            case "snip":
                SnippetText = string.Empty;
                _snippetSelection = null;
                if (!SingleLine) _request = EditorRequest.Snippets;
                break;
            case "snip add":
                SnippetText = CurrentLine;
                if (!SingleLine) _request = EditorRequest.SnippetAdd;
                break;
            case "snip edit":
                if (!SingleLine) _request = EditorRequest.SnippetsEdit;
                break;
            case "complete":
                SetAutoComplete(!AutoComplete);
                break;
            case "complete on":
                SetAutoComplete(true);
                break;
            case "complete off":
                SetAutoComplete(false);
                break;
            case "complete edit":
                if (!SingleLine) _request = EditorRequest.CompletionsEdit;
                break;
            case "vsel":
                StartVisual(EditorMode.Visual);
                break;
            case "vline":
                StartVisual(EditorMode.VisualLine);
                break;
            case "block":
                StartVisual(EditorMode.VisualBlock);
                break;
            case "d":
            case "delete":
                // Without lines in front: the line of the cursor
                ApplyOperatorToLines('d', Cursor.Line, Cursor.Line);
                break;
            case "y":
            case "yank":
                ApplyOperatorToLines('y', Cursor.Line, Cursor.Line);
                break;
            case "yankall":
                // All the text, as "gg V G y" (and ":%y"): to the string memory and the clipboard as a yank
                ApplyOperatorToLines('y', 0, Buffer.LineCount - 1);
                break;
            default:
                SetError(Lang.T("Not an editor command: ") + command);
                break;
        }
    }

    // Read the lines a command works on from the start of the command: "%" (all lines), a line ("5", "." for
    // the current, "$" for the last, "'<" and "'>" for the last selection) or two lines separated by a comma
    private bool ParseRange(ref string command, out (int First, int Last)? range)
    {
        range = null;
        var last = Buffer.LineCount - 1;
        var index = 0;
        if (command[0] == '%')
        {
            range = (0, last);
            command = command[1..].TrimStart();
            return true;
        }

        int? ReadLine(string text)
        {
            if (index >= text.Length) return null;
            if (char.IsAsciiDigit(text[index]))
            {
                var start = index;
                while (index < text.Length && char.IsAsciiDigit(text[index])) index++;
                return int.TryParse(text.AsSpan(start, index - start), out var number)
                    ? Math.Clamp(number - 1, 0, last)
                    : last;
            }

            switch (text[index])
            {
                case '.':
                    index++;
                    return Cursor.Line;
                case '$':
                    index++;
                    return last;
                case '\'' when index + 1 < text.Length && text[index + 1] is '<' or '>':
                    index += 2;
                    return Math.Min(last, text[index - 1] == '<' ? _selectionFirstLine : _selectionLastLine);
                default:
                    return null;
            }
        }

        var first = ReadLine(command);
        if (first == null) return true; // No range
        var second = first;
        if (index < command.Length && command[index] == ',')
        {
            index++;
            second = ReadLine(command);
            if (second == null) return false;
        }

        range = (Math.Min(first.Value, second.Value), Math.Max(first.Value, second.Value));
        command = command[index..].TrimStart();
        return true;
    }

    private void GoToLine(int line)
    {
        line = Math.Clamp(line, 0, Buffer.LineCount - 1);
        Cursor = new TextPos(line, FirstNonBlank(line));
    }

    // Remove blank space at the start and end of all lines
    private void TrimLines()
    {
        for (var i = 0; i < Buffer.LineCount; i++)
        {
            var line = Buffer[i];
            var trimmed = line.Trim();
            if (trimmed.Length == line.Length) continue;
            Delete(new TextPos(i, 0), new TextPos(i, line.Length));
            Insert(new TextPos(i, 0), trimmed);
        }

        Cursor = Cursor with { Col = 0 };
    }

    // The start of a line in a Markdown list: "- ", "* ", "+ ", "1. " or "1) " (after the indentation)
    private static readonly Regex ListMarker = new(@"^(\s*)(?:[-*+]|\d{1,9}[.)])\s+");

    // True if a line starts a numbered (or else a bulleted) list item
    private static bool IsListItem(string line, bool ordered)
    {
        var match = ListMarker.Match(line);
        return match.Success && char.IsAsciiDigit(line[match.Groups[1].Length]) == ordered;
    }

    // ":ol" and ":ul": make lines a numbered ("1. ") or bulleted ("- ") list. Lines that already are a list
    // of another kind are changed, and lines that all are a list of the kind asked for become plain text again.
    // Blank lines are left as they are. Numbers count each level of indentation by itself
    // The selection when ":" was pressed in a Visual mode (Normal: none), for ":dup"
    private EditorMode _commandSelectionMode = EditorMode.Normal;
    private TextPos _commandSelectionFrom;
    private TextPos _commandSelectionTo; // The last selected char (included)
    private (int First, int Last, int Left, int Right) _commandBlock;

    /// <summary>
    /// ":dup": put a copy of the selection right after it. Text selected in Visual mode is copied after its last
    /// char, the lines of Visual Line mode (or of a range, or the line of the cursor) below the last of them, and a
    /// block of Visual Block mode to the right of the block. The cursor goes to the start of the copy
    /// </summary>
    // The most copies ":dup" makes at once
    private const int MaxCopies = 500;

    private void Duplicate(int first, int last, bool fromSelection, int copies)
    {
        if (SingleLine) return;
        var mode = fromSelection ? _commandSelectionMode : EditorMode.VisualLine;
        _commandSelectionMode = EditorMode.Normal;
        switch (mode)
        {
            case EditorMode.Visual:
            {
                var from = _commandSelectionFrom;
                var to = _commandSelectionTo;
                // The last char is part of the selection (at the end of a line: the line break)
                if (to.Col < Buffer.LineLength(to.Line))
                {
                    to = new TextPos(to.Line, to.Col + TextMetrics.ElementLength(Buffer[to.Line], to.Col));
                }
                else if (to.Line + 1 < Buffer.LineCount)
                {
                    to = new TextPos(to.Line + 1, 0);
                }

                var text = Buffer.GetText(from, to);
                if (text.Length == 0) return;
                Insert(to, string.Concat(Enumerable.Repeat(text, copies)));
                Cursor = to;
                break;
            }
            case EditorMode.VisualBlock:
            {
                var (top, bottom, left, right) = _commandBlock;
                var copyStart = BlockRange(top, left, right).End;
                for (var line = top; line <= bottom; line++)
                {
                    var (start, end) = BlockRange(line, left, right);
                    if (start >= end) continue;
                    var piece = Buffer[line][start..end];
                    // Text to the right of the block stays in line
                    if (right != int.MaxValue && end < Buffer.LineLength(line))
                    {
                        piece += new string(' ', Math.Max(0, right - left - TextMetrics.WidthOf(piece)));
                    }

                    Insert(new TextPos(line, end), string.Concat(Enumerable.Repeat(piece, copies)));
                }

                Cursor = new TextPos(top, copyStart);
                break;
            }
            default:
            {
                var lines = string.Join(TextBuffer.NewLine,
                    Enumerable.Range(first, last - first + 1).Select(line => Buffer[line]));
                Insert(new TextPos(last, Buffer.LineLength(last)),
                    string.Concat(Enumerable.Repeat(TextBuffer.NewLine + lines, copies)));
                Cursor = new TextPos(last + 1, FirstNonBlank(last + 1));
                break;
            }
        }
    }

    // The text of the selection when ":" was pressed in a Visual mode, or else of the lines
    private string SelectedText(int first, int last, bool fromSelection)
    {
        var mode = fromSelection ? _commandSelectionMode : EditorMode.VisualLine;
        _commandSelectionMode = EditorMode.Normal;
        switch (mode)
        {
            case EditorMode.Visual:
            {
                var to = _commandSelectionTo;
                if (to.Col < Buffer.LineLength(to.Line))
                {
                    to = new TextPos(to.Line, to.Col + TextMetrics.ElementLength(Buffer[to.Line], to.Col));
                }
                else if (to.Line + 1 < Buffer.LineCount)
                {
                    to = new TextPos(to.Line + 1, 0);
                }

                return Buffer.GetText(_commandSelectionFrom, to);
            }
            case EditorMode.VisualBlock:
            {
                var (top, bottom, left, right) = _commandBlock;
                return string.Join(TextBuffer.NewLine, BlockText(top, bottom, left, right));
            }
            default:
                return Buffer.GetText(new TextPos(first, 0), new TextPos(last, Buffer.LineLength(last)));
        }
    }

    // The text ":snip" was typed with (a selection or lines): a snippet with the cursor mark is put around it.
    // Lines: whole lines. Otherwise from (included) to (not included)
    private (bool Lines, TextPos From, TextPos To)? _snippetSelection;

    // Where the selection (or the lines) of a command is, for a snippet to be put around it (not for a block)
    private (bool Lines, TextPos From, TextPos To)? SelectionRange(int first, int last, bool fromSelection)
    {
        var mode = fromSelection ? _commandSelectionMode : EditorMode.VisualLine;
        switch (mode)
        {
            case EditorMode.Visual:
            {
                var to = _commandSelectionTo;
                if (to.Col < Buffer.LineLength(to.Line))
                {
                    to = new TextPos(to.Line, to.Col + TextMetrics.ElementLength(Buffer[to.Line], to.Col));
                }
                else if (to.Line + 1 < Buffer.LineCount)
                {
                    to = new TextPos(to.Line + 1, 0);
                }

                return (false, _commandSelectionFrom, to);
            }
            case EditorMode.VisualBlock:
                return null;
            default:
                return (true, new TextPos(first, 0), new TextPos(last, Buffer.LineLength(last)));
        }
    }

    /// <summary>
    /// Put a snippet around the text ":snip" was typed with: the text is taken out, and put in again at the cursor
    /// mark of the snippet. Selected lines are replaced by the lines of the snippet, indented as they were (lines
    /// of the text get the indent the mark has in the snippet). One change
    /// </summary>
    private void WrapInSnippet(string snippet, (bool Lines, TextPos From, TextPos To) selection)
    {
        var mark = snippet.IndexOf(Snippets.CursorMark, StringComparison.Ordinal);
        var from = Buffer.Clamp(selection.From);
        var to = Buffer.Clamp(selection.To);
        _stepCursor = Cursor;
        string text;
        if (selection.Lines)
        {
            var lines = Enumerable.Range(from.Line, to.Line - from.Line + 1).Select(line => Buffer[line]).ToList();
            // The indent the lines have in common is the indent of the snippet
            var common = lines.Where(line => line.Trim().Length > 0)
                .Select(line => line.Length - line.TrimStart().Length).DefaultIfEmpty(0).Min();
            var firstText = lines.FirstOrDefault(line => line.Trim().Length > 0) ?? string.Empty;
            var indent = firstText[..Math.Min(common, firstText.Length)];
            // The lines after the first of the text get the indent the mark has (if it is first on its line)
            var lineStart = mark == 0 ? 0 : snippet.LastIndexOf(TextBuffer.NewLine, mark - 1) + 1;
            var markIndent = snippet[lineStart..mark].Trim().Length == 0 ? snippet[lineStart..mark] : string.Empty;
            var inner = lines.Select((line, i) =>
            {
                var rest = line.Trim().Length == 0 ? string.Empty : line[Math.Min(common, line.Length)..];
                return i == 0 || rest.Length == 0 ? rest : markIndent + rest;
            });
            var whole = snippet[..mark] + string.Join(TextBuffer.NewLine, inner) +
                        snippet[(mark + Snippets.CursorMark.Length)..];
            text = string.Join(TextBuffer.NewLine,
                whole.Split(TextBuffer.NewLine).Select(line => line.Length > 0 ? indent + line : line));
            from = new TextPos(from.Line, 0);
            to = new TextPos(to.Line, Buffer.LineLength(to.Line));
            Delete(from, to);
            Insert(from, text);
            Cursor = new TextPos(from.Line, FirstNonBlank(from.Line));
        }
        else
        {
            var selected = Buffer.GetText(from, to);
            // The lines after the first of the snippet are indented as the line of the selection
            var (prepared, markAt) = Snippets.Prepare(snippet, Buffer[from.Line][..FirstNonBlank(from.Line)]);
            text = prepared[..markAt] + selected + prepared[markAt..];
            Delete(from, to);
            Insert(from, text);
            Cursor = from;
        }

        Buffer.EndStep(Cursor);
        ClampCursor();
        EnsureCursorVisible();
    }

    // ":pb" and ":pbappend": the host writes the text to the paste bin
    private void WriteToPasteBin(string text, bool append)
    {
        if (SingleLine) return;
        PasteBinText = text;
        PasteBinAppend = append ? true : null;
        _request = EditorRequest.PasteBinWrite;
    }

    /// <summary>
    /// ":sort": sort lines (by the value of their characters; "i": not telling upper and lower case apart).
    /// ":sort!" sorts them the other way. One step to undo
    /// </summary>
    private void SortLines(int first, int last, bool reverse, bool ignoreCase)
    {
        if (SingleLine || last <= first) return;
        var lines = Enumerable.Range(first, last - first + 1).Select(line => Buffer[line]).ToList();
        var comparer = ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var sorted = lines.Order(comparer).ToList();
        if (reverse) sorted.Reverse();
        if (sorted.SequenceEqual(lines)) return;
        var from = new TextPos(first, 0);
        var to = new TextPos(last, Buffer.LineLength(last));
        Delete(from, to);
        Insert(from, string.Join(TextBuffer.NewLine, sorted));
        Cursor = from;
        Message = Lang.F("{0} lines sorted", lines.Count);
    }

    private void MakeList(int first, int last, bool ordered)
    {
        if (SingleLine) return;
        var lines = new List<int>();
        for (var i = first; i <= last; i++)
        {
            if (Buffer[i].Trim().Length > 0) lines.Add(i);
        }

        if (lines.Count == 0) return;
        var remove = lines.All(i => IsListItem(Buffer[i], ordered));
        var levels = new List<(int Indent, int Number)>(); // The numbers of the open levels, the innermost last
        foreach (var i in lines)
        {
            var line = Buffer[i];
            var match = ListMarker.Match(line);
            var indent = match.Success ? match.Groups[1].Length : FirstNonBlank(i);
            var contentStart = match.Success ? match.Length : indent;
            var marker = string.Empty;
            if (!remove && ordered)
            {
                var width = TextMetrics.WidthOfRange(line, 0, indent);
                while (levels.Count > 0 && levels[^1].Indent > width) levels.RemoveAt(levels.Count - 1);
                if (levels.Count == 0 || levels[^1].Indent < width) levels.Add((width, 0));
                levels[^1] = (width, levels[^1].Number + 1);
                marker = levels[^1].Number + ". ";
            }
            else if (!remove)
            {
                marker = "- ";
            }

            var from = new TextPos(i, indent);
            var to = new TextPos(i, contentStart);
            if (Buffer.GetText(from, to) == marker) continue;
            if (contentStart > indent) Delete(from, to);
            if (marker.Length > 0) Insert(from, marker);
        }

        Cursor = new TextPos(first, FirstNonBlank(first));
    }

    #endregion

    #region Search and replace

    private static readonly TimeSpan SearchTimeout = TimeSpan.FromSeconds(2);

    // Highlighting runs on every line shown, at every redraw: a pattern that is slow there turns it off quickly
    private static readonly TimeSpan HighlightTimeout = TimeSpan.FromMilliseconds(100);
    private Regex? _searchRegex;
    private Regex? _highlightRegex;

    // True when the last search was stopped because it took too long (it is not "not found")
    private bool _searchTimedOut;

    /// <summary>
    /// Create the regular expression for a search pattern. Like Vim's "smartcase" the search ignores case
    /// unless the pattern has uppercase letters. A pattern that is not a valid expression is searched for as text.
    /// </summary>
    private static Regex CreateRegex(string pattern, bool? ignoreCase = null, TimeSpan? timeout = null)
    {
        var options = RegexOptions.CultureInvariant;
        if (ignoreCase ?? !pattern.Any(char.IsUpper)) options |= RegexOptions.IgnoreCase;
        try
        {
            return new Regex(pattern, options, timeout ?? SearchTimeout);
        }
        catch (ArgumentException)
        {
            return new Regex(Regex.Escape(pattern), options, timeout ?? SearchTimeout);
        }
    }

    private void SetSearch(string pattern)
    {
        _lastSearch = pattern;
        _searchRegex = CreateRegex(pattern);
        _highlightRegex = CreateRegex(pattern, null, HighlightTimeout);
    }

    private void StartSearch(string pattern, bool forward)
    {
        if (pattern.Length == 0) pattern = _lastSearch; // Repeat the last search
        if (pattern.Length == 0) return;
        SetSearch(pattern);
        _searchForward = forward;
        HighlightSearch = true;
        if (FindNext(Cursor, forward, out var found))
        {
            Cursor = found;
        }
        else if (!_searchTimedOut)
        {
            SetError(Lang.T("Pattern not found: ") + pattern);
        }
    }

    /// <summary>
    /// Find the next match of the last search (the search wraps around the document)
    /// </summary>
    private bool FindNext(TextPos from, bool forward, out TextPos found)
    {
        found = from;
        _searchTimedOut = false;
        if (_searchRegex == null) return false;
        var count = Buffer.LineCount;
        try
        {
            // The line of the cursor is searched twice: first after the cursor and last before it
            for (var i = 0; i <= count; i++)
            {
                var lineIndex = ((forward ? from.Line + i : from.Line - i) % count + count) % count;
                var line = Buffer[lineIndex];
                var index = -1;
                if (forward)
                {
                    // The first match on the line (after the cursor when on its line). A match that the cursor
                    // is inside is passed over as a whole, like Vim does
                    foreach (Match match in _searchRegex.Matches(line))
                    {
                        if (i == 0 && match.Index <= from.Col) continue;
                        if (i == count && match.Index > from.Col) break;
                        index = match.Index;
                        break;
                    }
                }
                else
                {
                    // The last match on the line (before the cursor when on its line)
                    foreach (Match match in _searchRegex.Matches(line))
                    {
                        if (i == 0 && match.Index >= from.Col) break;
                        index = match.Index;
                    }

                    if (i == count && index < from.Col) index = -1;
                }

                if (index < 0) continue;
                found = new TextPos(lineIndex, Math.Min(index, line.Length));
                return true;
            }
        }
        catch (RegexMatchTimeoutException)
        {
            // Not highlighted either: that would take as long at every redraw
            _searchTimedOut = true;
            HighlightSearch = false;
            SetError(Lang.T("Search took too long"));
        }

        return false;
    }

    /// <summary>
    /// The parts of a line (start and end char index) matching the last search
    /// </summary>
    internal IEnumerable<(int Start, int End)> SearchMatches(int line)
    {
        if (!HighlightSearch || _highlightRegex == null) yield break;
        MatchCollection matches;
        try
        {
            matches = _highlightRegex.Matches(Buffer[line]);
            _ = matches.Count; // The matches are found here, so a timeout is caught
        }
        catch (RegexMatchTimeoutException)
        {
            // Too slow to be shown on every line at every redraw: the highlighting is turned off (n still works)
            HighlightSearch = false;
            yield break;
        }

        foreach (Match match in matches)
        {
            if (match.Length > 0) yield return (match.Index, match.Index + match.Length);
        }
    }

    // The ":s" command: "/pattern/replacement/flags" where the separator can be any punctuation character.
    // Flags: g = all matches on each line (else only the first), i = ignore case, I = match case
    private void Substitute(string arguments, int firstLine, int lastLine)
    {
        var parts = SplitUnescaped(arguments[1..], arguments[0]);
        var pattern = parts[0].Length > 0 ? parts[0] : _lastSearch;
        var replacement = parts.Count > 1 ? parts[1] : string.Empty;
        var flags = parts.Count > 2 ? parts[2].Trim() : string.Empty;
        if (pattern.Length == 0)
        {
            SetError(Lang.T("No previous search"));
            return;
        }

        if (flags.Any(flag => flag is not ('g' or 'i' or 'I')))
        {
            SetError(Lang.T("Unknown flags: ") + flags);
            return;
        }

        bool? ignoreCase = flags.Contains('i') ? true : flags.Contains('I') ? false : null;
        Substitute(pattern, ConvertReplacement(replacement), ignoreCase, flags.Contains('g'), firstLine, lastLine,
            pattern);
    }

    /// <summary>
    /// The lines selected when the find-and-replace box was asked for (null if none were)
    /// </summary>
    internal (int First, int Last)? ReplaceLines { get; private set; }

    /// <summary>
    /// The text selected when the find-and-replace box was asked for: the text to find (empty if none was)
    /// </summary>
    internal string ReplaceFind { get; private set; } = string.Empty;

    /// <summary>
    /// Replace all of a text on a range of lines (as one change that can be undone)
    /// </summary>
    /// <param name="find">The text to find</param>
    /// <param name="replacement">The text to replace it with</param>
    /// <param name="matchCase">False to ignore case</param>
    /// <param name="isRegex">True if "find" is a regular expression (and "replacement" as in the ":s" command)</param>
    /// <param name="firstLine">The first line</param>
    /// <param name="lastLine">The last line</param>
    internal void Replace(string find, string replacement, bool matchCase, bool isRegex, int firstLine,
        int lastLine)
    {
        if (find.Length == 0) return;
        _stepCursor = Cursor;
        Substitute(isRegex ? find : Regex.Escape(find),
            isRegex ? ConvertReplacement(replacement) : replacement.Replace("$", "$$"), !matchCase, true,
            Math.Max(0, firstLine), lastLine, find);
        if (Buffer.IsStepOpen) Buffer.EndStep(Cursor);
        ClampCursor();
        EnsureCursorVisible();
    }

    // Replace the matches of a pattern on a range of lines. The replacement is in the form of .NET
    private void Substitute(string pattern, string replacement, bool? ignoreCase, bool global, int firstLine,
        int lastLine, string shownPattern)
    {
        var regex = CreateRegex(pattern, ignoreCase);
        SetSearch(pattern);
        var substitutions = 0;
        var lines = 0;
        var cursorLine = -1;
        try
        {
            // From the bottom, as a replacement with a line break changes the numbers of the lines after it
            for (var i = Math.Min(lastLine, Buffer.LineCount - 1); i >= firstLine; i--)
            {
                var line = Buffer[i];
                var matches = regex.Matches(line).Count;
                if (matches == 0) continue;
                var changed = regex.Replace(line, replacement, global ? -1 : 1);
                substitutions += global ? matches : 1;
                lines++;
                if (changed == line) continue;
                Delete(new TextPos(i, 0), new TextPos(i, line.Length));
                Insert(new TextPos(i, 0), changed);
                if (cursorLine < 0) cursorLine = i + changed.Count(c => c == TextBuffer.NewLine);
                else cursorLine += changed.Count(c => c == TextBuffer.NewLine);
            }
        }
        catch (RegexMatchTimeoutException)
        {
            SetError(Lang.T("Search took too long"));
            return;
        }

        if (substitutions == 0)
        {
            SetError(Lang.T("Pattern not found: ") + shownPattern);
            return;
        }

        if (cursorLine >= 0) GoToLine(cursorLine);
        Message = Lang.F("{0} on {1}",
            substitutions == 1 ? Lang.T("1 substitution") : Lang.F("{0} substitutions", substitutions),
            lines == 1 ? Lang.T("1 line") : Lang.F("{0} lines", lines));
    }

    // Split a text at a separator character, except where it is escaped with a backslash
    private static List<string> SplitUnescaped(string text, char separator)
    {
        var parts = new List<string>();
        var part = new StringBuilder();
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 1 < text.Length && text[i + 1] == separator)
            {
                part.Append(separator);
                i++;
            }
            else if (text[i] == separator && parts.Count < 2)
            {
                parts.Add(part.ToString());
                part.Clear();
            }
            else
            {
                part.Append(text[i]);
            }
        }

        parts.Add(part.ToString());
        return parts;
    }

    // Convert a replacement written the Vim way ("&" or "\0" for the match, "\1" for the first group, "\n" or
    // "\r" for a line break) to the form used by .NET regular expressions
    private static string ConvertReplacement(string replacement)
    {
        var result = new StringBuilder();
        for (var i = 0; i < replacement.Length; i++)
        {
            var c = replacement[i];
            if (c == '\\' && i + 1 < replacement.Length)
            {
                var next = replacement[++i];
                if (char.IsAsciiDigit(next))
                {
                    result.Append("${").Append(next).Append('}');
                }
                else
                {
                    result.Append(next switch
                    {
                        'n' or 'r' => "\n",
                        't' => new string(' ', TabWidth),
                        '$' => "$$",
                        _ => next.ToString()
                    });
                }
            }
            else if (c == '&')
            {
                result.Append("$&");
            }
            else if (c == '$')
            {
                result.Append("$$");
            }
            else
            {
                result.Append(c);
            }
        }

        return result.ToString();
    }

    #endregion

    #region View

    private void EnsureCursorVisible()
    {
        if (!Layout.Wrap && !SingleLine)
        {
            // Sideways, with a margin of one cell at each side, where the marks of hidden text are shown
            var cell = Layout.CellOf(Cursor);
            if (cell < LeftCell + (LeftCell > 0 ? 1 : 0))
            {
                // Back to the start of the lines if the cursor can be shown there
                LeftCell = cell <= Layout.Width - 2 ? 0 : cell - 1;
            }
            else if (cell > LeftCell + Layout.Width - 2)
            {
                LeftCell = Math.Max(0, cell - Layout.Width + 2);
            }
        }
        else
        {
            LeftCell = 0;
        }

        var row = Layout.RowOf(Cursor);
        // Rows kept above and below the cursor (not past the start and the end of the text)
        var margin = SingleLine ? 0 : Math.Min(ScrollOff, (Height - 1) / 2);
        if (row < TopRow + margin)
        {
            TopRow = Math.Max(0, row - margin);
        }
        else if (row > TopRow + Height - 1 - margin)
        {
            var lastTop = Math.Max(0, Layout.TotalRows() - Height);
            TopRow = Math.Max(row - Height + 1, Math.Min(row - Height + 1 + margin, lastTop));
        }
    }

    /// <summary>
    /// True if the document has more rows than the view
    /// </summary>
    internal bool CanScroll => Layout.TotalRows() > Height;

    /// <summary>
    /// Scroll the view without regard to the cursor (used when the text is only displayed)
    /// </summary>
    internal void ScrollView(int rows)
    {
        TopRow = Math.Clamp(TopRow + rows, 0, Math.Max(0, Layout.TotalRows() - Height));
    }

    /// <summary>
    /// Scroll the view to show a number of rows below the cursor if possible (used when the text is only displayed)
    /// </summary>
    internal void ShowRowsBelowCursor(int rows)
    {
        var row = Layout.RowOf(Cursor);
        TopRow = 0;
        ScrollView(row + rows - (Height - 1));
    }

    /// <summary>
    /// The selected part of a line (start and end char index) in Visual mode, or null if nothing is selected on it
    /// </summary>
    internal (int Start, int End)? Selection(int line)
    {
        if (!IsVisual) return null;
        if (Mode == EditorMode.VisualBlock) return BlockSelection(line);
        var from = TextPos.Min(_visualAnchor, Cursor);
        var to = TextPos.Max(_visualAnchor, Cursor);
        if (line < from.Line || line > to.Line) return null;
        var length = Buffer.LineLength(line);
        if (Mode == EditorMode.VisualLine) return (0, length);
        var start = line == from.Line ? from.Col : 0;
        var end = line == to.Line
            ? Math.Min(length, to.Col + (to.Col < length ? TextMetrics.ElementLength(Buffer[line], to.Col) : 0))
            : length;
        return (start, end);
    }

    /// <summary>
    /// The keys typed so far of a command that is not yet complete
    /// </summary>
    internal string PendingKeys
    {
        get
        {
            var keys = _opCount + (_operator == '\0' ? string.Empty : OperatorName(_operator)) + _count;
            return _prefix == '\0' ? keys : keys + _prefix;
        }
    }

    #endregion
}
