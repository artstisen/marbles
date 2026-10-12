using System.Text;

namespace Marbles;

// Visual Block mode: a rectangle of text, the same columns (cells on screen) on each of a range of lines.
// Started with "Q" or ":block". The columns are counted as shown on screen, so wide characters line up the way
// they look; a character that is partly inside the block is taken whole.
internal sealed partial class EditorEngine
{
    private bool _blockToEnd; // "$" was pressed: each line is selected to its end
    private BlockInsert? _blockInsert; // Text typed on the first line of a block is repeated on the others

    // The text typed in Insert mode after "I", "A" or "c" on a block: where it goes on the other lines
    private sealed record BlockInsert(int FirstLine, int LastLine, int Column, bool Append, bool ToEnd,
        int InsertCol, int LengthBefore);

    // The last block yanked or deleted. When the string memory holds it, "p" puts it as a block. It is shared
    // by all editors, like the string memory
    private static string _blockRegister = string.Empty;

    // Start Visual, Visual Line or Visual Block mode at the cursor
    private void StartVisual(EditorMode mode)
    {
        if (SingleLine) return;
        _visualAnchor = Cursor;
        _blockToEnd = false;
        Mode = mode;
    }

    // The column (cell on screen, counted from 0) where a char of a line starts
    private int ColumnOf(int line, int col)
    {
        var s = Buffer[line];
        return TextMetrics.WidthOfRange(s, 0, Math.Min(col, s.Length));
    }

    // The number of cells of the character at a position (1 at the end of a line)
    private int CellsAt(TextPos pos)
    {
        var s = Buffer[pos.Line];
        if (pos.Col >= s.Length) return 1;
        return Math.Max(1, TextMetrics.WidthOfRange(s, pos.Col, pos.Col + TextMetrics.ElementLength(s, pos.Col)));
    }

    // The lines of the block, and its columns: from Left and up to (not including) Right (int.MaxValue after "$")
    private (int First, int Last, int Left, int Right) BlockBounds()
    {
        var anchor = ColumnOf(_visualAnchor.Line, _visualAnchor.Col);
        var cursor = ColumnOf(Cursor.Line, Cursor.Col);
        var left = Math.Min(anchor, cursor);
        var right = _blockToEnd
            ? int.MaxValue
            : Math.Max(anchor + CellsAt(_visualAnchor), cursor + CellsAt(Cursor));
        return (Math.Min(_visualAnchor.Line, Cursor.Line), Math.Max(_visualAnchor.Line, Cursor.Line), left, right);
    }

    // The chars of a line that are within columns (start and end index). A line ending before the left column
    // gives an empty range at its end
    private (int Start, int End) BlockRange(int line, int left, int right)
    {
        var s = Buffer[line];
        var col = 0;
        var cell = 0;
        var start = -1;
        while (col < s.Length && cell < right)
        {
            var length = TextMetrics.ElementLength(s, col);
            var width = TextMetrics.WidthOfRange(s, col, col + length);
            if (start < 0 && cell + Math.Max(width, 1) > left) start = col;
            cell += width;
            col += length;
        }

        return start < 0 ? (s.Length, s.Length) : (start, col);
    }

    // The selected part of a line in Visual Block mode
    private (int Start, int End)? BlockSelection(int line)
    {
        var (first, last, left, right) = BlockBounds();
        if (line < first || line > last) return null;
        var (start, end) = BlockRange(line, left, right);
        return start < end ? (start, end) : null;
    }

    // The char index where a column starts on a line. A shorter line gets blank spaces up to the column
    // if "pad" is set, or else -1 is returned
    private int ColAtColumn(int line, int column, bool pad)
    {
        var width = TextMetrics.WidthOf(Buffer[line]);
        if (width >= column) return BlockRange(line, column, int.MaxValue).Start;
        if (!pad) return -1;
        Insert(new TextPos(line, Buffer.LineLength(line)), new string(' ', column - width));
        return Buffer.LineLength(line);
    }

