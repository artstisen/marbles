namespace Marbles;

/// <summary>
/// ":changes": the steps of the undo history as a list to read, newest first. The edits of a step are joined where
/// they follow each other (the letters typed in Insert mode, a word deleted and typed again) into the parts of the
/// text the step changed, and each step is told by its first part: where it is now (moved by the steps made after
/// it), whether text was inserted, deleted or replaced, and the start of that text. It does not touch the console,
/// so it can be tested.
///
/// A step that changed very many parts of the text (":%s" on a long text) is told as Several at its first place in
/// the text, without joining its edits, and the list is made for at most BudgetMilliseconds: the steps left then (the
/// oldest) are told by one row of the kind NotListed
/// </summary>
internal static class ChangeHistory
{
    /// <summary>
    /// The most parts of a step that are joined: a step with more is told as Several (Count: the places it changed,
    /// see PlacesOf), at the place of its first edit in the text. (A paragraph typed is thousands of edits, but one part: it is still told as
    /// one insertion)
    /// </summary>
    internal const int MaxParts = 1000;

    /// <summary>
    /// How long the list is made for (as the other limits of the editor, it is never left waiting)
    /// </summary>
    internal const long BudgetMilliseconds = 500;

    /// <summary>
    /// What a step did to the text
    /// </summary>
    internal enum ChangeKind
    {
        Inserted,
        Deleted,
        Replaced,
        Several, // Several parts of the text (Count)
        None, // Text typed and deleted again
        NotListed // The last row when there was no time to list them all: the oldest steps (Count) are not listed
    }

    /// <summary>
    /// A step of the history: its first part in the text (where it is now, the text removed and inserted there), how
    /// many parts it changed, when it was made, and whether it was undone
    /// </summary>
    internal sealed record Change(ChangeKind Kind, TextPos Place, string Removed, string Inserted, int Count,
        DateTime Time, bool Undone);

    /// <summary>
    /// The steps of the history (TextBuffer.History, oldest first), newest first. When making the list takes longer
    /// than the budget, the steps left are told by a last row (NotListed)
    /// </summary>
    internal static List<Change> Of(IReadOnlyList<TextBuffer.Step> history, long budgetMilliseconds = BudgetMilliseconds)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        // The first place in the text that the edits of each step start at (null: none). A place before it is not
        // moved by the step, which is then passed over at once
        var firsts = history.Select(step => FirstStart(step.Edits)).ToList();
        var changes = new List<Change>();
        for (var i = history.Count - 1; i >= 0; i--)
        {
            if (clock.ElapsedMilliseconds > budgetMilliseconds)
            {
                changes.Add(new Change(ChangeKind.NotListed, default, string.Empty, string.Empty, i + 1,
                    history[i].Time, false));
                break;
            }

            var step = history[i];
            var parts = Parts(step.Edits, out var many);
            var first = parts.Count > 0 ? parts[0] : null;
            var place = first?.Start ?? step.CursorAfter;
            // Moved by the edits of the steps made after it that are in the text (an undone step is not, and its
            // place is that of the text it was made in)
            if (!step.Undone)
            {
                for (var j = i + 1; j < history.Count && !history[j].Undone; j++)
                {
                    if (firsts[j] is not { } firstOfStep || place < firstOfStep) continue;
                    foreach (var edit in history[j].Edits) place = Shift(place, edit);
                }
            }

            var kind = many ? ChangeKind.Several : parts.Count switch
            {
                0 => ChangeKind.None,
                > 1 => ChangeKind.Several,
                _ => first!.Removed.Length == 0 ? ChangeKind.Inserted
                    : first.Inserted.Length == 0 ? ChangeKind.Deleted
                    : ChangeKind.Replaced
            };
            changes.Add(new Change(kind, place, first?.Removed ?? string.Empty, first?.Inserted ?? string.Empty,
                many ? PlacesOf(step.Edits) : parts.Count, step.Time, step.Undone));
        }

        return changes;
    }

    // The first place in the text that edits start at, or null when there are none
    private static TextPos? FirstStart(IReadOnlyList<TextBuffer.Edit> edits)
    {
        TextPos? first = null;
        foreach (var edit in edits)
        {
            if (first == null || edit.Start < first.Value) first = edit.Start;
        }

        return first;
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
    // The parts that changed nothing are left out, and the others are in the order of the text. More than MaxParts
    // (many): only the first edit in the text, moved by the edits made after it (joining them all would take long)
    private static List<Part> Parts(IReadOnlyList<TextBuffer.Edit> edits, out bool many)
    {
        many = false;
        var parts = new List<Part>();
        foreach (var edit in edits)
        {
            var joined = parts.FirstOrDefault(part => Join(part, edit));
            foreach (var part in parts.Where(part => part != joined)) part.Start = Shift(part.Start, edit);
            if (joined == null)
            {
                parts.Add(new Part { Start = edit.Start, Removed = edit.Removed, Inserted = edit.Inserted });
                if (parts.Count > MaxParts)
                {
                    many = true;
                    return [FirstInText(edits)];
                }
            }
        }

        parts.RemoveAll(part => part.Removed == part.Inserted);
        return parts.OrderBy(part => part.Start).ToList();
    }

    // The edit of a step that starts first in the text (the first of them at that place), joined with the edits made
    // after it that follow it (a replacement is an edit that deletes and one that inserts), as a part where it is
    // when the step is done
    private static Part FirstInText(IReadOnlyList<TextBuffer.Edit> edits)
    {
        var first = 0;
        for (var i = 1; i < edits.Count; i++)
        {
            if (edits[i].Start < edits[first].Start) first = i;
        }

        var part = new Part { Start = edits[first].Start, Removed = edits[first].Removed, Inserted = edits[first].Inserted };
        for (var i = first + 1; i < edits.Count; i++)
        {
            if (!Join(part, edits[i])) part.Start = Shift(part.Start, edits[i]);
        }

        return part;
    }

    /// <summary>
    /// The places a step changed, told for a step with very many parts (Several): its edits, without those that
    /// start where the edit before them did (a replacement deletes, then inserts at the same place)
    /// </summary>
    internal static int PlacesOf(IReadOnlyList<TextBuffer.Edit> edits)
    {
        var count = 0;
        for (var i = 0; i < edits.Count; i++)
        {
            if (i == 0 || edits[i].Start != edits[i - 1].Start) count++;
        }

        return count;
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
