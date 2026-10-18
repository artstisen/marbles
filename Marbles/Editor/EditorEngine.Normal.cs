using System.Text;
using System.Text.RegularExpressions;

namespace Marbles;

// Normal and Visual mode: counts, operators ("d", "c", "y" ...), text objects and the single key commands.
internal sealed partial class EditorEngine
{
    private const char CtrlB = '\u0002';
    private const char CtrlD = '\u0004';
    private const char CtrlE = '\u0005';
    private const char CtrlF = '\u0006';
    private const char CtrlN = '\u000E';
    private const char CtrlP = '\u0010';
    private const char CtrlR = '\u0012';
    private const char CtrlU = '\u0015';
    private const char CtrlY = '\u0019';
    private const char NoKey = '\0';
    private const int MaxCount = 100000;

    // The parts typed so far of a command that is not yet complete, like the "2d" of "2d3w"
    private string _count = string.Empty; // The count being typed
    private string _opCount = string.Empty; // The count typed before the operator
    private char _operator; // "d", "c", "y", ">", "<", or "u", "U" and "~" for "gu", "gU" and "g~"
    private char _prefix; // A key waiting for a second key: "g", "z", "Z", "r", "f", "F", "t", "T", "i", "a", "[", "]", '"'
    private bool _clipboardAsked; // '"+' was typed: the next yank goes to the clipboard
    private bool _clipboardJustAsked; // ... it was typed with the key just handled
    private TextPos _visualAnchor; // Where the selection was started in Visual mode
    private Selected? _lastSelection; // The selection Visual mode was last left with ("gv")

    // A selection: where it was started, where the cursor was, the kind of selection, and "$" in Visual Block mode
    private sealed record Selected(TextPos Anchor, TextPos Cursor, EditorMode Mode, bool ToEnd);

    internal bool HasPending => _count.Length > 0 || _opCount.Length > 0 || _operator != NoKey || _prefix != NoKey;

    private void ResetPending()
    {
        _count = string.Empty;
        _opCount = string.Empty;
        _operator = NoKey;
        _prefix = NoKey;
        _pendingHigh = NoKey;
    }

    // The first half of a character outside the Basic Multilingual Plane (an emoji), typed after f, F, t, T or r:
    // the console gives it as two keys, and the command takes both
    private char _pendingHigh = NoKey;

    private static string OperatorName(char op)
    {
        return op is 'u' or 'U' or '~' ? "g" + op : op.ToString();
    }

    // The count of the current command (the counts before and after an operator are multiplied)
    private int? TakeCount()
    {
        if (_count.Length == 0 && _opCount.Length == 0) return null;
        var count = (long)(_opCount.Length > 0 ? int.Parse(_opCount) : 1) * (_count.Length > 0 ? int.Parse(_count) : 1);
        _count = string.Empty;
        _opCount = string.Empty;
        return (int)Math.Clamp(count, 1, MaxCount);
    }

    // Translate a key press to the character used for it in Normal mode
    private static char Translate(ConsoleKeyInfo key)
    {
        if (IsText(key)) return key.KeyChar;
        var control = (key.Modifiers & ConsoleModifiers.Control) != 0 && (key.Modifiers & ConsoleModifiers.Alt) == 0;
        switch (key.Key)
        {
            case ConsoleKey.LeftArrow:
                return control ? 'b' : 'h';
            case ConsoleKey.RightArrow:
                return control ? 'w' : 'l';
            case ConsoleKey.Backspace:
                return 'h';
            case ConsoleKey.UpArrow:
                return 'k';
            case ConsoleKey.DownArrow:
                return 'j';
            case ConsoleKey.Home:
                return '0';
            case ConsoleKey.End:
                return '$';
            case ConsoleKey.Delete:
                return 'x';
            case ConsoleKey.PageDown:
                return CtrlF;
            case ConsoleKey.PageUp:
                return CtrlB;
        }

        if (IsEnter(key)) return '\r';
        if (control && key.Key is >= ConsoleKey.A and <= ConsoleKey.Z) return (char)(key.Key - ConsoleKey.A + 1);
        return NoKey;
    }

