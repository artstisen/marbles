namespace Marbles;

/// <summary>
/// Comparisons in the editor: ":diff" asks the host to compare the text with the file on disk (or the saved
/// entry), and in the view of a comparison "n" "N" and "]c" "[c" go to the next and the previous change (the
/// lines where the changes start are given by the host). ":w name" asks the host to save the text as a new file
/// (a comparison is saved so).
/// </summary>
internal sealed partial class EditorEngine
{
    /// <summary>
    /// The lines where the changes of a comparison start (null: the text is not a comparison)
    /// </summary>
    internal IReadOnlyList<int>? ChangeLines { get; set; }

    /// <summary>
    /// Lines that differ only in blank space are the same (":diff -w")
    /// </summary>
    internal bool DiffIgnoreSpace { get; private set; }

    /// <summary>
    /// What follows ":book" (EditorRequest.JumpToBookmark): the number of a bookmark, or empty
    /// </summary>
    internal string BookArgument { get; private set; } = string.Empty;

    /// <summary>
    /// The name given with ":w name" (EditorRequest.WriteCopy)
    /// </summary>
    internal string WriteName { get; private set; } = string.Empty;

    /// <summary>
    /// The matches of a search are highlighted ("n" "N" go to them, not to the changes of a comparison)
    /// </summary>
    internal bool IsSearchHighlighted => HighlightSearch && _searchRegex != null;

    // "]c" "[c" (and "n" "N" in a comparison without a search): the next or the previous change. It does not go
    // round: at the last change it tells that there are no more
    private void JumpToChange(bool forward, int count)
    {
        if (ChangeLines == null || ChangeLines.Count == 0)
        {
            Message = DialogText.DiffNoChanges;
            return;
        }

        var line = Cursor.Line;
        var found = false;
        for (; count > 0; count--)
        {
            int next;
            if (forward)
            {
                next = ChangeLines.FirstOrDefault(change => change > line, -1);
            }
            else
            {
                next = ChangeLines.LastOrDefault(change => change < line, -1);
            }

            if (next < 0) break;
            line = next;
            found = true;
        }

        if (!found)
        {
            Message = forward ? DialogText.DiffNoMoreChanges : DialogText.DiffNoChangesBefore;
            return;
        }

        Cursor = new TextPos(line, 0);
        var index = ChangeLines.ToList().IndexOf(line) + 1;
        Message = Lang.F("Change {0} of {1}", index, ChangeLines.Count);
        EnsureCursorVisible();
    }

    // ":diff" and ":diff -w": the host shows the comparison
    private void RequestDiff(bool ignoreSpace)
    {
        if (SingleLine) return;
        DiffIgnoreSpace = ignoreSpace;
        _request = EditorRequest.Diff;
    }
}
