namespace Marbles;

/// <summary>
/// The layout of a frame with tabs (see TabFrame): the tabs on the top of the frame, the top line of the frame
/// (open below the tab shown), and the row of sub-tabs inside it. Positions are counted from the left corner of
/// the frame. It does not touch the console, so it can be tested.
///
///   .------. .---------------.
///   | Mode | | Configuration |
///  .'------'-'               '-----------------.
///  |  Page 1    Page 2                          |
/// </summary>
internal static class TabLayout
{
    internal const int TabGap = 1; // The blank columns between two tabs

    // A tab takes its name and four more columns ("| " and " |")
    private static int TabWidth(string name) => TextMetrics.WidthOf(name) + 4;

    /// <summary>
    /// The names of the tabs, shortened if they do not all fit in the width of the frame (the longest first)
    /// </summary>
    internal static List<string> Fit(IReadOnlyList<string> names, int width)
    {
        var fitted = names.ToList();
        int Total() => 1 + fitted.Sum(TabWidth) + TabGap * Math.Max(0, fitted.Count - 1) + 1;
        while (fitted.Count > 0 && Total() > width)
        {
            var longest = 0;
            for (var i = 1; i < fitted.Count; i++)
            {
                if (TextMetrics.WidthOf(fitted[i]) > TextMetrics.WidthOf(fitted[longest])) longest = i;
            }

            var name = fitted[longest];
            if (name.Length <= 1) break;
            fitted[longest] = name[..^TextMetrics.PreviousElementLength(name, name.Length)];
        }

        return fitted;
    }

    /// <summary>
    /// The two rows of the tabs: their tops (".------.") and their names ("| Mode |"), and where each name starts
    /// </summary>
    internal static (string Tops, string Names, List<int> NameStarts) Tabs(IReadOnlyList<string> names, char corner)
    {
        var tops = new System.Text.StringBuilder(" ");
        var labels = new System.Text.StringBuilder(" ");
        var starts = new List<int>();
        var column = 1;
        for (var i = 0; i < names.Count; i++)
        {
            if (i > 0)
            {
                tops.Append(' ', TabGap);
                labels.Append(' ', TabGap);
                column += TabGap;
            }

            var width = TabWidth(names[i]);
            tops.Append(corner).Append('-', width - 2).Append(corner);
            labels.Append("| ").Append(names[i]).Append(" |");
            starts.Add(column + 2);
            column += width;
        }

        return (tops.ToString(), labels.ToString(), starts);
    }

    /// <summary>
    /// The corner of a tab: where its left side is in the lines of the tabs (Tabs), and how long its corner and
    /// top are without the corner at its right end. It can have a color of its own in the tab shown
    /// </summary>
    internal static (int Index, int Length) Corner(IReadOnlyList<string> names, IReadOnlyList<int> nameStarts, int tab)
    {
        return (nameStarts[tab] - 2, TabWidth(names[tab]) - 1);
    }

    /// <summary>
    /// The top line of the frame: a corner at each end, closed below the tabs that are not shown, and open
    /// below the one that is
    /// </summary>
    internal static string Edge(IReadOnlyList<string> names, int selected, int width, char top, char bottom)
    {
        var line = new char[Math.Max(2, width)];
        Array.Fill(line, '-');
        line[0] = top;
        line[^1] = top;
        var column = 1;
        for (var i = 0; i < names.Count; i++)
        {
            var tabWidth = TabWidth(names[i]);
            if (column + tabWidth > line.Length - 1) break;
            line[column] = bottom;
            line[column + tabWidth - 1] = bottom;
            if (i == selected)
            {
                for (var c = column + 1; c < column + tabWidth - 1; c++) line[c] = ' ';
            }

            column += tabWidth + TabGap;
        }

        return new string(line);
    }

    /// <summary>
    /// The row of sub-tabs: each name with a blank space on each side (the one shown is highlighted there), and
    /// two between them. Returns where each name starts (counted from the start of the row)
    /// </summary>
    internal static List<int> SubTabStarts(IReadOnlyList<string> names)
    {
        var starts = new List<int>();
        var column = 0;
        foreach (var name in names)
        {
            starts.Add(column + 1);
            column += TextMetrics.WidthOf(name) + 2 + 2;
        }

        return starts;
    }

    /// <summary>
    /// What a key does to the sub-tabs of a tab: 1 for the next (l, Right), -1 for the one before (h, Left), 0 for
    /// nothing. On a page that uses h and l itself (a grid the cursor moves in) only H and L do it
    /// </summary>
    internal static int SubTabStep(ConsoleKeyInfo key, bool sideKeysTaken)
    {
        if ((key.Modifiers & (ConsoleModifiers.Control | ConsoleModifiers.Alt)) != 0) return 0;
        if (key.KeyChar == 'L') return 1;
        if (key.KeyChar == 'H') return -1;
        if (sideKeysTaken) return 0;
        if (key.KeyChar == 'l' || key.Key == ConsoleKey.RightArrow) return 1;
        if (key.KeyChar == 'h' || key.Key == ConsoleKey.LeftArrow) return -1;
        return 0;
    }

    /// <summary>
    /// The next page (or the one before) of a number of pages, going round from the last to the first
    /// </summary>
    internal static int Next(int page, int count, bool back)
    {
        if (count <= 0) return 0;
        return (page + (back ? count - 1 : 1)) % count;
    }
}
