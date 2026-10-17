using System.Text;

namespace Marbles;

/// <summary>
/// A position in a TextBuffer: a line index and a char index within that line
/// </summary>
internal readonly record struct TextPos(int Line, int Col) : IComparable<TextPos>
{
    public int CompareTo(TextPos other)
    {
        return Line != other.Line ? Line.CompareTo(other.Line) : Col.CompareTo(other.Col);
    }

    public static bool operator <(TextPos a, TextPos b) => a.CompareTo(b) < 0;
    public static bool operator >(TextPos a, TextPos b) => a.CompareTo(b) > 0;
    public static bool operator <=(TextPos a, TextPos b) => a.CompareTo(b) <= 0;
    public static bool operator >=(TextPos a, TextPos b) => a.CompareTo(b) >= 0;
    internal static TextPos Min(TextPos a, TextPos b) => a <= b ? a : b;
    internal static TextPos Max(TextPos a, TextPos b) => a >= b ? a : b;
}

/// <summary>
/// The text of a document kept as a list of lines, with undo/redo.
/// Lines never contain newline characters. An empty document is one empty line.
/// Edits are recorded in steps: everything between BeginStep and EndStep is undone and redone as one.
/// </summary>
internal sealed class TextBuffer
{
    internal const int MaxUndoSteps = 500;
    internal const char NewLine = '\n';

    private readonly List<string> _lines = new();
    private readonly List<UndoStep> _undo = new();
    private readonly List<UndoStep> _redo = new();
    private UndoStep? _openStep;
    private UndoStep? _savedStep; // The top of the undo history when the text was last saved
    private bool _savedStateLost; // True if the saved state can no longer be reached by undoing

    /// <summary>
    /// Raised when lines are changed: (index of first line, number of lines replaced, number of lines replacing them)
    /// </summary>
    internal Action<int, int, int>? LinesReplaced;

    private sealed record Edit(TextPos Start, string Removed, string Inserted);

    private sealed class UndoStep
    {
        internal readonly List<Edit> Edits = new();
        internal TextPos CursorBefore;
        internal TextPos CursorAfter;
    }

    internal TextBuffer(string text)
    {
        _lines.AddRange(Normalize(text).Split(NewLine));
    }

    /// <summary>
    /// All line endings are kept as a single newline character
    /// </summary>
    internal static string Normalize(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        return text.Contains('\r') ? text.Replace("\r\n", "\n").Replace('\r', NewLine) : text;
    }

    #region Reading

    internal int LineCount => _lines.Count;
    internal string this[int line] => _lines[line];
    internal int LineLength(int line) => _lines[line].Length;
    internal TextPos End => new(_lines.Count - 1, _lines[^1].Length);
    internal bool IsEmpty => _lines.Count == 1 && _lines[0].Length == 0;

    /// <summary>
    /// Increases every time the text is changed
    /// </summary>
    internal int Version { get; private set; }

    internal string GetText()
    {
        return string.Join(NewLine, _lines);
    }

    /// <summary>
    /// Get the text from a position and up to (not including) another
    /// </summary>
    internal string GetText(TextPos from, TextPos to)
    {
        if (to <= from) return string.Empty;
        if (from.Line == to.Line) return _lines[from.Line].Substring(from.Col, to.Col - from.Col);
        var builder = new StringBuilder();
        builder.Append(_lines[from.Line], from.Col, _lines[from.Line].Length - from.Col);
        for (var i = from.Line + 1; i < to.Line; i++)
        {
            builder.Append(NewLine).Append(_lines[i]);
        }

        builder.Append(NewLine).Append(_lines[to.Line], 0, to.Col);
        return builder.ToString();
    }

    internal TextPos Clamp(TextPos pos)
    {
        var line = Math.Clamp(pos.Line, 0, _lines.Count - 1);
        return new TextPos(line, Math.Clamp(pos.Col, 0, _lines[line].Length));
    }

    /// <summary>
    /// Convert a position to an index in the complete text (as returned by GetText)
    /// </summary>
    internal int ToIndex(TextPos pos)
    {
        pos = Clamp(pos);
        var index = 0;
        for (var i = 0; i < pos.Line; i++)
        {
            index += _lines[i].Length + 1;
        }

        return index + pos.Col;
    }

