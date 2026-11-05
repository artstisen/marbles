using System.Text;

namespace Marbles;

/* Console Text Editor: TextArea v2.0 (Lars Brandt Stisen, 2022-2026) */
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
     var textArea = new TextArea(0, 2, Init.LineWidth - 1, Init.LineCount - 3, "Some text...", 0, false, false, false, 0);
     if (!textArea.Canceled)
     {
         var text = textArea.GetText();
     }
 */

public partial class TextArea
{
    private const char StyleMarker = '\u0001'; // Separates the differently colored parts of a row
    private const int StyleNormal = 0;
    private const int StyleSelection = 1;
    private const int StyleSearchMatch = 2;
    private const int StyleMisspelled = 3;
    private const int StyleLineNumber = 4;
    private const int StyleCurrentLineNumber = 5; // The number of the line the cursor is on
    private const int StyleFound = 6; // The character found by f, F, t or T
    private const int StyleJumpLabel = 7; // The letter to type to jump to a word
    private const int StyleJumpNext = 8; // The letter to type after it
    private const int StyleWhitespace = 9; // Blank space shown (:whitespace): . > $
    private const int StyleTrailingSpace = 10; // ... at the end of a line
    private const int StyleMarkdown = 12; // The styles of Markdown follow from this number

    /// <summary>
    /// True to show line numbers in the editor (setting "linenumbers")
    /// </summary>
    internal static bool LineNumbers { get; set; }

    /// <summary>
    /// Long lines are wrapped (setting "wrap", on unless turned off)
    /// </summary>
    internal static bool WordWrap { get; set; } = true;

    /// <summary>
    /// Blank space is shown: a space as ".", a tab as ">" and the end of a line as "$", in the color of the line
    /// numbers, and the blank space at the end of a line highlighted (setting "whitespace", ":whitespace"). Classic
    /// ASCII, so every terminal shows it, and each mark takes the place of what it marks
    /// </summary>
    internal static bool ShowWhitespace { get; set; }

    // Not in a peek (or a comparison), or a field of one line
    private bool ShowsWhitespace => ShowWhitespace && _peek == null && !_singleLine;

    internal const int DefaultScrollOffLines = 12;

    /// <summary>
    /// Lines are kept above and below the cursor when the view scrolls (setting "scrolloff", on unless turned off)
    /// </summary>
    internal static bool ScrollOff { get; set; } = true;

    /// <summary>
    /// The number of lines kept above and below the cursor (setting "scrolllines")
    /// </summary>
    internal static int ScrollOffLines { get; set; } = DefaultScrollOffLines;

    private const int GutterGap = 2; // The blank columns between the line numbers and the text
    private const int MinNumberWidth = 2;
    private int _textWidth; // The width for the text when no line numbers are shown
    private int _gutter; // The columns in use for the line numbers and the gap after them (0: no numbers)

    // The editor of the text (in a split: of the pane with the focus, TextArea.Split.cs), and its place
    private EditorEngine _engine;
    private readonly int _top;
    private int _left;
    private int _width;
    private int _height; // Changed when the terminal window is resized (TextArea.Resize.cs)
    private readonly bool _singleLine;
    private readonly bool _readOnly;
    internal const int SaveAsEntry = 0;
    internal const int SaveAsSettings = 1;
    internal const int SaveAsFile = 2; // A plain text file opened from the file browser

    /// <summary>
    /// A peek: a file shown read-only in a pane of the file browser (":peek"), with the colors of its kind of
    /// text, and what draws the file browser behind it when the screen is drawn again
    /// </summary>
    internal sealed record PeekOptions(SyntaxType Syntax, Action DrawBehind)
    {
        /// <summary>
        /// The message shown when it is opened (null: that of a peek)
        /// </summary>
        internal string? Hint { get; init; }

        /// <summary>
        /// What is said when a change is refused (null: that of a peek)
        /// </summary>
        internal string? ReadOnlyText { get; init; }

        /// <summary>
        /// A comparison (DiffView): the lines where the changes start ("n" "N" "]c" "[c" go to them)
        /// </summary>
        internal IReadOnlyList<int>? Changes { get; init; }

        /// <summary>
        /// A comparison: ":w name" saves the text as a file in this directory (null: it is not saved)
        /// </summary>
        internal string? SaveDirectory { get; init; }

        /// <summary>
        /// Shown without word wrap, whatever the setting is (a peek of the file browser). ":wrap" changes the peek
        /// only, not the setting
        /// </summary>
        internal bool NoWrap { get; init; }

        /// <summary>
        /// Tab in Normal mode closes it, as Esc does (the peek of the file browser, which Tab went into)
        /// </summary>
        internal bool TabCloses { get; init; }

        /// <summary>
        /// The text can be changed (a macro edited in its box), and ":w" gives it to this, which returns null when
        /// it was saved, or why not (the text then stays as it is). Esc and ":q" ask about changes that are not
        /// saved, as in the editor. What would leave the text or open another page is refused (ReadOnlyText)
        /// </summary>
        internal Func<string, string?>? SaveText { get; init; }

        /// <summary>
        /// The text comes from a note of a data file (a macro recorded in a note): yanks go to the clipboard and the
        /// paste bin only as for a note (setting "clipboardnotes")
        /// </summary>
        internal bool FromNote { get; init; }
    }

    // Set for a peek: there is no title or mode divider above the text, it is read only, Esc closes it and Enter
    // asks for the file to be opened in the editor
    private readonly PeekOptions? _peek;

    // A peek that can be changed and saved (PeekOptions.SaveText)
    private bool EditablePeek => _peek?.SaveText != null;

    // The text is that of a note (or comes from one): the clipboard and the paste bin follow "clipboardnotes"
    private bool NoteText => _saveMode == SaveAsEntry || _peek?.FromNote == true;

    /// <summary>
    /// A peek was closed with Enter: the file is to be opened in the editor (at the cursor)
    /// </summary>
    internal bool OpenRequested { get; private set; }

    /// <summary>
    /// The syntax highlighting of the entry shown in the next text area that is made for an entry (the editor or
    /// the preview). It is used once
    /// </summary>
    internal static SyntaxType NextEntrySyntax { get; set; }

    /// <summary>
    /// The next entry opened in the editor is read only (it is used once)
    /// </summary>
    internal static bool NextEntryReadOnly { get; set; }

    /// <summary>
    /// The next editor is opened split (it was closed so): the cursor index of the other pane, and whether that
    /// pane is at the left. It is used once
    /// </summary>
    internal static (int Index, bool OtherLeft)? NextSplit { get; set; }

    /// <summary>
    /// The marks of the next file or note opened, kept in memory from when it was closed in this session (taken by
    /// the editor; never written to disk)
    /// </summary>
    internal static IReadOnlyDictionary<char, TextPos>? NextMarks { get; set; }

    /// <summary>
    /// The marks of the text ("ma"), kept by the host in memory while the text is closed (this session only)
    /// </summary>
    internal Dictionary<char, TextPos> CopyMarks() => _engine.CopyMarks();

    /// <summary>
    /// A file of Marbles opened from the editor, which is closed for it: the snippets (":snip edit", at a line) or
    /// the completions (":complete edit")
    /// </summary>
    internal sealed record SideFile(bool Completions, int Line);

    // The file to open when the editor was closed to open it
    private static SideFile? _sideFile;

    /// <summary>
    /// The editor was closed to open the file of snippets or completions: which, and the line to open it at (and
    /// it is forgotten). The caller opens the file (FileExplorer.EditSideFile), and the text that was edited again
    /// when it is closed
    /// </summary>
    internal static SideFile? TakeSideFileRequest()
    {
        var file = _sideFile;
        _sideFile = null;
        return file;
    }
    private readonly int _saveMode;
    private readonly string _leftMargin;
    private bool _exit;

    // What is currently shown on screen, used to only write the rows that have changed
    private string?[] _shownRows;
    private int[] _shownCells;
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

