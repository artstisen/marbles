using System.Text;
using System.Text.RegularExpressions;

namespace marbles;
/* Console Text Editor: TextArea v1.3.1 (LBS, Mar 2024) */
/* Originally based on the work by Haydend's TextArea control that was included with the ConsoleDraw project (github.com/Haydend/ConsoleDraw) */
/* Extracted into a single class (for platform independent plug-and-play implementation), heavily reworked and refactored with major performance improvements,
   lots of new functionality and UTF-8 support */
/*
    Editor Keyboard Functions
    Primary shortcuts
    CTRL+E ......... Save
    CTRL+Q ......... Save and close editor
    Esc ............ Deselect/Close editor

    Compound keyboard shortcuts
    CTRL+K ......... Repeat last shortcut
    CTRL+W ......... Enter shortcuts mode
    W .............. Sustain (lock) shortcuts mode
    Esc ............ Exit shortcuts mode
    Z .............. Undo (500 steps)
    Y .............. Redo (250 steps)
    S .............. Content search
    L .............. Line delete
    D .............. Line duplicate
    U .............. Line/selection to UPPERCASE
    G .............. Line/selection to lowercase
    S .............. Trim blank space (line start/end)
    T .............. Insert ISO timestamp (YYYY-MM-DD HH:MM:SS)
    F .............. Insert calendar...
    P .............. Generate strong password
    R .............. Clear all
    K .............. Copy all
    J .............. Paste (copy all)
    String Memory (limit: 64 lines)
    C .............. Copy line/selection
    X .............. Cut line/selection
    V .............. Paste
    Left/Right ..... Select from string memory
    Up ............. First memory string
    Down ........... Last memory string
    Tab ............ Insert all
    E .............. Clear string memory
    Standard shortcuts for document navigation
    CTRL+Left/N ......... Move cursor to previous word
    CTRL+Right/L ........ Move cursor to next word
    CTRL+SHIFT+Right .... Forward selection
    CTRL+O .............. Make selection
    SHIFT+Home/End ...... Select line
    Home | CTRL+W, N .... Move cursor to line start
    End  |         M .... Move cursor to line end
    PgUp |         I .... Move cursor up 1 page
    PgDn |         O .... Move cursor down 1 page
                   Q .... Move cursor to document top
                   A .... Move cursor to document end
    Basic editing
    Backspace ........... Delete left (1 character space/selection)
    Delete .............. Delete right (1 character space/selection)
    Tab ................. Insert blank space x 4
    Enter ............... Insert new line
    Up/Down ............. Move cursor
    Left/Right .......... Move cursor/Refine selection
    CTRL+W, 7 ........... Toggle scroll indicator On/Off (temporary)
    CTRL+W, 8 ........... Toggle undo cache On/Off (temporary)
    CTRL+W, 9 ........... Toggle row/column display On/Off (temporary)
    CTRL+W, 0 ........... Debug
    (Supports copy/paste trough default console behaviour. Wordwrap is on by default.)
 
   Example:

  * How to instantiate the editor:
    TextArea textArea = new TextArea(1, 1, Init.BufferWidth - 1, Init.BufferHeight - 2, "Some text...", true);
    // Setting a height of 1 will enable single line mode, useful for editing titles etc. in a single field with a maximum length = the set width.
    // Pressing Enter in single line mode will close the editor instead of inserting a new line.
    // All shortcuts also work in single line mode.

  * How to act on control exit:
    if (textArea.Canceled && DocumentInfo.HasChanged)
    {
        Console.WriteLine("Has changes (save Y/N...)");
        // text is extracted like this: textArea.GetText();
    }
    else
    {
        Console.WriteLine("No changes");
    }
 
 */

#region TextArea

public class TextArea
{
    #region Properties

    private bool _exit;
    public bool Canceled { get; private set; }
    private int _cursorIndex;
    private int _cursorDisplayY;

    private int CursorDisplayY
    {
        get => _cursorDisplayY;
        set
        {
            _cursorDisplayY = value;
            SetOffset();
        }
    }

    private int _cursorDisplayX;
    private int _topPosition;
    private int _leftPosition;
    private int _width;
    private int _height;
    private int _cursorRecallIndex;
    private bool IsSingleLineEditor { get; }
    private readonly int _maxInputLength;
    private int _offset;
    private List<string> _splitEditText = new();
    private string _text = string.Empty;
    private bool IsReadOnly { get; set; }
    private bool IgnoreChanges { get; set; }
    private readonly string[] _commandNames;
    private bool _openSearchDialog;

    private string Text
    {
        get => _text;
        set
        {
            if (_text != value)
            {
                DocumentInfo.HasChanged = true;
                if (!IsSingleLineEditor && !IsReadOnly)
                {
                    if (!IgnoreChanges)
                    {
                        Menu.IndicateChanges(false);
                    }
                    else
                    {
                        IgnoreChanges = false;
                    }
                }
            }
            else
            {
                DocumentInfo.HasChanged = false;
            }

            _text = value;
            _splitEditText = CreateSplitText();
        }
    }

    private Cursor _cursor = new();
    private CustomStack<object> _undoCache;
    private CustomStack<object> _redoCache;

    public const int
        UndoCacheSize = 1500; // ATTENTION: Must be dividable by 3 for the stack to push/pop the correct objects

    public const int
        RedoCacheSize = 750; // ATTENTION: Must be dividable by 3 for the stack to push/pop the correct objects

    #endregion


    internal bool SaveWasInvoked;
    private int _saveMode; // 0 = normal save, 1 = save as settings

    private void SetSaveMode(int mode)
    {
        _saveMode = mode;
    }

    /// <summary>
    /// Create an instance of the TextArea control
    /// </summary>
    /// <param name="left">Left padding (control placement in console window)</param>
    /// <param name="top">Top padding (control placement in console window)</param>
    /// <param name="width">Text area width</param>
    /// <param name="height">Text area height</param>
    /// <param name="text">Optional text content</param>
    /// <param name="recallCursorIndex">The cursor position to recall</param>
    /// <param name="scrollToEnd">Scroll to end of supplied text</param>
    /// <param name="readOnly">Limit the control to read only</param>
    /// <param name="openSearchDialog">Automatically open the search dialog</param>
    /// <param name="saveMode">Set which save method to call</param>
    public TextArea(int left, int top, int width, int height, string text, int recallCursorIndex, bool scrollToEnd, bool readOnly,
        bool openSearchDialog, int saveMode)
    {
        // Initialize command names list
        _commandNames = new[]
        {
            "", "Copy line/sel", "Cut line/sel", "Paste", "Line duplicate", "Line delete", "Undo",
            "Insert timestamp",
            "Generate password", "Show help", "Line/sel to UPPERCASE", "Line/sel to lowercase", "First memory",
            "Last memory",
            "Clear string memory",
            "Page up", "Page down", "Line start", "Line end", "Document end", "Toggle scroll indicator",
            "Toggle undo cache",
            "Next memory", "Previous memory", "Insert all memory", "Trim start/end", "Copy all", "Paste (copy all)",
            "Clear all", "Redo", "Calendar", "Search", "Document top", "Row/Column display"
        };

        SetSaveMode(saveMode);
        SaveWasInvoked = false;
        IsReadOnly = readOnly;
        _openSearchDialog = openSearchDialog;
        Mode.SetEditorUndoState(Mode.EditorUndoState.On);
        Mode.SetEditorScrollIndicator(Mode.EditorScrollIndicator.On);
        // Hide console cursor

        if (recallCursorIndex > 0)
        {
            _cursorRecallIndex = recallCursorIndex;
        }
        else
        {
            _cursorRecallIndex = 0;
        }

        // Initialize undo history
        _undoCache = new CustomStack<object>();
        _redoCache = new CustomStack<object>();

        // Enforce I/O encoding
        //SetEncoding();
        // Set properties
        Canceled = false;
        _topPosition = top;
        _leftPosition = left;
        _width = width;
        _height = height;

        // Ignore changes while setting the initial content <- must be set here
        IgnoreChanges = true;
        if (height == 1)
        {
            _maxInputLength = width - 3;
            IsSingleLineEditor = true;
            _leftMargin = " ";
        }
        else
        {
            _maxInputLength = 0;
            IsSingleLineEditor = false;
        }

        // Set text if any (must be set after defining the width)
        if (!string.IsNullOrEmpty(text))
        {
            Text = text;
        }
        else
        {
            Text = string.Empty;
        }

        // Draw background
        //DrawColourBlock(Xpostion, Ypostion, Xpostion + Height, Ypostion + Width); //Main Box
        DrawColourBlock(_topPosition, _leftPosition, _topPosition + _height, _leftPosition + _width); //Main Box

        // Draw contents
        Draw();
        // And scroll to end if true, else scroll to cursor index
        if (scrollToEnd)
        {
            CursorToLineEnd();
        }
        else
        {
            GotoCursorIndex();
        }

        if (IsReadOnly)
        {
            return;
        }

        // Reset changes while setting the initial content <- must be set here
        DocumentInfo.HasChanged = false;
        Menu.InitializeSaveIndicator();
        WriteEditorMemoryStatus(true);
        WriteRepeatCommand(false);
        // Run main loop
        MainLoop();
    }

    // The entire applications is set to use Encoding.UTF8 in the init class
    // hence the below code is commented out...
    //private static void SetEncoding()
    //{
    //    Console.OutputEncoding = Encoding.UTF8;
    //    Console.InputEncoding = Encoding.UTF8;
    //}


    /// <summary>
    /// Retrieve the text content of the TextArea control
    /// </summary>
    internal string GetText()
    {
        return Text;
    }

    internal int GetCursorIndex()
    {
        return _cursorIndex;
    }

    private void SaveUndoSnapshot()
    {
        _undoCache.Push(_cursorIndex);
        _undoCache.Push(_cursor);
        _undoCache.Push(Text);
        TruncateHistory();
    }

    private void SaveRedoSnapshot()
    {
        _redoCache.Push(_cursorIndex);
        _redoCache.Push(_cursor);
        _redoCache.Push(Text);
        TruncateHistory();
    }


