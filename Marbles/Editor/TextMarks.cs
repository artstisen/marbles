namespace Marbles;

/// <summary>
/// The marks of a text: places set with "ma" to "mz" (and the place before the last jump, "``"), which follow the
/// changes of the text, as the text is kept by its buffer (also undo and redo). Lines put in above a mark move it
/// down, and lines taken out move it up; a mark in text that is deleted goes to where the deletion was. They are kept
/// in memory while Marbles runs, never in a file (the file browser keeps them for each file while it is closed, and
/// the notes of a data file keep theirs until it is logged out of). This class does not touch the console.
/// </summary>
internal sealed class TextMarks
{
    /// <summary>
    /// The place before the last jump (``)
    /// </summary>
    internal const char Back = '`';

    private readonly Dictionary<char, TextPos> _marks = new();

    /// <summary>
    /// True for the name of a mark that can be set: a to z
    /// </summary>
    internal static bool IsName(char name) => name is >= 'a' and <= 'z';

    /// <summary>
    /// Where a mark is, or null if it is not set
    /// </summary>
    internal TextPos? Get(char name) => _marks.TryGetValue(name, out var at) ? at : null;

    internal void Set(char name, TextPos at) => _marks[name] = at;

    /// <returns>True if the mark was set</returns>
    internal bool Remove(char name) => _marks.Remove(name);

    /// <summary>
    /// The marks a to z that are set, in the order of their names
    /// </summary>
    internal IReadOnlyList<(char Name, TextPos At)> Letters =>
        _marks.Where(mark => IsName(mark.Key)).OrderBy(mark => mark.Key).Select(mark => (mark.Key, mark.Value))
            .ToList();

    /// <summary>
    /// The name of the first mark (a to z) on a line, or '\0' if there is none
    /// </summary>
    internal char NameOnLine(int line)
    {
        var name = '\0';
        foreach (var (key, at) in _marks)
        {
            if (at.Line == line && IsName(key) && (name == '\0' || key < name)) name = key;
        }

        return name;
    }

    /// <summary>
    /// The marks as they are now (to keep them while the file is closed)
    /// </summary>
    internal Dictionary<char, TextPos> Copy() => new(_marks);

    /// <summary>
    /// Set the marks kept for the text (when the file is opened again)
    /// </summary>
    internal void Load(IReadOnlyDictionary<char, TextPos> marks)
    {
        _marks.Clear();
        foreach (var (name, at) in marks) _marks[name] = at;
    }

    /// <summary>
    /// Text was put in at a place, and ends before another
    /// </summary>
    internal void Inserted(TextPos at, TextPos end)
    {
        foreach (var name in _marks.Keys.ToList()) _marks[name] = AfterInsert(_marks[name], at, end);
    }

    /// <summary>
    /// The text from a place up to another was deleted
    /// </summary>
    internal void Deleted(TextPos from, TextPos to)
    {
        foreach (var name in _marks.Keys.ToList()) _marks[name] = AfterDelete(_marks[name], from, to);
    }

    /// <summary>
    /// Where a place is after text was put in (at "at", ending before "end"): a place before it stays, a place on the
    /// same line after it moves along with the rest of the line, and a place on a later line moves down
    /// </summary>
    internal static TextPos AfterInsert(TextPos place, TextPos at, TextPos end)
    {
        if (place < at) return place;
        if (place.Line == at.Line) return new TextPos(end.Line, end.Col + place.Col - at.Col);
        return place with { Line = place.Line + end.Line - at.Line };
    }

    /// <summary>
    /// Where a place is after the text from "from" up to "to" was deleted: a place before it stays, a place in it goes
    /// to where it was, and a place after it moves back
    /// </summary>
    internal static TextPos AfterDelete(TextPos place, TextPos from, TextPos to)
    {
        if (place <= from) return place;
        if (place < to) return from;
        if (place.Line == to.Line) return new TextPos(from.Line, from.Col + place.Col - to.Col);
        return place with { Line = place.Line - (to.Line - from.Line) };
    }
}
