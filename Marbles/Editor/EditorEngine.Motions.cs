using System.Globalization;

namespace Marbles;

// Cursor movement: the motions used on their own in Normal and Visual mode, and as the target of an
// operator (like the "w" in "dw").
internal sealed partial class EditorEngine
{
    /// <summary>
    /// The result of a motion: where it leads, and how an operator must treat the text it passes over
    /// </summary>
    /// <param name="Target">Where the cursor ends</param>
    /// <param name="Linewise">The motion covers whole lines</param>
    /// <param name="Inclusive">The character at the target is part of the text passed over</param>
    private readonly record struct Motion(TextPos Target, bool Linewise = false, bool Inclusive = false);

    private char _lastFindCommand; // The last of the "f", "F", "t" and "T" commands used
    private string _lastFindChar = string.Empty; // One character: two chars for an emoji

    #region Basic movement

    private TextPos Left(TextPos pos, int count)
    {
        var line = Buffer[pos.Line];
        var col = pos.Col;
        while (count-- > 0 && col > 0)
        {
            col -= TextMetrics.PreviousElementLength(line, col);
        }

        return pos with { Col = col };
    }

    private TextPos Right(TextPos pos, int count, bool allowEndOfLine)
    {
        var line = Buffer[pos.Line];
        var col = pos.Col;
        while (count-- > 0 && col < line.Length)
        {
            var next = col + TextMetrics.ElementLength(line, col);
            if (next >= line.Length && !allowEndOfLine) break;
            col = next;
        }

        return pos with { Col = col };
    }

    // Move the cursor a number of rows up or down as they are shown on screen (a wrapped line is several rows)
    private void MoveRows(int rows)
    {
        if (_desiredCell < 0) _desiredCell = Layout.CellOf(Cursor);
        var row = Math.Clamp(Layout.RowOf(Cursor) + rows, 0, Layout.TotalRows() - 1);
        var (line, segment) = Layout.LineAtRow(row);
        Cursor = new TextPos(line, Layout.ColAtCell(line, segment, _desiredCell, Mode == EditorMode.Insert));
        _keepDesiredCell = true;
    }

    // Scroll the view a number of rows and move the cursor along
    private void ScrollRows(int rows)
    {
        var maxTop = Math.Max(0, Layout.TotalRows() - Height);
        var newTop = Math.Clamp(TopRow + rows, 0, maxTop);
        var moved = newTop - TopRow;
        TopRow = newTop;
        // At the top or bottom of the document only the cursor moves
        MoveRows(moved != 0 ? moved : rows);
    }

    #endregion

    #region Words

    // 0 = blank space, 1 = word character, 2 = other characters (punctuation). The chars of a character made of
    // more than one (an emoji with a variation selector, a letter with an accent) all have the class of the first,
    // so a word motion never stops inside it
    private static int CharClass(string line, int col, bool bigWord)
    {
        var c = line[col];
        // Looking back for the start of the character only for a char that can continue one: this is done for
        // each char a word motion passes, and on a long line without ASCII the start can be far back
        if (col > 0 && (char.IsLowSurrogate(c) || c is >= '\uFE00' and <= '\uFE0F' or '\u200D' ||
                        char.GetUnicodeCategory(c) is UnicodeCategory.NonSpacingMark
                            or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark))
        {
            c = line[TextMetrics.ElementStart(line, col)];
        }

        if (char.IsWhiteSpace(c)) return 0;
        if (bigWord) return 1;
        if (char.IsLetterOrDigit(c) || c == '_' || char.IsSurrogate(c)) return 1;
        return char.GetUnicodeCategory(c) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.EnclosingMark or UnicodeCategory.Format
            ? 1
            : 2;
    }

