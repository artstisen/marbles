using System.Text;
using System.Text.RegularExpressions;

namespace Marbles;

internal enum EditorMode
{
    Normal,
    Insert,
    Visual,
    VisualLine,
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
    InsertCalendar,
    ClearAll,
    ClearRegisters
}

/// <summary>
/// The modal (Normal/Insert/Visual) text editor. It holds the text, the cursor and the mode, and turns
/// key presses into edits. It never touches the console: the TextArea control feeds it keys and draws its state.
/// This file holds the state and the Insert and command line modes. Normal and Visual mode are found in
/// EditorEngine.Normal.cs and cursor movement in EditorEngine.Motions.cs.
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

    internal bool IsVisual => Mode is EditorMode.Visual or EditorMode.VisualLine;
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
    internal void SetMessage(string message)
    {
        Message = message;
        MessageIsError = false;
    }

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

        switch (Mode)
        {
            case EditorMode.Insert:
                HandleInsert(key);
                break;
            case EditorMode.Command:
            case EditorMode.Search:
                HandleCommandLine(key);
                break;
            default:
                HandleNormal(key);
                break;
        }

        // A command is complete when back in Normal mode with nothing pending
        if (Mode is EditorMode.Normal or EditorMode.Visual or EditorMode.VisualLine && !HasPending)
        {
            Buffer.EndStep(Cursor);
        }

        if (!_replaying && Mode == EditorMode.Normal && !HasPending && !_doNotRecord &&
            (_recordedInsert || Buffer.Version != _recordedVersion))
        {
            _lastChange = _recordedKeys.ToArray();
        }

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

    private void OpenCommandLine(char prefix)
    {
        ResetPending();
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
        // Commands may start with the lines to work on, like ":%s/a/b/" or ":2,5d"
        if (!ParseRange(ref command, out var range))
        {
            SetError("Invalid range");
            return;
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
            else
            {
                SetError("Not an editor command: " + command);
            }

            return;
        }

        if (command[0] == 's' && command.Length > 1 && !char.IsLetterOrDigit(command[1]))
        {
            Substitute(command[1..], Cursor.Line, Cursor.Line);
            return;
        }

        switch (command)
        {
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
            case "cal":
            case "calendar":
                _request = EditorRequest.InsertCalendar;
                break;
            case "trim":
                TrimLines();
                break;
            case "clear":
                if (!Buffer.IsEmpty) _request = EditorRequest.ClearAll;
                break;
            case "reg":
            case "registers":
                ShowRegister();
                break;
            case "regclear":
                if (Tools.HasLineMemory()) _request = EditorRequest.ClearRegisters;
                break;
            case "putall":
                PutAllRegisters();
                break;
            default:
                SetError("Not an editor command: " + command);
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

    #endregion

    #region Search and replace

    private static readonly TimeSpan SearchTimeout = TimeSpan.FromSeconds(2);
    private Regex? _searchRegex;

    /// <summary>
    /// Create the regular expression for a search pattern. Like Vim's "smartcase" the search ignores case
    /// unless the pattern has uppercase letters. A pattern that is not a valid expression is searched for as text.
    /// </summary>
    private static Regex CreateRegex(string pattern, bool? ignoreCase = null)
    {
        var options = RegexOptions.CultureInvariant;
        if (ignoreCase ?? !pattern.Any(char.IsUpper)) options |= RegexOptions.IgnoreCase;
        try
        {
            return new Regex(pattern, options, SearchTimeout);
        }
        catch (ArgumentException)
        {
            return new Regex(Regex.Escape(pattern), options, SearchTimeout);
        }
    }

    private void SetSearch(string pattern)
    {
        _lastSearch = pattern;
        _searchRegex = CreateRegex(pattern);
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
        else
        {
            SetError("Pattern not found: " + pattern);
        }
    }

    /// <summary>
    /// Find the next match of the last search (the search wraps around the document)
    /// </summary>
    private bool FindNext(TextPos from, bool forward, out TextPos found)
    {
        found = from;
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
            SetError("Search took too long");
        }

        return false;
    }

    /// <summary>
    /// The parts of a line (start and end char index) matching the last search
    /// </summary>
    internal IEnumerable<(int Start, int End)> SearchMatches(int line)
    {
        if (!HighlightSearch || _searchRegex == null) yield break;
        MatchCollection matches;
        try
        {
            matches = _searchRegex.Matches(Buffer[line]);
            _ = matches.Count; // The matches are found here, so a timeout is caught
        }
        catch (RegexMatchTimeoutException)
        {
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
            SetError("No previous search");
            return;
        }

        if (flags.Any(flag => flag is not ('g' or 'i' or 'I')))
        {
            SetError("Unknown flags: " + flags);
            return;
        }

        bool? ignoreCase = flags.Contains('i') ? true : flags.Contains('I') ? false : null;
        var regex = CreateRegex(pattern, ignoreCase);
        var global = flags.Contains('g');
        replacement = ConvertReplacement(replacement);
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
            SetError("Search took too long");
            return;
        }

        if (substitutions == 0)
        {
            SetError("Pattern not found: " + pattern);
            return;
        }

        if (cursorLine >= 0) GoToLine(cursorLine);
        Message = substitutions + (substitutions == 1 ? " substitution on " : " substitutions on ") + lines +
                  (lines == 1 ? " line" : " lines");
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
        var row = Layout.RowOf(Cursor);
        if (row < TopRow)
        {
            TopRow = row;
        }
        else if (row >= TopRow + Height)
        {
            TopRow = row - Height + 1;
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