    /// <inheritdoc cref="TextArea(int, int, int, int, string, int, bool, bool, bool, int)"/>
    /// <param name="modified">Treat the text as changed from the start (it can be saved without editing it)</param>
    /// <param name="peek">Show the text as a peek in a pane of the file browser (null: the editor)</param>
    internal TextArea(int left, int top, int width, int height, string text, int recallCursorIndex, bool scrollToEnd,
        bool readOnly, bool openSearchDialog, int saveMode, bool modified, PeekOptions? peek = null)
    {
        // Taken at once, so it is never left for another editor
        var split = NextSplit;
        NextSplit = null;
        var marks = NextMarks;
        NextMarks = null;
        _peek = peek;
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
        StartFollowingWindow();

        _textWidth = width - 1;
        _engine = new EditorEngine(text ?? string.Empty, _textWidth, _height, _singleLine)
        {
            MaxCells = width - 3,
            // A peek is narrow: no line numbers
            LineNumbers = LineNumbers && !_singleLine && !readOnly && peek == null,
            Wrap = _singleLine || (WordWrap && peek?.NoWrap != true),
            ScrollOffLines = ScrollOffLines,
            ScrollOff = ScrollOff && !_singleLine && !readOnly ? ScrollOffLines : 0,
            // Words are completed while typing, as set (not in a peek or a line of a dialog)
            AutoComplete = Completions.Enabled && !_singleLine && !readOnly && peek == null,
            // The macros played with ":play name" are read from their files
            MacroLoader = MacroFiles.Load
        };
        // The marks set in the text earlier in this session (kept in memory only)
        if (marks != null && !_singleLine && peek == null) _engine.LoadMarks(marks);
        // Tab after ":play " and ":macro " completes the names of the macros
        CommandCompletion.Arguments["play"] = MacroFiles.Names;
        CommandCompletion.Arguments["macro"] = MacroFiles.Names;
        // Tab after ":surround " completes the completions that can surround text, of the kind of text edited (not
        // for a line of a dialog or a peek, which would take the place of the editor's)
        // The ones before are put back when the editor is closed: this text area (and its text) is not kept by them
        CommandCompletion.Arguments.TryGetValue("surround", out var surroundBefore);
        CommandCompletion.Arguments.TryGetValue("sur", out var surBefore);
        if (!_singleLine && peek == null)
        {
            CommandCompletion.Arguments["surround"] = SurroundWords;
            CommandCompletion.Arguments["sur"] = SurroundWords;
        }

        try
        {
            // (read also when completing while typing is off: ":surround" uses them)
            if (!_singleLine) Completions.Refresh();
            // Files of a known kind (Markdown, HTML, CSS, JavaScript, JSON ...) are colored, unless that is turned off,
            // and so is the settings file (as INI) when it is edited from Configuration
            var fileType = peek != null ? peek.Syntax
                : saveMode == SaveAsFile && !_singleLine && !readOnly
                    ? SyntaxHighlighter.TypeOfFile(FileExplorer.CurrentFileName)
                    : saveMode == SaveAsSettings && !_singleLine && !readOnly
                        ? SyntaxType.Ini
                        : SyntaxType.None;
            _engine.CompletionSyntax = fileType;
            if (fileType != SyntaxType.None)
            {
                _engine.SyntaxDefault = fileType;
                if (SyntaxHighlighter.ColorFiles) _engine.Syntax = fileType;
            }

            // Yanks go to the clipboard as set (every yank, or only with "+ in front)
            _engine.ClipboardEveryYank = SystemClipboard.EveryYank;

            // An entry is colored as set for the entry (a tag in its options)
            if (saveMode == SaveAsEntry && !_singleLine && NextEntrySyntax != SyntaxType.None)
            {
                _engine.CompletionSyntax = NextEntrySyntax;
                _engine.SyntaxDefault = NextEntrySyntax;
                _engine.Syntax = NextEntrySyntax;
            }

            NextEntrySyntax = SyntaxType.None;
            // An entry that is read only cannot be changed in the editor
            _engine.ReadOnly = saveMode == SaveAsEntry && !_singleLine && NextEntryReadOnly;
            NextEntryReadOnly = false;
            if (peek is { SaveText: null })
            {
                // A peek only shows the file
                _engine.ReadOnly = true;
                _engine.ReadOnlyMessage = peek.ReadOnlyText ?? DialogText.PeekReadOnly;
                _engine.ChangeLines = peek.Changes;
                // A comparison is colored, also when colors are turned off for files
                if (peek.Changes != null) _engine.Syntax = peek.Syntax;
            }

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

            if (_peek != null)
            {
                // The pane is cleared where the text is written, and the keys of a peek are told
                Array.Fill(_shownCells, width - 1);
                _engine.SetMessage(_peek.Hint ?? DialogText.PeekHint, false);
            }
            else if (!_singleLine && !_readOnly)
            {
                // Split as it was when it was closed
                if (split is { } place)
                {
                    try
                    {
                        RestoreSplit(place.Index, place.OtherLeft);
                    }
                    catch (Exception e) when (e is not NavigationException)
                    {
                        // Opened in one pane: the split could not be made again
                        UndoSplit();
                    }
                }

                // Once, until the first key: where the keys are found (the top row has no room for it)
                _engine.SetMessage(DialogText.EditorHelpHint, false);
            }


            // (a page of its own, also when it is opened from a box: the note of a bookmark in the Jump menu; a field of one
            // row is a part of its box)
            using var outside = _singleLine ? null : ConsoleColors.OutsideBox();
            // (drawn first in the loop, so an error there is handled as one after a key: the text is not lost)
            MainLoop();
        }
        finally
        {
            if (!_singleLine && peek == null)
            {
                RestoreCompletion("surround", surroundBefore);
                RestoreCompletion("sur", surBefore);
            }
        }
    }

    // A completion of the arguments of a command put back as it was before this text area was opened
    private static void RestoreCompletion(string command, Func<IEnumerable<string>>? before)
    {
        if (before != null) CommandCompletion.Arguments[command] = before;
        else CommandCompletion.Arguments.Remove(command);
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
        var first = true;
        while (true)
        {
            try
            {
                if (first)
                {
                    first = false;
                    DrawReadOnlyLabel();
                    Draw();
                }

                RunKeys();
                break;
            }
            catch (Exception ex) when (ex is not NavigationException)
            {
                // An error that was not expected: the changes are offered to be saved (back to the editor to go on)
                if (!RecoverFromError(ex)) break;
            }
        }

        Tools.HideCursor();
    }

    // After an unexpected error: the error is told, and changes that are not saved are offered to be saved until
    // they are, or the user answers No (thrown away). Cancel or Esc goes back to the editor (if it cannot be drawn
    // again, the question is asked again). True: back to the editor
    private bool RecoverFromError(Exception error)
    {
        Canceled = true;
        try
        {
            Tools.HideCursor();
            Tools.ActionUnsupported(error.Message);
        }
        catch (Exception e) when (e is not NavigationException)
        {
            // The question below is what matters
        }

        if (_singleLine || !_engine.IsModified || (_peek != null && !EditablePeek)) return false;
        var note = Lang.T("Esc: back to the text. No: the changes are lost");
        while (true)
        {
            byte answer;
            var asked = true;
            try
            {
                answer = Menu.YesNoCancelPrompt(DialogText.Save, DialogText.SaveChangesYn, note, true);
            }
            catch (Exception e) when (e is not NavigationException)
            {
                // Nothing can be asked: saved, rather than lost
                answer = 1;
                asked = false;
            }

            if (answer == 2) return false;
            if (answer == 1)
            {
                var problem = TrySave();
                if (problem == null)
                {
                    Canceled = false;
                    return false;
                }

                // (not saved, and nothing can be asked: there is nothing more to do)
                if (!asked) return false;
                note = Lang.F("Not saved: {0}", problem);
                continue;
            }

            try
            {
                RedrawAll();
                Canceled = false;
                _exit = false;
                return true;
            }
            catch (Exception e) when (e is not NavigationException)
            {
                note = Lang.F("The text cannot be shown again: {0}", e.Message);
            }
        }
    }

    // Save after an error: null when it was saved, else why not (an error in the save itself is caught)
    private string? TrySave()
    {
        try
        {
            if (Save()) return null;
            return _engine.Message.Length > 0 ? _engine.Message : DialogText.ChangesNotSaved;
        }
        catch (Exception e) when (e is not NavigationException)
        {
            return e.Message;
        }
    }

