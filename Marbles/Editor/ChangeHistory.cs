namespace Marbles;

/// <summary>
/// ":changes": the steps of the undo history as a list to read, newest first. The edits of a step are joined where
/// they follow each other (the letters typed in Insert mode, a word deleted and typed again) into the parts of the
/// text the step changed, and each step is told by its first part: where it is now (moved by the steps made after
/// it), whether text was inserted, deleted or replaced, and the start of that text. It does not touch the console,
/// so it can be tested
/// </summary>
internal static class ChangeHistory
{
    /// <summary>
    /// What a step did to the text
    /// </summary>
    internal enum ChangeKind
    {
        Inserted,
        Deleted,
        Replaced,
        Several, // Several parts of the text (Count)
        None // Text typed and deleted again
    }

    /// <summary>
    /// A step of the history: its first part in the text (where it is now, the text removed and inserted there), how
    /// many parts it changed, when it was made, and whether it was undone
    /// </summary>
    internal sealed record Change(ChangeKind Kind, TextPos Place, string Removed, string Inserted, int Count,
        DateTime Time, bool Undone);

    /// <summary>
    /// The steps of the history (TextBuffer.History, oldest first), newest first
    /// </summary>
    internal static List<Change> Of(IReadOnlyList<TextBuffer.Step> history)
    {
        var changes = new List<Change>();
        for (var i = 0; i < history.Count; i++)
        {
            var step = history[i];
            var parts = Parts(step.Edits);
            var first = parts.Count > 0 ? parts[0] : null;
            var place = first?.Start ?? step.CursorAfter;
            // Moved by the edits of the steps made after it that are in the text (an undone step is not, and its
            // place is that of the text it was made in)
            if (!step.Undone)
            {
                for (var j = i + 1; j < history.Count && !history[j].Undone; j++)
                {
                    foreach (var edit in history[j].Edits) place = Shift(place, edit);
                }
            }

            var kind = parts.Count switch
            {
                0 => ChangeKind.None,
                > 1 => ChangeKind.Several,
                _ => first!.Removed.Length == 0 ? ChangeKind.Inserted
                    : first.Inserted.Length == 0 ? ChangeKind.Deleted
                    : ChangeKind.Replaced
            };
            changes.Add(new Change(kind, place, first?.Removed ?? string.Empty, first?.Inserted ?? string.Empty,
                parts.Count, step.Time, step.Undone));
        }

        changes.Reverse();
        return changes;
    }

    // A part of the text a step changed: where it starts, and the text removed and inserted there
    private sealed class Part
    {
        internal TextPos Start;
        internal string Removed = string.Empty;
        internal string Inserted = string.Empty;
    }

    // The edits of a step, joined where they follow each other: an edit within the text a part inserted (a letter
    // typed or deleted in it), one that ends where the part starts (Backspace before it), or one that starts where
    // its text ends (a letter deleted after it). The other parts move with each edit (":%s" replaces from the last
    // line up: a line break put in above a part moves it down), so they are where they are when the step is done.
    // The parts that changed nothing are left out, and the others are in the order of the text
    private static List<Part> Parts(IReadOnlyList<TextBuffer.Edit> edits)
    {
        var parts = new List<Part>();
        foreach (var edit in edits)
        {
            var joined = parts.FirstOrDefault(part => Join(part, edit));
            foreach (var part in parts.Where(part => part != joined)) part.Start = Shift(part.Start, edit);
            if (joined == null)
            {
                parts.Add(new Part { Start = edit.Start, Removed = edit.Removed, Inserted = edit.Inserted });
            }
        }

        parts.RemoveAll(part => part.Removed == part.Inserted);
        return parts.OrderBy(part => part.Start).ToList();
    }

    private static bool Join(Part part, TextBuffer.Edit edit)
    {
        // Within the text inserted (or at its end)
        var at = OffsetIn(part.Start, part.Inserted, edit.Start);
        if (at >= 0)
        {
            var inside = Math.Min(edit.Removed.Length, part.Inserted.Length - at);
            if (string.CompareOrdinal(part.Inserted, at, edit.Removed, 0, inside) != 0) return false;
            // What was removed after the end of the text inserted was there before the part
            part.Removed += edit.Removed[inside..];
            part.Inserted = part.Inserted[..at] + edit.Inserted + part.Inserted[(at + inside)..];
            return true;
        }

        // Just before it: the text removed there was there before it
        if (TextBuffer.EndOf(edit.Start, edit.Removed) == part.Start)
        {
            part.Start = edit.Start;
            part.Removed = edit.Removed + part.Removed;
            part.Inserted = edit.Inserted + part.Inserted;
            return true;
        }

        return false;
    }

    // The index in a text written from a place of the place given (0 to its length), or -1 when it is not in it
    private static int OffsetIn(TextPos start, string text, TextPos place)
    {
        var line = start.Line;
        var col = start.Col;
        for (var i = 0; ; i++)
        {
            if (line == place.Line && col == place.Col) return i;
            if (i == text.Length || line > place.Line) return -1;
            if (text[i] == TextBuffer.NewLine)
            {
                line++;
                col = 0;
            }
            else
            {
                col++;
            }
        }
    }

    /// <summary>
    /// A place in the text after an edit made later: before the edit it stays, in the text the edit removed it goes
    /// to where the edit is, and after it it moves with the text that follows
    /// </summary>
    internal static TextPos Shift(TextPos place, TextBuffer.Edit edit)
    {
        if (place < edit.Start) return place;
        var removedEnd = TextBuffer.EndOf(edit.Start, edit.Removed);
        if (place < removedEnd) return edit.Start;
        var insertedEnd = TextBuffer.EndOf(edit.Start, edit.Inserted);
        return place.Line == removedEnd.Line
            ? new TextPos(insertedEnd.Line, insertedEnd.Col + place.Col - removedEnd.Col)
            : new TextPos(place.Line + insertedEnd.Line - removedEnd.Line, place.Col);
    }

    /// <summary>
    /// The start of a text as one row: its first line that is not blank (tabs as spaces, the blank space around it
    /// left out), and " ..." when it has more lines that are not blank
    /// </summary>
    internal static string Excerpt(string text)
    {
        var lines = text.Split(TextBuffer.NewLine).Select(line => line.Replace('\t', ' ').Trim())
            .Where(line => line.Length > 0).ToList();
        if (lines.Count == 0) return string.Empty;
        return lines[0] + (lines.Count > 1 ? " ..." : string.Empty);
    }
}