    private void HandleNormal(ConsoleKeyInfo key)
    {
        if (IsEscape(key))
        {
            if (HasPending)
            {
                ResetPending();
            }
            else if (IsVisual)
            {
                Mode = EditorMode.Normal;
            }
            else if (HighlightSearch && _searchRegex != null)
            {
                HighlightSearch = false;
            }
            else
            {
                // Nothing to leave or cancel: close the editor (as ":q"). While a macro is recorded: ask about it
                _request = IsRecording && !IsPlayingMacro ? EditorRequest.MacroStop : EditorRequest.Quit;
            }

            return;
        }

        // Tab and Shift+Tab: the other pane of a split (a macro works on the pane it is recorded in)
        if (key.Key == ConsoleKey.Tab && !SingleLine && Mode == EditorMode.Normal)
        {
            ResetPending();
            if (IsRecording && !IsPlayingMacro)
            {
                SetError(Lang.T("The other pane is not used while recording"));
                // Not a step of the macro
                _macroCommandLeftOut = true;
            }
            else _request = EditorRequest.SwitchPane;
            return;
        }

        var cmd = Translate(key);
        if (cmd == NoKey)
        {
            ResetPending();
            return;
        }

        // Second key of a two key command
        if (_prefix != NoKey)
        {
            var prefix = _prefix;
            _prefix = NoKey;
            if (prefix is 'f' or 'F' or 't' or 'T' or 'r' && IsText(key))
            {
                if (_pendingHigh == NoKey && char.IsHighSurrogate(cmd))
                {
                    // Wait for the second half
                    _prefix = prefix;
                    _pendingHigh = cmd;
                    return;
                }

                if (_pendingHigh != NoKey)
                {
                    var high = _pendingHigh;
                    _pendingHigh = NoKey;
                    // A half without the other is not a character: nothing is done
                    if (char.IsLowSurrogate(cmd)) HandlePrefixed(prefix, cmd, true, new string(new[] { high, cmd }));
                    return;
                }
            }

            HandlePrefixed(prefix, cmd, IsText(key));
            return;
        }

        // Count
        if (char.IsAsciiDigit(cmd) && (cmd != '0' || _count.Length > 0))
        {
            if (_count.Length < 6) _count += cmd;
            return;
        }

        // First key of a two key command
        var operatorPending = _operator != NoKey;
        if (cmd is 'i' or 'a' && (operatorPending || IsVisual) ||
            cmd is 'f' or 'F' or 't' or 'T' or 'g' or 'z' ||
            (cmd == 'Z' && !IsVisual || cmd == 'r' && (!IsVisual || Mode == EditorMode.VisualBlock)) &&
            !operatorPending ||
            cmd is '[' or ']' && !operatorPending ||
            cmd == '"' && !operatorPending)
        {
            _prefix = cmd;
            return;
        }

        // Operators
        if (cmd is 'd' or 'c' or 'y' or '>' or '<' || operatorPending && cmd == _operator)
        {
            HandleOperatorKey(cmd);
            return;
        }

        // A comparison: "n" "N" go to the changes (to the matches when a search is highlighted)
        if (!operatorPending && cmd is 'n' or 'N' && ChangeLines != null && !IsSearchHighlighted)
        {
            JumpToChange(cmd == 'n', TakeCount() ?? 1);
            ResetPending();
            return;
        }

        // Up and down move by rows as shown on screen (as the target of an operator they are whole lines)
        if (!operatorPending && cmd is 'j' or 'k')
        {
            var rows = TakeCount() ?? 1;
            MoveRows(cmd == 'j' ? rows : -rows);
            ResetPending();
            return;
        }

        // Motions
        var count = TakeCount();
        var motionKey = cmd;
        // "cw" on a word changes to the end of the word, leaving the blank space after it
        if (_operator == 'c' && cmd is 'w' or 'W' && Cursor.Col < CurrentLine.Length &&
            !char.IsWhiteSpace(CurrentLine[Cursor.Col]))
        {
            motionKey = cmd == 'w' ? 'e' : 'E';
        }

        if (TryMotion(motionKey, count, operatorPending, out var motion))
        {
            // A block selected with "$" goes to the end of each line, until the cursor is moved sideways
            if (Mode == EditorMode.VisualBlock) _blockToEnd = motionKey == '$' || _blockToEnd && motionKey == 'G';
            RunMotion(motion);
            return;
        }

        // Everything else
        if (!operatorPending && !(IsVisual && HandleVisualCommand(cmd, count ?? 1)))
        {
            HandleCommand(cmd, count);
        }

        ResetPending();
    }

    private void RunMotion(Motion motion)
    {
        if (_operator != NoKey)
        {
            ApplyOperator(_operator, motion);
        }
        else
        {
            Cursor = motion.Target;
        }

        ResetPending();
    }