    private void RestoreUndoSnapshot()
    {
        if (_undoCache.Count() > 0)
        {
            SaveRedoSnapshot();
            Text = (string)_undoCache.Pop();
            _cursor = (Cursor)_undoCache.Pop();
            _cursorIndex = (int)_undoCache.Pop();
            Draw();
        }
    }

    private void RestoreRedoSnapshot()
    {
        if (_redoCache.Count() > 0)
        {
            SaveUndoSnapshot();
            Text = (string)_redoCache.Pop();
            _cursor = (Cursor)_redoCache.Pop();
            _cursorIndex = (int)_redoCache.Pop();
            Draw();
        }
    }

    private void ToggleReducedUndoHistory()
    {
        RemoveCursor();
        if (Mode.CurrentEditorUndoState == Mode.EditorUndoState.Off)
        {
            Mode.SetEditorUndoState(Mode.EditorUndoState.On);
            Menu.StatusMessage(DialogText.EditorUndoCache + DialogText.StatusEnabled, true, true, true,
                DialogText.CaptionsSettings, string.Empty, false);
            DrawEditMenu();
            WriteEditorMemoryStatus(true);
            Draw();
        }
        else
        {
            if (Menu.YesNoPrompt(DialogText.CaptionsSettings, DialogText.EditorDisableUndoCache,
                    DialogText.EditorDisableUndoCacheNote, true))
            {
                Mode.SetEditorUndoState(Mode.EditorUndoState.Off);
                // Do cleanup
                ClearUndoRedoCache();
                Menu.StatusMessage(DialogText.EditorUndoCache + DialogText.StatusDisabled, true, true, true,
                    DialogText.CaptionsSettings, string.Empty, false);
                Console.Clear();
                DrawEditMenu();
                WriteEditorMemoryStatus(true);
                Draw();
            }
            else
            {
                Console.Clear();
                DrawEditMenu();
                WriteEditorMemoryStatus(true);
                Draw();
            }
        }
    }

    // Limit to UndoCacheSize undo steps (3 x 500 = 1500)
    // Limit to RedoCacheSize redo steps (3 x 250 = 750)
    private void TruncateHistory()
    {
        if (Mode.CurrentEditorUndoState == Mode.EditorUndoState.On)
        {
            // Truncate undo cache
            if (_undoCache.Count() > UndoCacheSize) // 3 x 500 because I stack three objects at a time
            {
                _undoCache.Remove(0, 3);
            }

            // Truncate redo cache
            if (_redoCache.Count() > RedoCacheSize) // 3 x 250 because I stack three objects at a time
            {
                _redoCache.Remove(0, 3);
            }
        }
    }

    public void ClearUndoRedoCache()
    {
        // Do cleanup
        if (_undoCache.Count() > 0)
        {
            _undoCache.Remove(0, _undoCache.Count());
        }

        if (_redoCache.Count() > 0)
        {
            _redoCache.Remove(0, _redoCache.Count());
        }
    }


    private void DrawColourBlock(int startTop, int startLeft, int endTop, int endLeft)
    {
        for (var i = startTop; i < endTop; i++)
        {
            Tools.SetCursorEx(i, startLeft);
            Console.WriteLine(new string(' ', (endLeft - startLeft)));
        }
    }

    private void WriteText(String text, int startTop, int startLeft)
    {
        Tools.SetCursorEx(startTop, startLeft);
        Console.Write(text);
    }


    #region Main Loop (Keyboard input)

    private static int CommandMemoryIndex { get; set; }

    // Persist repeat command across instances
    private static int RepeatCommandMemoryIndex { get; set; }

    public static void InitCommandMemoryIndex()
    {
        RepeatCommandMemoryIndex = 6; // 6 = Undo
        CommandMemoryIndex = RepeatCommandMemoryIndex;
    }

    private void SetCommand(int number)
    {
        CommandMemoryIndex = number;
        if (IsRepeatableCommand(CommandMemoryIndex))
        {
            RepeatCommandMemoryIndex = CommandMemoryIndex;
        }

        WriteRepeatCommand(true);
        WriteEditorMemoryStatus(false);
    }


    // This is a light weight redraw of the top compared to the
    // DrawEditMenu which redraws the whole menu and all indicators
    private void DrawTopMenu()
    {
        if (!IsSingleLineEditor)
        {
            Menu.DrawTopMenu(false, false, false);
            // Indicate compound mode
            UpdateOverlays();
        }

        if (IsSingleLineEditor)
        {
            // Re-draw label and remove menu
            Menu.WriteModeLabel(string.Empty, string.Empty, 2);
        }

        // Only display current file name if in save mode zero
        if (_saveMode == 1)
        {
            Menu.WriteNameInTopBar(false);
        }
        else
        {
            Menu.WriteNameInTopBar(true);
        }
        ShowCursor();
    }

    private void UpdateOverlays()
    {
        if (_compoundMode)
        {
            Menu.WriteBottomText(" Shortcuts mode (Press Esc to exit, H for Help) ", true);
        }

        WriteEditorMemoryStatus(true);
        WriteRepeatCommand(false);
    }

    // This method was made to make recalling the last command easy
    private void DoCommand()
    {
        if (CommandMemoryIndex > 0)
        {
            switch (CommandMemoryIndex)
            {
                case 1: // Copy line/selection
                    Keyboard.ClearTopIndicatorArea();
                    CopyLine();
                    DrawTopMenu();
                    break;
                case 2: // Cut line/selection
                    Keyboard.ClearTopIndicatorArea();
                    CutLine();
                    DrawTopMenu();
                    break;
                case 3: // Paste line/into selection
                    Keyboard.ClearTopIndicatorArea();
                    PasteText();
                    DrawTopMenu();
                    break;
                case 4: // Duplicate line
                    DisableSelector();
                    Keyboard.ClearTopIndicatorArea();
                    DuplicateLine();
                    DrawTopMenu();
                    break;
                case 5: // Line/selection delete
                    Keyboard.ClearTopIndicatorArea();
                    LineDelete();
                    DrawTopMenu();
                    break;
                case 6: // Undo
                    DisableSelector();
                    Keyboard.ClearTopIndicatorArea();
                    Undo();
                    DrawTopMenu();
                    break;
                case 7: // Insert timestamp
                    DisableSelector();
                    Keyboard.ClearTopIndicatorArea();
                    TimeStamp();
                    DrawTopMenu();
                    break;
                case 8: // Insert strong password
                    DisableSelector();
                    Keyboard.ClearTopIndicatorArea();
                    Password();
                    DrawTopMenu();
                    break;
                case 9: // Show help
                    DisableSelector();
                    RemoveCursor();
                    Tools.WriteEditorHelp(true);
                    Menu.IndicateChanges(true);
                    UpdateOverlays();
                    Draw();
                    break;
                case 10: // Uppercase line/selection
                    Keyboard.ClearTopIndicatorArea();
                    UppercaseLine();
                    DrawTopMenu();
                    break;
                case 11: // Lowercase line/selection
                    Keyboard.ClearTopIndicatorArea();
                    LowercaseLine();
                    DrawTopMenu();
                    break;
                case 12: // First memory
                    DisableSelector();
                    Keyboard.ClearTopIndicatorArea();
                    FirstMemoryBank();
                    DrawTopMenu();
                    break;
                case 13: // Last memory
                    DisableSelector();
                    Keyboard.ClearTopIndicatorArea();
                    LastMemoryBank();
                    DrawTopMenu();
                    break;
                case 14: // Clear string memory
                    DisableSelector();
                    Keyboard.ClearTopIndicatorArea();
                    ClearMemoryBanks();
                    DrawTopMenu();
                    break;
                case 15: // Page up
                    DisableSelector();
                    Keyboard.ClearTopIndicatorArea();
                    PageUp();
                    DrawTopMenu();
                    break;
                case 16: // Page down
                    DisableSelector();
                    Keyboard.ClearTopIndicatorArea();
                    PageDown();
                    DrawTopMenu();
                    break;
                case 17: // Line start
                    DisableSelector();
                    Keyboard.ClearTopIndicatorArea();
                    CursorToLineStart();
                    DrawTopMenu();
                    break;
                case 18: // Line end
                    DisableSelector();
                    Keyboard.ClearTopIndicatorArea();
                    CursorToLineEnd();
                    DrawTopMenu();
                    break;
                case 19: // Document end
                    DisableSelector();
                    Keyboard.ClearTopIndicatorArea();
                    CursorToDocumentEnd();
                    DrawTopMenu();
                    break;
                case 20: // Toggle scroll indicator on/off
                    DisableSelector();
                    Keyboard.ClearTopIndicatorArea();
                    ToggleScrollIndicator();
                    DrawTopMenu();
                    break;
                case 21: // Toggle undo cache on/off
                    DisableSelector();
                    Keyboard.ClearTopIndicatorArea();
                    ToggleReducedUndoHistory();
                    DrawTopMenu();
                    break;
                case 22: // Next memory bank
                    DisableSelector();
                    Keyboard.ClearTopIndicatorArea();
                    NextMemoryBank();
                    DrawTopMenu();
                    break;
                case 23: // Previous memory bank
                    DisableSelector();
                    Keyboard.ClearTopIndicatorArea();
                    PreviousMemoryBank();
                    DrawTopMenu();
                    break;
                case 24: // Insert all memory lines
                    DisableSelector();
                    Keyboard.ClearTopIndicatorArea();
                    InsertAllMemoryLines();
                    DrawTopMenu();
                    break;
                case 25: // Trim blank space (line start/end)
                    DisableSelector();
                    Keyboard.ClearTopIndicatorArea();
                    TrimStartEnd();
                    DrawTopMenu();
                    break;
                case 26: // Copy all
                    DisableSelector();
                    Keyboard.ClearTopIndicatorArea();
                    CopyAll();
                    DrawTopMenu();
                    break;
                case 27: // Paste (Copy all)
                    DisableSelector();
                    Keyboard.ClearTopIndicatorArea();
                    InsertAllTextCopy();
                    DrawTopMenu();
                    break;
                case 28: // Clear all
                    DisableSelector();
                    RemoveCursor();
                    ClearAll();
                    Menu.IndicateChanges(true);
                    DrawEditMenu();
                    UpdateOverlays();
                    Draw();
                    break;
                case 29: // Redo
                    DisableSelector();
                    Keyboard.ClearTopIndicatorArea();
                    Redo();
                    DrawTopMenu();
                    break;
                case 30: // Calendar
                    DisableSelector();
                    RemoveCursor();
                    AddText(IsoDateTime.ConfigureCalendar());
                    // Ask to configure calendar (yes/no)
                    // Clear screen and use
                    // Draw selection menu...
                    Menu.IndicateChanges(true);
                    DrawEditMenu();
                    UpdateOverlays();
                    Draw();
                    break;
                case 31: // Search
                    DisableSelector();
                    DoSearch();
                    break;
                case 32: // Document top
                    DisableSelector();
                    Keyboard.ClearTopIndicatorArea();
                    CursorToDocumentTop();
                    DrawTopMenu();
                    break;
                case 33: // Toggle row/column display
                    DisableSelector();
                    Keyboard.ClearTopIndicatorArea();
                    ToggleRowColumnDisplay();
                    DrawTopMenu();
                    break;
                case 34: // Debug function
                    DisableSelector();
                    Keyboard.ClearTopIndicatorArea();
                    DebugMessage();
                    DrawTopMenu();
                    break;
            }
        }
        else
        {
            // No command
            NoCommandDisplay();
            Draw();
        }
    }

