using System.Text;

namespace Marbles;

/// <summary>
/// The comparison of two texts line by line (":diff"), as "diff" and "git diff" do it (the algorithm of Myers):
/// the fewest lines removed and added that make the old text the new one. The result is written as a unified
/// diff: the lines that differ with three unchanged lines around them, "-" in front of a removed line and "+"
/// in front of an added one, and a line "@@ -12,7 +12,8 @@" in front of each part (where it is in each text).
///
/// This class does not touch the console: the comparison is shown by DiffView.
/// </summary>
internal static class LineDiff
{
    internal enum Kind
    {
        Same,
        Removed, // Only in the old text
        Added // Only in the new text
    }

    /// <summary>
    /// A line of the comparison: what it is, its text, and its number in the old and the new text (counted from
    /// 0; -1 if it is not in that text)
    /// </summary>
    internal readonly record struct Line(Kind Kind, string Text, int Old, int New);

    /// <summary>
    /// The most lines that can differ (removed and added): more than that is not compared, as it takes too long
    /// </summary>
    internal const int MaxDifferences = 2000;

    /// <summary>
    /// The unchanged lines shown before and after the lines that differ
    /// </summary>
    internal const int Context = 3;

    /// <summary>
    /// Compare two texts, given as their lines
    /// </summary>
    /// <param name="oldLines">The lines of the old text</param>
    /// <param name="newLines">The lines of the new text</param>
    /// <param name="ignoreSpace">Lines that differ only in blank space are the same (":diff -w")</param>
    /// <returns>All the lines, in order, or null if more than MaxDifferences lines differ</returns>
    internal static List<Line>? Compare(IReadOnlyList<string> oldLines, IReadOnlyList<string> newLines,
        bool ignoreSpace)
    {
        // Each different line gets a number, so lines are compared as numbers
        var ids = new Dictionary<string, int>(StringComparer.Ordinal);
        int Id(string line)
        {
            var key = ignoreSpace ? Squeeze(line) : line;
            if (ids.TryGetValue(key, out var id)) return id;
            id = ids.Count;
            ids[key] = id;
            return id;
        }

        var a = oldLines.Select(Id).ToArray();
        var b = newLines.Select(Id).ToArray();
        // The lines the texts start and end with are the same: only the part between them is compared
        var start = 0;
        while (start < a.Length && start < b.Length && a[start] == b[start]) start++;
        var endA = a.Length;
        var endB = b.Length;
        while (endA > start && endB > start && a[endA - 1] == b[endB - 1])
        {
            endA--;
            endB--;
        }

        var middle = CompareMiddle(a, b, start, endA - start, endB - start);
        if (middle == null) return null;
        var lines = new List<Line>(Math.Max(a.Length, b.Length) + middle.Count);
        for (var i = 0; i < start; i++) lines.Add(new Line(Kind.Same, newLines[i], i, i));
        foreach (var (kind, oldIndex, newIndex) in middle)
        {
            lines.Add(kind switch
            {
                Kind.Removed => new Line(Kind.Removed, oldLines[oldIndex], oldIndex, -1),
                Kind.Added => new Line(Kind.Added, newLines[newIndex], -1, newIndex),
                _ => new Line(Kind.Same, newLines[newIndex], oldIndex, newIndex)
            });
        }

        for (var i = 0; i < a.Length - endA; i++) lines.Add(new Line(Kind.Same, newLines[endB + i], endA + i, endB + i));
        return lines;
    }

    // Blank space does not count: runs of it are one space, and none at the ends
    private static string Squeeze(string line)
    {
        var result = new StringBuilder(line.Length);
        var space = false;
        foreach (var c in line.Trim())
        {
            if (char.IsWhiteSpace(c))
            {
                space = true;
                continue;
            }

            if (space) result.Append(' ');
            space = false;
            result.Append(c);
        }

        return result.ToString();
    }

