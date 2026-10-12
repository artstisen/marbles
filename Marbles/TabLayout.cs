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
    /// The next page (or the one before) of a number of pages, going round from the last to the first
    /// </summary>
    internal static int Next(int page, int count, bool back)
    {
        if (count <= 0) return 0;
        return (page + (back ? count - 1 : 1)) % count;
    }
}
