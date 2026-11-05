namespace Marbles;

/// <summary>
/// ":surround /bold": the selection with a completion around it (see Completions). A completion that has "$0" in it
/// (the place of the cursor when it is typed) is a frame: the text goes in place of "$0", so "**$0**" makes the
/// selection bold and "[$0]()" makes it a link. Given in a Visual mode (":" makes it ":'&lt;,'&gt;surround"):
///   v       the text selected
///   V       each line by itself, or all the lines at once with a completion of several lines (a block of code)
///   Q       the part of each line in the block
/// Without a selection it surrounds the lines of the range (the line of the cursor)
/// </summary>
internal sealed partial class EditorEngine
{
    private void Surround(int first, int last, bool fromSelection, string word)
    {
        var mode = fromSelection ? _commandSelectionMode : EditorMode.VisualLine;
        _commandSelectionMode = EditorMode.Normal;
        if (SingleLine || Buffer.RefuseIfReadOnly()) return;
        if (word.Length == 0)
        {
            SetError(Lang.T("Which completion? As :surround /bold (Tab shows them)"));
            return;
        }

        var item = Completions.Named(word, Syntax != SyntaxType.None ? Syntax : CompletionSyntax);
        if (item == null)
        {
            SetError(Lang.F("There is no completion {0} for this text", word));
            return;
        }

        var frame = TextBuffer.Normalize(item.Text).Replace("\t", new string(' ', TabWidth));
        if (!frame.Contains(Snippets.CursorMark, StringComparison.Ordinal))
        {
            SetError(Lang.F("{0} has no place for the text ($0)", item.Word));
            return;
        }

        var severalLines = frame.Contains(TextBuffer.NewLine);
        switch (mode)
        {
            case EditorMode.Visual:
            {
                // The text selected (the last character included). Line breaks at its start or end stay outside
                // the frame (a selection from the end of a line, or onto an empty line)
                var from = _commandSelectionFrom;
                var to = _commandSelectionTo;
                to = to.Col < Buffer.LineLength(to.Line)
                    ? new TextPos(to.Line, to.Col + TextMetrics.ElementLength(Buffer[to.Line], to.Col))
                    : new TextPos(to.Line, Buffer.LineLength(to.Line));
                var text = Buffer.GetText(from, to);
                var start = 0;
                while (start < text.Length && text[start] == TextBuffer.NewLine) start++;
                var end = text.Length;
                while (end > start && text[end - 1] == TextBuffer.NewLine) end--;
                if (end == start)
                {
                    SetError(Lang.T("Nothing to surround"));
                    return;
                }

                var origin = Buffer.ToIndex(from);
                to = Buffer.FromIndex(origin + end);
                from = Buffer.FromIndex(origin + start);
                text = text[start..end];
                var framed = Framed(frame, text, Indent(from.Line));
                Delete(from, to);
                Cursor = Left(Insert(from, framed), 1);
                break;
            }
            case EditorMode.VisualBlock:
            {
                if (severalLines)
                {
                    SetError(Lang.T("A block is surrounded line by line: use a completion of one line"));
                    return;
                }

                var (top, bottom, left, right) = _commandBlock;
                var any = false;
                for (var line = top; line <= bottom; line++)
                {
                    // The part of the line in the block, without the blank space at its end
                    var (start, end) = BlockRange(line, left, right);
                    var part = Buffer[line][start..end].TrimEnd();
                    if (part.Length == 0) continue;
                    Delete(new TextPos(line, start), new TextPos(line, start + part.Length));
                    Insert(new TextPos(line, start), Framed(frame, part, string.Empty));
                    any = true;
                }

                if (!any) SetError(Lang.T("Nothing to surround"));

                Cursor = new TextPos(top, BlockRange(top, left, right).Start);
                break;
            }
            default:
            {
                if (severalLines)
                {
                    // All the lines at once (a block of code around them), indented as the first; the lines keep
                    // their own indent
                    var from = new TextPos(first, 0);
                    var to = new TextPos(last, Buffer.LineLength(last));
                    var indent = Indent(first);
                    var text = Buffer.GetText(from, to)[indent.Length..];
                    var framed = indent + Framed(frame, text, indent);
                    Delete(from, to);
                    Insert(from, framed);
                }
                else
                {
                    // Each line by itself, after its indent (empty lines are left as they are)
                    var any = false;
                    for (var line = first; line <= last; line++)
                    {
                        var indent = Indent(line);
                        var text = Buffer[line][indent.Length..].TrimEnd();
                        if (text.Length == 0) continue;
                        Delete(new TextPos(line, indent.Length), new TextPos(line, Buffer.LineLength(line)));
                        Insert(new TextPos(line, indent.Length), Framed(frame, text, indent));
                        any = true;
                    }

                    if (!any) SetError(Lang.T("Nothing to surround"));
                }

                Cursor = new TextPos(first, FirstNonBlank(first));
                break;
            }
        }

        ClampCursor();
        EnsureCursorVisible();
    }

    // A completion with the text in place of its "$0" (the lines after the first indented as the text is)
    private static string Framed(string frame, string text, string indent)
    {
        var (prepared, mark) = Snippets.Prepare(frame, indent);
        return prepared.Insert(mark, text);
    }

    // The blank space a line starts with
    private string Indent(int line)
    {
        var text = Buffer[line];
        return text[..(text.Length - text.TrimStart().Length)];
    }
}