    // typed: the character typed after f, F, t, T or r, when it is two chars (an emoji)
    private void HandlePrefixed(char prefix, char cmd, bool isText, string? typed = null)
    {
        typed ??= cmd.ToString();
        var last = Buffer.LineCount - 1;
        switch (prefix)
        {
            case '"':
                // A register: only "+ (and "*), the system clipboard, for the next yank
                if (cmd is '+' or '*')
                {
                    _clipboardAsked = true;
                    _clipboardJustAsked = true;
                }
                else
                {
                    // (short, to fit on the status line next to the position)
                    SetError(Lang.T("Type \"+y for the clipboard"));
                }

                return;
            case 'f':
            case 'F':
            case 't':
            case 'T':
                if (isText)
                {
                    _lastFindCommand = prefix;
                    _lastFindChar = typed;
                    if (FindChar(prefix, typed, TakeCount() ?? 1, false, out var motion))
                    {
                        RunMotion(motion);
                        return;
                    }
                }

                break;
            case 'r':
                if (isText && Mode == EditorMode.VisualBlock)
                {
                    ReplaceBlock(typed);
                }
                else if (isText)
                {
                    ReplaceChars(typed, TakeCount() ?? 1);
                }

                break;
            case 'i':
            case 'a':
                if (cmd == 'p')
                {
                    // A paragraph: whole lines (in Visual mode it selects them in Visual Line mode)
                    var (first, lastLine) = ParagraphLines(prefix == 'a');
                    if (IsVisual)
                    {
                        _visualAnchor = new TextPos(first, 0);
                        Cursor = new TextPos(lastLine, 0);
                        Mode = EditorMode.VisualLine;
                    }
                    else if (_operator != NoKey)
                    {
                        ApplyOperatorToLines(_operator, first, lastLine);
                    }

                    break;
                }

                if (TextObject(cmd, prefix == 'a', out var from, out var to))
                {
                    if (IsVisual && from.Col == 0 && to.Col == 0 && to.Line > from.Line)
                    {
                        // Whole lines (the inside of a block whose brackets are on lines of their own)
                        _visualAnchor = from;
                        Cursor = new TextPos(to.Line - 1, 0);
                        Mode = EditorMode.VisualLine;
                    }
                    else if (IsVisual)
                    {
                        _visualAnchor = from;
                        Cursor = Left(to, 1);
                        Mode = EditorMode.Visual;
                    }
                    else
                    {
                        ApplyOperatorToRange(_operator, from, to);
                    }
                }

                break;
            case 'g':
                switch (cmd)
                {
                    case 'g':
                    {
                        var count = TakeCount();
                        var line = count.HasValue ? Math.Clamp(count.Value - 1, 0, last) : 0;
                        RunMotion(new Motion(new TextPos(line, FirstNonBlank(line)), true));
                        return;
                    }
                    case 'u':
                    case 'U':
                    case '~':
                        HandleOperatorKey(cmd);
                        return;
                    case 'v' when _operator == NoKey:
                        ReselectLast();
                        break;
                    case 's' or 'S' when _operator == NoKey:
                        // The next/previous misspelled word (as "]s" "[s", for keyboards without [ and ])
                        JumpToMisspelled(cmd == 's', TakeCount() ?? 1);
                        break;
                    case 'm':
                    case 'M':
                    {
                        // Half the width of the text to the right (gm) or the left (gM), on the line
                        var half = Math.Max(1, Layout.Width / 2);
                        var line = CurrentLine;
                        var cell = TextMetrics.WidthOfRange(line, 0, Math.Min(Cursor.Col, line.Length));
                        var target = Math.Max(0, cell + (cmd == 'm' ? half : -half));
                        var col = 0;
                        var width = 0;
                        while (col < line.Length)
                        {
                            var length = TextMetrics.ElementLength(line, col);
                            var next = width + TextMetrics.WidthOfRange(line, col, col + length);
                            if (next > target) break;
                            width = next;
                            col += length;
                        }

                        // Not past the last char in Normal mode
                        if (col >= line.Length && line.Length > 0 && Mode != EditorMode.Insert)
                        {
                            col = line.Length - TextMetrics.PreviousElementLength(line, line.Length);
                        }

                        RunMotion(new Motion(new TextPos(Cursor.Line, col)));
                        return;
                    }
                    case 'j':
                    case 'k':
                    {
                        // Move by lines of the document instead of rows on screen
                        var count = TakeCount() ?? 1;
                        var line = Math.Clamp(Cursor.Line + (cmd == 'j' ? count : -count), 0, last);
                        if (_operator == NoKey)
                        {
                            Cursor = new TextPos(line, Cursor.Col);
                        }
                        else
                        {
                            ApplyOperator(_operator, new Motion(new TextPos(line, 0), true));
                        }

                        break;
                    }
                }

                break;
            case '[' or ']':
                if (cmd == 's') JumpToMisspelled(prefix == ']', TakeCount() ?? 1);
                // A comparison: the next/previous change
                if (cmd == 'c' && ChangeLines != null) JumpToChange(prefix == ']', TakeCount() ?? 1);
                break;
            case 'z' when cmd == '=':
                RequestSuggestions();
                break;
            case 'z' when cmd == 'g':
                AddSpellWord();
                break;
            case 'z' when cmd is 'h' or 'l' or 'H' or 'L' or 's' or 'e':
                ScrollSideways(cmd, TakeCount() ?? 1);
                break;
            case 'z':
            {
                // Scroll the view to place the cursor row at the top (t), center (z) or bottom (b)
                var row = Layout.RowOf(Cursor);
                var top = cmd switch
                {
                    't' or '\r' => row,
                    'z' or '.' => row - Height / 2,
                    'b' or '-' => row - Height + 1,
                    _ => TopRow
                };
                TopRow = Math.Clamp(top, 0, Math.Max(0, Layout.TotalRows() - 1));
                break;
            }
            case 'Z':
                if (cmd == 'Z') _request = EditorRequest.SaveAndQuit;
                if (cmd == 'Q') _request = EditorRequest.ForceQuit;
                break;
        }

        ResetPending();
    }

    #region Operators

    private void HandleOperatorKey(char op)
    {
        if (IsVisual)
        {
            ApplyOperatorToSelection(op, false);
            ResetPending();
        }
        else if (_operator == op)
        {
            // The operator typed twice works on whole lines ("dd", "3yy", "guu")
            var count = TakeCount() ?? 1;
            var lastLine = Math.Min(Buffer.LineCount - 1, Cursor.Line + count - 1);
            ApplyOperator(op, new Motion(new TextPos(lastLine, 0), true));
            ResetPending();
        }
        else if (_operator != NoKey)
        {
            ResetPending();
        }
        else
        {
            _operator = op;
            _opCount = _count;
            _count = string.Empty;
        }
    }

    private void ApplyOperator(char op, Motion motion)
    {
        if (motion.Linewise)
        {
            ApplyOperatorToLines(op, Math.Min(Cursor.Line, motion.Target.Line),
                Math.Max(Cursor.Line, motion.Target.Line));
            return;
        }

        var from = TextPos.Min(Cursor, motion.Target);
        var to = TextPos.Max(Cursor, motion.Target);
        if (motion.Inclusive)
        {
            if (to.Col < Buffer.LineLength(to.Line))
            {
                to = to with { Col = to.Col + TextMetrics.ElementLength(Buffer[to.Line], to.Col) };
            }
        }
        else if (to.Col == 0 && to.Line > from.Line)
        {
            // A motion ending at the start of a line stops at the end of the line before it
            to = new TextPos(to.Line - 1, Buffer.LineLength(to.Line - 1));
        }

        ApplyOperatorToRange(op, from, to);
    }