    /// <summary>
    /// Convert an index in the complete text to a position
    /// </summary>
    internal TextPos FromIndex(int index)
    {
        if (index <= 0) return new TextPos(0, 0);
        for (var i = 0; i < _lines.Count; i++)
        {
            if (index <= _lines[i].Length) return new TextPos(i, index);
            index -= _lines[i].Length + 1;
        }

        return End;
    }

    /// <summary>
    /// The position just after a text inserted at a position
    /// </summary>
    private static TextPos EndOf(TextPos start, string text)
    {
        var lastNewLine = text.LastIndexOf(NewLine);
        if (lastNewLine < 0) return start with { Col = start.Col + text.Length };
        var lines = 0;
        foreach (var c in text)
        {
            if (c == NewLine) lines++;
        }

        return new TextPos(start.Line + lines, text.Length - lastNewLine - 1);
    }

    #endregion

    #region Editing

    /// <summary>
    /// The text cannot be changed: inserting and deleting do nothing, and ChangeRefused tells that it was tried
    /// </summary>
    internal bool ReadOnly { get; set; }

    /// <summary>
    /// A change was tried while the text is read only (it is forgotten when it is taken)
    /// </summary>
    internal bool TakeChangeRefused()
    {
        var refused = _changeRefused;
        _changeRefused = false;
        return refused;
    }

    private bool _changeRefused;

    /// <summary>
    /// Insert text at a position
    /// </summary>
    /// <returns>The position just after the inserted text</returns>
    internal TextPos Insert(TextPos at, string text)
    {
        if (string.IsNullOrEmpty(text)) return at;
        if (ReadOnly)
        {
            _changeRefused = true;
            return at;
        }

        Record(new Edit(at, string.Empty, text));
        return ApplyInsert(at, text);
    }

    /// <summary>
    /// Delete the text from a position and up to (not including) another
    /// </summary>
    /// <returns>The deleted text</returns>
    internal string Delete(TextPos from, TextPos to)
    {
        if (to <= from) return string.Empty;
        if (ReadOnly)
        {
            _changeRefused = true;
            return string.Empty;
        }

        var removed = GetText(from, to);
        Record(new Edit(from, removed, string.Empty));
        ApplyDelete(from, to);
        return removed;
    }

    /// <summary>
    /// Replace the text from a position and up to (not including) another
    /// </summary>
    /// <returns>The position just after the inserted text</returns>
    internal TextPos Replace(TextPos from, TextPos to, string text)
    {
        Delete(from, to);
        return Insert(from, text);
    }

    private TextPos ApplyInsert(TextPos at, string text)
    {
        Version++;
        var line = _lines[at.Line];
        if (!text.Contains(NewLine))
        {
            _lines[at.Line] = line.Insert(at.Col, text);
            LinesReplaced?.Invoke(at.Line, 1, 1);
            return at with { Col = at.Col + text.Length };
        }

        var parts = text.Split(NewLine);
        var tail = line.Substring(at.Col);
        _lines[at.Line] = string.Concat(line.AsSpan(0, at.Col), parts[0]);
        parts[^1] += tail;
        _lines.InsertRange(at.Line + 1, parts.Skip(1));
        LinesReplaced?.Invoke(at.Line, 1, parts.Length);
        return new TextPos(at.Line + parts.Length - 1, parts[^1].Length - tail.Length);
    }

    private void ApplyDelete(TextPos from, TextPos to)
    {
        Version++;
        _lines[from.Line] = string.Concat(_lines[from.Line].AsSpan(0, from.Col), _lines[to.Line].AsSpan(to.Col));
        if (to.Line > from.Line)
        {
            _lines.RemoveRange(from.Line + 1, to.Line - from.Line);
        }

        LinesReplaced?.Invoke(from.Line, to.Line - from.Line + 1, 1);
    }

    #endregion

    #region Undo/redo

    internal bool IsStepOpen => _openStep != null;

    // While a group is open (a macro plays) the steps are kept as usual, so an undo in it undoes one step; when the
    // group ends, the steps made in it are merged into one
    private int _groupDepth;
    private int _groupStart; // The number of steps there were when the group started

    /// <summary>
    /// Start a group of edits that is undone as one step (all that a macro does)
    /// </summary>
    internal void BeginGroup(TextPos cursor)
    {
        if (_groupDepth++ > 0) return;
        EndStep(cursor);
        _groupStart = _undo.Count;
    }