    // The shortest way from a[start..start+n] to b[start..start+m] (Myers): the steps, with the numbers of the lines
    // in each text. Null if more than MaxDifferences lines differ
    private static List<(Kind Kind, int Old, int New)>? CompareMiddle(int[] a, int[] b, int start, int n, int m)
    {
        var limit = Math.Min(n + m, MaxDifferences);
        var offset = limit + 1;
        var v = new int[2 * limit + 3];
        Array.Fill(v, Unreachable);
        v[offset + 1] = 0;
        // The furthest x on each diagonal k (x - y) after each number of differences d (for k from -d to d)
        var trace = new List<int[]>();
        var found = -1;
        for (var d = 0; d <= limit && found < 0; d++)
        {
            for (var k = -d; k <= d; k += 2)
            {
                var step = Choose(kk => v[offset + kk], k, d, n, m);
                if (step == null)
                {
                    v[offset + k] = Unreachable;
                    continue;
                }

                var x = step.Value.X;
                var y = x - k;
                while (x < n && y < m && a[start + x] == b[start + y])
                {
                    x++;
                    y++;
                }

                v[offset + k] = x;
                if (x == n && y == m) found = d;
            }

            var row = new int[2 * d + 1];
            Array.Copy(v, offset - d, row, 0, row.Length);
            trace.Add(row);
        }

        if (found < 0) return null;

        // Back from the end: each difference, and the lines that are the same before it
        var steps = new List<(Kind, int, int)>();
        var cx = n;
        var cy = m;
        for (var d = found; d > 0; d--)
        {
            var before = trace[d - 1];
            var dBefore = d - 1;
            var kNow = cx - cy;
            var step = Choose(kk => Math.Abs(kk) <= dBefore ? before[kk + dBefore] : Unreachable, kNow, d, n, m)!.Value;
            var xBefore = step.FromX;
            var yBefore = xBefore - step.FromK;
            // The lines that are the same after the difference
            var xAfterStep = step.X;
            while (cx > xAfterStep)
            {
                cx--;
                cy--;
                steps.Add((Kind.Same, start + cx, start + cy));
            }

            if (step.FromK == kNow + 1)
            {
                // Down: a line of the new text is added
                cy--;
                steps.Add((Kind.Added, -1, start + cy));
            }
            else
            {
                // Right: a line of the old text is removed
                cx--;
                steps.Add((Kind.Removed, start + cx, -1));
            }

            System.Diagnostics.Debug.Assert(cx == xBefore && cy == yBefore);
        }

        while (cx > 0 && cy > 0)
        {
            cx--;
            cy--;
            steps.Add((Kind.Same, start + cx, start + cy));
        }

        steps.Reverse();
        return steps;
    }

    // A diagonal that cannot be reached with the differences so far
    private const int Unreachable = -1;

    // The step onto diagonal k with d differences: down from diagonal k + 1 (a line added) or right from k - 1 (a
    // line removed), the one that gets furthest without leaving the texts. X is where the step ends (before the
    // lines that are the same after it). Null if neither can be taken
    private static (int X, int FromK, int FromX)? Choose(Func<int, int> furthest, int k, int d, int n, int m)
    {
        (int X, int FromK, int FromX)? down = null;
        (int X, int FromK, int FromX)? right = null;
        // Down from k + 1 (not on the highest diagonal, which only the step right reaches)
        if (k < d || d == 0)
        {
            var from = furthest(k + 1);
            if (from != Unreachable && from - k <= m) down = (from, k + 1, from);
        }

        // Right from k - 1 (not on the lowest diagonal)
        if (k > -d)
        {
            var from = furthest(k - 1);
            if (from != Unreachable && from + 1 <= n) right = (from + 1, k - 1, from);
        }

        if (down == null) return right;
        if (right == null) return down;
        return down.Value.X >= right.Value.X ? down : right;
    }

