namespace Marbles;

/// <summary>
/// The view of a comparison (":diff" in the file browser and in the editor): two texts are compared (LineDiff),
/// and the comparison is shown read only in the editor, colored, as a unified diff. "n" "N" and "]c" "[c" go to
/// the changes, Enter asks for the line of the cursor to be opened, Esc closes, and ":w name" saves the comparison
/// as a file.
/// </summary>
internal static class DiffView
{
    /// <summary>
    /// The line to open (Enter): in the old or the new text, its number there (from 0), and the nearest line of the
    /// new text
    /// </summary>
    internal readonly record struct Place(bool InOld, int Line, int NewLine);

    /// <summary>
    /// Compare two texts and show the comparison
    /// </summary>
    /// <param name="oldText">The old text (the lines removed are taken from it)</param>
    /// <param name="newText">The new text (the lines added are taken from it)</param>
    /// <param name="oldName">How the old text is named (on the line "---")</param>
    /// <param name="newName">How the new text is named (on the line "+++")</param>
    /// <param name="saveDirectory">Where ":w name" saves the comparison</param>
    /// <param name="ignoreSpace">Lines that differ only in blank space are the same</param>
    /// <param name="note">Shown after the numbers of the changes (the line endings differ ...), or empty</param>
    /// <param name="message">When the comparison is not shown: why (the texts are the same ...)</param>
    /// <returns>The place to open, or null if the view was closed (or not shown)</returns>
    internal static Place? Show(string oldText, string newText, string oldName, string newName,
        string saveDirectory, bool ignoreSpace, string note, out string message)
    {
        message = string.Empty;
        var oldLines = TextBuffer.Normalize(oldText).Split('\n');
        var newLines = TextBuffer.Normalize(newText).Split('\n');
        var lines = LineDiff.Compare(oldLines, newLines, ignoreSpace);
        if (lines == null)
        {
            message = Lang.F("Too many differences to show (more than {0} lines)", LineDiff.MaxDifferences);
            return null;
        }

        if (lines.TrueForAll(line => line.Kind == LineDiff.Kind.Same))
        {
            message = ignoreSpace ? DialogText.DiffSameIgnoringSpace : DialogText.DiffSame;
            return null;
        }

        var unified = LineDiff.Format(lines, oldName, newName);
        var summary = Lang.F("Changes: {0}, lines added: {1}, removed: {2}", unified.Changes.Count, unified.Added,
            unified.Removed);
        if (ignoreSpace) summary += " " + DialogText.DiffBlankSpaceIgnored;
        if (note.Length > 0) summary += " " + note;

        void DrawHeader() => Actions.DrawCompareMenu(summary);

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Tools.ClearScreen();
        DrawHeader();
        var start = Snippets.IndexOfLine(unified.Text, unified.Changes.Count > 0 ? unified.Changes[0] : 0);
        var textArea = new TextArea(0, Console.CursorTop, Init.LineWidth - 1, Init.LineCount - 3, unified.Text, start,
            false, false, false, TextArea.SaveAsFile, false,
            new TextArea.PeekOptions(SyntaxType.Diff, DrawHeader)
            {
                Hint = DialogText.DiffHint,
                ReadOnlyText = DialogText.DiffReadOnly,
                Changes = unified.Changes,
                SaveDirectory = saveDirectory
            });
        Tools.HideCursor();
        if (!textArea.OpenRequested) return null;
        var row = unified.Text.AsSpan(0, Math.Min(textArea.GetCursorIndex(), unified.Text.Length)).Count('\n');
        var (inOld, line, newLine) = unified.Places[Math.Clamp(row, 0, unified.Places.Count - 1)];
        return new Place(inOld, line, newLine);
    }
}
