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
    private const int StyleLineNumber = 4;
    private const int StyleCurrentLineNumber = 5; // The number of the line the cursor is on
    private const int StyleMarkdown = 10; // The styles of Markdown follow from this number

    /// <summary>
    /// True to show line numbers in the editor (setting "linenumbers")
    /// </summary>
    internal static bool LineNumbers { get; set; }

    /// <summary>
    /// Long lines are wrapped (setting "wrap", on unless turned off)
    /// </summary>
    internal static bool WordWrap { get; set; } = true;

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
    private readonly int _textWidth; // The width for the text when no line numbers are shown
    private int _gutter; // The columns in use for the line numbers and the gap after them (0: no numbers)

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
    }

    // Set for a peek: there is no title or mode divider above the text, it is read only, Esc closes it and Enter
    // asks for the file to be opened in the editor
    private readonly PeekOptions? _peek;

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
    /// <param name="peek">Show the text as a peek in a pane of the file browser (null: the editor)</param>
    internal TextArea(int left, int top, int width, int height, string text, int recallCursorIndex, bool scrollToEnd,
        bool readOnly, bool openSearchDialog, int saveMode, bool modified, PeekOptions? peek = null)
    {
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

        _textWidth = width - 1;
        _engine = new EditorEngine(text ?? string.Empty, _textWidth, _height, _singleLine)
        {
            MaxCells = width - 3,
            // A peek is narrow: no line numbers
            LineNumbers = LineNumbers && !_singleLine && !readOnly && peek == null,
            Wrap = WordWrap || _singleLine,
            ScrollOffLines = ScrollOffLines,
            ScrollOff = ScrollOff && !_singleLine && !readOnly ? ScrollOffLines : 0,
            // Words are completed while typing, as set (not in a peek or a line of a dialog)
            AutoComplete = Completions.Enabled && !_singleLine && !readOnly && peek == null
        };
        if (_engine.AutoComplete) Completions.Refresh();
        // Files of a known kind (Markdown, HTML, CSS, JavaScript, JSON) are colored, unless that is turned off
        var fileType = peek != null ? peek.Syntax
            : saveMode == SaveAsFile && !_singleLine && !readOnly
                ? SyntaxHighlighter.TypeOfFile(FileExplorer.CurrentFileName)
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
        if (peek != null)
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

        DrawReadOnlyLabel();
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
                if (_peek != null && key.Key == ConsoleKey.Enter && _engine.Mode == EditorMode.Normal)
                {
                    // Enter in a peek: the file in the editor
                    OpenRequested = true;
                    break;
                }

                // Esc closes a peek, also when a search is highlighted (a key typed before it is only cancelled). In a
                // comparison it removes the highlighting first, so "n" "N" go to the changes again
                if (_peek != null && key.Key == ConsoleKey.Escape && _engine.Mode == EditorMode.Normal &&
                    !_engine.HasPending && !(_peek.Changes != null && _engine.IsSearchHighlighted)) break;

                var request = _engine.HandleKey(key);
                if (_peek != null && request is EditorRequest.Quit or EditorRequest.ForceQuit
                        or EditorRequest.SaveAndQuit)
                {
                    // Esc or :q closes a peek (nothing was changed)
                    break;
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
                        or EditorRequest.WrapChanged or EditorRequest.ScrollOffChanged))
                {
                    // What changes the text, saves it, or opens another page is not done in a peek
                    _engine.SetMessage(_peek.ReadOnlyText ?? DialogText.PeekReadOnly, true);
                    Draw();
                    continue;
                }

                if (_engine.ReadOnly && request is EditorRequest.InsertCalendar or EditorRequest.ClearAll
                        or EditorRequest.PasteBinRead or EditorRequest.Snippets or EditorRequest.StringMemory)
                {
                    // Read only: what would only insert or clear text is not opened
                    _engine.SetMessage(DialogText.EditorReadOnly, true);
                }
                else if (request != EditorRequest.None)
                {
                    HandleRequest(request);
                    _engine.ReportReadOnly();
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
                Menu.YesNoCancelPrompt(DialogText.Save, DialogText.SaveChangesYn, string.Empty, true) == 1)
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
            case EditorRequest.Clipboard:
            {
                // Yanked text: to the system clipboard and/or the paste bin (the notes of a data file only if allowed)
                var (message, isError) = SystemClipboard.Copy(_engine.ClipboardText, _saveMode == SaveAsEntry,
                    !SystemClipboard.EveryYank);
                if (message.Length > 0) _engine.ShowMessage(message, isError);
                _shownStatus = null;
                break;
            }
            case EditorRequest.PasteBinWrite:
            {
                if (_saveMode == SaveAsEntry && !SystemClipboard.FromNotes)
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

                var (message, isError) = SystemClipboard.Copy(FileExplorer.CurrentFilePath, false, true);
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
            case EditorRequest.WriteCopy:
                _engine.ShowMessage(DialogText.WriteCopyNotHere, true);
                break;
            case EditorRequest.CompleteChanged:
                Completions.Enabled = _engine.AutoComplete;
                if (Completions.Enabled) TurnCompletionsOn();
                if (_saveMode != SaveAsSettings) FileIo.SaveSettings();
                break;
            case EditorRequest.Jump:
                Tools.HideCursor();
                // Leaving the text asks about changes that are not saved
                Jump.Open(() =>
                {
                    var leave = !_engine.IsModified ||
                                Menu.YesNoCancelPrompt(DialogText.Save, DialogText.SaveChangesYn, string.Empty, false)
                                    switch
                                    {
                                        1 => Save(),
                                        2 => true,
                                        _ => false
                                    };
                    // A file is opened where the cursor is the next time, as when it is closed
                    if (leave && _saveMode == SaveAsFile) FileExplorer.KeepCaretOfCurrentFile(this);
                    return leave;
                });
                RedrawAll();
                break;
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
            case EditorRequest.LineNumbersChanged:
                // Remembered (but not while the settings file itself is being edited)
                LineNumbers = _engine.LineNumbers;
                if (_saveMode != SaveAsSettings) FileIo.SaveSettings();
                break;
            case EditorRequest.WrapChanged:
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
            directory, _engine.DiffIgnoreSpace, string.Empty, out var message);
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
        for (var r = firstRow; r < firstRow + ReplaceDialog.TotalHeight && r < _height; r++)
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
        // The tab of the title on the first row, the box below it
        ChoiceBox.DrawTitleTab(_top + firstRow, column, inner, caption);
        firstRow++;
        for (var i = 0; i < rows.Count; i++)
        {
            Tools.SetCursorEx(_top + firstRow + 1 + i, column);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.BoxBorder);
            Console.Write('|');
            ConsoleColors.SetColors(i == selected
                ? ConsoleColors.ColorPreset.HighlightInverted
                : ConsoleColors.ColorPreset.Normal);
            Console.Write(" " + rows[i] + new string(' ', inner - 1 - TextMetrics.WidthOf(rows[i])));
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.BoxBorder);
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
        var frameHeight = choices.Length + 4; // The box, from its top line to its bottom line
        var boxHeight = frameHeight + 1; // And the tab of its title above it
        var firstRow = Math.Max(0, (_height - boxHeight) / 2);
        var left = Math.Max(0, (Init.LineWidth - inner - 2) / 2 - _left);
        var column = _left + left;
        DrawStatusLine(); // Remove the command from the bottom line

        // The tab of the title, the frame and the question
        var frameRow = firstRow + 1;
        ChoiceBox.DrawTitleTab(_top + firstRow, column, inner, DialogText.Save);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.BoxBorder);
        for (var row = 1; row < frameHeight - 1; row++)
        {
            Tools.SetCursorEx(_top + frameRow + row, column);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.BoxBorder);
            Console.Write('|');
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Console.Write(new string(' ', inner));
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.BoxBorder);
            Console.Write('|');
        }

        Tools.SetCursorEx(_top + frameRow + frameHeight - 1, column);
        Console.Write(AsciiStyles.GetBottomCorner() + new string('-', inner) + AsciiStyles.GetBottomCorner());
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
        Tools.SetCursorEx(_top + frameRow + 1, column + 2);
        Console.Write(" " + DialogText.SaveQuestion + " ");
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);

        var selected = 0;
        var result = 0;
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
    // The label "Read only" at the top right, where the label "Unsaved" is shown for a text with changes
    private void DrawReadOnlyLabel()
    {
        if (!_engine.ReadOnly || _peek != null) return;
        var label = " " + Lang.T("Read only") + " ";
        Tools.SetCursorEx(2, Math.Max(0, Init.LineWidth - TextMetrics.WidthOf(label)));
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
        Tools.HideCursor();
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

        if (_readOnly) return;
        if (!_singleLine)
        {
            DrawModeDivider();
            DrawStatusLine();
        }

        PlaceCursor();
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

        var label = _engine.Mode switch
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
        Console.Write(new string('-', Init.LineWidth));
        Tools.SetCursorEx(_top - 1, Math.Max(0, Init.LineWidth / 2 - (TextMetrics.WidthOf(label) / 2 + 1)));
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
        var width = Init.LineWidth;
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
        // The number of the line on its first row (the following rows of a long line have none)
        var number = segment == 0 ? (line + 1).ToString() : string.Empty;
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
            return BuildRange(row, line, s, layout.SegmentStart(line, segment), layout.SegmentEnd(line, segment));
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

        return cells;
    }

    // The text of a part of a line (from the char index "start" to "end"), with its colors
    private int BuildRange(StringBuilder row, int line, string s, int start, int end)
    {
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
        var markdown = _engine.SyntaxStyles(line);
        if (selection == null && !hasMatches && misspelled.Count == 0 && markdown == null)
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

        if (text.Length == 0 && _engine.Message.Length > 0)
        {
            // A message follows the blank space after the mode ("-- NORMAL -- Saved ---"). An error is
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
        if (Tools.HasLineMemory()) right += DialogText.EditorMemory + " " + Tools.GetLineMemoryPosition() + " | ";
        if (_engine.IsSpellOn) right += DialogText.SpellStatus + " " + _engine.SpellLanguages + " | ";
        // A text file: its type and encoding
        // (not in a peek or a comparison: it is not the file being edited)
        if (_saveMode == SaveAsFile && _peek == null) right += FileExplorer.CurrentFileLabel + " | ";
        right += (cursor.Line + 1) + ":" + column + " | " + percent + "% ";

        // A blank space between the mode and the line, as a break in the line
        var gap = label.Length > 0 ? " " : string.Empty;
        // Make it fit
        var available = width - label.Length - gap.Length - right.Length;
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
        var status = label + gap + StyleMarker + text + StyleMarker + filler + right + _engine.MessageIsError;
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