    // The start of the next word
    private TextPos WordForward(TextPos pos, bool bigWord)
    {
        var lineIndex = pos.Line;
        var col = pos.Col;
        var line = Buffer[lineIndex];
        if (col < line.Length)
        {
            var charClass = CharClass(line, col, bigWord);
            if (charClass != 0)
            {
                while (col < line.Length && CharClass(line, col, bigWord) == charClass) col++;
            }
        }

        while (true)
        {
            while (col < line.Length && CharClass(line, col, bigWord) == 0) col++;
            if (col < line.Length) return new TextPos(lineIndex, col);
            if (lineIndex == Buffer.LineCount - 1) return new TextPos(lineIndex, line.Length);
            lineIndex++;
            col = 0;
            line = Buffer[lineIndex];
            if (line.Length == 0) return new TextPos(lineIndex, 0); // An empty line counts as a word
        }
    }

    // The start of the current or previous word
    private TextPos WordBackward(TextPos pos, bool bigWord)
    {
        var lineIndex = pos.Line;
        var col = pos.Col;
        var line = Buffer[lineIndex];
        while (true)
        {
            if (col == 0)
            {
                if (lineIndex == 0) return new TextPos(0, 0);
                lineIndex--;
                line = Buffer[lineIndex];
                col = line.Length;
                if (col == 0) return new TextPos(lineIndex, 0); // An empty line counts as a word
            }

            while (col > 0 && CharClass(line, col - 1, bigWord) == 0) col--;
            if (col == 0) continue;
            var charClass = CharClass(line, col - 1, bigWord);
            while (col > 0 && CharClass(line, col - 1, bigWord) == charClass) col--;
            return new TextPos(lineIndex, col);
        }
    }

    // The last character of the current or next word
    private TextPos WordEnd(TextPos pos, bool bigWord)
    {
        var lineIndex = pos.Line;
        var line = Buffer[lineIndex];
        var col = pos.Col < line.Length ? pos.Col + TextMetrics.ElementLength(line, pos.Col) : pos.Col;
        while (true)
        {
            while (col >= line.Length)
            {
                if (lineIndex == Buffer.LineCount - 1)
                {
                    return new TextPos(lineIndex, Math.Max(0, line.Length - 1));
                }

                lineIndex++;
                line = Buffer[lineIndex];
                col = 0;
            }

            if (CharClass(line, col, bigWord) != 0) break;
            col++;
        }

        var charClass = CharClass(line, col, bigWord);
        while (col + 1 < line.Length && CharClass(line, col + 1, bigWord) == charClass) col++;
        return new TextPos(lineIndex, TextMetrics.ElementStart(line, col));
    }

    #endregion

    #region Paragraphs and find character

    private bool IsBlankLine(int line)
    {
        return string.IsNullOrWhiteSpace(Buffer[line]);
    }

    // The next (or previous) blank line
    private TextPos Paragraph(int line, bool forward)
    {
        var last = Buffer.LineCount - 1;
        if (forward)
        {
            while (line < last && IsBlankLine(line)) line++;
            while (line < last && !IsBlankLine(line)) line++;
            return new TextPos(line, IsBlankLine(line) ? 0 : Buffer.LineLength(line));
        }

        while (line > 0 && IsBlankLine(line)) line--;
        while (line > 0 && !IsBlankLine(line)) line--;
        return new TextPos(line, 0);
    }

    // The "f", "F", "t" and "T" commands: find a character, on the line and the lines after it ("F" and "T": before
    // it), up to the end (the start) of the text. "t" lands on the character before the one found, which is the end of
    // the line above when it is the first of its line, and "T" on the one after it (the start of the line below)
    private bool FindChar(char command, string target, int count, bool repeated, out Motion motion)
    {
        motion = default;
        if (target.Length == 0) return false;
        var forward = command is 'f' or 't';
        var pos = Cursor;
        // Repeating "t" must not stay in front of the same character
        var skipAdjacent = repeated && command is 't' or 'T';
        while (count-- > 0)
        {
            if ((forward ? FindForward(pos, target, skipAdjacent) : FindBackward(pos, target, skipAdjacent))
                is not { } found)
            {
                return false;
            }

            pos = found;
            skipAdjacent = false;
        }

        var line = Buffer[pos.Line];
        switch (command)
        {
            case 't' when pos.Col > 0:
                pos = pos with { Col = pos.Col - TextMetrics.PreviousElementLength(line, pos.Col) };
                break;
            case 't' when pos.Line > 0:
            {
                var above = Buffer[pos.Line - 1];
                pos = new TextPos(pos.Line - 1,
                    above.Length == 0 ? 0 : above.Length - TextMetrics.PreviousElementLength(above, above.Length));
                break;
            }
            case 'T':
                var after = pos.Col + TextMetrics.ElementLength(line, pos.Col);
                pos = after < line.Length || pos.Line + 1 >= Buffer.LineCount
                    ? pos with { Col = after }
                    : new TextPos(pos.Line + 1, 0);
                break;
        }

        // Moving forward the found character is included, moving back the character under the cursor is not
        motion = new Motion(pos, false, forward);
        return true;
    }

