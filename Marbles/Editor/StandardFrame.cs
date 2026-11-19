namespace Marbles;

/// <summary>
/// The frame of the editor as it has always been: the top row (the caption EDITOR, the name of the file or the title of
/// the note, and the label Unsaved or Read only at the right), the divider below it with the mode (NAVIGATE, EDIT,
/// SELECT, COMMAND) or the completions, and the status line at the bottom (the mode, messages, and what is known of
/// the cursor and the file). Every editor that is not a file or a note in Zen mode has it: a peek and a field use only
/// its bottom line, or nothing of it
/// </summary>
internal sealed class StandardFrame : EditorFrame
{
    /// <summary>
    /// The top row and the divider
    /// </summary>
    internal const int Rows = 2;

    internal override string Name => "standard";

    internal override int RowsAbove => Rows;

    internal override void DrawAround(EditorFacts facts)
    {
        switch (facts.Kind)
        {
            case EditorKind.Settings:
                Actions.DrawEditSettingsMenu();
                break;
            case EditorKind.File:
                Actions.DrawEditFileFrame();
                break;
            default:
                Actions.DrawEditFrame();
                break;
        }

        Menu.IndicateChanges(true);
        DrawReadOnlyLabel(facts);
        ShownTop = null;
        ShownStatus = null;
    }

    internal override void ShowChanged(EditorFacts facts)
    {
        if (facts.Modified)
        {
            Menu.IndicateChanges(true);
        }
        else
        {
            Menu.ClearChangeIndicator();
            Menu.InitializeSaveIndicator();
        }
    }

