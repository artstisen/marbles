namespace Marbles;

/// <summary>
/// The frame of Zen mode (setting "zen", ":zen"): one row above the text, with the mode and the name at the left
/// ("[I]test1.md*": the name of the file, or the title of the note, and a star for changes that are not saved), and
/// at the bottom only the line and column of the cursor, in the middle ("312:17"). A message is shown at the left of
/// the bottom line until the next key (a long one in place of the line and column), and REC and the search count at
/// the right while there are any. The completions of the word before the cursor follow the name on the top row; in a
/// split each pane has the name over it. The command line is as in the standard frame. Only the editor of a file or
/// a note has it
/// </summary>
internal sealed class ZenFrame : EditorFrame
{
    /// <summary>
    /// The row with the mode and the name
    /// </summary>
    internal const int Rows = 1;

    internal override string Name => "zen";

    internal override int RowsAbove => Rows;

    internal override void DrawAround(EditorFacts facts)
    {
        // Nothing else is around the text: the top row is drawn with the text
        ShownTop = null;
        ShownStatus = null;
    }

    // The star after the name
    internal override void ShowChanged(EditorFacts facts) => ShownTop = null;

    // Told on the top row, after the name
    internal override void DrawReadOnlyLabel(EditorFacts facts)
    {
    }

    /// <summary>
    /// The mode as Zen mode shows it, in front of the name (English, as the names of the modes): [N] Normal,
    /// [I] Insert, [V] the Visual modes, [C] a command or search being typed, [J] the jump keys shown
    /// </summary>
    internal static string ModeTag(EditorFacts facts) => facts.Jumping ? "[J]" : facts.Mode switch
    {
        EditorMode.Insert => "[I]",
        EditorMode.Visual or EditorMode.VisualLine or EditorMode.VisualBlock => "[V]",
        EditorMode.Command or EditorMode.Search => "[C]",
        _ => "[N]"
    };

    /// <summary>
    /// The name over the pane with the focus, after the mode: with a star when there are changes that are not saved
    /// ("test1.md*")
    /// </summary>
    internal static string NameOf(EditorFacts facts) => facts.Title + (facts.Modified ? "*" : string.Empty);

    /// <summary>
    /// What is written over a pane in a width (cut at the end to fit, with a column left free after it)
    /// </summary>
    internal static string Fit(string text, int width) =>
        Tools.TruncateString(TextMetrics.Printable(text), Math.Max(0, width - 1), true);