    /// <summary>
    /// End the group: the steps made in it become one
    /// </summary>
    internal void EndGroup(TextPos cursor)
    {
        if (_groupDepth == 0 || --_groupDepth > 0) return;
        EndStep(cursor);
        var count = _undo.Count - _groupStart;
        if (count < 2) return;
        var merged = new UndoStep { CursorBefore = _undo[_groupStart].CursorBefore, CursorAfter = _undo[^1].CursorAfter };
        foreach (var step in _undo.Skip(_groupStart)) merged.Edits.AddRange(step.Edits);
        _undo.RemoveRange(_groupStart, count);
        _undo.Add(merged);
    }

    /// <summary>
    /// Start collecting edits to be undone as one (does nothing if a step is already open)
    /// </summary>
    /// <param name="cursor">The cursor position to return to when the step is undone</param>
    internal void BeginStep(TextPos cursor)
    {
        _openStep ??= new UndoStep { CursorBefore = cursor };
    }

    /// <summary>
    /// Finish the current step
    /// </summary>
    /// <param name="cursor">The cursor position to return to when the step is redone</param>
    internal void EndStep(TextPos cursor)
    {
        if (_openStep == null) return;
        if (_openStep.Edits.Count > 0)
        {
            _openStep.CursorAfter = cursor;
            AddStep(_openStep);
        }

        _openStep = null;
    }

    private void AddStep(UndoStep step)
    {
        _undo.Add(step);
        if (_undo.Count <= MaxUndoSteps) return;
        // The oldest step is forgotten, and with it the way back to the state before it
        if (_savedStep == null || _savedStep == _undo[0]) _savedStateLost = true;
        _undo.RemoveAt(0);
        if (_groupDepth > 0 && _groupStart > 0) _groupStart--;
    }

    private void Record(Edit edit)
    {
        // Edits made outside a step become a step of their own
        if (_openStep == null)
        {
            AddStep(new UndoStep { CursorBefore = edit.Start, CursorAfter = edit.Start, Edits = { edit } });
        }
        else
        {
            _openStep.Edits.Add(edit);
        }

        // A new edit makes the undone steps unreachable
        if (_redo.Count > 0)
        {
            if (_savedStep != null && _redo.Contains(_savedStep)) _savedStateLost = true;
            _redo.Clear();
        }
    }

    internal bool Undo(out TextPos cursor)
    {
        EndStep(End);
        cursor = default;
        if (_undo.Count == 0) return false;
        // An undo in a group (a macro) can go back past where it started: the group starts there now
        if (_groupDepth > 0 && _undo.Count <= _groupStart) _groupStart = _undo.Count - 1;
        var step = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        for (var i = step.Edits.Count - 1; i >= 0; i--)
        {
            var edit = step.Edits[i];
            if (edit.Inserted.Length > 0) ApplyDelete(edit.Start, EndOf(edit.Start, edit.Inserted));
            if (edit.Removed.Length > 0) ApplyInsert(edit.Start, edit.Removed);
        }

        _redo.Add(step);
        cursor = Clamp(step.CursorBefore);
        return true;
    }

    internal bool Redo(out TextPos cursor)
    {
        EndStep(End);
        cursor = default;
        if (_redo.Count == 0) return false;
        var step = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        foreach (var edit in step.Edits)
        {
            if (edit.Removed.Length > 0) ApplyDelete(edit.Start, EndOf(edit.Start, edit.Removed));
            if (edit.Inserted.Length > 0) ApplyInsert(edit.Start, edit.Inserted);
        }

        _undo.Add(step);
        cursor = Clamp(step.CursorAfter);
        return true;
    }

    /// <summary>
    /// True if the text differs from when it was loaded or last marked as saved
    /// </summary>
    internal bool IsModified
    {
        get
        {
            if (_savedStateLost || _openStep is { Edits.Count: > 0 }) return true;
            return (_undo.Count > 0 ? _undo[^1] : null) != _savedStep;
        }
    }

    internal void MarkModified()
    {
        _savedStateLost = true;
    }

    internal void MarkSaved()
    {
        // Edits made before saving and after saving must not end up in the same step
        if (_openStep != null)
        {
            EndStep(_openStep.CursorBefore);
        }

        _savedStep = _undo.Count > 0 ? _undo[^1] : null;
        _savedStateLost = false;
    }

    /// <summary>
    /// Forget the undo/redo history (the texts it holds are no longer referenced)
    /// </summary>
    internal void ClearHistory()
    {
        if (IsModified) _savedStateLost = true;
        _openStep = null;
        _undo.Clear();
        _redo.Clear();
        _savedStep = null;
    }

    #endregion
}