    private void ApplyOperatorToSelection(char op, bool forceLinewise)
    {
        if (Mode == EditorMode.VisualBlock && !forceLinewise)
        {
            ApplyOperatorToBlock(op, false);
            return;
        }

        var from = TextPos.Min(_visualAnchor, Cursor);
        var to = TextPos.Max(_visualAnchor, Cursor);
        var linewise = Mode == EditorMode.VisualLine || forceLinewise;
        Mode = EditorMode.Normal;
        Cursor = from;
        if (linewise)
        {
            ApplyOperatorToLines(op, from.Line, to.Line);
            return;
        }

        if (to.Col < Buffer.LineLength(to.Line))
        {
            to = to with { Col = to.Col + TextMetrics.ElementLength(Buffer[to.Line], to.Col) };
        }

        ApplyOperatorToRange(op, from, to);
    }

    // Apply an operator to the text from a position and up to (not including) another
    private void ApplyOperatorToRange(char op, TextPos from, TextPos to)
    {
        if (op is '>' or '<')
        {
            ApplyOperatorToLines(op, from.Line, to.Line);
            return;
        }

        var text = Buffer.GetText(from, to);
        switch (op)
        {
            case 'd':
            case 'c':
                if (text.Length > 0)
                {
                    // Like in Vim, text deleted within a line does not replace what was yanked
                    StoreRegister(text, false, !text.Contains(TextBuffer.NewLine));
                    Delete(from, to);
                }

                Cursor = from;
                if (op == 'c') EnterInsert();
                break;
            case 'y':
                StoreRegister(text, false, false);
                Yanked(text);
                Cursor = from;
                break;
            case 'u':
            case 'U':
            case '~':
                ChangeCase(op, from, to);
                Cursor = from;
                break;
        }
    }

    // Apply an operator to whole lines
    private void ApplyOperatorToLines(char op, int first, int last)
    {
        var start = new TextPos(first, 0);
        var end = new TextPos(last, Buffer.LineLength(last));
        var count = last - first + 1;
        switch (op)
        {
            case 'd':
                StoreRegister(Buffer.GetText(start, end), true, false);
                if (last < Buffer.LineCount - 1)
                {
                    Delete(start, new TextPos(last + 1, 0));
                }
                else if (first > 0)
                {
                    Delete(new TextPos(first - 1, Buffer.LineLength(first - 1)), end);
                }
                else
                {
                    Delete(start, end);
                }

                var line = Math.Min(first, Buffer.LineCount - 1);
                Cursor = new TextPos(line, FirstNonBlank(line));
                if (count > 2) Message = Lang.F("{0} fewer lines", count);
                break;
            case 'c':
                StoreRegister(Buffer.GetText(start, end), true, false);
                Delete(start, end);
                Cursor = start;
                EnterInsert();
                break;
            case 'y':
                StoreRegister(Buffer.GetText(start, end), true, false);
                Yanked(Buffer.GetText(start, end) + TextBuffer.NewLine);
                Cursor = new TextPos(first, Cursor.Col);
                if (count > 2) Message = Lang.F("{0} lines yanked", count);
                break;
            case '>':
                for (var i = first; i <= last; i++)
                {
                    if (Buffer.LineLength(i) > 0) Insert(new TextPos(i, 0), new string(' ', TabWidth));
                }

                Cursor = new TextPos(first, FirstNonBlank(first));
                break;
            case '<':
                for (var i = first; i <= last; i++)
                {
                    var indent = Math.Min(TabWidth, FirstNonBlank(i));
                    Delete(new TextPos(i, 0), new TextPos(i, indent));
                }

                Cursor = new TextPos(first, FirstNonBlank(first));
                break;
            case 'u':
            case 'U':
            case '~':
                ChangeCase(op, start, end);
                Cursor = new TextPos(first, Cursor.Line == first ? Cursor.Col : 0);
                break;
        }
    }

    private void ChangeCase(char op, TextPos from, TextPos to)
    {
        var text = Buffer.GetText(from, to);
        var changed = op switch
        {
            'u' => text.ToLower(),
            'U' => text.ToUpper(),
            _ => string.Concat(text.Select(c => char.IsUpper(c) ? char.ToLower(c) : char.ToUpper(c)))
        };
        // Leave the text alone if the change of case alters its length (possible for a few special characters)
        if (changed == text || changed.Length != text.Length) return;
        Delete(from, to);
        Insert(from, changed);
    }

    #endregion

    #region Text objects

    // The "iw", "aw", "i(" ... objects: a piece of text on the current line selected by what it is
    private bool TextObject(char obj, bool around, out TextPos from, out TextPos to)
    {
        from = to = Cursor;
        var line = CurrentLine;
        if (line.Length == 0) return false;
        var col = Math.Min(Cursor.Col, line.Length - 1);
        int start, end;
        switch (obj)
        {
            case 'w':
            case 'W':
            {
                var big = obj == 'W';
                var charClass = CharClass(line, col, big);
                start = col;
                end = col + 1;
                while (start > 0 && CharClass(line, start - 1, big) == charClass) start--;
                while (end < line.Length && CharClass(line, end, big) == charClass) end++;
                if (around && charClass != 0)
                {
                    // Include the blank space after the word, or else before it
                    var wordEnd = end;
                    while (end < line.Length && CharClass(line, end, big) == 0) end++;
                    if (end == wordEnd)
                    {
                        while (start > 0 && CharClass(line, start - 1, big) == 0) start--;
                    }
                }
                else if (around && end < line.Length)
                {
                    // On blank space: include the word after it
                    var nextClass = CharClass(line, end, big);
                    while (end < line.Length && CharClass(line, end, big) == nextClass) end++;
                }

                break;
            }
            case '"':
            case '\'':
            case '`':
            {
                // The quotes on the line are paired from the left. Use the first pair ending at or after the cursor
                start = -1;
                end = -1;
                var open = -1;
                for (var i = 0; i < line.Length; i++)
                {
                    if (line[i] != obj) continue;
                    if (open < 0)
                    {
                        open = i;
                    }
                    else if (i >= col)
                    {
                        start = open;
                        end = i;
                        break;
                    }
                    else
                    {
                        open = -1;
                    }
                }

                if (start < 0) return false;
                if (around)
                {
                    end++;
                }
                else
                {
                    start++;
                }

                break;
            }
            default:
                return BracketObject(obj, around, out from, out to);
        }

        from = Cursor with { Col = start };
        to = Cursor with { Col = end };
        return true;
    }