    private List<string> BlockText(int first, int last, int left, int right)
    {
        var lines = new List<string>();
        for (var line = first; line <= last; line++)
        {
            var (start, end) = BlockRange(line, left, right);
            lines.Add(Buffer[line][start..end]);
        }

        return lines;
    }

    // Keep the text of a block in the string memory, where "p" will put it as a block
    private void StoreBlock(List<string> lines)
    {
        var text = string.Join(TextBuffer.NewLine, lines);
        if (text.Replace(TextBuffer.NewLine.ToString(), string.Empty).Length == 0) return;
        _blockRegister = text;
        StoreRegister(text, false, false);
    }

    private bool RegisterIsBlock(string text)
    {
        return !_useSmallRegister && _blockRegister.Length > 0 && text == _blockRegister;
    }

    private void DeleteBlock(int first, int last, int left, int right)
    {
        for (var line = first; line <= last; line++)
        {
            var (start, end) = BlockRange(line, left, right);
            if (start < end) Delete(new TextPos(line, start), new TextPos(line, end));
        }
    }

    // An operator on the block: "d", "c", "y", the case changes, and ">" "<" on its lines
    // ("toEnd": the block goes to the end of each line, as for "D", "C" and "Y")
    private void ApplyOperatorToBlock(char op, bool toEnd)
    {
        var (first, last, left, right) = BlockBounds();
        if (toEnd) right = int.MaxValue;
        Mode = EditorMode.Normal;
        _blockToEnd = false;
        switch (op)
        {
            case '>':
            case '<':
                ApplyOperatorToLines(op, first, last);
                return;
            case 'y':
                StoreBlock(BlockText(first, last, left, right));
                Yanked(string.Join(TextBuffer.NewLine, BlockText(first, last, left, right)));
                break;
            case 'd':
            case 'c':
                StoreBlock(BlockText(first, last, left, right));
                DeleteBlock(first, last, left, right);
                break;
            case 'u':
            case 'U':
            case '~':
                for (var line = first; line <= last; line++)
                {
                    var (start, end) = BlockRange(line, left, right);
                    if (start < end) ChangeCase(op, new TextPos(line, start), new TextPos(line, end));
                }

                break;
        }

        if (op == 'c')
        {
            BeginBlockInsert(first, last, left, false, false);
            return;
        }

        Cursor = new TextPos(first, BlockRange(first, left, int.MaxValue).Start);
    }

    // "I" and "A": type text in front of or after the block, on each of its lines
    private void StartBlockInsert(bool append)
    {
        var (first, last, left, right) = BlockBounds();
        Mode = EditorMode.Normal;
        var toEnd = append && right == int.MaxValue;
        _blockToEnd = false;
        BeginBlockInsert(first, last, append ? right : left, append, toEnd);
    }

    private void BeginBlockInsert(int first, int last, int column, bool append, bool toEnd)
    {
        // The text is typed on the first line (which gets blank spaces up to the column if it is shorter)
        var col = toEnd ? Buffer.LineLength(first) : ColAtColumn(first, column, true);
        Cursor = new TextPos(first, col);
        _blockInsert = new BlockInsert(first, last, column, append, toEnd, col, Buffer.LineLength(first));
        EnterInsert();
    }

    // When Insert mode is left: put the text typed on the first line on the other lines of the block
    private void FinishBlockInsert()
    {
        var block = _blockInsert;
        _blockInsert = null;
        // Only text typed on the first line, without a line break, is repeated
        if (block == null || Cursor.Line != block.FirstLine) return;
        var added = Buffer.LineLength(block.FirstLine) - block.LengthBefore;
        if (added <= 0 || block.InsertCol + added > Buffer.LineLength(block.FirstLine)) return;
        var text = Buffer[block.FirstLine].Substring(block.InsertCol, added);
        for (var line = block.FirstLine + 1; line <= block.LastLine && line < Buffer.LineCount; line++)
        {
            int col;
            if (block.ToEnd)
            {
                col = Buffer.LineLength(line);
            }
            else
            {
                // "I": a line ending before the block is left alone. "A": it gets blank spaces up to the block
                col = ColAtColumn(line, block.Column, block.Append);
                if (col < 0) continue;
            }

            Insert(new TextPos(line, col), text);
        }
    }

