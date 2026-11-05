using System.Text.RegularExpressions;

namespace Marbles;

/// <summary>
/// Marks in the editor, as in Vim: "ma" sets mark a at the cursor (a to z), "`a" jumps to its place and "'a" to the
/// start of its line, and "``" (or "''") jumps back to where the cursor was before the last jump. The marks are kept
/// by the text (TextMarks), so they follow its changes, and they are kept in memory only (never written to a file or
/// the log): the host keeps them while Marbles runs. ":mark a" sets a mark, ":marks" lists them (the host shows the
/// list) and ":delmarks a" deletes one
/// </summary>
internal sealed partial class EditorEngine
{
    /// <summary>
    /// "'a" jumps to the place of mark a, as "`a" (and "''" back to the place before the jump), not to the start of its
    /// line: for keyboards where ` is hard to type, or a dead key (setting "marksapostrophe", off: as in Vim)
    /// </summary>
    internal static bool MarksApostrophe { get; set; }

    private TextMarks Marks => Buffer.Marks;

    // "m" and a letter: the mark is set at the cursor
    private void SetMark(char name)
    {
        if (!TextMarks.IsName(name))
        {
            SetError(Lang.T("A mark is a letter: ma to mz"));
            return;
        }

        Marks.Set(name, Cursor);
        Message = Lang.F("Mark {0}: line {1}", name, Cursor.Line + 1);
    }

    // "`a" (exact: its place) or "'a" (the start of its line); "``" and "''" go back to the place before the last jump
    private void JumpToMark(char name, bool exact)
    {
        var back = name is TextMarks.Back or '\'';
        if (!back && !TextMarks.IsName(name))
        {
            SetError(Lang.T("A mark is a letter: ma to mz"));
            return;
        }

        if (Marks.Get(back ? TextMarks.Back : name) is not { } at)
        {
            SetError(back ? Lang.T("No jump to go back from yet") : Lang.F("Mark {0} is not set (m{0} sets it)", name));
            return;
        }

        at = Buffer.Clamp(at);
        var from = Cursor;
        Cursor = exact ? at : new TextPos(at.Line, FirstNonBlank(at.Line));
        ClampCursor();
        RememberJump(from);
    }

    // A jump (a mark, G, gg, a search, a line by its number): "``" goes back to where it was made from
    private void RememberJump(TextPos from)
    {
        if (from != Cursor) Marks.Set(TextMarks.Back, from);
    }

    /// <summary>
    /// Go to a place in the text as a jump ("``" goes back): a bookmark of a place in the file being edited
    /// </summary>
    internal void JumpToIndex(int index)
    {
        var from = Cursor;
        SetCursorIndex(index);
        RememberJump(from);
    }

    /// <summary>
    /// The marks a to z that are set, with the text of their lines (for ":marks")
    /// </summary>
    internal IReadOnlyList<(char Name, TextPos At)> MarkList =>
        Marks.Letters.Select(mark => (mark.Name, Buffer.Clamp(mark.At))).ToList();

    /// <summary>
    /// Jump to a mark picked in the list of ":marks"
    /// </summary>
    internal void JumpToMark(char name) => JumpToMark(name, true);

    /// <summary>
    /// Delete a mark (from the list of ":marks")
    /// </summary>
    internal bool DeleteMark(char name) => Marks.Remove(name);

    /// <summary>
    /// The name of the first mark on a line, or '\0' (shown in place of its number)
    /// </summary>
    internal char MarkOnLine(int line) => Marks.NameOnLine(line);

    /// <summary>
    /// The marks kept by the text, and set again when the file is opened later in the session
    /// </summary>
    internal Dictionary<char, TextPos> CopyMarks() => Marks.Copy();

    internal void LoadMarks(IReadOnlyDictionary<char, TextPos> marks) => Marks.Load(marks);

    // ":mark a" (or ":k a") sets a mark, ":marks" lists them, ":delmarks a b" (or "a-c", "!" all) deletes them. False
    // if the command is none of them
    private bool MarkCommand(string command)
    {
        if (command == "marks")
        {
            if (!SingleLine) _request = EditorRequest.Marks;
            return true;
        }

        var set = Regex.Match(command, "^(?:mark|k)(?: +(.*))?$");
        if (set.Success)
        {
            var name = set.Groups[1].Value.Trim();
            if (name.Length == 1) SetMark(name[0]);
            else SetError(Lang.T("A mark is a letter: ma to mz"));
            return true;
        }

        var delete = Regex.Match(command, "^delmarks(!)?(?: +(.*))?$");
        if (!delete.Success) return false;
        var names = new List<char>();
        if (delete.Groups[1].Success)
        {
            names.AddRange(Marks.Letters.Select(mark => mark.Name));
        }
        else
        {
            foreach (var part in delete.Groups[2].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (part.Length == 3 && part[1] == '-' && TextMarks.IsName(part[0]) && TextMarks.IsName(part[2]))
                {
                    for (var c = part[0]; c <= part[2]; c++) names.Add(c);
                }
                else if (part.Length == 1 && TextMarks.IsName(part[0]))
                {
                    names.Add(part[0]);
                }
                else
                {
                    SetError(Lang.T("Marks to delete: :delmarks a b, a-c, or :delmarks! (all)"));
                    return true;
                }
            }
        }

        if (names.Count == 0)
        {
            SetError(Lang.T("Marks to delete: :delmarks a b, a-c, or :delmarks! (all)"));
            return true;
        }

        var deleted = names.Count(Marks.Remove);
        Message = Lang.F("Marks deleted: {0}", deleted);
        return true;
    }
}
