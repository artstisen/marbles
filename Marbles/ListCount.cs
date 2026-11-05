namespace Marbles;

/// <summary>
/// A count typed in front of a key in a list, as in the editor: 5j and 5k move 5 rows down and up, 5gg and 5G go to
/// row 5 (the rows shown, as numbered at the bottom right). The digits are kept until the next key, which takes the
/// count (a key that has no use for it drops it). A 0 is only a digit after another digit.
///
/// The lists of the file browser (favorites, history, hotlist, search results) and the Jump menu use it. This class
/// does not touch the console.
/// </summary>
internal sealed class ListCount
{
    // The most a count can be (more digits are not added)
    private const int Max = 99999;

    private int _count;

    /// <summary>
    /// The count typed so far, to show (empty: none)
    /// </summary>
    internal string Shown => _count > 0 ? _count.ToString() : string.Empty;

    /// <summary>
    /// A key that is a digit of the count is added to it
    /// </summary>
    /// <returns>True if the key was a digit of the count (it has no other use then)</returns>
    internal bool Add(ConsoleKeyInfo key)
    {
        var digit = key.KeyChar - '0';
        if (key.KeyChar is < '0' or > '9' || digit == 0 && _count == 0 ||
            (key.Modifiers & (ConsoleModifiers.Control | ConsoleModifiers.Alt)) != 0)
        {
            return false;
        }

        _count = Math.Min(Max, _count * 10 + digit);
        return true;
    }

    /// <summary>
    /// The count typed before a key, which is gone after it
    /// </summary>
    /// <returns>The count, or null if none was typed</returns>
    internal int? Take()
    {
        var count = _count;
        _count = 0;
        return count > 0 ? count : null;
    }

    /// <summary>
    /// Where the selection goes with gg (first: true) or G and a count: to that row (from 1), or the first or the last
    /// row without one. Past the end: the last row
    /// </summary>
    /// <returns>The place in the list (from 0)</returns>
    internal static int Row(int? count, int rows, bool first) =>
        rows == 0 ? 0 : count is { } row ? Math.Min(row, rows) - 1 : first ? 0 : rows - 1;
}
