namespace Marbles;

/// <summary>
/// Completing words while typing in Insert mode (":complete"): the word before the cursor is looked up in the
/// lists of the Completions class after each character typed. Tab completes when there is one completion; when
/// there are more, Tab goes through them (Shift+Tab back) and Enter uses the one chosen. Esc closes the list (the
/// next Esc leaves Insert mode). The host (TextArea) shows the list on the line above the text.
/// </summary>
internal sealed partial class EditorEngine
{
    private List<Completions.Item> _completions = new();
    private TextPos _completionFrom; // Where the word that is completed starts

    /// <summary>
    /// Complete words while typing (set from the setting "autocomplete"; ":complete" turns it on and off)
    /// </summary>
    internal bool AutoComplete { get; set; }

    /// <summary>
    /// The kind of text used for the lists of completions when the text is not colored (the kind of the file)
    /// </summary>
    internal SyntaxType CompletionSyntax { get; set; }

    /// <summary>
    /// What the word before the cursor can be completed to (empty: nothing is shown)
    /// </summary>
    internal IReadOnlyList<Completions.Item> CompletionList => _completions;

    /// <summary>
    /// The completion chosen with Tab (-1: none yet)
    /// </summary>
    internal int CompletionIndex { get; private set; } = -1;

    private void ClearCompletions()
    {
        _completions = new List<Completions.Item>();
        CompletionIndex = -1;
    }

    // A key in Insert mode while completions are shown. True if it was used for them
    private bool HandleCompletionKey(ConsoleKeyInfo key)
    {
        if (_completions.Count == 0) return false;
        if (IsEscape(key))
        {
            // The list is closed, and Insert mode is left with the next Esc
            ClearCompletions();
            return true;
        }

        if (key.Key == ConsoleKey.Tab)
        {
            var back = (key.Modifiers & ConsoleModifiers.Shift) != 0;
            if (_completions.Count == 1 && !back)
            {
                AcceptCompletion(0);
            }
            else if (back)
            {
                CompletionIndex = CompletionIndex <= 0 ? _completions.Count - 1 : CompletionIndex - 1;
            }
            else
            {
                CompletionIndex = (CompletionIndex + 1) % _completions.Count;
            }

            return true;
        }

        // Enter uses the completion chosen with Tab (before that, it starts a new line as usual)
        if (IsEnter(key) && CompletionIndex >= 0)
        {
            AcceptCompletion(CompletionIndex);
            return true;
        }

        return false;
    }

    // After a key in Insert mode: look up the word before the cursor if a character was typed or deleted
    private void UpdateCompletions(ConsoleKeyInfo key)
    {
        ClearCompletions();
        if (!AutoComplete || SingleLine || ReadOnly || _blockInsert != null || Mode != EditorMode.Insert ||
            IsPlayingMacro || IsRecording) return;
        if (!IsText(key) && key.Key != ConsoleKey.Backspace) return;
        var line = CurrentLine;
        var syntax = Syntax != SyntaxType.None ? Syntax : CompletionSyntax;
        foreach (var start in Completions.WordStarts(line, Cursor.Col))
        {
            var found = Completions.Find(line[start..Cursor.Col], syntax);
            if (found.Count == 0) continue;
            _completions = found;
            _completionFrom = Cursor with { Col = start };
            return;
        }
    }

    // Put the completion in place of the word typed, as one change. The cursor goes to "$0" in it (or after it),
    // and the editor stays in Insert mode
    private void AcceptCompletion(int index)
    {
        var item = _completions[index];
        var from = _completionFrom;
        ClearCompletions();
        if (from.Line != Cursor.Line || from.Col > Cursor.Col) return;
        var text = TextBuffer.Normalize(item.Text).Replace("\t", new string(' ', TabWidth));
        var line = CurrentLine;
        var (inserted, mark) = Snippets.Prepare(text, line[..(line.Length - line.TrimStart().Length)]);
        // The completion is a change of its own (the text typed before it is one too)
        if (Buffer.IsStepOpen) Buffer.EndStep(Cursor);
        _stepCursor = Cursor;
        Delete(from, Cursor);
        var end = Insert(from, inserted);
        Cursor = mark < 0 ? end : Buffer.FromIndex(Buffer.ToIndex(from) + mark);
        Buffer.EndStep(Cursor);
        _stepCursor = Cursor;
        ClampCursor();
        EnsureCursorVisible();
    }

    // ":complete" (on/off), ":complete on", ":complete off"
    private void SetAutoComplete(bool on)
    {
        if (SingleLine) return;
        AutoComplete = on;
        ClearCompletions();
        Message = on ? DialogText.CompleteOn : DialogText.CompleteOff;
        // The host remembers the choice
        _request = EditorRequest.CompleteChanged;
    }
}