    public void NoCommandDisplay()
    {
        if (CommandMemoryIndex == 0)
        {
            Tools.CursorTopLeft();
            // Clear area
            Console.Write(new String(' ', 30));
            Menu.WriteNameInTopBar(true);
            Tools.CursorReturn();
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.WarningInverted);
            Console.Write(" " + "No command" + " ");
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            Console.Write(" ");
        }
    }


    private ConsoleKeyInfo _input;
    private ConsoleKeyInfo _secondaryKey;
    private bool _compoundMode;
    private bool _enableCompoundMode;
    private List<Shortcut> _tempShortcuts = new();

    private void MainLoop()
    {
        try
        {
            // The command memory index is initialized from the Init class and
            // from the Action class when resetting settings...
            // TextArea.InitCommandMemoryIndex();

            while (!_exit)
            {
                if (_openSearchDialog)
                {
                    _openSearchDialog = false;
                    DoSearch();
                }

                _input = ReadKey();
                string note = string.Empty;
                //if (!IsSingleLineEditor)
                //{
                //    note = Keyboard.GetTriggerAsString() + ": Sustain | Esc: Exit | H: Help";
                //    //note = Keyboard.GetTriggerAsString() + ": Sustain | Esc: Exit | H: Help";
                //}

                // Handle all trigger keys first
                if (Keyboard.IsPrimaryShortcutTrigger(_input, note) || _compoundMode)
                {
                    // Display single line editor shortcuts in the floating menu
                    if (IsSingleLineEditor)
                    {
                        // Display options menu
                        _tempShortcuts.Clear();
                        _tempShortcuts.Add(new Shortcut("Z", "Undo", false, 13));
                        _tempShortcuts.Add(new Shortcut("Y", "Redo", false, 13));
                        _tempShortcuts.Add(new Shortcut("N", "Line start", false, 13));
                        _tempShortcuts.Add(new Shortcut("M", "Line end", false, 13));
                        _tempShortcuts.Add(new Shortcut("L", "Line delete (clear)", false, 13));
                        _tempShortcuts.Add(new Shortcut("U", "Line/selection to UPPERCASE", false, 13));
                        _tempShortcuts.Add(new Shortcut("G", "Line/selection to lowercase", false, 13));
                        _tempShortcuts.Add(new Shortcut("C", "Copy line/selection", false, 13));
                        _tempShortcuts.Add(new Shortcut("X", "Cut line/selection", false, 13));
                        _tempShortcuts.Add(new Shortcut("V", "Paste", false, 13));
                        _tempShortcuts.Add(new Shortcut("B", "Trim start/end", false, 13));
                        //tempShortcuts.Add(new Shortcut("R", "Clear", false, 13));
                        //tempShortcuts.Add(new Shortcut("Home | N", "Line start", false, 13)); Home is not an options key
                        //tempShortcuts.Add(new Shortcut("End | M", "Line end", false, 13)); End is not an options key
                        _tempShortcuts.Add(new Shortcut("T", "Insert timestamp", false, 13));
                        _tempShortcuts.Add(new Shortcut("P", "Generate password", false, 13));
                        //tempShortcuts.Add(new Shortcut("H", "More help...", false, 13));
                        _tempShortcuts.Add(new Shortcut(string.Empty, "-", true, 13));
                        _tempShortcuts.Add(new Shortcut(DialogText.CtrlPlus + Keyboard.KbRepeat, "Repeat shortcut", false, 13));
                        _tempShortcuts.Add(new Shortcut(DialogText.CtrlPlus + Keyboard.KbSelect, "Make selection", false, 13));
                        _tempShortcuts.Add(new Shortcut(DialogText.CtrlPlus + Keyboard.KbNxtword + "/" + Keyboard.KbPreword, "Next/Previous word", false, 13));
                        //if (!IsSingleLineEditor)
                        //{ 
                        //    tempShortcuts.Add(new Shortcut("D", "Line duplicate", false, 13));
                        //}

                        Menu.FloatingMenu(
                            DialogText.CtrlPlus + Keyboard.GetTriggerAsString() + " " + DialogText.CaptionsOptions,
                            _tempShortcuts, 4, 10, true);
                    }


                    // Enable compound shortcuts automatically if not in single line mode
                    if (!IsSingleLineEditor && _enableCompoundMode && !_compoundMode)
                    {
                        _compoundMode = true;
                        Keyboard.ClearTopIndicatorArea();
                        DrawTopMenu();
                    }
                    else
                    {
                        // Get second key
                        if (_compoundMode)
                        {
                            // Get from input and fall through
                            _secondaryKey = _input;
                        }
                        else
                        {
                            _secondaryKey = ReadKey();
                        }

                        // Copy line
                        if (_secondaryKey.Key == ConsoleKey.C)
                        {
                            SetCommand(1);
                            DoCommand();
                        }
                        // Cut line
                        else if (_secondaryKey.Key == ConsoleKey.X)
                        {
                            SetCommand(2);
                            DoCommand();
                        }
                        // Paste line
                        else if (_secondaryKey.Key == ConsoleKey.V)
                        {
                            SetCommand(3);
                            DoCommand();
                        }
                        // Duplicate line
                        else if (!IsSingleLineEditor && _secondaryKey.Key == ConsoleKey.D)
                        {
                            SetCommand(4);
                            DoCommand();
                        }
                        // Line delete
                        else if (_secondaryKey.Key == ConsoleKey.L)
                        {
                            SetCommand(5);
                            DoCommand();
                        }
                        // Undo
                        else if (_secondaryKey.Key == ConsoleKey.Z)
                        {
                            SetCommand(6);
                            DoCommand();
                        }
                        // Insert timestamp
                        else if (_secondaryKey.Key == ConsoleKey.T)
                        {
                            SetCommand(7);
                            DoCommand();
                        }
                        // Insert strong password
                        else if (_secondaryKey.Key == ConsoleKey.P)
                        {
                            SetCommand(8);
                            DoCommand();
                        }
                        // Show help
                        else if (!IsSingleLineEditor && _secondaryKey.Key == ConsoleKey.H)
                        {
                            SetCommand(9);
                            DoCommand();
                        }
                        // Uppercase line
                        else if (_secondaryKey.Key == ConsoleKey.U)
                        {
                            SetCommand(10);
                            DoCommand();
                        }
                        // Lowercase line
                        else if (_secondaryKey.Key == ConsoleKey.G)
                        {
                            SetCommand(11);
                            DoCommand();
                        }
                        // First memory
                        else if (!IsSingleLineEditor && _secondaryKey.Key == ConsoleKey.UpArrow)
                        {
                            SetCommand(12);
                            DoCommand();
                        }
                        // Last memory
                        else if (!IsSingleLineEditor && _secondaryKey.Key == ConsoleKey.DownArrow)
                        {
                            SetCommand(13);
                            DoCommand();
                        }
                        // Clear string memory
                        else if (!IsSingleLineEditor && _secondaryKey.Key == ConsoleKey.E)
                        {
                            SetCommand(14);
                            DoCommand();
                        }
                        // Page up
                        else if (!IsSingleLineEditor && _secondaryKey.Key == ConsoleKey.I)
                        {
                            SetCommand(15);
                            DoCommand();
                        }
                        // Page down
                        else if (!IsSingleLineEditor && _secondaryKey.Key == ConsoleKey.O)
                        {
                            SetCommand(16);
                            DoCommand();
                        }
                        // Line start
                        else if (_secondaryKey.Key == ConsoleKey.N)
                        {
                            SetCommand(17);
                            DoCommand();
                        }
                        // Line end
                        else if (_secondaryKey.Key == ConsoleKey.M)
                        {
                            SetCommand(18);
                            DoCommand();
                        }
                        // Document end
                        else if (!IsSingleLineEditor && _secondaryKey.Key == ConsoleKey.A)
                        {
                            SetCommand(19);
                            DoCommand();
                        }
                        // Toggle scroll indicator on/off
                        else if (!IsSingleLineEditor && _secondaryKey.Key == ConsoleKey.D7)
                        {
                            SetCommand(20);
                            DoCommand();
                        }
                        // Toggle undo cache on/off
                        else if (!IsSingleLineEditor && _secondaryKey.Key == ConsoleKey.D8)
                        {
                            SetCommand(21);
                            DoCommand();
                        }
                        // Next memory
                        else if (!IsSingleLineEditor && _secondaryKey.Key == ConsoleKey.RightArrow)
                        {
                            SetCommand(22);
                            DoCommand();
                        }
                        // Previous memory
                        else if (!IsSingleLineEditor && _secondaryKey.Key == ConsoleKey.LeftArrow)
                        {
                            SetCommand(23);
                            DoCommand();
                        }
                        // Insert all memory
                        else if (!IsSingleLineEditor && _secondaryKey.Key == ConsoleKey.Tab)
                        {
                            SetCommand(24);
                            DoCommand();
                        }
                        // Trim start/end
                        else if (_secondaryKey.Key == ConsoleKey.B)
                        {
                            SetCommand(25);
                            DoCommand();
                        }
                        // Copy all
                        else if (_secondaryKey.Key == ConsoleKey.K)
                        {
                            SetCommand(26);
                            DoCommand();
                        }
                        // Paste (Copy all)
                        else if (_secondaryKey.Key == ConsoleKey.J)
                        {
                            SetCommand(27);
                            DoCommand();
                        }
                        // Clear all
                        else if (!IsSingleLineEditor && _secondaryKey.Key == ConsoleKey.R)
                        {
                            SetCommand(28);
                            DoCommand();
                        }
                        // Redo
                        else if (_secondaryKey.Key == ConsoleKey.Y)
                        {
                            SetCommand(29);
                            DoCommand();
                        }
                        // Calendar
                        else if (!IsSingleLineEditor && _secondaryKey.Key == ConsoleKey.F)
                        {
                            SetCommand(30);
                            DoCommand();
                        }
                        // Search
                        else if (!IsSingleLineEditor && _secondaryKey.Key == ConsoleKey.S)
                        {
                            SetCommand(31);
                            DoCommand();
                        }
                        // Document top
                        else if (!IsSingleLineEditor && _secondaryKey.Key == ConsoleKey.Q)
                        {
                            SetCommand(32);
                            DoCommand();
                        }
                        // Toggle row/column display on/off
                        else if (!IsSingleLineEditor && _secondaryKey.Key == ConsoleKey.D9)
                        {
                            SetCommand(33);
                            DoCommand();
                        }
                        // Debug function
                        else if (!IsSingleLineEditor && _secondaryKey.Key == ConsoleKey.D0)
                        {
                            SetCommand(34);
                            DoCommand();
                        }
                        // Toggle lingering compound mode
                        else if (_secondaryKey.Key == Keyboard.GetTriggerKey())
                        {
                            DisableSelector();
                            if (!IsSingleLineEditor)
                            {
                                if (!_enableCompoundMode)
                                {
                                    _enableCompoundMode = true;
                                    _compoundMode = _enableCompoundMode;
                                    Keyboard.ClearTopIndicatorArea();
                                    DrawTopMenu();
                                }
                                else
                                {
                                    _enableCompoundMode = false;
                                    _compoundMode = _enableCompoundMode;
                                    Keyboard.ClearTopIndicatorArea();
                                    DrawEditMenu();
                                    ShowCursor();
                                }
                            }
                            else
                            {
                                Keyboard.ClearTopIndicatorArea();
                                DrawTopMenu();
                            }
                        }
                        // Exit compound mode
                        else if (_secondaryKey.Key == ConsoleKey.Escape)
                        {
                            DisableSelector();
                            // If singleline editor just clear the top dialog
                            if (IsSingleLineEditor)
                            {
                                Keyboard.ClearTopIndicatorArea();
                                DrawTopMenu();
                            }
                            else if (_compoundMode)
                            {
                                _enableCompoundMode = false;
                                _compoundMode = false;
                                Keyboard.ClearTopIndicatorArea();
                                DrawEditMenu();
                                ShowCursor();
                            }
                            else
                            {
                                Keyboard.ClearTopIndicatorArea();
                                DrawEditMenu();
                                ShowCursor();
                            }
                        }
                        else
                        {
                            DisableSelector();
                            if (IsSingleLineEditor) // If single line editor just clear the top dialog
                            {
                                Keyboard.ClearTopIndicatorArea();
                                DrawTopMenu();
                            }
                            else
                            {
                                SetCommand(0);
                                Keyboard.DisplayUnboundKey(_secondaryKey);
                                Tools.ClearScreen();
                                DrawEditMenu();
                                UpdateOverlays();
                                Menu.IndicateChanges(true);
                                Draw();
                            }
                        }
                    }
                }
                // Handle all other keyboard input
                else if (_input.Key == ConsoleKey.Tab)
                {
                    DisableSelector();
                    Tab();
                }
                else if (!IsSingleLineEditor && _input.Key == ConsoleKey.Enter)
                {
                    DisableSelector();
                    Enter();
                }
                else if (IsSingleLineEditor && _input.Key == ConsoleKey.Enter)
                {
                    DisableSelector();
                    // Close for text extraction
                    Canceled = false;
                    ExitWindow();
                }
                // Select with CTRL+Left arrow
                else if ((ConsoleModifiers.Control & _input.Modifiers) == ConsoleModifiers.Control && (ConsoleModifiers.Shift & _input.Modifiers) == ConsoleModifiers.Shift &&
                        _input.Key == ConsoleKey.LeftArrow)
                {
                    RunSelector(false, false, false, true); // Do selection
                }
                // Move with CTRL + Left arrow
                else if ((ConsoleModifiers.Control & _input.Modifiers) == ConsoleModifiers.Control &&
                        _input.Key == ConsoleKey.LeftArrow)
                {
                    MoveCursorByWord(true);
                }
                else if (_input.Key == ConsoleKey.LeftArrow)
                {
                    if (IsSelecting())
                    {
                        RunSelector(true, false, true, false);
                    }
                    else
                    {
                        CursorMoveLeft();
                    }
                }
                // Select with CTRL+Right arrow
                else if ((ConsoleModifiers.Control & _input.Modifiers) == ConsoleModifiers.Control && (ConsoleModifiers.Shift & _input.Modifiers) == ConsoleModifiers.Shift &&
                        _input.Key == ConsoleKey.RightArrow)
                {
                    RunSelector(false, false, false, false); // Do selection
                }
                //Move with CTRL + Right arrow
                else if ((ConsoleModifiers.Control & _input.Modifiers) == ConsoleModifiers.Control &&
                        _input.Key == ConsoleKey.RightArrow)
                {
                    MoveCursorByWord(false);
                }

                else if (_input.Key == ConsoleKey.RightArrow)
                {
                    if (IsSelecting())
                    {
                        RunSelector(true, false, false, false);
                    }
                    else
                    {
                        CursorMoveRight();
                    }
                }
                else if (_input.Key == ConsoleKey.UpArrow)
                {
                    DisableSelector();
                    CursorMoveUp();
                }
                else if (_input.Key == ConsoleKey.PageUp)
                {
                    DisableSelector();
                    PageUp();
                }
                else if (_input.Key == ConsoleKey.DownArrow)
                {
                    DisableSelector();
                    CursorMoveDown();
                }
                else if (_input.Key == ConsoleKey.PageDown)
                {
                    DisableSelector();
                    PageDown();
                }
                else if (_input.Key == ConsoleKey.Backspace)
                {
                    BackSpace();
                }
                else if (_input.Key == ConsoleKey.Delete)
                {
                    Delete();
                }

                // Select line (home)
                else if ((ConsoleModifiers.Shift & _input.Modifiers) == ConsoleModifiers.Shift &&
                         _input.Key == ConsoleKey.Home)
                {
                    CursorToLineStart();
                    RunSelector(false, true, false, false); // Do selection
                }
                // Select line (end)
                else if ((ConsoleModifiers.Shift & _input.Modifiers) == ConsoleModifiers.Shift &&
                         _input.Key == ConsoleKey.End)
                {
                    CursorToLineStart();
                    RunSelector(false, true, false, false); // Do selection
                }
                else if (_input.Key == ConsoleKey.Home)
                {
                    DisableSelector();
                    CursorToLineStart();
                }
                else if (_input.Key == ConsoleKey.End)
                {
                    DisableSelector();
                    CursorToLineEnd();
                }
                else if (_input.Key == ConsoleKey.Escape)
                {
                    InvokeEscape(false);
                }
                else if ((ConsoleModifiers.Control & _input.Modifiers) == ConsoleModifiers.Control &&
                         _input.Key == Keyboard.KbSaveExit)
                {
                    DisableSelector();
                    // Save
                    Save();
                    Canceled = false;
                    ExitWindow();
                }
                else if ((ConsoleModifiers.Control & _input.Modifiers) == ConsoleModifiers.Control &&
                         _input.Key == Keyboard.KbRepeat)
                {
                    // DisableSelector(); <- is controlled through the DoCommand method
                    // Repeat last command
                    if (IsRepeatableCommand(RepeatCommandMemoryIndex))
                    {
                        // Update to using the repeat command
                        CommandMemoryIndex = RepeatCommandMemoryIndex;
                        DoCommand();
                    }
                }
                else if ((ConsoleModifiers.Control & _input.Modifiers) == ConsoleModifiers.Control &&
                         _input.Key == Keyboard.KbSave)
                {
                    DisableSelector();
                    if (IsSingleLineEditor)
                    {
                        Canceled = false;
                        ExitWindow();
                    }
                    else
                    {
                        // Save on the fly
                        Save();
                    }
                }
                else if ((ConsoleModifiers.Control & _input.Modifiers) == ConsoleModifiers.Control &&
                         _input.Key == Keyboard.KbSelect)
                {
                    RunSelector(false, false, false, false); // Do selection
                }
                else if ((ConsoleModifiers.Control & _input.Modifiers) == ConsoleModifiers.Control &&
                         _input.Key == Keyboard.KbNxtword)
                {
                    // Next word (as the selector does it)
                    DisableSelector();
                    MoveCursorByWord(false);
                }
                else if ((ConsoleModifiers.Control & _input.Modifiers) == ConsoleModifiers.Control &&
                         _input.Key == Keyboard.KbPreword)
                {
                    // Previous word (as the selector does it)
                    DisableSelector();
                    MoveCursorByWord(true);
                }

                else if (IsTextChar(_input))
                {
                    DisableSelector();
                    AddLetter(_input.KeyChar);
                }
            }
        }
        catch (Exception ex)
        {
            Tools.HideCursor();
            Tools.ActionUnsupported(ex.Message);
            InvokeEscape(true);
            return;
        }
    }

    private void InvokeEscape(bool limitedActions)
    {
        if (IsSelecting())
        {
            DisableSelector();
            Draw();
        }
        // Save yes/no cancel exit
        else if (!IsSingleLineEditor && DocumentInfo.HasChanged)
        {
            int result;
            if (limitedActions)
            {
                result = Menu.YesNoCancelPrompt(DialogText.Save, DialogText.SaveChangesYn, string.Empty, 0, string.Empty, true);
            }
            else 
            {
                result = Menu.YesNoCancelPrompt(DialogText.Save, DialogText.SaveChangesYnCancel, DialogText.YesContinue, 15, string.Empty, true);
            }
            
                
            if (result == 1) // Yes, save
            {
                // Save
                Save();
                Canceled = false;
                ExitWindow();
            }
            else if (result == 2) // No, dont save
            {
                Canceled = true;
                ExitWindow();
            }
            else if (!limitedActions && result == 3) // Cancel, return to edit
            {
                Console.Clear();
                DrawEditMenu();
                WriteEditorMemoryStatus(true);
                WriteRepeatCommand(false);
                Menu.IndicateChanges(true);
                Draw();
            }
            else if (!limitedActions && result == 4) // Save, return to edit
            {
                // Save
                Save();
                Console.Clear();
                DrawEditMenu();
                WriteEditorMemoryStatus(true);
                WriteRepeatCommand(false);
                Menu.IndicateChanges(true);
                Draw();
            }
        }
        else
        {
            Canceled = true;
            ExitWindow();
        }
    }

    private void Save()
    {
        // Save
        if (_saveMode == 1)
        {
            // Save as settings
            if (!DocumentInfo.HasChanged) return;
            SaveWasInvoked = true;
            FileIo.WriteSettingsFile(GetText());
            DocumentInfo.HasChanged = false;
            DrawEditMenu();
            WriteEditorMemoryStatus(true);
            WriteRepeatCommand(false);
            Menu.IndicateChanges(false);
            Menu.ClearChangeIndicator();
            Draw();
        }
        else
        {
            // Save entry
            SaveWasInvoked = true;
            Actions.SaveContentDuringEdit(this);
            DocumentInfo.HasChanged = false;
            DrawEditMenu();
            WriteEditorMemoryStatus(true);
            WriteRepeatCommand(false);
            Menu.IndicateChanges(false);
            Draw();
        }
    }

    private void DrawEditMenu()
    {
        if (_saveMode == 1)
        {
            Actions.DrawEditSettingsMenu(); // Settings menu
        }
        else
        {
            Actions.DrawEditMenu();
        }
    }

    private string GetCommandName(int number)
    {
        if (number > 0 && number < _commandNames.Length)
        {
            return _commandNames[number];
        }
        else
        {
            return DialogText.NotAvailable;
        }
    }

    private bool IsTextChar(ConsoleKeyInfo character)
    {
        return !char.IsControl(character.KeyChar);
    }

    private static ConsoleKeyInfo ReadKey()
    {
        return Console.ReadKey(true);
    }

    private void ExitWindow()
    {
        _exit = true;
        _lines.Clear();
        _line = string.Empty;
        RemoveCursor();
    }

    private void Undo()
    {
        RestoreUndoSnapshot();
    }

    private void Redo()
    {
        RestoreRedoSnapshot();
    }

    #endregion


    private StringBuilder _tempText = new();

    private void AddText(string text)
    {
        if (string.IsNullOrEmpty(text) || !AllowInput(text)) return;
        SaveUndoSnapshot();
        _tempText.Clear();
        _tempText.Append(Text.AsSpan(0, _cursorIndex));
        _tempText.Append(text);
        _tempText.Append(Text.AsSpan(_cursorIndex, Text.Length - _cursorIndex));
        Text = _tempText.ToString();
        if (text.Length > 1)
        {
            _cursorIndex += text.Length;
        }
        else
        {
            _cursorIndex++;
        }

        Draw();
    }

    private void AddLetter(char letter)
    {
        if (AllowInput(letter.ToString()))
        {
            SaveUndoSnapshot();
            _tempText = new StringBuilder();
            _tempText.Append(Text.Substring(0, _cursorIndex));
            _tempText.Append(letter);
            _tempText.Append(Text.Substring(_cursorIndex, Text.Length - _cursorIndex));
            Text = _tempText.ToString();
            _cursorIndex++;
            Draw();
        }
    }

    private bool AllowInput(string input)
    {
        if (!IsSingleLineEditor)
        {
            return true;
        }
        else if (IsSingleLineEditor && !string.IsNullOrEmpty(input))
        {
            if ((Text + input).Length <= _maxInputLength)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        return false;
    }

    private void CursorMoveLeft()
    {
        if (_cursorIndex != 0)
        {
            _cursorIndex--;
            Draw();
        }
    }

    private void CursorMoveRight()
    {
        if (_cursorIndex != Text.Length)
        {
            _cursorIndex++;
            Draw();
        }
    }

    internal void CursorMoveDown()
    {
        var splitText = _splitEditText;

        if (splitText.Count == CursorDisplayY + 1 || splitText.Count == 0) //Cursor at end of text in text area
        {
            ShowCursor();
            return;
        }

        var nextLine = splitText[CursorDisplayY + 1];

        var newCursor = 0;
        for (var i = 0; i < _cursorDisplayY + 1; i++)
        {
            newCursor += splitText[i].Length;
        }

        if (nextLine.Length > _cursorDisplayX)
            newCursor += _cursorDisplayX;
        else
            newCursor += nextLine.Count(x => x != Entries.NewLineChar);


        _cursorIndex = newCursor;

        Draw();
    }


    private void CursorMoveUp()
    {
        var splitText = _splitEditText;

        if (CursorDisplayY == 0) //Cursor at top of text area
        {
            ShowCursor();
            return;
        }

        var nextLine = splitText[CursorDisplayY - 1];

        var newCursor = 0;
        for (var i = 0; i < _cursorDisplayY - 1; i++)
        {
            newCursor += splitText[i].Length;
        }

        if (nextLine.Length >= _cursorDisplayX)
            newCursor += _cursorDisplayX;
        else
            newCursor += nextLine.Count(x => x != Entries.NewLineChar);

        _cursorIndex = newCursor;
        Draw();
    }

    internal void PageUp()
    {
        if (CursorDisplayY != 0)
        {
            for (int i = 0; i < _height; i++)
            {
                CursorMoveUp();
            }
        }
        else { ShowCursor(); }
    }

    internal void PageDown()
    {
        var splitText = _splitEditText;
        if (splitText.Count != CursorDisplayY + 1 || splitText.Count == 0)
        {
            for (int i = 0; i < _height; i++)
            {
                CursorMoveDown();
            }
        }
        else { ShowCursor(); }
    }

    private void CursorToDocumentEnd()
    {
        try
        {
            if (Text.Length > 0)
            {
                _cursorIndex = Text.Length;
                Draw();
            }
        }
        catch
        {
            // Do nothing
        }
    }

    private void CursorToDocumentTop()
    {
        try
        {
            if (Text.Length > 0)
            {
                _cursorIndex = 0;
                Draw();
            }
        }
        catch
        {
            // Do nothing
        }
    }


    private void CursorToLineStart()
    {
        var splitText = _splitEditText;
        var newCursor = 0;
        for (var i = 0; i < _cursorDisplayY; i++)
        {
            newCursor += splitText[i].Length;
        }

        _cursorIndex = newCursor;
        Draw();
    }


    // End of line
    private void CursorToLineEnd()
    {
        var splitText = _splitEditText;
        var currentLine = splitText[_cursorDisplayY];

        var newCursor = 0;
        for (var i = 0; i < _cursorDisplayY + 1; i++)
        {
            newCursor += splitText[i].Length;
        }

        _cursorIndex = newCursor - currentLine.Count(x => x == Entries.NewLineChar);
        Draw();
    }


    private bool _selectorEnabled;

    private bool IsSelecting()
    {
        return _selectorEnabled;
    }

    private void DisableSelector()
    {
        if (_selectorEnabled)
        {
            _selectorEnabled = false;
            _selSelectionIndex = 0;
            _selSelectedText = string.Empty;
            _selStartIndex = 0;
            _selLength = 0;
            _selMaxed = false;
        }
    }

    private void EnableSelector()
    {
        _selectorEnabled = true;
    }

    private int GetSelectionStart()
    {
        return _selStartIndex;
    }

    private int GetSelectionLength()
    {
        return _selLength;
    }

    private string GetSelectionText()
    {
        return _selSelectedText;
    }

    private string _selSelectedText = string.Empty;
    private int _selSelectionIndex;
    private int _selStartIndex;
    private int _selLength;
    private List<string> _selSplitText = new();
    private string _selCurrentLine = string.Empty;
    private bool _selMaxed;

    // Used for Cut, Copy, Paste
    // Paste must remove the selection and paste
    // memory content...
    private void RunSelector(bool singleStep, bool selectLine, bool backwards, bool reverse) // backwards == minus selection, revers = left direction
    {
        if (!backwards && _selMaxed) return;

        // If new selection set index and get current line
        if (!IsSelecting())
        {
            // Get all lines
            _selSplitText = _splitEditText;
            // Get current line
            _selCurrentLine = _selSplitText[_cursorDisplayY];
            // Commented out because it breaks the functionality:
            //_selCurrentLine = _selCurrentLine.Replace(Entries.NewLineCharString, String.Empty);
            // Get cursor position on current line
            _selSelectionIndex = _cursorDisplayX;
            _selStartIndex = _selSelectionIndex;
            _selLength = _selStartIndex;
        }

        // Step backwards if possible
        if (!selectLine && singleStep && backwards && _selSelectedText.Length > 0)
        {
            _selMaxed = false;
            Draw();
            Tools.HideCursor();
            _selSelectedText = _selSelectedText.Remove(_selSelectedText.Length - 1, 1);
            if (_selSelectionIndex > 0)
                _selSelectionIndex -= 1;
        }
        // Select reverse
        if (!selectLine && !singleStep && reverse && _selSelectedText.Length > 0)
        {
            // Not implemented
        }
        else if (selectLine) // Select entire line
        {
            _selMaxed = false;
            Draw();
            Tools.HideCursor();
            _selSelectedText = _selCurrentLine;
            _selSelectionIndex = 0;
        }
        else // Step forwards (make selection) or expand by one (using single step mode)
        {
            // Add characters until a blank space is discovered then break and write to console
            for (int i = _selSelectionIndex; i < _selCurrentLine.Length; i++)
            {
                // Keep adding chars until a blank space is detected
                _selSelectedText += _selCurrentLine[i];
                // If the newly added char is a blank space, break the loop
                if (singleStep || Regex.IsMatch(_selSelectedText[^1].ToString(), @"\s"))
                {
                    // Update the index with the last position for next iteration
                    _selSelectionIndex += (i - _selSelectionIndex) + 1;
                    break; // exit the loop
                }
            }
        }


        // Set length
        _selLength = _selSelectedText.Length;

        // Return if selection has reached end
        if (_selStartIndex + _selLength >= _selCurrentLine.Length)
        {
            // Indicate that the selection is maxed out
            _selMaxed = true;
        }

        // If selection exists highlight it
        if (_selSelectedText.Length > 0)
        {
            EnableSelector();
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
            Console.Write(_selSelectedText);
            Tools.CursorLeft(_selSelectedText.Length); // Move the cursor back to the start position
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        }
        else
        {
            DisableSelector();
        }
    }


    private List<string> _movSplitText = new();
    private string _movCurrentLine = string.Empty;
    private int _movSelectionIndex;
    private string _movSelectedText = string.Empty;
    private int indexRunningCount = 0;
    //private string _testString = string.Empty;
    private void MoveCursorByWord(bool backwards)
    {
        if (Text.Length > 0)
        {
            // Get all lines
            _movSplitText = _splitEditText;
            // Get current line
            _movCurrentLine = _movSplitText[_cursorDisplayY];
            // Get cursor position on current line
            _movSelectionIndex = _cursorDisplayX;
            _movSelectedText = string.Empty;

            if (backwards)
            {
                // Move cursor backwards by one word
                for (int i = _movSelectionIndex; i > 0; i--)
                {
                    // Start by moving right if not at the end of the line (controlled by the -1 above)
                    CursorMoveLeft();
                    // Keep adding chars until a blank space is detected
                    _movSelectedText = _movCurrentLine[_cursorDisplayX] + _movSelectedText;

                    // If the newly added char is a blank space run forward until a non-blank space is encountered
                    if (IsBlankSpace(GetFirstCharInString(_movSelectedText)))
                    {
                        indexRunningCount = i -2;

                        while (IsBlankSpace(GetCharFromIndex(_movCurrentLine, indexRunningCount)))
                        {
                            // Move cursor left and count 1 down
                            CursorMoveLeft();
                            indexRunningCount -= 1;
                        }
                        // Exit the loop
                        break;
                    }
                }

                Tools.ShowCursor();
            }
            else
            {

                // Move cursor forwards by one word
                for (int i = _movSelectionIndex; i < _movCurrentLine.Length -1; i++)
                {
                    // Start by moving right if not at the end of the line (controlled by the -1 above)
                    CursorMoveRight();
                    // Keep adding chars until a blank space is detected
                    _movSelectedText += _movCurrentLine[i];

                    // If the newly added char is a blank space run forward until a non-blank space is encountered
                    if (IsBlankSpace(GetLastCharInString(_movSelectedText)))
                    {
                        indexRunningCount = i + 1;
                        while (IsBlankSpace(GetCharFromIndex(_movCurrentLine, indexRunningCount)))
                        {
                            // Move cursor right and count 1 up
                            CursorMoveRight();
                            indexRunningCount += 1;
                        }
                        // Exit the loop
                        break;
                    }
                }
                Tools.ShowCursor();
            }
        }
    }

    private bool IsBlankSpace(string s)
    {
        if (!string.IsNullOrEmpty(s) && Regex.IsMatch(s, @"\s") && !Regex.IsMatch(s, @"[\t\n\r]"))
        {
            return true;
        }
        return false;
    }

    private string GetLastCharInString(string s)
    {
        if (!string.IsNullOrEmpty(s))
        { 
            return s[^1].ToString();
        }
        return string.Empty;
    }

    private string GetFirstCharInString(string s)
    {
        if (!string.IsNullOrEmpty(s))
        {
            return s[0].ToString();
        }
        return string.Empty;
    }

    private string GetCharFromIndex(string s, int index)
    {
        if (!string.IsNullOrEmpty(s) && index >= 0 && index < s.Length)
        {
            return s[index].ToString();
        }
        else 
        {
            return string.Empty;
        }
    }

    private void BackSpace()
    {
        if (Text.Length > 0 && IsSelecting() && GetSelectionText().Length > 0)
        {
            DeleteSelection();
        }
        else if (_cursorIndex != 0)
        {
            SaveUndoSnapshot();
            string textBefore = Text.Substring(0, _cursorIndex);
            string textAfter = Text.Substring(_cursorIndex, Text.Length - _cursorIndex);

            textBefore = textBefore.Substring(0, textBefore.Length - 1);

            Text = textBefore + textAfter;
            _cursorIndex--;
            Draw();
        }
        else { ShowCursor(); }
    }


    private void Delete()
    {
        if (Text.Length > 0 && IsSelecting() && GetSelectionText().Length > 0)
        {
            DeleteSelection();
        }
        else if (_cursorIndex != Text.Length && Text.Length > 0)
        {
            SaveUndoSnapshot();
            string textBefore = Text.Substring(0, _cursorIndex);
            string textAfter = Text.Substring(_cursorIndex, Text.Length - _cursorIndex);

            textAfter = textAfter.Substring(1, textAfter.Length - 1);

            Text = textBefore + textAfter;
            Draw();
        }
        else { ShowCursor(); }
    }

    private void DeleteSelection()
    {
        // Delete selection
        if (Text.Length > 0 && IsSelecting() && GetSelectionText().Length > 0)
        {
            SaveUndoSnapshot();
            var splitText = _splitEditText;
            var currentLine = splitText[_cursorDisplayY];
            currentLine = currentLine.Remove(GetSelectionStart(), GetSelectionLength());
            ReplaceCurrentLine(currentLine);
            DisableSelector();
        }
    }

    private void LineDelete()
    {
        if (Text.Length > 0)
        {
            var splitText = _splitEditText;
            var currentLine = splitText[_cursorDisplayY];
            var tempRow = _cursorDisplayY;

            if (splitText.Count > 0)
            {
                SaveUndoSnapshot();
                // Remove the line in question
                for (int i = 0; i < splitText.Count; i++)
                {
                    if (i == tempRow && splitText[i] == currentLine)
                    {
                        splitText.RemoveAt(i);
                        break;
                    }
                }

                // Collect the edited lines
                var tempText = new StringBuilder();
                for (int i = 0; i < splitText.Count; i++)
                {
                    if (i == splitText.Count - 1)
                    {
                        tempText.Append(splitText[i].Replace(Entries.NewLineCharString, string.Empty));
                    }
                    else
                    {
                        tempText.Append(splitText[i]);
                    }
                }

                // Add content back to Text
                Text = tempText.ToString();


                if (splitText.Count == tempRow && splitText.Count != 0)
                {
                    CursorMoveUp();
                    CursorToLineStart();
                }
                else
                {
                    CursorToLineStart();
                }

                Draw();
            }
        }
    }

    private void CopyLine()
    {
        if (Text.Length > 0)
        {
            var splitText = _splitEditText;
            var currentLine = splitText[_cursorDisplayY];
            // Copy selection
            if (IsSelecting() && GetSelectionText().Length > 0)
            {
                // Add selection to memory
                LastStringMemoryAction = Tools.AddToLineMemory(GetSelectionText());
                DisableSelector();
            }
            else
            {
                currentLine = currentLine.Replace(Entries.NewLineCharString, String.Empty);
                LastStringMemoryAction = Tools.AddToLineMemory(currentLine);
            }
        }
    }

    private void PasteText()
    {
        ResetStringMemoryAction();
        // Paste into selection
        // If selection is active, remove selection and insert from memory
        if (Text.Length > 0 && IsSelecting() && GetSelectionText().Length > 0)
        {
            SaveUndoSnapshot();
            var splitText = _splitEditText;
            var currentLine = splitText[_cursorDisplayY];
            currentLine = currentLine.Remove(GetSelectionStart(), GetSelectionLength());
            currentLine = currentLine.Insert(GetSelectionStart(), Tools.GetCurrentLineFromLineMemory());
            ReplaceCurrentLine(currentLine);
            DisableSelector();
        }

        // Else paste text as normal
        else if (Tools.GetCurrentLineFromLineMemory().Length > 0)
        {
            // Get current string memory and write it to the content
            AddText(Tools.GetCurrentLineFromLineMemory());
        }
    }

    private void InsertAllMemoryLines()
    {
        ResetStringMemoryAction();
        if (Tools.HasLineMemory())
        {
            for (int i = 0; i < Tools.GetAllLinesFromLineMemory().Count; i++)
            {
                AddText(Tools.GetAllLinesFromLineMemory()[i] + Entries.NewLineCharString);
            }
        }
    }


    private void CopyAll()
    {
        if (Text.Length > 0)
        {
            Tools.TextCopy = Text;
        }
    }

    private void InsertAllTextCopy()
    {
        if (Tools.IsValid(Tools.TextCopy))
        {
            AddText(Tools.TextCopy);
        }
    }

    private void ClearAll()
    {
        if (Text.Length <= 0) return;
        if (Menu.YesNoPrompt(DialogText.CaptionsClear, DialogText.ClearTextYn, string.Empty, false))
        {
            SaveUndoSnapshot();
            Tools.ClearScreen();
            // Ensure the cursor lands at the top of the visible page
            RemoveCursor();
            _cursorIndex = 0;
            // Clear text
            Text = String.Empty;
            // Draw the "content"
            Draw();
        }
        else
        {
            Tools.ClearScreen();
        }
    }

    private void TrimStartEnd()
    {
        if (Text.Length > 0)
        {
            SaveUndoSnapshot();
            // Ensure the cursor lands at the top of the visible page
            RemoveCursor();
            _cursorIndex = 0;
            var splitText = _splitEditText;
            for (int i = 0; i < splitText.Count; i++)
            {
                splitText[i] = splitText[i].Trim();
            }

            // Add the lines back to the text
            var tempText = new StringBuilder();
            for (int i = 0; i < splitText.Count; i++)
            {
                // Add new lines except after the last line.
                if (i != splitText.Count - 1)
                {
                    tempText.Append(splitText[i] + Entries.NewLineCharString);
                }
                else
                {
                    tempText.Append(splitText[i]);
                }
            }

            // Set text
            Text = tempText.ToString();
            // Draw the content
            Draw();
        }
    }

    private void CutLine() // Cut line or selection
    {
        if (Text.Length > 0)
        {
            var splitText = _splitEditText;
            var currentLine = splitText[_cursorDisplayY];

            // Cut selection
            if (IsSelecting() && GetSelectionText().Length > 0)
            {
                SaveUndoSnapshot();
                currentLine = currentLine.Remove(GetSelectionStart(), GetSelectionLength());
                ReplaceCurrentLine(currentLine);
                // Add selection to memory
                LastStringMemoryAction = Tools.AddToLineMemory(GetSelectionText());
                DisableSelector();
            }
            else // Cut line
            {
                currentLine = currentLine.Replace(Entries.NewLineCharString, String.Empty);
                LastStringMemoryAction = Tools.AddToLineMemory(currentLine);
                LineDelete();
            }
        }
    }


    private void DuplicateLine()
    {
        if (Text.Length > 0)
        {
            var splitText = _splitEditText;
            var currentLine = splitText[_cursorDisplayY];
            currentLine = currentLine.Replace(Entries.NewLineCharString, String.Empty);
            CursorToLineEnd();
            Enter();
            AddText(currentLine);
        }
    }

    private void UppercaseLine()
    {
        // Make selection uppercase
        if (Text.Length > 0 && IsSelecting() && GetSelectionText().Length > 0)
        {
            SaveUndoSnapshot();
            var splitText = _splitEditText;
            var currentLine = splitText[_cursorDisplayY];
            currentLine = currentLine.Remove(GetSelectionStart(), GetSelectionLength());
            currentLine = currentLine.Insert(GetSelectionStart(), GetSelectionText().ToUpper());
            ReplaceCurrentLine(currentLine);
            DisableSelector();
        }

        else if (Text.Length > 0)
        {
            var splitText = _splitEditText;
            var currentLine = splitText[_cursorDisplayY];
            currentLine = currentLine.Replace(Entries.NewLineCharString, String.Empty);
            currentLine = currentLine.ToUpper() + Entries.NewLineCharString;
            ReplaceCurrentLine(currentLine);
        }
    }

    private void LowercaseLine()
    {
        // Make selection uppercase
        if (Text.Length > 0 && IsSelecting() && GetSelectionText().Length > 0)
        {
            SaveUndoSnapshot();
            var splitText = _splitEditText;
            var currentLine = splitText[_cursorDisplayY];
            currentLine = currentLine.Remove(GetSelectionStart(), GetSelectionLength());
            currentLine = currentLine.Insert(GetSelectionStart(), GetSelectionText().ToLower());
            ReplaceCurrentLine(currentLine);
            DisableSelector();
        }

        else if (Text.Length > 0)
        {
            var splitText = _splitEditText;
            var currentLine = splitText[_cursorDisplayY];
            currentLine = currentLine.Replace(Entries.NewLineCharString, String.Empty);
            currentLine = currentLine.ToLower() + Entries.NewLineCharString;
            ReplaceCurrentLine(currentLine);
        }
    }

    private void ReplaceCurrentLine(string textToInsert)
    {
        if (Text.Length > 0)
        {
            var splitText = _splitEditText;
            var currentLine = splitText[_cursorDisplayY];
            int tempRow = _cursorDisplayY;

            if (splitText.Count > 0)
            {
                SaveUndoSnapshot();
                // Replace the line in question
                for (int i = 0; i < splitText.Count; i++)
                {
                    if (i == tempRow && splitText[i] == currentLine)
                    {
                        splitText[i] = textToInsert;
                        break;
                    }
                }

                // Collect the edited lines
                var tempText = new StringBuilder();
                for (int i = 0; i < splitText.Count; i++)
                {
                    if (i == splitText.Count - 1)
                    {
                        tempText.Append(splitText[i].Replace(Entries.NewLineCharString, string.Empty));
                    }
                    else
                    {
                        tempText.Append(splitText[i]);
                    }
                }

                // Add content back to Text
                Text = tempText.ToString();

                // Draw the content
                Draw();
            }
        }
    }

    private void NextMemoryBank()
    {
        ResetStringMemoryAction();
        Tools.NextLineMemoryIndex();
    }

    private void PreviousMemoryBank()
    {
        ResetStringMemoryAction();
        Tools.PreviousLineMemoryIndex();
    }

    private void FirstMemoryBank()
    {
        ResetStringMemoryAction();
        Tools.FirstLineMemoryIndex();
    }

    private void LastMemoryBank()
    {
        ResetStringMemoryAction();
        Tools.LastLineMemoryIndex();
    }

    private void ClearMemoryBanks()
    {
        if (Tools.HasLineMemory())
        {
            if (Menu.YesNoPrompt(DialogText.CaptionsStringMemory, DialogText.ClearStringMemory, string.Empty, false))
            {
                ResetStringMemoryAction();
                Tools.ClearLineMemory();
            }

            Tools.ClearScreen();
            DrawEditMenu();
            Draw();
        }
    }

    private void ResetStringMemoryAction()
    {
        LastStringMemoryAction = 0;
    }

    private int LastStringMemoryAction { get; set; }

    private void WriteEditorMemoryStatus(bool reDraw)
    {
        if (!IsSingleLineEditor && !IsReadOnly && Tools.HasLineMemory())
        {
            if (reDraw)
            {
                Draw(); // Call Draw to clear the editor area for each new display
            }

            RemoveCursor();
            if (LastStringMemoryAction == 0)
            {
                Menu.WriteStringMemoryAction(" " + Tools.GetLineMemoryCount() + " ",
                    Tools.GetTruncatedLineMemoryPreview());
            }
            else if (LastStringMemoryAction == 1)
            {
                Menu.WriteStringMemoryAction(" " + Tools.GetLineMemoryCount() + " ", "[Already in memory]");
            }

            ShowCursor();
        }
    }

    private void WriteRepeatCommand(bool reDraw)
    {
        if (!IsSingleLineEditor && !IsReadOnly && IsRepeatableCommand(RepeatCommandMemoryIndex))
        {
            RemoveCursor();
            if (reDraw)
            {
                Draw(); // Call Draw to clear the editor area for each new display
                RemoveCursor();
                DrawEditMenu();
            }

            Menu.WriteRepeatCommand(" " + DialogText.CtrlPlus + Keyboard.KbRepeat + ": ",
                GetCommandName(RepeatCommandMemoryIndex) + " ");
            ShowCursor();
        }
    }

    // Prevent some commands from being repeated
    private static bool IsRepeatableCommand(int command)
    {
        if (command > 0)
        {
            // Check for commands that are unfit for repetition
            // Help = 9
            // Clear string memory = 14
            // Clear all = 28
            if (command == 9 || command == 14 || command == 28)
            {
                return false;
            }
            else
            {
                return true;
            }
        }
        else
        {
            return false;
        }
    }

    private void Tab()
    {
        AddText(new string(' ', 4));
    }

    private void Password()
    {
        AddText(new PasswordGenerator().GeneratePassword());
    }

    private void ToggleScrollIndicator()
    {
        SaveCursorPosition();
        RemoveCursor();
        Console.Clear();
        if (Mode.CurrentEditorScrollIndicator == Mode.EditorScrollIndicator.On)
        {
            Mode.SetEditorScrollIndicator(Mode.EditorScrollIndicator.Off);
            Menu.StatusMessage(DialogText.EditorScrollIndicator + DialogText.StatusDisabled, true, true, true,
                DialogText.CaptionsSettings, string.Empty, false);
        }
        else
        {
            Mode.SetEditorScrollIndicator(Mode.EditorScrollIndicator.On);
            Menu.StatusMessage(DialogText.EditorScrollIndicator + DialogText.StatusEnabled, true, true, true,
                DialogText.CaptionsSettings, string.Empty, false);
        }

        Console.Clear();
        LoadCursorPosition();
        DrawEditMenu();
        WriteEditorMemoryStatus(true);
        Draw();
    }

    private void TimeStamp()
    {
        AddText(IsoDateTime.GetDateTimeNow());
    }

    private void Enter()
    {
        AddLetter(Entries.NewLineChar);
    }


    private string _leftMargin = string.Empty;

    private string LeftMargin()
    {
        return _leftMargin;
    }

    private List<string> _lines = new();
    private string _line = string.Empty;
    private string _emptyLine = string.Empty;
    private double _linesPerPixel;
    private int _position;

    //private bool _skipSelectionReset;
    private void Draw()
    {
        RemoveCursor();
        UpdateCursorDisplayPosition();
        // Create empty "filler" line to overwrite previous content
        _emptyLine = LeftMargin() + new string(' ', _width - 1);
        //Draw text area
        for (int i = _offset; i < _height + _offset; i++)
        {
            _line = _emptyLine;
            //line = LeftMargin() + "".PadRight(Width - 1, ' ');
            //line = LeftMargin() + new string(' ', Width - 1); same as above just done the "normal way"
            if (_lines.Count > i)
            {
                _line = LeftMargin() + RemoveNewLine(_lines[i]).PadRight(_width - 1, ' ');
            }

            WriteText(_line, i + _topPosition - _offset, _leftPosition);
        }

        if (!IsReadOnly)
        {
            // Scrollbar indicator
            if (!IsSingleLineEditor && Mode.CurrentEditorScrollIndicator == Mode.EditorScrollIndicator.On)
            {
                SaveCursorPosition();
                // Draw Scroll Bar
                DrawColourBlock(_topPosition, _leftPosition + _width, _topPosition + _height,
                    _leftPosition + _width + 1);

                _linesPerPixel = (double)_lines.Count() / _height;
                _position = 0;
                if (_linesPerPixel > 0)
                {
                    _position = (int)Math.Floor(_cursorDisplayY / _linesPerPixel);
                }

                // Scroll indicator char
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.EditorTitle);
                WriteText("*", _topPosition + _position, _leftPosition + _width);
                ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                LoadCursorPosition();
            }
            DisplayRowColumnIndex();
            ShowCursor();

        }
    }

    // Goto cursor index
    private void GotoCursorIndex()
    {
        // Dont recall cursor indexes if single line
        if (IsSingleLineEditor)
            return;

        // Recall values above zero and below current text length
        if (_cursorRecallIndex > 0 && _cursorRecallIndex <= Text.Length)
        {
            _cursorIndex = _cursorRecallIndex;
            Draw();
        }
    }

    private void DebugMessage()
    {
        Menu.StatusMessage("Debug function (not implemented)", true, true, false, string.Empty, string.Empty, true);
        Tools.ClearScreen();
        DrawEditMenu();
        UpdateOverlays();
        Menu.IndicateChanges(true);
        Draw();
    }

    private void ToggleRowColumnDisplay()
    {
        _enableRowColumnDisplay = !_enableRowColumnDisplay;
        if (_enableRowColumnDisplay)
        {
            // Display
            DisplayRowColumnIndex();
        }
        else
        {
            // Remove by redrawing interface
            Tools.ClearScreen();
            DrawEditMenu();
            UpdateOverlays();
            Menu.IndicateChanges(true);
            Draw();
        }
    }

    private bool _enableRowColumnDisplay = true; // True by default
    private string _rowColumnIndexText = string.Empty;
    private void DisplayRowColumnIndex()
    {
        if (_enableRowColumnDisplay && !IsSingleLineEditor)
        {
            _rowColumnIndexText = string.Empty;
            SaveCursorPosition();
            Tools.CursorTopLeft();
            // Place cursor in bottom right corner
            Tools.CursorDown(Init.LineCount - 1);
            Tools.CursorRight(Init.LineWidth - 1);
            // Move cursor left in the length of the message
            _rowColumnIndexText = (_cursorDisplayY + 1).ToString("000000") + "/" + (_cursorDisplayX + 1).ToString("000000");
            Tools.CursorLeft(_rowColumnIndexText.Length);
            // Write
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write(_rowColumnIndexText);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
            LoadCursorPosition();
        }
    }

    int _lastSplit;
    private List<string> _splitText = new();

    private List<string> CreateSplitText()
    {
        _splitText.Clear();
        _lastSplit = 0;
        for (int i = 0; i < Text.Length + 1; i++) // Changed from Count to Length
        {
            if (Text.Length > i && Text[i] == Entries.NewLineChar)
            {
                _splitText.Add(Text.Substring(_lastSplit, i - _lastSplit + 1));
                _lastSplit = i + 1;
            }
            else if (i - _lastSplit == _width - 2)
            {
                _splitText.Add(Text.Substring(_lastSplit, i - _lastSplit));
                _lastSplit = i;
            }

            if (i == Text.Length)
                _splitText.Add(Text.Substring(_lastSplit, Text.Length - _lastSplit));
        }

        return _splitText.Select(x => x.Replace(Entries.ReturnChar, ' ')).ToList();
    }

    private void ShowCursor()
    {
        if (!IsReadOnly)
        {
            _cursor.PlaceCursor(_topPosition + CursorDisplayY - _offset,
                _leftPosition + LeftMargin().Length + _cursorDisplayX,
                (Text + ' ')[_cursorIndex]);
        }
    }

    private int _tempCursorMemoryTop;
    private int _tempCursorMemoryLeft;

    private void SaveCursorPosition()
    {
        _tempCursorMemoryTop = Console.CursorTop;
        _tempCursorMemoryLeft = Console.CursorLeft;
    }

    private void LoadCursorPosition()
    {
        Tools.SetCursorEx(_tempCursorMemoryTop, _tempCursorMemoryLeft);
    }

    private static int _displayX;
    private static int _displayY;

    private void UpdateCursorDisplayPosition()
    {
        _lines = _splitEditText;
        _displayX = 0;
        _displayY = 0;

        for (int i = 0; i < _cursorIndex; i++)
        {
            if (_lines[_displayX].Length > _displayY &&
                _lines[_displayX][_displayY] == Entries.NewLineChar) // Skip NewLine characters
            {
                _displayY++;
            }

            if (_lines.Count > _displayX)
            {
                if (_lines[_displayX].Length > _displayY)
                {
                    _displayY++;
                }
                else if (_lines.Count - 1 > _displayX)
                {
                    _displayX++;
                    _displayY = 0;
                }
            }

            if (_displayY == 0 && _displayX - 1 >= 0 &&
                _lines[_displayX - 1].Last() != Entries.NewLineChar) // Wordwrap Stuff
            {
                _displayY++;
            }
            else if (_displayY == 1 && _displayX - 1 >= 0 && _lines[_displayX - 1].Last() != Entries.NewLineChar)
            {
                _displayY--;
            }
        }

        CursorDisplayY = _displayX;
        _cursorDisplayX = _displayY;
    }

    public void RemoveCursor()
    {
        _cursor.RemoveCursor();
    }

    private void SetOffset()
    {
        while (CursorDisplayY - _offset > _height - 1)
            _offset++;

        while (CursorDisplayY - _offset < 0)
            _offset--;
    }

    private StringBuilder _toReturn = new();

    private string RemoveNewLine(string text)
    {
        _toReturn.Clear();
        foreach (var letter in text.Where(letter => letter != Entries.NewLineChar))
        {
            _toReturn.Append(letter);
        }

        return _toReturn.ToString();
    }

    #region Search

    private void DoSearch()
    {
        // Return if no text
        if (_text.Length == 0) return;

        // Draw search box
        Menu.WriteInputPrompt(DialogText.CaptionsContentSearch, DialogText.PressEscToExit, 34,
            ConsoleColors.ColorPreset.HighlightInverted);

        // Search query (Esc to exit)
        bool takeInput = true;
        string search = string.Empty;
        ConsoleKeyInfo c;
        do
        {
            Tools.ShowCursor();
            c = Console.ReadKey(intercept: true);
            Tools.HideCursor();

            if (c.Key == ConsoleKey.Enter && search.Length > 0)
            {
                takeInput = false;
            }
            else if (c.Key == ConsoleKey.Escape)
            {
                ExitSearch();
                return;
            }
            else if (c.Key == ConsoleKey.Backspace && search.Length > 0)
            {
                // Delete with backspace
                Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
                Console.Write(" ");
                Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
                search = search[0..^1];
            }
            else if (!char.IsControl(c.KeyChar) && search.Length < 32)
            {
                // Write to output
                Console.Write(c.KeyChar);
                search += c.KeyChar;
            }
        } while (takeInput);

        if (string.IsNullOrEmpty(search)) return;
        // Collection to hold found indexes
        List<int> foundAt = new List<int>();
        // ToLower all
        search = search.ToLower();
        var tempContent = _text.ToLower();

        // Reset colors
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);

        // Collect matching indexes
        if (tempContent.Length > 0)
        {
            for (int i = 0; i < tempContent.Length; i++)
            {
                if ((i + search.Length) <= tempContent.Length)
                {
                    if (tempContent.Substring(i, search.Length) == search)
                    {
                        foundAt.Add(i);
                    }
                }
            }
        }

        // If one or more indexes found the search is a success
        if (foundAt.Count > 0)
        {
            do
            {
                for (int i = 0; i < foundAt.Count; i++)
                {
                    // Write count
                    // Place cursor
                    Tools.HideCursor();
                    Tools.CursorTopLeft();
                    Tools.CursorRight(3);
                    ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
                    Tools.CursorReturn();
                    Tools.CursorRight((5 + DialogText.CaptionsContentSearch.Length));
                    Console.Write("(" + (i + 1) + "/" + foundAt.Count + ") ");
                    Tools.CursorReturn();
                    Tools.CursorDown(2);
                    Tools.CursorRight(33 - DialogText.PressStoRetryEscToExit.Length);
                    Console.Write(" " + DialogText.PressStoRetryEscToExit + " ");
                    ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);

                    // Place cursor at current index (search result) in the text
                    if (foundAt[i] <= Text.Length)
                    {
                        _cursorIndex = foundAt[i];
                        Draw();
                        // Highlight first letter of result (avoid the whole word as there might
                        // be word brakes and words continuing below the scroll line).
                        if (search.Length >= 1)
                        {
                            ConsoleColors.SetColors(ConsoleColors.ColorPreset.HighlightInverted);
                            Console.Write(search.Substring(0, 1));
                            Tools.CursorLeft(); // Place cursor on highlighted character
                            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
                        }
                    }

                    // Await Esc or run through the results
                    c = Console.ReadKey(intercept: true);
                    if (c.Key == ConsoleKey.Escape)
                    {
                        ExitSearch();
                        return;
                    }
                    else if (c.Key == ConsoleKey.S)
                    {
                        DoSearch();
                        return;
                    }
                }
            } while (true);
        }
        else
        {
            // Indicate nothing was found
            Tools.HideCursor();
            Menu.StatusMessage(DialogText.UnableToFind + " \"" + search + "\"", true, true, true,
                DialogText.CaptionsSearch, string.Empty, false);
            ExitSearch();
            DoSearch();
        }
    }

    private void ExitSearch()
    {
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Tools.ClearScreen();
        DrawEditMenu();
        Draw();
    }

    #endregion
}

#endregion

#region Cursor

public class Cursor
{
    private int _top;
    private int _left;
    private bool _visible;

    public void PlaceCursor(int top, int left, char letter)
    {
        Console.CursorVisible = true;
        _visible = true;
        _top = top;
        _left = left;
        Tools.SetCursorEx(_top, _left);
    }

    public void RemoveCursor()
    {
        if (_visible)
        {
            Console.CursorVisible = false;
            _visible = false;
        }
    }
}

#endregion

internal static class DocumentInfo
{
    public static bool HasChanged;
}

#region Custom undo stack

internal class CustomStack<T>
{
    private List<T> items = new List<T>();

    public void Push(T item)
    {
        items.Add(item);
    }

    public T Pop()
    {
        if (items.Count <= 0) return default(T)!;
        var temp = items[^1];
        items.RemoveAt(items.Count - 1);
        return temp;
    }

    public int Count()
    {
        return items.Count;
    }

    public void Remove(int index, int count)
    {
        items.RemoveRange(index, count);
    }
}

#endregion