    // A text object in brackets ("ib" "i(" "iB" "i{" "i[" "i<", or with "a" the brackets too), which can go over
    // several lines. In a block whose brackets are on lines of their own, "i" takes the lines between them
    private bool BracketObject(char obj, bool around, out TextPos from, out TextPos to)
    {
        from = to = Cursor;
        char openChar, closeChar;
        switch (obj)
        {
            case '(' or ')' or 'b':
                (openChar, closeChar) = ('(', ')');
                break;
            case '[' or ']':
                (openChar, closeChar) = ('[', ']');
                break;
            case '{' or '}' or 'B':
                (openChar, closeChar) = ('{', '}');
                break;
            case '<' or '>':
                (openChar, closeChar) = ('<', '>');
                break;
            default:
                return false;
        }

        var text = Buffer.GetText();
        var cursor = Buffer.ToIndex(Buffer.Clamp(Cursor));
        if (cursor >= text.Length) cursor = Math.Max(0, text.Length - 1);
        if (text.Length == 0) return false;
        // The bracket opening before the cursor (or under it), and the one closing it
        var open = -1;
        var depth = 0;
        for (var i = cursor; i >= 0; i--)
        {
            if (text[i] == closeChar && i != cursor)
            {
                depth++;
            }
            else if (text[i] == openChar)
            {
                if (depth == 0)
                {
                    open = i;
                    break;
                }

                depth--;
            }
        }

        if (open < 0) return false;
        var close = -1;
        depth = 0;
        for (var i = open + 1; i < text.Length; i++)
        {
            if (text[i] == openChar)
            {
                depth++;
            }
            else if (text[i] == closeChar)
            {
                if (depth == 0)
                {
                    close = i;
                    break;
                }

                depth--;
            }
        }

        if (close < 0) return false;
        int first, last;
        if (around)
        {
            first = open;
            last = close + 1;
        }
        else
        {
            first = open + 1;
            last = close;
            // The opening bracket ends its line: the inner text starts on the next line
            if (first < last && text[first] == TextBuffer.NewLine) first++;
            // The closing bracket starts its line (after blank space): the inner text ends with the line before
            var lineStart = last;
            while (lineStart > first && text[lineStart - 1] is ' ' or '\t') lineStart--;
            if (lineStart > first && text[lineStart - 1] == TextBuffer.NewLine) last = lineStart;
        }

        from = Buffer.FromIndex(first);
        to = Buffer.FromIndex(last);
        return true;
    }

    // The lines of a paragraph ("ip"): the lines around the cursor that are not blank (or that are blank, on a
    // blank line). With "ap" the blank lines after it too (or before it, if there are none after it)
    private (int First, int Last) ParagraphLines(bool around)
    {
        bool Blank(int line) => Buffer[line].Trim().Length == 0;
        var blank = Blank(Cursor.Line);
        var first = Cursor.Line;
        var last = Cursor.Line;
        while (first > 0 && Blank(first - 1) == blank) first--;
        while (last < Buffer.LineCount - 1 && Blank(last + 1) == blank) last++;
        if (!around) return (first, last);
        if (last < Buffer.LineCount - 1)
        {
            // The lines of the other kind after it
            var other = !blank;
            while (last < Buffer.LineCount - 1 && Blank(last + 1) == other) last++;
        }
        else
        {
            var other = !blank;
            while (first > 0 && Blank(first - 1) == other) first--;
        }

        return (first, last);
    }

    #endregion

    #region Visual mode commands

    private bool HandleVisualCommand(char cmd, int count)
    {
        if (Mode == EditorMode.VisualBlock)
        {
            switch (cmd)
            {
                case 'I':
                    StartBlockInsert(false);
                    return true;
                case 'A':
                    StartBlockInsert(true);
                    return true;
                case 'O':
                    BlockOtherCorner();
                    return true;
                case 'D':
                case 'X':
                    ApplyOperatorToBlock('d', true);
                    return true;
                case 'C':
                    ApplyOperatorToBlock('c', true);
                    return true;
                case 'Y':
                    ApplyOperatorToBlock('y', true);
                    return true;
                case 'p':
                case 'P':
                    PutOverBlock();
                    return true;
            }
        }

        switch (cmd)
        {
            case 'v':
                Mode = Mode == EditorMode.Visual ? EditorMode.Normal : EditorMode.Visual;
                return true;
            case 'V':
                Mode = Mode == EditorMode.VisualLine ? EditorMode.Normal : EditorMode.VisualLine;
                return true;
            case 'Q':
                Mode = Mode == EditorMode.VisualBlock ? EditorMode.Normal : EditorMode.VisualBlock;
                _blockToEnd = false;
                return true;
            case 'o':
                // Move the cursor to the other end of the selection
                (_visualAnchor, Cursor) = (Cursor, _visualAnchor);
                return true;
            case 'x':
                ApplyOperatorToSelection('d', false);
                return true;
            case 's':
                ApplyOperatorToSelection('c', false);
                return true;
            case 'X':
            case 'D':
                ApplyOperatorToSelection('d', true);
                return true;
            case 'Y':
                ApplyOperatorToSelection('y', true);
                return true;
            case 'C':
            case 'S':
            case 'R':
                ApplyOperatorToSelection('c', true);
                return true;
            case 'u':
            case 'U':
            case '~':
                ApplyOperatorToSelection(cmd, false);
                return true;
            case 'J' when Mode == EditorMode.VisualLine:
                MoveSelectedLines(count);
                return true;
            case 'K' when Mode == EditorMode.VisualLine:
                MoveSelectedLines(-count);
                return true;
            case 'J':
            {
                var first = Math.Min(_visualAnchor.Line, Cursor.Line);
                var last = Math.Max(_visualAnchor.Line, Cursor.Line);
                Mode = EditorMode.Normal;
                Cursor = new TextPos(first, 0);
                JoinLines(last - first + 1);
                return true;
            }
            case 'p':
            case 'P':
            {
                // Replace the selection with the register
                var text = GetRegister(out _).TrimEnd(TextBuffer.NewLine);
                var from = TextPos.Min(_visualAnchor, Cursor);
                var to = TextPos.Max(_visualAnchor, Cursor);
                if (Mode == EditorMode.VisualLine)
                {
                    from = new TextPos(from.Line, 0);
                    to = to with { Col = Buffer.LineLength(to.Line) };
                }
                else if (to.Col < Buffer.LineLength(to.Line))
                {
                    to = to with { Col = to.Col + TextMetrics.ElementLength(Buffer[to.Line], to.Col) };
                }

                Mode = EditorMode.Normal;
                Delete(from, to);
                Cursor = Left(Insert(from, text), 1);
                return true;
            }
            case 'i':
            case 'a':
            case 'I':
            case 'A':
            case '.':
                return true; // Not available in Visual mode
            default:
                return false;
        }
    }