    // The next place of a character after a position (skipAdjacent: not the one right after it)
    private TextPos? FindForward(TextPos from, string target, bool skipAdjacent)
    {
        var lineIndex = from.Line;
        var line = Buffer[lineIndex];
        // After the character under the cursor (all of it, when it is more than one char)
        var col = from.Col < line.Length ? from.Col + TextMetrics.ElementLength(line, from.Col) : from.Col + 1;
        if (skipAdjacent)
        {
            if (col < line.Length)
            {
                if (string.CompareOrdinal(line, col, target, 0, target.Length) == 0) col += target.Length;
            }
            else if (lineIndex + 1 < Buffer.LineCount && Buffer[lineIndex + 1].StartsWith(target, StringComparison.Ordinal))
            {
                // (the first of the next line)
                lineIndex++;
                line = Buffer[lineIndex];
                col = target.Length;
            }
        }

        while (true)
        {
            var found = col <= line.Length ? line.IndexOf(target, col, StringComparison.Ordinal) : -1;
            if (found >= 0) return new TextPos(lineIndex, found);
            if (++lineIndex >= Buffer.LineCount) return null;
            line = Buffer[lineIndex];
            col = 0;
        }
    }

    // The place before a position where a character was last (skipAdjacent: not the one right before it)
    private TextPos? FindBackward(TextPos from, string target, bool skipAdjacent)
    {
        var lineIndex = from.Line;
        var line = Buffer[lineIndex];
        var start = from.Col - 1;
        if (skipAdjacent)
        {
            if (from.Col - target.Length >= 0)
            {
                if (string.CompareOrdinal(line, from.Col - target.Length, target, 0, target.Length) == 0)
                {
                    start -= target.Length;
                }
            }
            else if (from.Col == 0 && lineIndex > 0 && Buffer[lineIndex - 1].EndsWith(target, StringComparison.Ordinal))
            {
                // (the last of the line above)
                lineIndex--;
                line = Buffer[lineIndex];
                start = line.Length - target.Length - 1;
            }
        }

        while (true)
        {
            var found = start >= 0 && line.Length > 0
                ? line.LastIndexOf(target, Math.Min(start, line.Length - 1), StringComparison.Ordinal)
                : -1;
            if (found >= 0) return new TextPos(lineIndex, found);
            if (--lineIndex < 0) return null;
            line = Buffer[lineIndex];
            start = line.Length - 1;
        }
    }

    #endregion

    #region Motions by key