    // "r": replace each character of the block (a wide character with as many as the cells it takes)
    private void ReplaceBlock(char replacement)
    {
        var (first, last, left, right) = BlockBounds();
        Mode = EditorMode.Normal;
        _blockToEnd = false;
        for (var line = first; line <= last; line++)
        {
            var (start, end) = BlockRange(line, left, right);
            if (start >= end) continue;
            var cells = TextMetrics.WidthOfRange(Buffer[line], start, end);
            Delete(new TextPos(line, start), new TextPos(line, end));
            Insert(new TextPos(line, start), new string(replacement, Math.Max(1, cells)));
        }

        Cursor = new TextPos(first, BlockRange(first, left, int.MaxValue).Start);
    }

    // "O": the cursor goes to the other corner of the block on the same line
    private void BlockOtherCorner()
    {
        var anchor = ColumnOf(_visualAnchor.Line, _visualAnchor.Col);
        var cursor = ColumnOf(Cursor.Line, Cursor.Col);
        _visualAnchor = _visualAnchor with { Col = BlockRange(_visualAnchor.Line, cursor, int.MaxValue).Start };
        Cursor = Cursor with { Col = BlockRange(Cursor.Line, anchor, int.MaxValue).Start };
        _blockToEnd = false;
    }

    // "p" with a block selected: the block is replaced with the string memory
    private void PutOverBlock()
    {
        var text = GetRegister(out var linewise);
        var isBlock = RegisterIsBlock(text);
        var (first, last, left, right) = BlockBounds();
        Mode = EditorMode.Normal;
        _blockToEnd = false;
        DeleteBlock(first, last, left, right);
        Cursor = new TextPos(first, ColAtColumn(first, left, true));
        if (text.Length == 0) return;
        if (isBlock)
        {
            PutBlock(false, 1);
        }
        else
        {
            var plain = linewise ? text.TrimEnd(TextBuffer.NewLine) : text;
            Cursor = Left(Insert(Cursor, plain), 1);
        }
    }

    // "p" and "P" of a block: its lines go in at the same column on the lines from the cursor down (new lines are
    // added at the end of the text if needed). Text to the right of the block stays in line
    private void PutBlock(bool after, int count)
    {
        var lines = _blockRegister.Split(TextBuffer.NewLine);
        var width = lines.Max(TextMetrics.WidthOf);
        var column = ColumnOf(Cursor.Line, Cursor.Col) +
                     (after && CurrentLine.Length > 0 ? CellsAt(Cursor) : 0);
        var top = Cursor.Line;
        var topCol = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = top + i;
            if (line >= Buffer.LineCount) Insert(Buffer.End, TextBuffer.NewLine.ToString());
            if (lines[i].Length == 0 && TextMetrics.WidthOf(Buffer[line]) <= column)
            {
                // Nothing to put on a line that ends before the column: it is left as it is
                if (i == 0) topCol = Buffer.LineLength(line);
                continue;
            }

            var padded = lines[i] + new string(' ', width - TextMetrics.WidthOf(lines[i]));
            var col = ColAtColumn(line, column, true);
            var atEnd = col >= Buffer.LineLength(line);
            var piece = new StringBuilder();
            for (var n = 1; n < count; n++) piece.Append(padded);
            // No blank spaces are added at the end of a line
            piece.Append(atEnd ? lines[i] : padded);
            if (piece.Length > 0) Insert(new TextPos(line, col), piece.ToString());
            if (i == 0) topCol = col;
        }

        Cursor = new TextPos(top, topCol);
    }
}