    // The keys typed in the editor, until it is closed
    private void RunKeys()
    {
        while (!_exit)
        {
            var key = ReadKey();
            if (FollowsWindow && IsCancelKey(key))
            {
                // Ctrl+C that comes as a key (some terminals send it so): as the signal, it does not close
                _engine.SetMessage(DialogText.CancelKeyHint, false);
                Draw();
                continue;
            }

            if (_peek != null && !EditablePeek && key.Key == ConsoleKey.Enter && _engine.Mode == EditorMode.Normal)
            {
                // Enter in a peek: the file in the editor
                OpenRequested = true;
                break;
            }

            // Esc closes a peek, also when a search is highlighted (a key typed before it is only cancelled). In a
            // comparison it removes the highlighting first, so "n" "N" go to the changes again
            // (one that can be changed closes as the editor does: Esc asks about changes that are not saved)
            if (_peek != null && !EditablePeek && key.Key == ConsoleKey.Escape &&
                _engine.Mode == EditorMode.Normal && !_engine.HasPending &&
                !(_peek.Changes != null && _engine.IsSearchHighlighted)) break;
            if (_peek is { TabCloses: true } && key.Key == ConsoleKey.Tab && _engine.Mode == EditorMode.Normal &&
                !_engine.HasPending) break;

            var request = _engine.HandleKey(key);
            if (_peek != null && !EditablePeek && request is EditorRequest.Quit or EditorRequest.ForceQuit
                    or EditorRequest.SaveAndQuit)
            {
                // Esc or :q closes a peek (nothing was changed)
                break;
            }

            if (_peek is { FromNote: true, Changes: not null } && request == EditorRequest.WriteCopy)
            {
                // ":w name" in a comparison of a note: refused, the file would hold the note unencrypted
                _engine.ShowMessage(DialogText.DiffNoteNotSaved, true);
                Draw();
                continue;
            }

            if (_peek?.SaveDirectory != null && request == EditorRequest.WriteCopy)
            {
                // ":w name" in a comparison
                SaveComparison(_peek.SaveDirectory);
                Draw();
                continue;
            }

            if (_peek != null && request is not (EditorRequest.None or EditorRequest.Clipboard
                    or EditorRequest.PasteBinWrite or EditorRequest.Help or EditorRequest.SyntaxChanged
                    or EditorRequest.WrapChanged or EditorRequest.ScrollOffChanged) &&
                !(EditablePeek && request is EditorRequest.Save or EditorRequest.SaveAndQuit
                    or EditorRequest.Quit or EditorRequest.ForceQuit or EditorRequest.PasteBinRead
                    or EditorRequest.StringMemory or EditorRequest.ReplaceDialog or EditorRequest.ClearRegisters))
            {
                // What changes the text, saves it, or opens another page is not done in a peek
                _engine.SetMessage(_peek.ReadOnlyText ?? DialogText.PeekReadOnly, true);
                Draw();
                continue;
            }

            if (_engine.ReadOnly && request is EditorRequest.ClearAll or EditorRequest.PasteBinRead or EditorRequest.Snippets or EditorRequest.StringMemory)
            {
                // Read only: what would only insert or clear text is not opened
                _engine.SetMessage(DialogText.EditorReadOnly, true);
            }
            else if (request != EditorRequest.None)
            {
                HandleRequest(request);
                if (_engine.IsRecording && request is EditorRequest.Snippets or EditorRequest.PasteBinRead
                        or EditorRequest.ReplaceDialog or EditorRequest.StringMemory or EditorRequest.SpellSuggest)
                {
                    _engine.ShowMessage(Lang.T("Not recorded in the macro"), true);
                }
                _engine.ReportReadOnly();
            }

            if (_exit) break;
            UpdateChangeIndicator();
            // When more keys are waiting (as when pasting text) they are all handled before drawing
            if (IsKeyWaiting()) continue;
            Draw();
            // The text could not be drawn (the window was made smaller): drawn again as the window is now
            if (FollowsWindow && Tools.TakeRedrawRequest()) FitToWindow();
        }

        // The key typed right after the Esc that closed it (a peek): for the page shown now
        if (_keyAfterEscape is { } left)
        {
            _keyAfterEscape = null;
            Keyboard.PushBack(left);
        }
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
            case EditorRequest.ForceQuit when _otherPane != null:
                // In a split: only the pane is closed (the text is still shown in the other)
                ClosePane();
                break;
            case EditorRequest.SaveAndQuit when _otherPane != null:
                if (Save()) ClosePane();
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
            case EditorRequest.Split:
                Split();
                break;
            case EditorRequest.SwitchPane:
                SwitchPane();
                break;
            case EditorRequest.ClosePane:
                ClosePane();
                break;
            case EditorRequest.OnlyPane:
                CloseOtherPane();
                break;
            case EditorRequest.NewDocument:
                NewDocument();
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
            case EditorRequest.Clipboard:
            {
                // Yanked text: to the system clipboard and/or the paste bin (the notes of a data file only if allowed)
                var (message, isError) = SystemClipboard.Copy(_engine.ClipboardText, NoteText,
                    !SystemClipboard.EveryYank);
                if (message.Length > 0) _engine.ShowMessage(message, isError);
                _shownStatus = null;
                break;
            }
            case EditorRequest.PasteBinWrite:
            {
                if (NoteText && !SystemClipboard.FromNotes)
                {
                    _engine.ShowMessage(DialogText.ClipboardNotesOff, true);
                    break;
                }

                var append = _engine.PasteBinAppend ?? SystemClipboard.PasteBinAppend;
                var result = SystemClipboard.WritePasteBin(_engine.PasteBinText, append);
                _engine.ShowMessage(result == DialogText.Success
                    ? append ? DialogText.PasteBinAdded : DialogText.PasteBinWritten
                    : result, result != DialogText.Success);
                break;
            }
            case EditorRequest.PasteBinRead:
            {
                var result = SystemClipboard.ReadPasteBin(out var text);
                if (result == DialogText.Success)
                {
                    _engine.InsertAtCursor(text);
                    _engine.ShowMessage(DialogText.PasteBinInserted, false);
                }
                else
                {
                    _engine.ShowMessage(result, true);
                }

                break;
            }
            case EditorRequest.PasteBinClear:
            {
                var result = SystemClipboard.ClearPasteBin();
                _engine.ShowMessage(result == DialogText.Success ? DialogText.PasteBinCleared : result,
                    result != DialogText.Success);
                break;
            }
            case EditorRequest.CopyPath:
            {
                // A note of a data file has no path
                if (_saveMode != SaveAsFile)
                {
                    _engine.ShowMessage(DialogText.NoFilePath, true);
                    break;
                }

                // Also to the string memory, as the current string: "p" puts it in
                var (message, isError) = SystemClipboard.CopyWithMemory(FileExplorer.CurrentFilePath);
                _engine.ShowMessage(message, isError);
                _shownStatus = null;
                break;
            }
            case EditorRequest.Snippets:
            {
                Tools.HideCursor();
                DrawStatusLine(); // Remove the command from the bottom line
                var result = SnippetBox.Show(_engine.SnippetText, SnippetSyntax(), _saveMode == SaveAsEntry);
                RedrawAll();
                if (result.Action == SnippetBox.Action.Insert) _engine.InsertSnippet(result.Text);
                if (result.Action == SnippetBox.Action.Edit) OpenSnippets(result.Line);
                break;
            }
            case EditorRequest.MacroRecord:
                // A warning in a note of a data file: what is typed is saved unencrypted
                Tools.HideCursor();
                MacroBoxes.StartRecording(_engine, _saveMode == SaveAsEntry);
                RedrawAll();
                break;
            case EditorRequest.MacroStop:
                Tools.HideCursor();
                MacroBoxes.Stop(_engine, _saveMode == SaveAsEntry, RedrawAll);
                RedrawAll();
                break;
            case EditorRequest.MacroList:
                Tools.HideCursor();
                DrawStatusLine();
                MacroBoxes.Pick(_engine, _saveMode == SaveAsEntry, RedrawAll);
                RedrawAll();
                break;
            case EditorRequest.Marks:
                Tools.HideCursor();
                DrawStatusLine();
                PickMark();
                RedrawAll();
                break;
            case EditorRequest.StringMemory:
            {
                // The newest first, the current one selected
                Tools.HideCursor();
                DrawStatusLine();
                var all = Tools.GetAllLinesFromLineMemory();
                var count = all.Count;
                var rows = Enumerable.Range(0, count)
                    .Select(i => ((count - i).ToString().PadLeft(2) + "  " + Tools.LineMemoryRow(all[count - 1 - i])))
                    .ToList();
                // The title tells how many strings there are, of how many can be kept
                var picked = PickListBox.Show(Lang.F("String memory ({0} of {1})", count, Tools.MaxLineMemoryCount), rows,
                    i => all[count - 1 - i].TrimEnd(TextBuffer.NewLine), count - 1 - Tools.LineMemoryIndex,
                    DialogText.StringMemoryKeys);
                RedrawAll();
                if (picked >= 0) _engine.PutFromMemory(count - 1 - picked);
                break;
            }
            case EditorRequest.SnippetAdd:
            {
                Tools.HideCursor();
                DrawStatusLine();
                var title = SnippetBox.Add(_engine.SnippetText, _saveMode == SaveAsEntry);
                RedrawAll();
                if (title != null) _engine.ShowMessage(DialogText.SnippetAdded, false);
                break;
            }
            case EditorRequest.SnippetsEdit:
                OpenSnippets(0);
                break;
            case EditorRequest.CompletionsEdit:
                OpenCompletions();
                break;
            case EditorRequest.Diff:
                CompareWithSaved();
                break;
            case EditorRequest.Bookmark:
                // The place of the cursor in the file being edited: ":book" and the Jump menu open the file there
                // (":bookmark name": by the name). A note is in its data file: that one is bookmarked in the Jump
                // menu, and no place in a note is kept (bookmarks.txt is not encrypted)
                if (_saveMode == SaveAsFile && FileExplorer.CurrentFilePath.Length > 0)
                {
                    var message = Bookmarks.AddWithMessage(
                        new JumpTarget(JumpKind.File, FileExplorer.CurrentFilePath)
                        {
                            At = CaretPosition.Of(GetText(), GetCursorIndex())
                        }, _engine.BookmarkName, _engine.BookmarkMovesName, true, out var failed);
                    _engine.ShowMessage(message, failed);
                }
                else
                {
                    _engine.ShowMessage(DialogText.BookmarkNoteNotKept, true);
                }

                break;
            case EditorRequest.Favorite:
                // A file: in or out of the favorites of the file browser. A note: of the favorites of its data file
                if (_saveMode == SaveAsFile && FileExplorer.CurrentFilePath.Length > 0)
                {
                    FileFavorites.Load();
                    var added = FileFavorites.Toggle(FileExplorer.CurrentFilePath);
                    var status = FileFavorites.Save();
                    if (status == DialogText.Success)
                    {
                        _engine.ShowMessage(added ? DialogText.FavoriteAdded : DialogText.FavoriteRemoved, false);
                    }
                    else
                    {
                        _engine.ShowMessage(status, true);
                    }
                }
                else if (_saveMode == SaveAsEntry && Entries.CurrentEntryExists())
                {
                    _engine.ShowMessage(Actions.ToggleCurrentEntryFavorite(), false);
                }
                else
                {
                    _engine.ShowMessage(Lang.T("Only a file or a note can be a favorite"), true);
                }

                break;
            case EditorRequest.WriteCopy:
                _engine.ShowMessage(DialogText.WriteCopyNotHere, true);
                break;
            case EditorRequest.CompleteChanged:
                Completions.Enabled = _engine.AutoComplete;
                if (Completions.Enabled) TurnCompletionsOn();
                if (_saveMode != SaveAsSettings) FileIo.SaveSettings();
                break;
            case EditorRequest.Jump when _engine.IsRecording:
            case EditorRequest.JumpToBookmark when _engine.IsRecording:
            case EditorRequest.Recent when _engine.IsRecording:
                // Leaving would throw the macro away: it is stopped first (Esc)
                _engine.SetMessage(Lang.T("Stop recording the macro first (Esc)"), true);
                break;
            case EditorRequest.Jump:
                Tools.HideCursor();
                // Leaving the text asks about changes that are not saved (":jump colors": that page at once)
                Jump.ToPage(_engine.JumpArgument, LeaveForJump, MoveToBookmark);
                RedrawAll();
                break;
            case EditorRequest.JumpToBookmark:
                Tools.HideCursor();
                Jump.ToBookmark(_engine.BookArgument, LeaveForJump, MoveToBookmark);
                RedrawAll();
                break;
            case EditorRequest.Recent:
            {
                // The latest files, without this one: the one picked is opened as a jump (changes are asked about)
                Tools.HideCursor();
                DrawStatusLine();
                var current = _saveMode == SaveAsFile && FileExplorer.CurrentFilePath.Length > 0
                    ? FileExplorer.CurrentFilePath
                    : null;
                var recent = RecentBox.Pick(current);
                RedrawAll();
                if (recent != null) Jump.ToPlace(RecentBox.TargetOf(recent), LeaveForJump);
                break;
            }
            case EditorRequest.Rename:
                Tools.HideCursor();
                RenameDocument();
                RedrawAll();
                break;
            case EditorRequest.DocumentInfo:
                Tools.HideCursor();
                ShowDocumentInfo();
                RedrawAll();
                break;
            case EditorRequest.ClipboardSettings:
                Tools.HideCursor();
                ClipboardPage.Show();
                _engine.ClipboardEveryYank = SystemClipboard.EveryYank;
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
            case EditorRequest.LineNumbersChanged:
                // Remembered (but not while the settings file itself is being edited)
                LineNumbers = _engine.LineNumbers;
                if (_saveMode != SaveAsSettings) FileIo.SaveSettings();
                break;
            case EditorRequest.WhitespaceToggle:
                ShowWhitespace = !ShowWhitespace;
                if (ShowWhitespace) _engine.ShowMessage(Lang.T("Blank space shown: . space, > tab, $ line end"), false);
                else _engine.ShowMessage(Lang.T("Blank space not shown"), false);
                if (_saveMode != SaveAsSettings) FileIo.SaveSettings();
                break;
            case EditorRequest.WrapChanged:
                if (_peek?.NoWrap == true) break;
                WordWrap = _engine.Wrap;
                if (_saveMode != SaveAsSettings) FileIo.SaveSettings();
                break;
            case EditorRequest.ScrollOffChanged:
                ScrollOff = _engine.ScrollOff > 0;
                ScrollOffLines = _engine.ScrollOffLines;
                if (_saveMode != SaveAsSettings) FileIo.SaveSettings();
                break;
            case EditorRequest.Reload:
            case EditorRequest.ReloadForce:
                ReloadFile(request == EditorRequest.ReloadForce);
                break;
            case EditorRequest.SyntaxChanged:
                // An entry remembers its syntax
                if (_saveMode == SaveAsEntry)
                {
                    Actions.SetEntrySyntax(_engine.Syntax);
                    _shownStatus = null;
                }

                // Colors on or off is remembered for the files that are colored when they are opened
                if (_saveMode == SaveAsFile &&
                    SyntaxHighlighter.TypeOfFile(FileExplorer.CurrentFileName) != SyntaxType.None)
                {
                    SyntaxHighlighter.ColorFiles = _engine.Syntax != SyntaxType.None;
                    FileIo.SaveSettings();
                }

                break;
            case EditorRequest.SaveSettings:
                // Not while the settings file itself is being edited
                if (_saveMode != SaveAsSettings) FileIo.SaveSettings();
                break;
        }
    }

    // ":marks": the marks of the text in a list, each with its line; Enter jumps to the one picked, D deletes it
    private void PickMark()
    {
        while (true)
        {
            var marks = _engine.MarkList;
            if (marks.Count == 0)
            {
                _engine.ShowMessage(Lang.T("No marks: m and a letter sets one (ma)"), false);
                return;
            }

            var rows = marks.Select(mark => mark.Name + "  " + (mark.At.Line + 1).ToString().PadLeft(6) + "  " +
                                            _engine.Buffer[mark.At.Line].Trim().Replace('\t', ' ')).ToList();
            var picked = PickListBox.Show(Lang.T("Marks"), rows, i =>
                    string.Join("\n", Enumerable.Range(marks[i].At.Line,
                            Math.Min(8, _engine.Buffer.LineCount - marks[i].At.Line))
                        .Select(line => _engine.Buffer[line].Replace("\t", "    "))),
                0, Lang.T("j/k Select | Enter Jump | D Delete | / Filter | Esc Close"), (key, i) =>
                {
                    if (key.Key != ConsoleKey.D || (key.Modifiers & ConsoleModifiers.Control) != 0) return false;
                    _engine.DeleteMark(marks[i].Name);
                    return true;
                });
            if (picked == PickListBox.Reload) continue;
            if (picked >= 0) _engine.JumpToMark(marks[picked].Name);
            return;
        }
    }

    private IEnumerable<string> SurroundWords() =>
        Completions.SurroundWords(_engine.Syntax != SyntaxType.None ? _engine.Syntax : _engine.CompletionSyntax);

    // The kind of text being edited, for the snippets made for it (a file of a known kind with its colors off too)
    private SyntaxType SnippetSyntax()
    {
        if (_engine.Syntax != SyntaxType.None) return _engine.Syntax;
        return _saveMode == SaveAsFile ? SyntaxHighlighter.TypeOfFile(FileExplorer.CurrentFileName) : SyntaxType.None;
    }

    // ":snip edit": close the editor to open the file of snippets (at a line). The caller opens it, and this text
    // again when it is closed. Changes that are not saved are asked about first
    private void OpenSnippets(int line)
    {
        if (_saveMode == SaveAsSettings)
        {
            _engine.ShowMessage(DialogText.SnippetsNotHere, true);
            return;
        }

        Tools.HideCursor();
        DrawStatusLine();
        if (!SnippetBox.EnsureFile())
        {
            RedrawAll();
            return;
        }

        OpenSideFile(new SideFile(false, line), Snippets.FilePath, DialogText.SnippetsFileOpen,
            DialogText.SnippetSwitchNote);
    }

    // ":complete edit": the file of completions, as ":snip edit" (it is made first if there is none)
    private void OpenCompletions()
    {
        if (_saveMode == SaveAsSettings)
        {
            _engine.ShowMessage(DialogText.SnippetsNotHere, true);
            return;
        }

        var status = Completions.EnsureFile();
        if (status != DialogText.Success)
        {
            _engine.ShowMessage(status, true);
            return;
        }

        Tools.HideCursor();
        OpenSideFile(new SideFile(true, 0), Completions.FilePath, DialogText.CompletionsFileOpen,
            DialogText.CompletionsSwitchNote);
    }

    // Close the editor to open a file of Marbles: the caller opens it, and this text again when it is closed
    private void OpenSideFile(SideFile file, string path, string isOpenMessage, string switchNote)
    {
        // The file is the one being edited: go to the line
        if (_saveMode == SaveAsFile && FileOperations.SamePath(FileExplorer.CurrentFilePath,
                System.IO.Path.GetFullPath(path)))
        {
            RedrawAll();
            _engine.SetCursorIndex(Snippets.IndexOfLine(GetText(), file.Line));
            _engine.ShowMessage(isOpenMessage, false);
            return;
        }

        if (_engine.IsModified)
        {
            var answer = Menu.YesNoCancelPrompt(DialogText.Save, DialogText.SaveChangesYn, switchNote, false);
            if (answer == 3 || (answer == 1 && !Save()))
            {
                RedrawAll();
                return;
            }
        }

        _sideFile = file;
        Canceled = _engine.IsModified;
        _exit = true;
    }

    // ":diff": the text compared with the file on disk (a file), or with the text saved (an entry). Enter in the
    // comparison goes to the line in this text
    private void CompareWithSaved()
    {
        string saved;
        string name;
        switch (_saveMode)
        {
            case SaveAsFile when FileExplorer.CurrentFilePath.Length > 0:
                try
                {
                    saved = TextFile.Load(FileExplorer.CurrentFilePath).Text;
                }
                catch (Exception e)
                {
                    _engine.ShowMessage(e.Message, true);
                    return;
                }

                name = FileExplorer.CurrentFileName;
                break;
            case SaveAsEntry when Entries.CurrentEntry != null:
                saved = Entries.CurrentEntry.Text;
                name = Entries.CurrentEntry.Title;
                break;
            default:
                _engine.ShowMessage(DialogText.DiffNotHere, true);
                return;
        }

        var directory = _saveMode == SaveAsFile
            ? System.IO.Path.GetDirectoryName(FileExplorer.CurrentFilePath) ?? FileIo.GetDataDirectoryName()
            : FileIo.GetDataDirectoryName();
        Tools.HideCursor();
        var place = DiffView.Show(saved, GetText(), Lang.F("{0} (saved)", name), Lang.F("{0} (edited)", name),
            directory, _engine.DiffIgnoreSpace, string.Empty, out var message, NoteText);
        if (message.Length > 0)
        {
            _engine.ShowMessage(message, false);
            return;
        }

        RedrawAll();
        // Enter: the line in this text (a line that was removed: where it was)
        if (place is { } at) _engine.SetCursorIndex(Snippets.IndexOfLine(GetText(), at.NewLine));
    }

    // ":w name" in a comparison: the text is saved as a file (in the directory, if the name is not a full path).
    // A file that is there is replaced if the user wants that
    private void SaveComparison(string directory)
    {
        if (NoteText)
        {
            // Never a note's text unencrypted on disk
            _engine.ShowMessage(DialogText.DiffNoteNotSaved, true);
            return;
        }

        string path;
        try
        {
            path = System.IO.Path.GetFullPath(System.IO.Path.Combine(directory, _engine.WriteName));
        }
        catch (Exception e)
        {
            _engine.ShowMessage(e.Message, true);
            return;
        }

        var name = System.IO.Path.GetFileName(path);
        if (Directory.Exists(path))
        {
            _engine.ShowMessage(Lang.F("{0} is a directory", name), true);
            return;
        }

        if (File.Exists(path))
        {
            Tools.HideCursor();
            var replace = ChoiceBox.Confirm(DialogText.CaptionsCompare, Lang.F("Replace {0}?", name),
                DialogText.DiffReplaceNote, true);
            RedrawAll();
            if (!replace) return;
        }

        try
        {
            FileIo.WriteAllBytesSafe(path, new System.Text.UTF8Encoding(false).GetBytes(GetText() + "\n"));
            _engine.ShowMessage(Lang.F("Saved as {0}", name), false);
        }
        catch (Exception e)
        {
            _engine.ShowMessage(e.Message, true);
        }
    }

    // Completing was turned on: the file of completions is made if there is none (with the lists it starts with)
    private void TurnCompletionsOn()
    {
        if (!Completions.Exists && Completions.Create() == DialogText.Success)
        {
            _engine.ShowMessage(Lang.F("{0} is made (:complete edit)",
                Completions.FileName), false);
        }

        Completions.Refresh();
    }

    // Before a jump leaves the text: changes that are not saved are asked about (false: stay)
    private bool LeaveForJump()
    {
        var leave = !_engine.IsModified ||
                    Menu.YesNoCancelPrompt(DialogText.Save, DialogText.SaveChangesYn, string.Empty, false) switch
                    {
                        1 => Save(),
                        2 => true,
                        _ => false
                    };
        // A file is opened where the cursor is the next time, as when it is closed
        if (leave && _saveMode == SaveAsFile) FileExplorer.KeepCaretOfCurrentFile(this);
        // An entry is opened split again if it is now
        if (leave && _saveMode == SaveAsEntry)
        {
            Actions.KeepSplitOfEntry(this);
            Actions.KeepMarksOfEntry(this);
        }
        return leave;
    }

    // ":new name": the file or entry to open when the editor is closed for it (and it is forgotten)
    private static string? _newDocument;

    /// <summary>
    /// The editor was closed for ":new name": the name of the new file, made next to the file that was edited, or
    /// the title of the new entry, which the caller makes (and it is forgotten). Null if it was not
    /// </summary>
    internal static string? TakeNewDocumentRequest()
    {
        var name = _newDocument;
        _newDocument = null;
        return name;
    }

    // ":new name" in a text file: a new, empty file in the same directory (a plain name, not one that is there);
    // ":new Title" in an entry: a new entry in the data file. The name is asked for if it is not given, the text
    // being edited is asked about if it has changes, and the new one is opened in its place
    private void NewDocument()
    {
        if (_engine.IsRecording)
        {
            _engine.SetMessage(Lang.T("A new file or note is not made while recording"), true);
            return;
        }

        if (_peek != null || _saveMode is not (SaveAsFile or SaveAsEntry) || FileExplorer.EditingMarblesFile)
        {
            _engine.SetMessage(Lang.T("A new file or note is not made here"), true);
            return;
        }

        var name = _engine.NewName;
        if (name.Length == 0)
        {
            ChoiceBox.Forget();
            name = (_saveMode == SaveAsFile
                ? ChoiceBox.AskText(DialogText.NewFile, DialogText.TypeNameAndPressEnter, DialogText.NameColon, string.Empty)
                : ChoiceBox.AskText(DialogText.New, DialogText.EnterTitleAndPressEnter, DialogText.TitleColon,
                    string.Empty))?.Trim() ?? string.Empty;
            RedrawAll();
            if (name.Length == 0) return;
        }

        if (_saveMode == SaveAsEntry)
        {
            name = Tools.ConstrainTitle(name);
            if (!Tools.IsValid(name)) return;
        }
        else if (FileExplorer.NewFileProblem(name) is { } problem)
        {
            _engine.SetMessage(problem, true);
            return;
        }

        // The text being edited first (saved or not, as asked), then the new file is made
        if (!LeaveForJump())
        {
            RedrawAll();
            return;
        }

        if (_saveMode == SaveAsFile && FileExplorer.CreateFileNextToCurrent(name) is { } failed)
        {
            RedrawAll();
            _engine.SetMessage(failed, true);
            return;
        }

        _newDocument = name;
        Canceled = _engine.IsModified;
        _exit = true;
    }

    // ":rename" and ":title": a file gets another name (the changes that are not saved stay so, and are saved to
    // it with ":w"), and an entry another title (saved at once, as when it is changed in a list)
    private void RenameDocument()
    {
        string status;
        switch (_saveMode)
        {
            case SaveAsFile when _engine.RenameIsTitle:
                _engine.SetMessage(DialogText.TitleIsForEntries, true);
                return;
            case SaveAsFile:
                status = FileExplorer.RenameCurrentFile(_engine.RenameText);
                if (status != DialogText.Success) break;
                // The colors follow the kind of file the new name tells
                var type = SyntaxHighlighter.TypeOfFile(FileExplorer.CurrentFileName);
                _engine.SyntaxDefault = type;
                if (SyntaxHighlighter.ColorFiles) _engine.Syntax = type;
                _engine.SetMessage(Lang.F("Renamed to {0}", FileExplorer.CurrentFileName), false);
                return;
            case SaveAsEntry:
                status = Actions.RetitleCurrentEntry(_engine.RenameText);
                if (status != DialogText.Success) break;
                _engine.SetMessage(DialogText.TitleChanged, false);
                return;
            default:
                status = DialogText.SnippetsNotHere;
                break;
        }

        // Nothing is said when the box was closed without a name
        if (status.Length > 0) _engine.SetMessage(status, true);
    }

    // ":info": the text being edited in a box: its name (a file) or title (an entry), how many characters, words
    // and lines it has as it is now (with the changes that are not saved), and for a file its dates and path
    private void ShowDocumentInfo()
    {
        var text = GetText();
        var rows = new List<(string Label, string Value)>();
        var path = _saveMode == SaveAsFile ? FileExplorer.CurrentFilePath
            : _saveMode == SaveAsSettings ? System.IO.Path.GetFullPath(System.IO.Path.Combine(
                FileIo.GetDataDirectoryName(), FileIo.SettingsFileName))
            : string.Empty;
        var entry = _saveMode == SaveAsEntry && Entries.CurrentEntryExists() ? Entries.CurrentEntry : null;
        if (path.Length > 0) rows.Add((DialogText.InfoName, System.IO.Path.GetFileName(path)));
        else if (entry != null) rows.Add((DialogText.TitleColon, entry.Title));
        rows.Add((Lang.T("Characters:"), Tools.CharacterCount(text).ToString()));
        rows.Add((Lang.T("Words:"), Tools.WordCount(text).ToString()));
        rows.Add((Lang.T("Lines:"), Tools.LineCount(text).ToString()));
        if (path.Length > 0)
        {
            try
            {
                // A file that is not saved yet has no dates
                var info = new FileInfo(path);
                if (info.Exists)
                {
                    rows.Add((DialogText.InfoCreated, info.CreationTime.ToString("yyyy-MM-dd HH:mm:ss")));
                    rows.Add((DialogText.InfoChanged, info.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")));
                }
            }
            catch
            {
                // The dates cannot be read: the rest is shown
            }

            rows.Add((DialogText.InfoPath, path));
        }
        else if (entry != null)
        {
            rows.Add((DialogText.InfoCreated, entry.DateCreated));
            rows.Add((DialogText.InfoChanged, entry.DateChanged));
        }

        ChoiceBox.Forget();
        ChoiceBox.ShowRows(DialogText.InfoCaptionDocument, rows);
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
        // In the middle of the window (also in a split: counted from the pane, it can be left of it)
        var left = Math.Max(0, (Init.LineWidth - ReplaceDialog.Width) / 2) - _left;
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
        Uncover(firstRow, ReplaceDialog.TotalHeight, left + ReplaceDialog.Width);

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
        var boxHeight = rows.Count + 3; // The tab of the title, the frame and the rows
        var firstRow = cursorRow + 1;
        if (firstRow + boxHeight > _height) firstRow = cursorRow - boxHeight;
        firstRow = Math.Clamp(firstRow, 0, Math.Max(0, _height - boxHeight));
        var left = Math.Max(0,
            Math.Min(_leftMargin.Length + _gutter + _engine.Layout.CellOf(cursor),
                Init.LineWidth - 1 - _left - inner - 2));
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
        Uncover(firstRow, boxHeight, left + inner + 2);

        UpdateChangeIndicator();
        Draw();
    }

    private void DrawBox(int firstRow, int left, int inner, string caption, List<string> rows, int selected)
    {
        using var box = ConsoleColors.BoxScope();
        var column = _left + left;
        // The tab of the title on the first row, the box below it
        ChoiceBox.DrawBox(_top + firstRow, column, inner, rows.Count, caption);
        firstRow++;
        for (var i = 0; i < rows.Count; i++)
        {
            Tools.SetCursorEx(_top + firstRow + 1 + i, column + 1);
            ConsoleColors.SetColors(i == selected
                ? ConsoleColors.ColorPreset.HighlightInverted
                : ConsoleColors.ColorPreset.Normal);
            Console.Write(" " + rows[i] + new string(' ', inner - 1 - TextMetrics.WidthOf(rows[i])));
        }

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
        var frameHeight = choices.Length + 4; // The box, from its top line to its bottom line
        var boxHeight = frameHeight + 1; // And the tab of its title above it
        var firstRow = Math.Max(0, (_height - boxHeight) / 2);
        var left = Math.Max(0, (Init.LineWidth - inner - 2) / 2) - _left;
        var column = _left + left;
        DrawStatusLine(); // Remove the command from the bottom line

        // The tab of the title, the frame and the question
        var frameRow = firstRow + 1;
        var selected = 0;
        var result = 0;
        using (ConsoleColors.BoxScope())
        {
            ChoiceBox.DrawBox(_top + firstRow, column, inner, frameHeight - 2, DialogText.Save);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
            Tools.SetCursorEx(_top + frameRow + 1, column + 2);
            Console.Write(" " + DialogText.SaveQuestion + " ");
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);

            while (true)
            {
                for (var i = 0; i < choices.Length; i++)
                {
                    Tools.SetCursorEx(_top + frameRow + 3 + i, column + 2);
                    if (i == selected) ConsoleColors.SetColors(ConsoleColors.ColorPreset.SelectorInverted);
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

                // gg and G: the first and the last choice
                if (key.KeyChar == 'G') selected = choices.Length - 1;
                if (key.KeyChar == 'g' && Console.ReadKey(true).KeyChar == 'g') selected = 0;
            }
        }

        // The rows that were covered are written again when the editor is drawn
        Uncover(firstRow, boxHeight, left + inner + 2);

        return result;
    }

    /// <returns>False if the text could not be saved (the reason is shown as a message)</returns>
    private bool Save()
    {
        // A peek that can be changed is saved by the one that showed it
        if (_peek?.SaveText is { } saveText) return SaveEditablePeek(saveText);
        // A peek is never saved: it shows another file than the one the editor saves to, and maybe only the start
        // of it (it is read only, so this only guards against a request let through by mistake)
        if (_peek != null) return true;
        if (!_engine.IsModified) return true;
        Tools.HideCursor();
        DocumentInfo.HasChanged = true;
        SaveWasInvoked = true;
        if (_saveMode == SaveAsSettings)
        {
            // Save as settings (a write that failed is told, and the text stays changed)
            var written = FileIo.WriteSettingsFile(GetText());
            if (written != DialogText.Success)
            {
                _engine.SetMessage(written, true);
                return false;
            }
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
            // Save entry: when the data file was not saved, the text stays unsaved (":q" warns about it)
            if (!Actions.SaveContentDuringEdit(this))
            {
                _engine.SetMessage(DialogText.ChangesNotSaved, true);
                return false;
            }
        }

        _engine.MarkSaved();
        DocumentInfo.HasChanged = false;
        Menu.InitializeSaveIndicator();
        return true;
    }

    // The text of a peek that can be changed, to the one that showed it (which can ask for a name in a box: all is
    // drawn again after it)
    private bool SaveEditablePeek(Func<string, string?> saveText)
    {
        if (!_engine.IsModified) return true;
        Tools.HideCursor();
        var problem = saveText(GetText());
        RedrawAll();
        if (problem != null)
        {
            _engine.SetMessage(problem, true);
            return false;
        }

        _engine.MarkSaved();
        return true;
    }

    // Show or remove the "unsaved" indicator when the state changes
    // The label "Read only" at the top right, where the label "Unsaved" is shown for a text with changes
    private void DrawReadOnlyLabel()
    {
        if (!_engine.ReadOnly || _peek != null) return;
        // At the right of the top row, where Unsaved is shown (a text that is read only is not changed)
        var label = DialogText.ReadOnlyLabel;
        Tools.SetCursorEx(0, Math.Max(0, Init.LineWidth - 1 - TextMetrics.WidthOf(label)));
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
        Console.Write(label);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

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
        if (_peek != null)
        {
            // The file browser behind the peek, and the peek on top of its pane
            _peek.DrawBehind();
            Array.Fill(_shownRows, null);
            Array.Fill(_shownCells, _width - 1);
            _shownStatus = null;
            Draw();
            return;
        }

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
        DrawReadOnlyLabel();
        Array.Fill(_shownRows, null);
        Array.Fill(_shownCells, 0);
        _shownStatus = null;
        _shownModeLabel = null;
        RedrawSplit();
        Draw();
    }

    // The line numbers take the columns they need: as many as the number of the last line has digits, and
    // the gap. The text is wrapped in the width that is left
    private void UpdateGutter()
    {
        var gutter = 0;
        if (_engine.LineNumbers)
        {
            gutter = Math.Max(MinNumberWidth, _engine.Buffer.LineCount.ToString().Length) + GutterGap;
        }

        if (gutter == _gutter) return;
        _gutter = gutter;
        _engine.SetLayoutWidth(_textWidth - gutter);
        // All rows are written again
        Array.Fill(_shownRows, null);
    }

    private void Draw()
    {
        if (_redrawWhole)
        {
            // A box covered both panes of a split
            _redrawWhole = false;
            RedrawAll();
            return;
        }

        Tools.HideCursor();
        // (each redraw has its time for highlighting the matches of the search)
        _engine.StartHighlighting();
        _otherPane?.Engine.StartHighlighting();
        DrawRows();
        DrawOtherPane();
        if (_readOnly) return;
        if (!_singleLine)
        {
            DrawModeDivider();
            DrawStatusLine();
            if (DrawCommandList()) return;
        }

        PlaceCursor();
    }

    // A bookmark of a place in the file being edited: the cursor goes there, and the file is not opened again (its
    // changes stay as they are). False for any other place
    private bool MoveToBookmark(JumpTarget target)
    {
        if (target is not { Kind: JumpKind.File, At: { } at } || _saveMode != SaveAsFile ||
            FileExplorer.CurrentFilePath.Length == 0 ||
            !FileOperations.SamePath(FileExplorer.CurrentFilePath, target.FullPath))
        {
            return false;
        }

        _engine.JumpToIndex(CaretPosition.IndexOf(GetText(), at.Line, at.Column));
        return true;
    }

    private CommandList? _commandList; // The box of commands above the command line, while it is shown

    // The commands in a box above the command line while a command is typed (not a search). When the command line
    // is closed, what the box covered is drawn again (all of it: also the other pane of a split). True if the
    // window was drawn again (and the cursor placed)
    private bool DrawCommandList()
    {
        if (_engine.Mode == EditorMode.Command && CommandList.Enabled && _peek == null)
        {
            _commandList ??= new CommandList(CommandContext.Editor);
            _commandList.Draw(_engine.CommandText);
            return false;
        }

        if (_commandList == null) return false;
        _commandList = null;
        if (_otherPane != null)
        {
            RedrawAll();
            return true;
        }

        // The rows are written again over the box (without clearing the screen first, which flickers on a slow
        // terminal): each row whole, with the last column of the area that the text never uses (the right edge of
        // the box is there), and the divider and the status line
        Array.Fill(_shownRows, null);
        Array.Fill(_shownCells, _width);
        _shownStatus = null;
        _shownModeLabel = null;
        Draw();
        return true;
    }

    // The rows of the text that have changed since they were written
    private void DrawRows()
    {
        UpdateGutter();
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
    }

    private string? _shownModeLabel; // The mode written on the divider above the text

    // The mode on the divider above the text: EDIT (Insert), NAVIGATE (Normal), SELECT (the Visual modes) or
    // COMMAND (the command line and search). Written when it changes
    private void DrawModeDivider()
    {
        // A peek has no divider above it: the pane is shown there
        if (_top < 1 || _peek != null) return;
        if (_engine.CompletionList.Count > 0)
        {
            DrawCompletions();
            return;
        }

        var label = _engine.IsJumping ? DialogText.DividerJump : _engine.Mode switch
        {
            EditorMode.Insert => DialogText.CaptionsEdit,
            EditorMode.Visual or EditorMode.VisualLine or EditorMode.VisualBlock => DialogText.DividerSelect,
            EditorMode.Command or EditorMode.Search => DialogText.DividerCommand,
            _ => DialogText.DividerNavigate
        };
        if (label == _shownModeLabel) return;
        _shownModeLabel = label;
        Tools.SetCursorEx(_top - 1, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(new string('-', Init.LineWidth - 1));
        // Over the pane with the focus in a split
        Tools.SetCursorEx(_top - 1, Math.Max(0, ModeCenter - (TextMetrics.WidthOf(label) / 2 + 1)));
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.EditorTitle);
        Console.Write(" " + label + " ");
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    // The longest a completion is shown on the divider
    private const int MaxCompletionWidth = 24;

    // The completions of the word before the cursor, on the divider above the text: the one chosen with Tab is
    // highlighted, and the keys are shown at the right
    private void DrawCompletions()
    {
        var list = _engine.CompletionList;
        var chosen = _engine.CompletionIndex;
        var hints = list.Count == 1 ? DialogText.CompleteOneKeys
            : chosen < 0 ? DialogText.CompleteChooseKeys : DialogText.CompleteManyKeys;
        var shown = "\u0001" + chosen + "\u0001" + string.Join("\u0001", list.Select(item => item.Text));
        if (shown == _shownModeLabel) return;
        _shownModeLabel = shown;
        var width = Init.LineWidth - 1;
        Tools.SetCursorEx(_top - 1, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(new string('-', width));
        // The keys at the right (as Menu.WriteKeyHints draws them: each with three cells around it)
        var hintsWidth = hints.Split(" | ").Sum(hint => TextMetrics.WidthOf(hint) + 3) + 1;
        var hintsAt = Math.Max(0, width - 2 - hintsWidth);
        var column = 2;
        for (var i = 0; i < list.Count; i++)
        {
            var text = " " + Tools.TruncateString(list[i].Shown, MaxCompletionWidth, true) + " ";
            var cells = TextMetrics.WidthOf(text);
            if (column + cells > hintsAt - 1) break;
            Tools.SetCursorEx(_top - 1, column);
            ConsoleColors.SetColors(i == chosen
                ? ConsoleColors.ColorPreset.KeyHintInverted
                : ConsoleColors.ColorPreset.EditorTitle);
            Console.Write(text);
            column += cells + 1;
        }

        Tools.SetCursorEx(_top - 1, hintsAt);
        Menu.WriteKeyHints(hints);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    // Build the content of a row: the text of a segment of a line, split where the color changes
    private int BuildRow(StringBuilder row, int line, int segment)
    {
        if (_gutter == 0) return BuildTextRow(row, line, segment);
        // The number of the line on its first row (the following rows of a long line have none); a line with a mark
        // shows its letter instead
        var mark = segment == 0 ? _engine.MarkOnLine(line) : '\0';
        var number = segment != 0 ? string.Empty : mark != '\0' ? mark.ToString() : (line + 1).ToString();
        row.Append(StyleMarker)
            .Append((char)('0' + (line == _engine.Cursor.Line ? StyleCurrentLineNumber : StyleLineNumber)))
            .Append(number.PadLeft(_gutter - GutterGap)).Append(' ', GutterGap);
        var textStart = row.Length;
        var cells = BuildTextRow(row, line, segment);
        // The text follows as a part of its own
        if (row.Length == textStart || row[textStart] != StyleMarker)
        {
            row.Insert(textStart, new string(new[] { StyleMarker, (char)('0' + StyleNormal) }));
        }

        return cells + _gutter;
    }

    // The text of a row: a segment of a wrapped line, or the part of the line in view
    private int BuildTextRow(StringBuilder row, int line, int segment)
    {
        var layout = _engine.Layout;
        var s = _engine.Buffer[line];
        if (layout.Wrap)
        {
            var segmentEnd = layout.SegmentEnd(line, segment);
            var rangeStart = row.Length;
            var shown = BuildRange(row, line, s, layout.SegmentStart(line, segment), segmentEnd);
            return shown + LineEndMark(row, rangeStart, s, segmentEnd, shown, layout.Width);
        }

        // Not wrapped: the cells from LeftCell, with "<" and ">" where text is hidden to the left or the right
        var rowStart = row.Length;
        var left = _engine.LeftCell;
        var width = layout.Width;
        var leftMark = left > 0 && s.Length > 0;
        var rightMark = TextMetrics.WidthOf(s) > left + width;
        var from = left + (leftMark ? 1 : 0);
        var to = left + width - (rightMark ? 1 : 0);
        var col = 0;
        var cell = 0;
        while (col < s.Length && cell < from)
        {
            var length = TextMetrics.ElementLength(s, col);
            cell += TextMetrics.Width(s, col, length);
            col += length;
        }

        var start = col;
        // A wide char cut by the left side is shown as blank space
        var padLeft = Math.Max(0, cell - from);
        while (col < s.Length)
        {
            var length = TextMetrics.ElementLength(s, col);
            var next = cell + TextMetrics.Width(s, col, length);
            if (next > to) break;
            cell = next;
            col += length;
        }

        var end = col;
        var cells = 0;
        if (leftMark)
        {
            row.Append(StyleMarker).Append((char)('0' + StyleLineNumber)).Append('<');
            cells++;
        }

        if (padLeft > 0)
        {
            row.Append(StyleMarker).Append((char)('0' + StyleNormal)).Append(' ', padLeft);
            cells += padLeft;
        }

        var textStart = row.Length;
        cells += start < end || end == s.Length ? BuildRange(row, line, s, start, end) : 0;
        if (rightMark)
        {
            // A wide char that does not fit before the mark leaves a blank space
            var gap = Math.Max(0, to - cell);
            if (row.Length > textStart && row[textStart] != StyleMarker)
            {
                row.Insert(textStart, new string(new[] { StyleMarker, (char)('0' + StyleNormal) }));
            }

            if (gap > 0) row.Append(StyleMarker).Append((char)('0' + StyleNormal)).Append(' ', gap);
            row.Append(StyleMarker).Append((char)('0' + StyleLineNumber)).Append('>');
            cells += gap + 1;
        }
        else if (row.Length > textStart && row[textStart] != StyleMarker && textStart > rowStart)
        {
            row.Insert(textStart, new string(new[] { StyleMarker, (char)('0' + StyleNormal) }));
        }

        if (!rightMark) cells += LineEndMark(row, rowStart, s, end, cells, width);
        return cells;
    }

    // The "$" at the end of a line while blank space is shown, when the row has room for it (the cells it took).
    // A row with colors has its colors from its start: the text from "start" is given its own, if it has none
    private int LineEndMark(StringBuilder row, int start, string s, int end, int cells, int width)
    {
        if (!ShowsWhitespace || end != s.Length || cells >= width) return 0;
        if (row.Length > start && row[start] != StyleMarker)
        {
            row.Insert(start, new string(new[] { StyleMarker, (char)('0' + StyleNormal) }));
        }

        row.Append(StyleMarker).Append((char)('0' + StyleWhitespace)).Append('$');
        return 1;
    }

    // A part of a line as written to the terminal: control characters as blank space, and while blank space is
    // shown, a space as "." and a tab as ">"
    private string Visible(string s, int start, int end)
    {
        if (!ShowsWhitespace) return TextMetrics.Printable(s, start, end);
        var builder = new StringBuilder(end - start);
        for (var i = start; i < end; i++)
        {
            builder.Append(s[i] switch
            {
                ' ' => '.',
                '\t' => '>',
                _ when char.IsControl(s[i]) => ' ',
                _ => s[i]
            });
        }

        return builder.ToString();
    }

    // The text of a part of a line (from the char index "start" to "end"), with its colors
    private int BuildRange(StringBuilder row, int line, string s, int start, int end)
    {
        var cells = TextMetrics.WidthOfRange(s, start, end);
        var selection = _engine.Selection(line);
        var hasMatches = false;
        foreach (var _ in _engine.SearchMatches(line).Concat(_engine.FoundMatches(line)))
        {
            hasMatches = true;
            break;
        }

        // While a jump to a word is made: its letters, shown in place of the first characters of the word
        var labels = JumpLabelsOf(line, s);

        // The common case is a row in a single color
        var misspelled = _engine.Misspelled(line);
        var markdown = _engine.SyntaxStyles(line);
        var blank = ShowsWhitespace && s.AsSpan(start, end - start).IndexOfAny(' ', '\t') >= 0;
        if (selection == null && !hasMatches && misspelled.Count == 0 && markdown == null && labels == null &&
            !blank)
        {
            row.Append(TextMetrics.Printable(s, start, end));
            return cells;
        }

        var styles = new int[end - start];
        if (markdown != null)
        {
            // The colors of the text are below the marks: misspelled words, search matches and the selection
            for (var i = start; i < end && i < markdown.Length; i++)
            {
                if (markdown[i] != SyntaxStyle.None) styles[i - start] = StyleMarkdown + (int)markdown[i];
            }
        }

        if (blank)
        {
            // Dimmed, and highlighted at the end of the line (below the other marks). The spaces of a line break of
            // Markdown, which ":trim" keeps, are only dimmed
            var trailing = _engine.IsLineBreakSpace(line) ? s.Length : EditorEngine.TrailingBlankStart(s);
            for (var i = start; i < end; i++)
            {
                if (s[i] is ' ' or '\t') styles[i - start] = i >= trailing ? StyleTrailingSpace : StyleWhitespace;
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

        foreach (var (matchStart, matchEnd) in _engine.FoundMatches(line))
        {
            for (var i = Math.Max(start, matchStart); i < Math.Min(end, matchEnd); i++)
            {
                styles[i - start] = StyleFound;
            }
        }

        if (selection != null)
        {
            for (var i = Math.Max(start, selection.Value.Start); i < Math.Min(end, selection.Value.End); i++)
            {
                styles[i - start] = StyleSelection;
            }
        }

        if (labels != null)
        {
            foreach (var (at, (_, next)) in labels)
            {
                var length = Math.Max(1, TextMetrics.ElementLength(s, at));
                for (var i = Math.Max(start, at); i < Math.Min(end, at + length); i++)
                {
                    styles[i - start] = next ? StyleJumpNext : StyleJumpLabel;
                }
            }
        }

        var runStart = start;
        for (var i = start; i <= end; i++)
        {
            if (i < end && styles[i - start] == styles[runStart - start]) continue;
            if (i > runStart)
            {
                row.Append(StyleMarker).Append((char)('0' + styles[runStart - start]));
                AppendShown(row, s, runStart, i, labels);
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

    // The letters of the jump to a word on a line, by the char index of the character they are shown in place of
    // (Next: the letter to type after the first). Null when there are none. The second letter is shown over the
    // character after the first, when that is on the line and not a tab
    private Dictionary<int, (char Letter, bool Next)>? JumpLabelsOf(int line, string s)
    {
        Dictionary<int, (char, bool)>? labels = null;
        foreach (var (col, label) in _engine.JumpLabels(line))
        {
            if (label.Length == 0 || col >= s.Length) continue;
            labels ??= new Dictionary<int, (char, bool)>();
            labels[col] = (label[0], false);
            var after = col + Math.Max(1, TextMetrics.ElementLength(s, col));
            if (label.Length > 1 && after < s.Length && s[after] != '\t' &&
                TextMetrics.WidthOfRange(s, after, after + Math.Max(1, TextMetrics.ElementLength(s, after))) > 0)
            {
                labels[after] = (label[1], true);
            }
        }

        return labels;
    }

    // A part of a line as shown, with the letters of a jump in place of the characters they cover (a wide character
    // keeps its width: the letter and a blank space)
    private void AppendShown(StringBuilder row, string s, int start, int end,
        Dictionary<int, (char Letter, bool Next)>? labels)
    {
        if (labels == null)
        {
            row.Append(Visible(s, start, end));
            return;
        }

        var from = start;
        for (var i = start; i < end; i += Math.Max(1, TextMetrics.ElementLength(s, i)))
        {
            if (!labels.TryGetValue(i, out var label)) continue;
            if (i > from) row.Append(Visible(s, from, i));
            var after = i + Math.Max(1, TextMetrics.ElementLength(s, i));
            row.Append(label.Letter).Append(' ', Math.Max(0, TextMetrics.WidthOfRange(s, i, after) - 1));
            from = after;
        }

        if (end > from) row.Append(Visible(s, from, end));
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
                StyleFound => ConsoleColors.ColorPreset.SelectorInverted,
                StyleJumpLabel => ConsoleColors.ColorPreset.KeyHintInverted,
                StyleJumpNext => ConsoleColors.ColorPreset.Accent,
                StyleWhitespace => ConsoleColors.ColorPreset.LineNumber,
                StyleTrailingSpace => ConsoleColors.ColorPreset.WarningInverted,
                StyleMisspelled => ConsoleColors.ColorPreset.WarningInverted,
                StyleLineNumber => ConsoleColors.ColorPreset.LineNumber,
                StyleCurrentLineNumber => ConsoleColors.ColorPreset.CurrentLineNumber,
                _ => ConsoleColors.ColorPreset.Normal
            });
            return;
        }

        ConsoleColors.SetSyntaxColor(SyntaxColorOf((SyntaxStyle)(style - StyleMarkdown)));
    }

    /// <summary>
    /// The color of the color scheme (Syntax 1 to 12, counted from 0) a kind of colored text is shown in: the
    /// colors are shared by Markdown, HTML, CSS, JavaScript, JSON and XML
    /// </summary>
    internal static int SyntaxColorOf(SyntaxStyle style)
    {
        return style switch
        {
            // 1: the words of a language, tags and selectors; bold and list marks
            SyntaxStyle.Keyword or SyntaxStyle.Bold or SyntaxStyle.ListMarker or SyntaxStyle.DiffFile => 0,
            // 2: the names of attributes and properties; italic
            SyntaxStyle.Name or SyntaxStyle.Italic => 1,
            // 3: text in quotes and values; code
            SyntaxStyle.String or SyntaxStyle.Code or SyntaxStyle.CodeBlock => 2,
            // 4: numbers
            SyntaxStyle.Number => 3,
            // 5: links
            SyntaxStyle.Link => 4,
            // 7 to 12: the headings of Markdown
            SyntaxStyle.Heading1 => 6,
            SyntaxStyle.Heading2 => 7,
            SyntaxStyle.Heading3 => 8,
            SyntaxStyle.Heading4 => 9,
            SyntaxStyle.Heading5 => 10,
            SyntaxStyle.Heading6 => 11,
            // A comparison: the colors of headings 1, 5 and 4 (red, green and cyan in the Default scheme)
            SyntaxStyle.DiffRemoved => 6,
            SyntaxStyle.DiffAdded => 10,
            SyntaxStyle.DiffPart => 9,
            // 6: comments, quotes, web addresses and rules
            _ => 5
        };
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
            case EditorMode.VisualBlock:
                label = DialogText.EditorModeVisualBlock;
                break;
            default:
                label = DialogText.EditorModeNormal;
                break;
        }

        // The mode is highlighted, with a blank space on each side inside the highlight (" NORMAL ")
        if (label.Length > 0) label = " " + label + " ";

        if (text.Length == 0 && _engine.Message.Length > 0)
        {
            // A message follows the blank space after the mode (" NORMAL  Saved ---"). An error is
            // highlighted, with a blank space on each side inside the highlight
            text = (_engine.MessageIsError ? " " : string.Empty) + _engine.Message + " ";
        }

        // Pending keys, string memory, line:column and position in the document
        var cursor = _engine.Cursor;
        var rows = _engine.Layout.TotalRows();
        var percent = rows <= 1 ? 100 : _engine.Layout.RowOf(cursor) * 100 / (rows - 1);
        var column = TextMetrics.WidthOfRange(_engine.Buffer[cursor.Line], 0, cursor.Col) + 1;
        var right = " ";
        if (_engine.PendingKeys.Length > 0) right += _engine.PendingKeys + " | ";
        // A macro is being recorded (English, as the names of the modes)
        if (_engine.IsRecording) right += "REC | ";
        // The match of the search the cursor is on, and how many there are: "[2/12]", "[-/12]" when it is on none
        // (English, as Vim shows it)
        if (_engine.GetSearchCount(out var current, out var total))
        {
            right += (total > EditorEngine.SearchCountLimit
                ? "[>" + EditorEngine.SearchCountLimit + "]"
                : "[" + (current > 0 ? current.ToString() : "-") + "/" + total + "]") + " | ";
        }

        if (Tools.HasLineMemory()) right += DialogText.EditorMemory + " " + Tools.GetLineMemoryPosition() + " | ";
        if (_engine.IsSpellOn) right += DialogText.SpellStatus + " " + _engine.SpellLanguages + " | ";
        // A text file: its type and encoding
        // (not in a peek or a comparison: it is not the file being edited)
        if (_saveMode == SaveAsFile && _peek == null) right += FileExplorer.CurrentFileLabel + " | ";
        right += (cursor.Line + 1) + ":" + column + " | " + percent + "% ";

        // A blank space between the mode and the line, as a break in the line
        var gap = label.Length > 0 ? " " : string.Empty;
        // A message comes before what is at the right (it is shown until the next key): if it does not fit, only
        // the line and column are kept, and else nothing
        var room = width - label.Length - gap.Length;
        // (all that is at the right can be wider than the line too, in a narrow window or a long language: then
        // the line and column are kept, as with a message)
        if (TextMetrics.WidthOf(right) > room) right = " " + (cursor.Line + 1) + ":" + column + " ";
        if (TextMetrics.WidthOf(right) > room) right = string.Empty;
        if (text.Length > 0 && TextMetrics.WidthOf(text) > room - right.Length)
        {
            right = " " + (cursor.Line + 1) + ":" + column + " ";
            if (TextMetrics.WidthOf(text) > room - right.Length) right = string.Empty;
        }

        // Make it fit
        var available = room - right.Length;
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
        var status = label + gap + StyleMarker + text + StyleMarker + filler + right + _engine.MessageIsError +
                     _engine.MessageIsCommandError;
        if (status == _shownStatus) return;
        _shownStatus = status;

        Tools.SetCursorEx(Init.LineCount - 1, 0);
        // The mode label has a color of its own for each mode
        ConsoleColors.SetColors(_engine.Mode switch
        {
            EditorMode.Insert => ConsoleColors.ColorPreset.AttentionInverted,
            EditorMode.Visual or EditorMode.VisualLine or EditorMode.VisualBlock =>
                ConsoleColors.ColorPreset.WarningInverted,
            _ => ConsoleColors.ColorPreset.HighlightInverted
        });
        Console.Write(label);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Console.Write(gap);
        ConsoleColors.SetColors(commandLine
            ? ConsoleColors.ColorPreset.CommandLineInverted
            : _engine.MessageIsCommandError
                ? ConsoleColors.ColorPreset.CommandErrorInverted
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
                _left + _leftMargin.Length + _gutter + _engine.Layout.CellOf(cursor) - _engine.LeftCell);
        }

        Tools.ShowCursor();
    }

    #endregion
}

internal static class DocumentInfo
{
    public static bool HasChanged;
}
