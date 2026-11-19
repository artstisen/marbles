namespace Marbles;

/// <summary>
/// What the editor (TextArea) is: the editor of a file, of an entry (a note of a data file), of the settings file, a
/// peek (a text shown read only in a pane or a box: a comparison, a macro), or another text (a field)
/// </summary>
internal enum EditorKind
{
    File,
    Note,
    Settings,
    Peek,
    Other
}

/// <summary>
/// What a frame of the editor shows, gathered by the editor once each time it is drawn (TextArea.Facts): both frames
/// get the same facts, and differ only in how they show them. The facts are plain values, so what a frame makes of
/// them can be tested without a console. They are only shown, never logged (the title of a note is among them)
/// </summary>
internal sealed record EditorFacts
{
    internal EditorKind Kind { get; init; }

    /// <summary>
    /// The row of the window the text starts on (the rows above it are those of the frame)
    /// </summary>
    internal int Top { get; init; }

    internal EditorMode Mode { get; init; }

    /// <summary>
    /// The jump keys are shown in the text ("s" in Normal mode)
    /// </summary>
    internal bool Jumping { get; init; }

    /// <summary>
    /// What is shown in front of a command or search being typed ("COMMAND:", "/" or "?"), and what is typed
    /// </summary>
    internal string CommandLinePrefix { get; init; } = string.Empty;

    internal string CommandText { get; init; } = string.Empty;

    /// <summary>
    /// The message of the editor (shown until the next key), and whether it tells of an error, or of a command
    /// that was not known
    /// </summary>
    internal string Message { get; init; } = string.Empty;

    internal bool MessageIsError { get; init; }
    internal bool MessageIsCommandError { get; init; }

    /// <summary>
    /// The line of the cursor and its column on the screen, from 1, and how far down the text it is (in percent)
    /// </summary>
    internal int Line { get; init; }

    internal int Column { get; init; }
    internal int Percent { get; init; }

    /// <summary>
    /// The keys typed of a command that is not complete ("d", "g")
    /// </summary>
    internal string PendingKeys { get; init; } = string.Empty;

    /// <summary>
    /// A macro is being recorded
    /// </summary>
    internal bool Recording { get; init; }

    /// <summary>
    /// The match of the search the cursor is on, and how many there are ("[2/12]"), or empty
    /// </summary>
    internal string SearchCount { get; init; } = string.Empty;

    /// <summary>
    /// The string memory ("MEM 2/5"), the languages of the spell check, and the kind and encoding of the file:
    /// empty when there is nothing to tell
    /// </summary>
    internal string LineMemory { get; init; } = string.Empty;

    internal string Spell { get; init; } = string.Empty;
    internal string FileLabel { get; init; } = string.Empty;

    /// <summary>
    /// The completions of the word before the cursor, and the one chosen with Tab (-1: none)
    /// </summary>
    internal IReadOnlyList<Completions.Item> Completions { get; init; } = Array.Empty<Completions.Item>();

    internal int CompletionIndex { get; init; } = -1;

    /// <summary>
    /// The name of what is edited (the name of the file, the title of the note, or the settings file), whether it
    /// has changes that are not saved, and whether it is read only
    /// </summary>
    internal string Title { get; init; } = string.Empty;

    /// <summary>
    /// What is told after the name (a file opened from the notes of a data file: "Not encrypted"), or empty
    /// </summary>
    internal string Reminder { get; init; } = string.Empty;

    internal bool Modified { get; init; }
    internal bool ReadOnly { get; init; }

    /// <summary>
    /// The pane with the focus (the whole width when not split): its first column and its width
    /// </summary>
    internal int FocusLeft { get; init; }

    internal int FocusWidth { get; init; }

    /// <summary>
    /// The other pane of a split, or null: its first column and its width
    /// </summary>
    internal (int Left, int Width)? Other { get; init; }
}

/// <summary>
/// What is around the text of the editor: the rows above it and the bottom line. The editor (TextArea) knows
/// nothing of how they look: it gathers the facts (EditorFacts) and asks the frame to draw them. There are two:
/// StandardFrame (the caption, the divider with the mode, and the status line) and ZenFrame (one row with the mode
/// and the name above the text, and the line and column below it). Each remembers what it has shown, so it only
/// writes what has changed. The command line is the same in both (DrawCommandLine)
/// </summary>
internal abstract class EditorFrame
{
    /// <summary>
    /// The color of the mode label (and of the mode in Zen mode, when it is colored): Attention for Insert, Warning for
    /// the Visual modes, Highlight for the others (Normal, a command or search being typed, the jump keys)
    /// </summary>
    internal static ConsoleColors.ColorPreset ModeColor(EditorFacts facts) => facts.Mode switch
    {
        EditorMode.Insert => ConsoleColors.ColorPreset.AttentionInverted,
        EditorMode.Visual or EditorMode.VisualLine or EditorMode.VisualBlock =>
            ConsoleColors.ColorPreset.WarningInverted,
        _ => ConsoleColors.ColorPreset.HighlightInverted
    };