    /// <summary>
    /// The comparison written as a unified diff, and for each of its lines the place it shows
    /// </summary>
    /// <param name="Text">The text: the two lines naming the texts, then the parts that differ</param>
    /// <param name="Changes">The lines of the text where the changes start (the first line removed or added)</param>
    /// <param name="Places">For each line of the text: in which text the line is (the old one or the new one),
    /// its number there, and the nearest line of the new text</param>
    /// <param name="Added">The number of lines added</param>
    /// <param name="Removed">The number of lines removed</param>
    internal sealed record Unified(string Text, List<int> Changes, List<(bool InOld, int Line, int NewLine)> Places,
        int Added, int Removed);

    /// <summary>
    /// Write a comparison as a unified diff
    /// </summary>
    /// <param name="lines">The lines of the comparison (Compare)</param>
    /// <param name="oldName">How the old text is named (after "---")</param>
    /// <param name="newName">How the new text is named (after "+++")</param>
    internal static Unified Format(List<Line> lines, string oldName, string newName)
    {
        var text = new StringBuilder();
        var changes = new List<int>();
        var places = new List<(bool, int, int)>();
        var row = 0;

        void Add(string line, (bool, int, int) place)
        {
            if (row > 0) text.Append('\n');
            text.Append(line);
            places.Add(place);
            row++;
        }

        // The nearest line of the new text for each line (a removed line: the next one of the new text)
        var newNear = new int[lines.Count];
        var next = lines.Count > 0 ? Math.Max(0, lines.Max(line => line.New)) : 0;
        for (var i = lines.Count - 1; i >= 0; i--)
        {
            if (lines[i].New >= 0) next = lines[i].New;
            newNear[i] = next;
        }

        Add("--- " + oldName, (true, 0, 0));
        Add("+++ " + newName, (false, 0, 0));
        var added = 0;
        var removed = 0;
        var index = 0;
        while (index < lines.Count)
        {
            // The next line that differs, and the part around it (parts closer than twice the context are one)
            var first = lines.FindIndex(index, line => line.Kind != Kind.Same);
            if (first < 0) break;
            var last = first;
            while (true)
            {
                var following = lines.FindIndex(last + 1, line => line.Kind != Kind.Same);
                if (following < 0 || following - last > 2 * Context) break;
                last = following;
            }

            var from = Math.Max(index, first - Context);
            var to = Math.Min(lines.Count - 1, last + Context);
            var part = lines.GetRange(from, to - from + 1);
            Add(PartHeader(lines, from, part), (false, newNear[first], newNear[first]));
            for (var i = from; i <= to; i++)
            {
                var line = lines[i];
                var isChange = line.Kind != Kind.Same;
                if (isChange && (i == from || lines[i - 1].Kind == Kind.Same)) changes.Add(row);
                switch (line.Kind)
                {
                    case Kind.Removed:
                        removed++;
                        Add("-" + line.Text, (true, line.Old, newNear[i]));
                        break;
                    case Kind.Added:
                        added++;
                        Add("+" + line.Text, (false, line.New, line.New));
                        break;
                    default:
                        Add(" " + line.Text, (false, line.New, line.New));
                        break;
                }
            }

            index = to + 1;
        }

        return new Unified(text.ToString(), changes, places, added, removed);
    }

    // "@@ -12,7 +12,8 @@": where a part starts in each text (from 1), and how many lines of each it has. A part
    // without lines of a text tells the line before it
    private static string PartHeader(List<Line> lines, int from, List<Line> part)
    {
        var oldCount = part.Count(line => line.Kind != Kind.Added);
        var newCount = part.Count(line => line.Kind != Kind.Removed);
        var oldBefore = lines.Take(from).Count(line => line.Kind != Kind.Added);
        var newBefore = lines.Take(from).Count(line => line.Kind != Kind.Removed);
        var oldStart = oldCount == 0 ? oldBefore : oldBefore + 1;
        var newStart = newCount == 0 ? newBefore : newBefore + 1;
        return "@@ -" + oldStart + "," + oldCount + " +" + newStart + "," + newCount + " @@";
    }
}