    // The top row: the mode and the name over the pane with the focus, the name over the other pane of a split, and
    // the completions after the name while there are any. Written whole when any of it has changed
    internal override void DrawTop(EditorFacts facts)
    {
        // A peek has no row above it: the pane is shown there
        if (facts.Top < 1 || facts.Kind == EditorKind.Peek) return;
        var row = facts.Top - 1;
        var tag = ModeTag(facts);
        var focusWidth = facts.Other == null ? Init.LineWidth - 1 : facts.FocusWidth;
        var readOnly = facts.ReadOnly ? DialogText.ReadOnlyLabel : string.Empty;
        // "Not encrypted" after the name, while there is room for it beside a name of a few letters
        var reminder = facts.Reminder.Length > 0 ? " " + facts.Reminder : string.Empty;
        var nameRoom = focusWidth - TextMetrics.WidthOf(tag) - TextMetrics.WidthOf(readOnly);
        if (nameRoom - TextMetrics.WidthOf(reminder) < 10) reminder = string.Empty;
        var name = Fit(NameOf(facts), nameRoom - TextMetrics.WidthOf(reminder));
        var other = facts.Other is { } pane ? Fit(facts.Title, pane.Width) : null;

        var key = tag + TextArea.ZenColors + "\u0001" + name + "\u0001" + reminder + "\u0001" + readOnly + "\u0001" +
                  other + "\u0001" + facts.FocusLeft +
                  (facts.Completions.Count > 0 ? CompletionsKey(facts) : string.Empty);
        if (key == ShownTop) return;
        ShownTop = key;

        Tools.SetCursorEx(row, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Console.Write(new string(' ', Init.LineWidth - 1));
        Tools.SetCursorEx(row, facts.Other == null ? 0 : facts.FocusLeft);
        // The mode in the color of its mode, as the mode label outside Zen mode (setting "zencolors"), or in the color
        // of the name (of the text)
        ConsoleColors.SetColors(TextArea.ZenColors ? ModeColor(facts) : ConsoleColors.ColorPreset.Normal);
        Console.Write(tag);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Console.Write(name);
        if (reminder.Length > 0)
        {
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
            Console.Write(reminder);
        }

        if (readOnly.Length > 0)
        {
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
            Console.Write(readOnly);
        }

        if (facts.Other is { } otherPane && other != null)
        {
            Tools.SetCursorEx(row, otherPane.Left);
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.OtherTabs);
            Console.Write(other);
        }

        // The completions after the name (over the other pane too: they are shown only while typing)
        if (facts.Completions.Count > 0)
        {
            var start = (facts.Other == null ? 0 : facts.FocusLeft) +
                        TextMetrics.WidthOf(tag + name + reminder + readOnly) + 1;
            DrawCompletions(facts, row, start, ' ');
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    /// <summary>
    /// The parts of the bottom line, from the left, filling the width: a message (empty when there is none), the
    /// blank space after it, the line and column, the blank space after them, and REC and the search count (empty
    /// when there are none)
    /// </summary>
    internal readonly record struct StatusParts(string Message, string Before, string Position, string After,
        string Right);

    /// <summary>
    /// The bottom line in a width (not while a command is typed): the line and column of the cursor in the middle
    /// ("312:17"), a message at the left, and REC and the search count at the right. A message that does not fit
    /// at the left of the line and column takes their place (until the next key), and is cut if it is still too
    /// long; REC and the search count are left out when there is no room for them
    /// </summary>
    internal static StatusParts Status(EditorFacts facts, int width)
    {
        // An error is highlighted, with a blank space on each side inside the highlight
        var message = facts.Message.Length == 0 ? string.Empty
            : facts.MessageIsError ? " " + facts.Message + " " : facts.Message;
        var position = facts.Line + ":" + facts.Column;
        var parts = new List<string>();
        // A macro is being recorded (English, as the names of the modes)
        if (facts.Recording) parts.Add("REC");
        if (facts.SearchCount.Length > 0) parts.Add(facts.SearchCount);
        var right = parts.Count == 0 ? string.Empty : string.Join(" | ", parts) + " ";

        var positionWidth = TextMetrics.WidthOf(position);
        var positionAt = Math.Max(0, (width - positionWidth) / 2);
        // A message that does not fit before the line and column (with a blank space between): it takes the line,
        // and REC and the search count stay at the right if there is room for them
        if (TextMetrics.WidthOf(message) > positionAt - 1)
        {
            if (TextMetrics.WidthOf(message) + 1 + TextMetrics.WidthOf(right) > width) right = string.Empty;
            var room = width - TextMetrics.WidthOf(right) - (right.Length > 0 ? 1 : 0);
            message = Cut(message, room);
            return new StatusParts(message, Blank(width - TextMetrics.WidthOf(message) - TextMetrics.WidthOf(right)),
                string.Empty, string.Empty, right);
        }

        // What is at the right, if it fits after the line and column (with a blank space between)
        var after = width - positionAt - positionWidth;
        if (TextMetrics.WidthOf(right) > after - 1) right = string.Empty;
        return new StatusParts(message, Blank(positionAt - TextMetrics.WidthOf(message)), position,
            Blank(after - TextMetrics.WidthOf(right)), right);
    }

    private static string Blank(int cells) => new(' ', Math.Max(0, cells));

    // A text cut at the end to a width
    private static string Cut(string text, int width)
    {
        while (text.Length > 0 && TextMetrics.WidthOf(text) > width)
        {
            text = text[..^TextMetrics.PreviousElementLength(text, text.Length)];
        }

        return text;
    }

    internal override void DrawStatus(EditorFacts facts)
    {
        if (facts.Mode is EditorMode.Command or EditorMode.Search)
        {
            DrawCommandLine(facts);
            return;
        }

        var parts = Status(facts, Init.LineWidth - 1);
        var status = parts.Message + "\u0001" + parts.Before + parts.Position + parts.After + parts.Right +
                     facts.MessageIsError + facts.MessageIsCommandError;
        if (status == ShownStatus) return;
        ShownStatus = status;

        Tools.SetCursorEx(Init.LineCount - 1, 0);
        ConsoleColors.SetColors(facts.MessageIsCommandError
            ? ConsoleColors.ColorPreset.CommandErrorInverted
            : facts.MessageIsError
                ? ConsoleColors.ColorPreset.AttentionInverted
                : ConsoleColors.ColorPreset.Normal);
        Console.Write(parts.Message);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Console.Write(parts.Before);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(parts.Position);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Console.Write(parts.After);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(parts.Right);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }
}