    private Selected? CurrentSelection()
    {
        return IsVisual ? new Selected(_visualAnchor, Cursor, Mode, _blockToEnd) : null;
    }

    // "gv": select again what was selected when Visual mode was last left. In Visual mode the selection
    // shown and the last one change places
    private void ReselectLast()
    {
        if (_lastSelection == null)
        {
            Message = Lang.T("No selection yet");
            return;
        }

        var current = CurrentSelection();
        var last = _lastSelection;
        // The text may have changed since: the selection is kept within it
        _visualAnchor = Buffer.Clamp(last.Anchor);
        Cursor = Buffer.Clamp(last.Cursor);
        Mode = last.Mode;
        _blockToEnd = last.ToEnd;
        if (current != null) _lastSelection = current;
    }

    // Move the selected lines down (or up for a negative number of lines), keeping them selected
    private void MoveSelectedLines(int lines)
    {
        for (var i = 0; i < Math.Abs(lines); i++)
        {
            var first = Math.Min(_visualAnchor.Line, Cursor.Line);
            var last = Math.Max(_visualAnchor.Line, Cursor.Line);
            if (lines > 0)
            {
                // The line below the selection is moved above it
                if (last >= Buffer.LineCount - 1) break;
                var below = Buffer[last + 1];
                Delete(new TextPos(last, Buffer.LineLength(last)), new TextPos(last + 1, below.Length));
                Insert(new TextPos(first, 0), below + TextBuffer.NewLine);
                _visualAnchor = _visualAnchor with { Line = _visualAnchor.Line + 1 };
                Cursor = Cursor with { Line = Cursor.Line + 1 };
            }
            else
            {
                // The line above the selection is moved below it
                if (first == 0) break;
                var above = Buffer[first - 1];
                Delete(new TextPos(first - 1, 0), new TextPos(first, 0));
                Insert(new TextPos(last - 1, Buffer.LineLength(last - 1)), TextBuffer.NewLine + above);
                _visualAnchor = _visualAnchor with { Line = _visualAnchor.Line - 1 };
                Cursor = Cursor with { Line = Cursor.Line - 1 };
            }
        }
    }

    #endregion

    #region Normal mode commands

