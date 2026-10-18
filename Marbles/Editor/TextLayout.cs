namespace Marbles;

/// <summary>
/// Maps the lines of a TextBuffer to the rows shown on screen. A line wider than the available width
/// is wrapped (at a blank space when possible) into several rows, here called segments.
/// The wrapping of each line is cached until the line changes.
/// </summary>
internal sealed class TextLayout
{
    private static readonly int[] SingleSegment = { 0 };
    private readonly TextBuffer _buffer;
    private readonly List<int[]?> _segments = new(); // For each line: the char index where each of its rows start
    private bool _wrap;

    /// <summary>
    /// Lines wider than the width are wrapped into several rows (else each line is one row)
    /// </summary>
    internal bool Wrap
    {
        get => _wrap;
        set
        {
            if (value == _wrap) return;
            _wrap = value;
            for (var i = 0; i < _segments.Count; i++) _segments[i] = null;
        }
    }

    /// <summary>
    /// The number of terminal cells available for text on each row
    /// </summary>
    internal int Width { get; private set; }

    /// <summary>
    /// Change the width: all lines are wrapped again (when they are asked for)
    /// </summary>
    internal void SetWidth(int width)
    {
        width = Math.Max(1, width);
        if (width == Width) return;
        Width = width;
        for (var i = 0; i < _segments.Count; i++) _segments[i] = null;
    }

    internal TextLayout(TextBuffer buffer, int width, bool wrap)
    {
        _buffer = buffer;
        _wrap = wrap;
        Width = Math.Max(1, width);
        _segments.AddRange(new int[]?[buffer.LineCount]);
        buffer.LinesReplaced += LinesReplaced;
    }

    private void LinesReplaced(int first, int removed, int inserted)
    {
        _segments.RemoveRange(first, removed);
        _segments.InsertRange(first, new int[]?[inserted]);
    }

    /// <summary>
    /// The layout is no longer used (a pane of a split that is closed): it stops following the text
    /// </summary>
    internal void Detach()
    {
        _buffer.LinesReplaced -= LinesReplaced;
    }

    /// <summary>
    /// The char index where each row of a line starts (always at least one row, starting at 0)
    /// </summary>
    internal int[] Segments(int line)
    {
        return _segments[line] ??= ComputeSegments(_buffer[line]);
    }

    private int[] ComputeSegments(string s)
    {
        if (!_wrap || s.Length == 0) return SingleSegment;
        List<int>? starts = null;
        var rowStart = 0;
        var cells = 0;
        var breakAt = -1; // The index after the last blank space on the current row
        var i = 0;
        while (i < s.Length)
        {
            var length = TextMetrics.ElementLength(s, i);
            var width = TextMetrics.Width(s, i, length);
            if (cells + width > Width && i > rowStart)
            {
                // Wrap at the last blank space if there is one, else in the middle of the word
                var next = breakAt > rowStart && s[i] != ' ' ? breakAt : i;
                starts ??= new List<int> { 0 };
                starts.Add(next);
                cells = TextMetrics.WidthOfRange(s, next, i);
                rowStart = next;
                breakAt = -1;
            }

            cells += width;
            if (s[i] == ' ') breakAt = i + length;
            i += length;
        }

        return starts == null ? SingleSegment : starts.ToArray();
    }

    internal int RowCount(int line)
    {
        return Segments(line).Length;
    }

    /// <summary>
    /// The total number of rows of the document
    /// </summary>
    internal int TotalRows()
    {
        var rows = 0;
        for (var i = 0; i < _buffer.LineCount; i++)
        {
            rows += Segments(i).Length;
        }

        return rows;
    }

    /// <summary>
    /// The row (counted from the top of the document) where a line starts
    /// </summary>
    internal int RowOfLine(int line)
    {
        var rows = 0;
        for (var i = 0; i < line; i++)
        {
            rows += Segments(i).Length;
        }

        return rows;
    }

    /// <summary>
    /// The row (counted from the top of the document) of a position
    /// </summary>
    internal int RowOf(TextPos pos)
    {
        return RowOfLine(pos.Line) + SegmentOf(pos.Line, pos.Col);
    }

    /// <summary>
    /// The line and segment shown on a row counted from the top of the document (the last row if beyond the end)
    /// </summary>
    internal (int Line, int Segment) LineAtRow(int row)
    {
        row = Math.Max(0, row);
        for (var i = 0; i < _buffer.LineCount; i++)
        {
            var count = Segments(i).Length;
            if (row < count) return (i, row);
            row -= count;
        }

        var last = _buffer.LineCount - 1;
        return (last, Segments(last).Length - 1);
    }

    /// <summary>
    /// The segment of a line that a char index is shown on
    /// </summary>
    internal int SegmentOf(int line, int col)
    {
        var segments = Segments(line);
        var segment = segments.Length - 1;
        while (segment > 0 && segments[segment] > col)
        {
            segment--;
        }

        return segment;
    }

    internal int SegmentStart(int line, int segment)
    {
        return Segments(line)[segment];
    }

    /// <summary>
    /// The char index just after the last char of a segment
    /// </summary>
    internal int SegmentEnd(int line, int segment)
    {
        var segments = Segments(line);
        return segment + 1 < segments.Length ? segments[segment + 1] : _buffer[line].Length;
    }

    /// <summary>
    /// The cell (counted from the start of its row) of a char index
    /// </summary>
    internal int CellOf(TextPos pos)
    {
        var start = Segments(pos.Line)[SegmentOf(pos.Line, pos.Col)];
        return TextMetrics.WidthOfRange(_buffer[pos.Line], start, pos.Col);
    }

    /// <summary>
    /// The char index on a segment that is nearest to (not after) a cell
    /// </summary>
    /// <param name="line">The line</param>
    /// <param name="segment">The segment of the line</param>
    /// <param name="cell">The cell counted from the start of the row</param>
    /// <param name="allowEndOfLine">Allow the position just after the last char of the line</param>
    internal int ColAtCell(int line, int segment, int cell, bool allowEndOfLine)
    {
        var s = _buffer[line];
        var start = SegmentStart(line, segment);
        var end = SegmentEnd(line, segment);
        var col = start;
        var cells = 0;
        while (col < end)
        {
            var length = TextMetrics.ElementLength(s, col);
            var width = TextMetrics.Width(s, col, length);
            if (cells + width > cell) return col;
            // Stay on the last char of the row, unless the position after the end of the line is wanted
            if (col + length >= end && !(allowEndOfLine && end == s.Length)) return col;
            cells += width;
            col += length;
        }

        return col;
    }
}
