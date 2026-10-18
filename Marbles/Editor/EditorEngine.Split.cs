namespace Marbles;

/// <summary>
/// A split (":split"): the text shown in two panes side by side, each with an editor of its own (its cursor, view,
/// mode and search) on the same TextBuffer, so a change made in one is seen in the other. The host gives the keys to
/// one of them; the other follows the changes, keeping its cursor on the same text when lines are added or removed
/// above it. Undo is shared, as the text is
/// </summary>
internal sealed partial class EditorEngine
{
    /// <summary>
    /// The text is shown in two panes (set by the host): ":q" closes this pane, not the editor
    /// </summary>
    internal bool InSplit { get; set; }

    /// <summary>
    /// The pane without the focus in a split: it does not get keys, and follows the changes made in the other pane
    /// </summary>
    internal bool IsFollowing { get; private set; }

    // While following: the row of the view the cursor is kept on, and whether the view must be found again
    private int _followRow;
    private bool _followMoved;

    /// <summary>
    /// A second view of the text of an editor (a split): the same text, cursor and view, with the settings of the
    /// view (line numbers, wrapping, colors ...) as they are there
    /// </summary>
    internal EditorEngine(EditorEngine shown, int width, int height)
    {
        Buffer = shown.Buffer;
        Layout = new TextLayout(Buffer, width, shown.Wrap);
        Height = Math.Max(1, height);
        Mode = EditorMode.Normal;
        LineNumbers = shown.LineNumbers;
        ScrollOffLines = shown.ScrollOffLines;
        ScrollOff = shown.ScrollOff;
        AutoComplete = shown.AutoComplete;
        MacroLoader = shown.MacroLoader;
        CompletionSyntax = shown.CompletionSyntax;
        SyntaxDefault = shown.SyntaxDefault;
        Syntax = shown.Syntax;
        ReadOnlyMessage = shown.ReadOnlyMessage;
        ClipboardEveryYank = shown.ClipboardEveryYank;
        Cursor = shown.Cursor;
        TopRow = shown.TopRow;
        ClampCursor();
        EnsureCursorVisible();
        Buffer.LinesReplaced += Follow;
    }

    /// <summary>
    /// The other pane gets the keys: this one follows its changes, with the cursor on the row of the view it is on
    /// </summary>
    internal void StartFollowing()
    {
        Mode = EditorMode.Normal;
        ResetPending();
        IsFollowing = true;
        _followRow = Layout.RowOf(Cursor) - TopRow;
    }

    /// <summary>
    /// This pane gets the keys again
    /// </summary>
    internal void StopFollowing()
    {
        UpdateFollowingView();
        IsFollowing = false;
        ClampCursor();
        EnsureCursorVisible();
    }

    /// <summary>
    /// While following: the view after the changes made in the other pane, with the cursor on its row of the view
    /// (found once, before the pane is drawn, as a command can change many lines)
    /// </summary>
    internal void UpdateFollowingView()
    {
        if (!_followMoved) return;
        _followMoved = false;
        ClampCursor();
        TopRow = Math.Clamp(Layout.RowOf(Cursor) - _followRow, 0, Math.Max(0, Layout.TotalRows() - 1));
    }

    /// <summary>
    /// The pane gets the focus from another: the text it deleted within a line is put with "p" here too (as yanks
    /// are, which go to the string memory of all)
    /// </summary>
    internal void TakeSmallRegister(EditorEngine from)
    {
        _smallRegister = from._smallRegister;
        _useSmallRegister = from._useSmallRegister;
    }

    /// <summary>
    /// The pane is closed: it no longer follows the text (the text and the other pane go on)
    /// </summary>
    internal void Detach()
    {
        Buffer.LinesReplaced -= Follow;
        Layout.Detach();
        if (_watchingLines) Buffer.LinesReplaced -= ForgetLineStates;
    }

    // Lines were changed in the other pane: the cursor stays on its text (moved with the lines added or removed above
    // it), and on the same row of the view (UpdateFollowingView). Lines changed below it change nothing here
    private void Follow(int first, int removed, int inserted)
    {
        if (!IsFollowing || first > Cursor.Line) return;
        var line = Cursor.Line;
        if (line >= first + removed) line += inserted - removed;
        else line = Math.Min(line, first + Math.Max(0, inserted - 1));
        Cursor = new TextPos(Math.Clamp(line, 0, Math.Max(0, Buffer.LineCount - 1)), Cursor.Col);
        _followMoved = true;
    }
}