    // The label "Read only" at the top right, where the label "Unsaved" is shown for a text with changes (a text
    // that is read only is not changed)
    internal override void DrawReadOnlyLabel(EditorFacts facts)
    {
        if (!facts.ReadOnly || facts.Kind == EditorKind.Peek) return;
        var label = DialogText.ReadOnlyLabel;
        Tools.SetCursorEx(0, Math.Max(0, Init.LineWidth - 1 - TextMetrics.WidthOf(label)));
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.AttentionInverted);
        Console.Write(label);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    // The mode on the divider above the text: EDIT (Insert), NAVIGATE (Normal), SELECT (the Visual modes) or
    // COMMAND (the command line and search), over the pane with the focus. The completions of the word before the
    // cursor take the divider while they are shown
    internal override void DrawTop(EditorFacts facts)
    {
        // A peek has no divider above it: the pane is shown there
        if (facts.Top < 1 || facts.Kind == EditorKind.Peek) return;
        var row = facts.Top - 1;
        if (facts.Completions.Count > 0)
        {
            var key = CompletionsKey(facts);
            if (key == ShownTop) return;
            ShownTop = key;
            DrawCompletions(facts, row, 0);
            return;
        }

        var label = ModeLabel(facts);
        // (written again when the focus goes to the other pane of a split: the mode is over the pane with the focus)
        var shown = label + "\u0001" + facts.FocusLeft;
        if (shown == ShownTop) return;
        ShownTop = shown;
        Tools.SetCursorEx(row, 0);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(new string('-', Init.LineWidth - 1));
        // Over the pane with the focus in a split
        var center = facts.Other == null ? Init.LineWidth / 2 : facts.FocusLeft + facts.FocusWidth / 2;
        Tools.SetCursorEx(row, Math.Max(0, center - (TextMetrics.WidthOf(label) / 2 + 1)));
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.EditorTitle);
        Console.Write(" " + label + " ");
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }

    /// <summary>
    /// The mode as the divider shows it
    /// </summary>
    internal static string ModeLabel(EditorFacts facts) => facts.Jumping ? DialogText.DividerJump : facts.Mode switch
    {
        EditorMode.Insert => DialogText.CaptionsEdit,
        EditorMode.Visual or EditorMode.VisualLine or EditorMode.VisualBlock => DialogText.DividerSelect,
        EditorMode.Command or EditorMode.Search => DialogText.DividerCommand,
        _ => DialogText.DividerNavigate
    };

    /// <summary>
    /// The parts of the status line: the mode (highlighted, with a blank space on each side inside the highlight:
    /// " NORMAL "), the gap after it, the message, the dashes after it, and what is at the right
    /// </summary>
    internal readonly record struct StatusParts(string Label, string Gap, string Text, string Filler, string Right);

    /// <summary>
    /// The status line in a width (not while a command is typed): the mode, a message, and at the right the keys
    /// pending, REC, the search count, the string memory, the spell check, the kind of the file, the line and
    /// column and how far down the text the cursor is. What does not fit is left out: a message is cut, and the
    /// right part is only the line and column, or nothing
    /// </summary>
    internal static StatusParts Status(EditorFacts facts, int width)
    {
        var label = facts.Mode switch
        {
            EditorMode.Insert => DialogText.EditorModeInsert,
            EditorMode.Visual => DialogText.EditorModeVisual,
            EditorMode.VisualLine => DialogText.EditorModeVisualLine,
            EditorMode.VisualBlock => DialogText.EditorModeVisualBlock,
            _ => DialogText.EditorModeNormal
        };

        // The mode is highlighted, with a blank space on each side inside the highlight (" NORMAL ")
        label = " " + label + " ";

        // A message follows the blank space after the mode (" NORMAL  Saved ---"). An error is highlighted, with a
        // blank space on each side inside the highlight
        var text = facts.Message.Length > 0
            ? (facts.MessageIsError ? " " : string.Empty) + facts.Message + " "
            : string.Empty;

        // Pending keys, string memory, line:column and position in the document
        var position = facts.Line + ":" + facts.Column;
        var right = " ";
        if (facts.PendingKeys.Length > 0) right += facts.PendingKeys + " | ";
        // A macro is being recorded (English, as the names of the modes)
        if (facts.Recording) right += "REC | ";
        if (facts.SearchCount.Length > 0) right += facts.SearchCount + " | ";
        if (facts.LineMemory.Length > 0) right += facts.LineMemory + " | ";
        if (facts.Spell.Length > 0) right += facts.Spell + " | ";
        if (facts.FileLabel.Length > 0) right += facts.FileLabel + " | ";
        right += position + " | " + facts.Percent + "% ";

        // A blank space between the mode and the line, as a break in the line
        const string gap = " ";
        // A message comes before what is at the right (it is shown until the next key): if it does not fit, only
        // the line and column are kept, and else nothing
        var room = width - label.Length - gap.Length;
        // (all that is at the right can be wider than the line too, in a narrow window or a long language: then
        // the line and column are kept, as with a message)
        if (TextMetrics.WidthOf(right) > room) right = " " + position + " ";
        if (TextMetrics.WidthOf(right) > room) right = string.Empty;
        if (text.Length > 0 && TextMetrics.WidthOf(text) > room - right.Length)
        {
            right = " " + position + " ";
            if (TextMetrics.WidthOf(text) > room - right.Length) right = string.Empty;
        }

        // Make it fit
        var available = room - right.Length;
        while (text.Length > 0 && TextMetrics.WidthOf(text) > available)
        {
            text = text[..^TextMetrics.PreviousElementLength(text, text.Length)];
        }

        var filler = new string('-', Math.Max(0, available - TextMetrics.WidthOf(text)));
        return new StatusParts(label, gap, text, filler, right);
    }

    // The bottom line of the window: mode, messages or command line to the left and cursor position to the right
    internal override void DrawStatus(EditorFacts facts)
    {
        if (facts.Mode is EditorMode.Command or EditorMode.Search)
        {
            DrawCommandLine(facts);
            return;
        }

        var parts = Status(facts, Init.LineWidth - 1);
        var status = parts.Label + parts.Gap + "\u0001" + parts.Text + "\u0001" + parts.Filler + parts.Right +
                     facts.MessageIsError + facts.MessageIsCommandError;
        if (status == ShownStatus) return;
        ShownStatus = status;

        Tools.SetCursorEx(Init.LineCount - 1, 0);
        // The mode label has a color of its own for each mode
        ConsoleColors.SetColors(ModeColor(facts));
        Console.Write(parts.Label);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Console.Write(parts.Gap);
        ConsoleColors.SetColors(facts.MessageIsCommandError
            ? ConsoleColors.ColorPreset.CommandErrorInverted
            : facts.MessageIsError
                ? ConsoleColors.ColorPreset.AttentionInverted
                : ConsoleColors.ColorPreset.Normal);
        Console.Write(parts.Text);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Divider);
        Console.Write(parts.Filler + parts.Right);
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
    }
}