    /// <summary>
    /// Resolve a motion key
    /// </summary>
    /// <param name="key">The key (special keys are translated to their Normal mode equivalent)</param>
    /// <param name="count">The count typed before the motion (null if none)</param>
    /// <param name="forOperator">The motion is the target of an operator</param>
    /// <param name="motion">The resolved motion</param>
    /// <returns>False if the key is not a motion or the motion is not possible</returns>
    private bool TryMotion(char key, int? count, bool forOperator, out Motion motion)
    {
        var n = count ?? 1;
        var last = Buffer.LineCount - 1;
        int targetLine;
        motion = default;
        switch (key)
        {
            case 'h':
                motion = new Motion(Left(Cursor, n));
                return true;
            case 'l':
            case ' ':
                motion = new Motion(Right(Cursor, n, forOperator));
                return true;
            case 'j':
            case 'k':
                if (!forOperator) return false; // Without an operator these move by screen rows (see HandleNormal)
                targetLine = Math.Clamp(Cursor.Line + (key == 'j' ? n : -n), 0, last);
                motion = new Motion(new TextPos(targetLine, Cursor.Col), true);
                return true;
            case '+':
            case '\r':
                targetLine = Math.Min(last, Cursor.Line + n);
                motion = new Motion(new TextPos(targetLine, FirstNonBlank(targetLine)), true);
                return true;
            case '-':
                targetLine = Math.Max(0, Cursor.Line - n);
                motion = new Motion(new TextPos(targetLine, FirstNonBlank(targetLine)), true);
                return true;
            case 'w':
            case 'W':
            {
                var pos = Cursor;
                while (n-- > 0) pos = WordForward(pos, key == 'W');
                motion = new Motion(pos);
                return true;
            }
            case 'b':
            case 'B':
            {
                var pos = Cursor;
                while (n-- > 0) pos = WordBackward(pos, key == 'B');
                motion = new Motion(pos);
                return true;
            }
            case 'e':
            case 'E':
            {
                var pos = Cursor;
                while (n-- > 0) pos = WordEnd(pos, key == 'E');
                motion = new Motion(pos, false, true);
                return true;
            }
            case '0':
                motion = new Motion(Cursor with { Col = 0 });
                return true;
            case '^':
                motion = new Motion(Cursor with { Col = FirstNonBlank(Cursor.Line) });
                return true;
            case '_':
                targetLine = Math.Min(last, Cursor.Line + n - 1);
                motion = new Motion(new TextPos(targetLine, FirstNonBlank(targetLine)), true);
                return true;
            case '$':
            {
                targetLine = Math.Min(last, Cursor.Line + n - 1);
                var length = Buffer.LineLength(targetLine);
                var col = length == 0 ? 0 : length - TextMetrics.PreviousElementLength(Buffer[targetLine], length);
                motion = new Motion(new TextPos(targetLine, col), false, true);
                if (!forOperator)
                {
                    // Stay at the end of the lines when moving up and down afterwards
                    _desiredCell = int.MaxValue;
                    _keepDesiredCell = true;
                }

                return true;
            }
            case 'G':
                targetLine = count.HasValue ? Math.Clamp(n - 1, 0, last) : last;
                motion = new Motion(new TextPos(targetLine, FirstNonBlank(targetLine)), true);
                return true;
            case '{':
            case '}':
            {
                var pos = Cursor;
                while (n-- > 0) pos = Paragraph(pos.Line, key == '}');
                motion = new Motion(pos);
                return true;
            }
            case ';':
            case ',':
            {
                if (_lastFindCommand == '\0') return false;
                var command = _lastFindCommand;
                if (key == ',') command = char.IsLower(command) ? char.ToUpper(command) : char.ToLower(command);
                var found = FindChar(command, _lastFindChar, n, true, out motion);
                // (still shown when there is no more to find)
                if (!forOperator && (found || _found != null)) ShowFound(_lastFindChar);
                return found;
            }
            case 'n':
            case 'N':
            {
                if (_lastSearch.Length == 0)
                {
                    SetError(Lang.T("No previous search"));
                    return false;
                }

                var forward = key == 'n' ? _searchForward : !_searchForward;
                var pos = Cursor;
                // One time limit for all the jumps of a count ("99999n")
                var deadline = SearchDeadline();
                while (n-- > 0)
                {
                    if (FindNext(pos, forward, out pos, deadline)) continue;
                    if (!_searchTimedOut) SetError(Lang.T("Pattern not found: ") + _lastSearch);
                    return false;
                }

                HighlightSearch = true;
                motion = new Motion(pos);
                return true;
            }
            default:
                return false;
        }
    }

    #endregion
}