    private void HandleCommand(char cmd, int? count)
    {
        var n = count ?? 1;
        var line = CurrentLine;
        switch (cmd)
        {
            // Entering Insert mode
            case 'i':
                EnterInsert();
                break;
            case 'a':
                if (line.Length > 0)
                {
                    Cursor = Cursor with { Col = Cursor.Col + TextMetrics.ElementLength(line, Cursor.Col) };
                }

                EnterInsert();
                break;
            case 'I':
                Cursor = Cursor with { Col = FirstNonBlank(Cursor.Line) };
                EnterInsert();
                break;
            case 'A':
                Cursor = Cursor with { Col = line.Length };
                EnterInsert();
                break;
            case 'o':
                Cursor = Insert(Cursor with { Col = line.Length }, TextBuffer.NewLine.ToString());
                EnterInsert();
                break;
            case 'O':
                Insert(Cursor with { Col = 0 }, TextBuffer.NewLine.ToString());
                Cursor = Cursor with { Col = 0 };
                EnterInsert();
                break;

            // Shorthands for an operator and a motion
            case 'x':
                OperatorWithMotion('d', 'l', n);
                break;
            case 'X':
                OperatorWithMotion('d', 'h', n);
                break;
            case 'D':
                OperatorWithMotion('d', '$', n);
                break;
            case 'C':
                OperatorWithMotion('c', '$', n);
                break;
            case 's':
                OperatorWithMotion('c', 'l', n);
                break;
            case 'S':
                ApplyOperatorToLines('c', Cursor.Line, Math.Min(Buffer.LineCount - 1, Cursor.Line + n - 1));
                break;
            case 'Y':
                ApplyOperatorToLines('y', Cursor.Line, Math.Min(Buffer.LineCount - 1, Cursor.Line + n - 1));
                break;

            // Editing
            case 'p':
                Put(true, n);
                break;
            case 'P':
                Put(false, n);
                break;
            case 'J':
                JoinLines(Math.Max(2, n));
                break;
            case '~':
            {
                var to = Right(Cursor, n, true);
                ChangeCase('~', Cursor, to);
                Cursor = to;
                break;
            }
            case 'u':
                Undo(n);
                break;
            case 'U':
            case CtrlR:
                Redo(n);
                break;
            case '.':
                Repeat(n);
                break;

            // Visual mode
            case 'v':
                _visualAnchor = Cursor;
                Mode = EditorMode.Visual;
                break;
            case 'V':
                _visualAnchor = Cursor;
                Mode = EditorMode.VisualLine;
                break;
            case 'Q':
                StartVisual(EditorMode.VisualBlock);
                break;

            // Command line and search
            case ':':
                if (IsVisual)
                {
                    // The command will work on the selected lines
                    _selectionFirstLine = Math.Min(_visualAnchor.Line, Cursor.Line);
                    _selectionLastLine = Math.Max(_visualAnchor.Line, Cursor.Line);
                    // The selection itself, for ":dup"
                    _commandSelectionMode = Mode;
                    _commandSelectionFrom = _visualAnchor < Cursor ? _visualAnchor : Cursor;
                    _commandSelectionTo = _visualAnchor < Cursor ? Cursor : _visualAnchor;
                    if (Mode == EditorMode.VisualBlock) _commandBlock = BlockBounds();
                    // Text selected within a line (a word) can be used by the command
                    _selectionText = string.Empty;
                    if (Mode == EditorMode.Visual && _visualAnchor.Line == Cursor.Line && CurrentLine.Length > 0)
                    {
                        var first = Math.Min(_visualAnchor.Col, Cursor.Col);
                        var last = Math.Min(Math.Max(_visualAnchor.Col, Cursor.Col), CurrentLine.Length - 1);
                        _selectionText = CurrentLine.Substring(first,
                            last + TextMetrics.ElementLength(CurrentLine, last) - first);
                    }

                    Mode = EditorMode.Normal;
                    OpenCommandLine(':');
                    CommandText = "'<,'>";
                }
                else
                {
                    _selectionText = string.Empty;
                    _commandSelectionMode = EditorMode.Normal;
                    OpenCommandLine(':');
                }

                break;
            case '/':
            case '?':
                OpenCommandLine(cmd);
                break;
            case '*':
            case '#':
                if (TextObject('w', false, out var from, out var end) && !char.IsWhiteSpace(line[from.Col]))
                {
                    if (IsVisual) Mode = EditorMode.Normal;
                    Cursor = from;
                    // Search for the whole word
                    var word = Buffer.GetText(from, end);
                    var boundary = char.IsLetterOrDigit(word[0]) || word[0] == '_' ? @"\b" : string.Empty;
                    StartSearch(boundary + Regex.Escape(word) + boundary, cmd == '*');
                }

                break;

            // Registers
            case CtrlN:
                Tools.NextLineMemoryIndex();
                _useSmallRegister = false;
                ShowRegister();
                break;
            case CtrlP:
                Tools.PreviousLineMemoryIndex();
                _useSmallRegister = false;
                ShowRegister();
                break;

            // Scrolling
            case CtrlF:
                ScrollRows(Height * n);
                break;
            case CtrlB:
                ScrollRows(-Height * n);
                break;
            case CtrlD:
                ScrollRows(Math.Max(1, Height / 2));
                break;
            case CtrlU:
                ScrollRows(-Math.Max(1, Height / 2));
                break;
            case CtrlE:
            case CtrlY:
            {
                // Scroll the view one row and only move the cursor if it would leave the view
                ScrollView(cmd == CtrlE ? n : -n);
                var row = Layout.RowOf(Cursor);
                if (row < TopRow) MoveRows(TopRow - row);
                if (row >= TopRow + Height) MoveRows(TopRow + Height - 1 - row);
                break;
            }
        }
    }

    private void OperatorWithMotion(char op, char motionKey, int count)
    {
        if (TryMotion(motionKey, count, true, out var motion)) ApplyOperator(op, motion);
    }

    // The "r" command: replace characters with another
    private void ReplaceChars(string replacement, int count)
    {
        var line = CurrentLine;
        var col = Cursor.Col;
        var replaced = 0;
        while (replaced < count && col < line.Length)
        {
            col += TextMetrics.ElementLength(line, col);
            replaced++;
        }

        if (replaced < count) return; // Not enough characters on the line
        var to = Cursor with { Col = col };
        Delete(Cursor, to);
        Cursor = Left(Insert(Cursor, string.Concat(Enumerable.Repeat(replacement, count))), 1);
    }

    // The "J" command: join lines, with a single blank space between them
    private void JoinLines(int lineCount)
    {
        for (var i = 1; i < lineCount && Cursor.Line < Buffer.LineCount - 1; i++)
        {
            var line = CurrentLine;
            var next = Buffer[Cursor.Line + 1];
            var joinAt = Cursor with { Col = line.Length };
            var nextStart = FirstNonBlank(Cursor.Line + 1);
            Delete(joinAt, new TextPos(Cursor.Line + 1, nextStart));
            if (line.Length > 0 && !char.IsWhiteSpace(line[^1]) && nextStart < next.Length)
            {
                Insert(joinAt, " ");
            }

            Cursor = joinAt;
        }
    }

    private void Undo(int count)
    {
        Buffer.EndStep(Cursor);
        _doNotRecord = true;
        while (count-- > 0)
        {
            if (Buffer.Undo(out var cursor))
            {
                Cursor = cursor;
            }
            else
            {
                Message = Lang.T("Already at oldest change");
                break;
            }
        }
    }