    /// <summary>
    /// The name of the frame, for the log (English)
    /// </summary>
    internal abstract string Name { get; }

    /// <summary>
    /// The rows above the text when the editor takes the whole window
    /// </summary>
    internal abstract int RowsAbove { get; }

    /// <summary>
    /// After the window was cleared: all of the frame (the rows above the text and the labels in them)
    /// </summary>
    internal abstract void DrawAround(EditorFacts facts);

    /// <summary>
    /// The row just above the text (the mode, the completions, the names of the panes), when it has changed
    /// </summary>
    internal abstract void DrawTop(EditorFacts facts);

    /// <summary>
    /// The bottom line, when it has changed (while a command or search is typed: the command line)
    /// </summary>
    internal abstract void DrawStatus(EditorFacts facts);

    /// <summary>
    /// The text got changes that are not saved, or has none again
    /// </summary>
    internal abstract void ShowChanged(EditorFacts facts);

    /// <summary>
    /// When the editor opens, after the page drew what is around it: the label of a text that is read only
    /// </summary>
    internal abstract void DrawReadOnlyLabel(EditorFacts facts);

    // What was written last on the row above the text and on the bottom line (null: they are written again)
    protected string? ShownTop;
    protected string? ShownStatus;

    /// <summary>
    /// The row above the text is written again at the next draw
    /// </summary>
    internal void ForgetTop() => ShownTop = null;

    /// <summary>
    /// The bottom line is written again at the next draw
    /// </summary>
    internal void ForgetStatus() => ShownStatus = null;

    /// <summary>
    /// The command line: the whole bottom line highlighted, with what is typed after "COMMAND:" (or "/" or "?").
    /// Written when it has changed. The same in both frames
    /// </summary>
    protected void DrawCommandLine(EditorFacts facts)
    {
        var line = CommandLineText(facts.CommandLinePrefix + facts.CommandText, Init.LineWidth - 1);
        if (line == ShownStatus) return;
        ShownStatus = line;
        Tools.SetCursorEx(Init.LineCount - 1, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.CommandLineInverted);
        Console.Write(line);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    /// <summary>
    /// The command line in a width: the text typed (cut at the end so the cursor fits after it), and blank space
    /// to the end
    /// </summary>
    internal static string CommandLineText(string text, int width)
    {
        while (TextMetrics.WidthOf(text) > width - 1)
        {
            text = text[..^TextMetrics.PreviousElementLength(text, text.Length)];
        }

        return text + new string(' ', Math.Max(0, width - TextMetrics.WidthOf(text)));
    }

    // The longest a completion is shown
    private const int MaxCompletionWidth = 24;

    // The room the keys must leave for the completions: one of the longest, with the columns around it
    private const int MinCompletionsRoom = MaxCompletionWidth + 6;

    /// <summary>
    /// The completions of the word before the cursor on a row, from a column: the one chosen with Tab is
    /// highlighted, and the keys are shown at the right. The row is written from the column to its end, with the
    /// fill (dashes on the divider)
    /// </summary>
    protected static void DrawCompletions(EditorFacts facts, int row, int start, char fill = '-')
    {
        var list = facts.Completions;
        var chosen = facts.CompletionIndex;
        var hints = list.Count == 1 ? DialogText.CompleteOneKeys
            : chosen < 0 ? DialogText.CompleteChooseKeys : DialogText.CompleteManyKeys;
        var width = Init.LineWidth - 1;
        Tools.SetCursorEx(row, start);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(new string(fill, Math.Max(0, width - start)));
        // The keys at the right (as Menu.WriteKeyHints draws them: each with three cells around it)
        var hintsWidth = hints.Split(" | ").Sum(hint => TextMetrics.WidthOf(hint) + 3) + 1;
        var hintsAt = Math.Max(start, width - 2 - hintsWidth);
        // Without the keys when they would leave no room for a completion (after a long name in Zen mode, over the
        // pane at the right of a split)
        var withHints = hintsAt - start >= MinCompletionsRoom;
        if (!withHints) hintsAt = width;
        var column = start + 2;
        for (var i = 0; i < list.Count; i++)
        {
            var text = " " + Tools.TruncateString(list[i].Shown, MaxCompletionWidth, true) + " ";
            var cells = TextMetrics.WidthOf(text);
            if (column + cells > hintsAt - 1) break;
            Tools.SetCursorEx(row, column);
            ConsoleColors.SetColors(i == chosen
                ? ConsoleColors.ColorPreset.KeyHintInverted
                : ConsoleColors.ColorPreset.EditorTitle);
            Console.Write(text);
            column += cells + 1;
        }

        if (withHints)
        {
            Tools.SetCursorEx(row, hintsAt);
            Menu.WriteKeyHints(hints);
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    /// <summary>
    /// What tells that the completions shown have changed (the one chosen too)
    /// </summary>
    protected static string CompletionsKey(EditorFacts facts) =>
        "\u0001" + facts.CompletionIndex + "\u0001" + string.Join("\u0001", facts.Completions.Select(item => item.Text));
}