    private void Redo(int count)
    {
        Buffer.EndStep(Cursor);
        _doNotRecord = true;
        while (count-- > 0)
        {
            if (Buffer.Redo(out var cursor))
            {
                Cursor = cursor;
            }
            else
            {
                Message = Lang.T("Already at newest change");
                break;
            }
        }
    }

    // The "." command: repeat the last change by replaying its keys
    private void Repeat(int count)
    {
        if (_replaying || _lastChange.Length == 0) return;
        ResetPending();
        Buffer.EndStep(Cursor);
        var keys = _lastChange;
        _replaying = true;
        try
        {
            while (count-- > 0)
            {
                foreach (var key in keys)
                {
                    HandleKey(key);
                }
            }
        }
        finally
        {
            _replaying = false;
            _doNotRecord = true;
            _request = EditorRequest.None;
        }
    }

    #endregion

    #region Registers

    // Yanked and deleted text is kept in the string memory shared by all editors in the session, where the
    // current string acts as the register used by "p". Text ending with a newline is whole lines.
    private string _smallRegister = string.Empty; // The last text deleted within a line
    private bool _useSmallRegister;

    // Text was yanked: it also goes to the clipboard when every yank does, or when "+ was typed before it. The
    // host sends it on (the system clipboard and/or the paste bin, as set)
    private void Yanked(string text)
    {
        var asked = _clipboardAsked;
        _clipboardAsked = false;
        if (text.Length == 0 || SingleLine || !(ClipboardEveryYank || asked)) return;
        ClipboardText = text;
        _request = EditorRequest.Clipboard;
    }

    private void StoreRegister(string text, bool linewise, bool small)
    {
        if (text.Length == 0) return;
        if (linewise && !text.EndsWith(TextBuffer.NewLine)) text += TextBuffer.NewLine;
        _useSmallRegister = small;
        if (small)
        {
            _smallRegister = text;
        }
        else
        {
            Tools.StoreInLineMemory(text);
        }
    }

    private string GetRegister(out bool linewise)
    {
        var text = _useSmallRegister ? _smallRegister : Tools.GetCurrentLineFromLineMemory();
        linewise = !SingleLine && text.EndsWith(TextBuffer.NewLine);
        return text;
    }

    // The most characters "p" and "P" put in at once (the register times the count): more would take the memory
    private const long MaxPutLength = 10_000_000;

    // The "p" and "P" commands: insert the register after or before the cursor
    private void Put(bool after, int count)
    {
        var text = GetRegister(out var linewise);
        if (text.Length == 0) return;
        if ((long)text.Length * count > MaxPutLength)
        {
            SetError(Lang.T("Too much text to put in at once (use a smaller count)"));
            return;
        }

        if (RegisterIsBlock(text))
        {
            PutBlock(after, count);
            return;
        }

        if (count > 1) text = new StringBuilder().Insert(0, text, count).ToString();
        if (linewise)
        {
            var line = Cursor.Line;
            if (!after)
            {
                Insert(new TextPos(line, 0), text);
            }
            else if (line == Buffer.LineCount - 1)
            {
                Insert(new TextPos(line, Buffer.LineLength(line)), TextBuffer.NewLine + text[..^1]);
                line++;
            }
            else
            {
                line++;
                Insert(new TextPos(line, 0), text);
            }

            Cursor = new TextPos(line, FirstNonBlank(line));
        }
        else
        {
            var at = Cursor;
            if (after && CurrentLine.Length > 0)
            {
                at = at with { Col = at.Col + TextMetrics.ElementLength(CurrentLine, at.Col) };
            }

            var end = Insert(at, text);
            Cursor = text.Contains(TextBuffer.NewLine) ? at : Left(end, 1);
        }
    }

    /// <summary>
    /// Insert a string of the string memory as "p" does (whole lines below the line of the cursor), and make
    /// it the current string. One change
    /// </summary>
    internal void PutFromMemory(int index)
    {
        if (SingleLine) return;
        ResetPending();
        Mode = EditorMode.Normal;
        Tools.SetLineMemoryIndex(index);
        _useSmallRegister = false;
        if (Buffer.IsStepOpen) Buffer.EndStep(Cursor);
        _stepCursor = Cursor;
        Put(true, 1);
        Buffer.EndStep(Cursor);
        ClampCursor();
        EnsureCursorVisible();
    }

    // Insert all strings in the string memory as lines below the cursor
    private void PutAllRegisters()
    {
        var all = Tools.GetAllLinesFromLineMemory();
        if (all.Count == 0) return;
        var builder = new StringBuilder();
        foreach (var text in all)
        {
            builder.Append(text.TrimEnd(TextBuffer.NewLine)).Append(TextBuffer.NewLine);
        }

        var line = Cursor.Line;
        Insert(new TextPos(line, Buffer.LineLength(line)), TextBuffer.NewLine + builder.ToString()[..^1]);
        Cursor = new TextPos(line + 1, 0);
    }

    private void ShowRegister()
    {
        if (!Tools.HasLineMemory())
        {
            Message = Lang.T("String memory is empty");
            return;
        }

        var text = Tools.GetCurrentLineFromLineMemory();
        var preview = Tools.TruncateString(text.TrimEnd(TextBuffer.NewLine).Replace(TextBuffer.NewLine, ' '), 40, true);
        Message = text.EndsWith(TextBuffer.NewLine)
            ? Lang.F("String {0} (lines): {1}", Tools.GetLineMemoryPosition(), preview)
            : Lang.F("String {0}: {1}", Tools.GetLineMemoryPosition(), preview);
    }

    #endregion
}
