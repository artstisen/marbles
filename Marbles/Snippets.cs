using System.Text;

namespace Marbles;

/// <summary>
/// The snippets of the editor (":snip"): pieces of text (code) with a title, inserted at the cursor.
///
/// They are kept in the file "snippets.txt" in the data directory, which can be edited by hand (":snip edit").
/// A line starting with "===" starts a snippet, and the rest of the line is its title. The lines after it, up to
/// the next title, are the snippet (blank lines at its end are not part of it). A line of a snippet that starts
/// with "===" is written with a "\" in front of it. The text above the first title is not used, and is kept.
///
/// A tag at the end of a title, like "(html)" or "(css)", tells the kind of text the snippet is for: such
/// snippets are shown first when that kind of text is edited. "$0" in a snippet marks where the cursor is placed
/// after it is inserted.
///
/// This class does not touch the console: the box that shows the snippets is SnippetBox.
/// </summary>
internal static class Snippets
{
    internal const string FileName = "snippets.txt";

    /// <summary>
    /// Where the cursor is placed after the snippet is inserted
    /// </summary>
    internal const string CursorMark = "$0";

    private const string TitleMark = "===";

    /// <summary>
    /// A snippet, and the line of its title in the file (counted from 0)
    /// </summary>
    internal sealed record Snippet(string Title, string Text, int Line);

    /// <summary>
    /// The order of the snippets in the box (setting "snipsort")
    /// </summary>
    internal enum SortOrder
    {
        File, // As in the file
        Title, // A-Z
        Used // The last inserted first (in this session), then as in the file
    }

    internal static SortOrder Sort { get; set; } = SortOrder.File;

    private static readonly List<Snippet> List = new();

    // The text above the first title
    private static string _preamble = string.Empty;

    // When each snippet was last inserted (by title), in this session
    private static readonly Dictionary<string, long> Used = new(StringComparer.Ordinal);
    private static long _useCount;

    internal static IReadOnlyList<Snippet> Items => List;

    /// <summary>
    /// The file with the snippets. It can be set to another file than the one in the data directory (for testing)
    /// </summary>
    internal static string? FilePathOverride { get; set; }

    internal static string FilePath =>
        FilePathOverride ?? Path.Combine(FileIo.GetDataDirectoryName(), FileName);

    internal static bool Exists => File.Exists(FilePath);

    // The file made when there is none: how it works, and two examples (English: the file is not translated)
    private const string NewFileText =
        "Snippets for the editor of Marbles (type :snip in the editor).\n" + // English
        "\n" +
        "Each snippet starts with a line \"=== \" and its title. The lines after\n" + // English
        "it, up to the next title, are the snippet. A tag at the end of the\n" + // English
        "title, like (html) or (css), shows the snippet first in the list when\n" + // English
        "that kind of text is edited. $0 marks where the cursor is placed when\n" + // English
        "the snippet has been inserted. A line of a snippet that starts with ===\n" + // English
        "is written with a \\ in front of it. The text above the first title is\n" + // English
        "not used.\n" + // English
        "\n" +
        "=== HTML page (html)\n" + // English
        "<!DOCTYPE html>\n" +
        "<html lang=\"en\">\n" +
        "<head>\n" +
        "    <meta charset=\"utf-8\">\n" +
        "    <title>$0</title>\n" +
        "</head>\n" +
        "<body>\n" +
        "</body>\n" +
        "</html>\n" +
        "\n" +
        "=== Center with flexbox (css)\n" + // English
        "display: flex;\n" +
        "justify-content: center;\n" +
        "align-items: center;\n";

    /// <summary>
    /// Read the snippets from the file
    /// </summary>
    /// <returns>DialogText.Success or an error message</returns>
    internal static string Load()
    {
        try
        {
            Parse(File.ReadAllText(FilePath, Encoding.UTF8));
            return DialogText.Success;
        }
        catch (Exception e)
        {
            List.Clear();
            _preamble = string.Empty;
            return e.Message;
        }
    }

    internal static void Parse(string text)
    {
        List.Clear();
        var lines = TextBuffer.Normalize(text).Split('\n');
        var preamble = new List<string>();
        string? title = null;
        var titleLine = 0;
        var body = new List<string>();

        void Finish()
        {
            if (title == null) return;
            // Blank lines at the end are not part of the snippet
            while (body.Count > 0 && body[^1].Trim().Length == 0) body.RemoveAt(body.Count - 1);
            List.Add(new Snippet(title, string.Join("\n", body), titleLine));
            body.Clear();
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.StartsWith(TitleMark, StringComparison.Ordinal))
            {
                Finish();
                title = line[TitleMark.Length..].Trim();
                titleLine = i;
                continue;
            }

            // "\===" is a line of a snippet starting with "===" (and "\\===" one starting with "\===" ...)
            if (line.StartsWith('\\') && line.TrimStart('\\').StartsWith(TitleMark, StringComparison.Ordinal))
            {
                line = line[1..];
            }

            if (title == null) preamble.Add(line);
            else body.Add(line);
        }

        Finish();
        while (preamble.Count > 0 && preamble[^1].Trim().Length == 0) preamble.RemoveAt(preamble.Count - 1);
        _preamble = string.Join("\n", preamble);
    }

    /// <summary>
    /// The text of the file: the text above the first title, and the snippets with a blank line between them
    /// </summary>
    internal static string Format()
    {
        var text = new StringBuilder();
        if (_preamble.Length > 0) text.Append(_preamble).Append("\n\n");
        for (var i = 0; i < List.Count; i++)
        {
            if (i > 0) text.Append('\n');
            text.Append(TitleMark).Append(' ').Append(List[i].Title).Append('\n');
            if (List[i].Text.Length == 0) continue;
            foreach (var line in List[i].Text.Split('\n'))
            {
                if (line.TrimStart('\\').StartsWith(TitleMark, StringComparison.Ordinal)) text.Append('\\');
                text.Append(line).Append('\n');
            }
        }

        return text.ToString();
    }

    /// <summary>
    /// Write the snippets to the file (and read them again, so the lines of the titles are the ones in the file)
    /// </summary>
    /// <returns>DialogText.Success or an error message</returns>
    internal static string Save()
    {
        return Write(Format());
    }

    /// <summary>
    /// Make the file, with two examples
    /// </summary>
    /// <returns>DialogText.Success or an error message</returns>
    internal static string Create()
    {
        return Write(NewFileText);
    }

    private static string Write(string text)
    {
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(FilePath));
            if (directory != null) Directory.CreateDirectory(directory);
            FileIo.WriteAllBytesSafe(FilePath, new UTF8Encoding(false).GetBytes(text));
            Parse(text);
            return DialogText.Success;
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }

    /// <summary>
    /// The kind of text a snippet is for, from the tag at the end of its title ("(css)"). None if it has none
    /// </summary>
    internal static SyntaxType Tag(string title)
    {
        title = title.TrimEnd();
        if (!title.EndsWith(')')) return SyntaxType.None;
        var open = title.LastIndexOf('(');
        return open < 0 ? SyntaxType.None : SyntaxHighlighter.ParseType(title[(open + 1)..^1]);
    }

    /// <summary>
    /// True if another snippet than the one at the index has the title
    /// </summary>
    internal static bool TitleExists(string title, int except = -1)
    {
        for (var i = 0; i < List.Count; i++)
        {
            if (i != except && string.Equals(List[i].Title, title.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Add a snippet at the end. False if the title is empty or taken
    /// </summary>
    internal static bool Add(string title, string text)
    {
        title = title.Trim();
        if (title.Length == 0 || TitleExists(title)) return false;
        // Blank lines at the end are not kept in the file
        var lines = TextBuffer.Normalize(text).Split('\n').ToList();
        while (lines.Count > 0 && lines[^1].Trim().Length == 0) lines.RemoveAt(lines.Count - 1);
        List.Add(new Snippet(title, string.Join("\n", lines), -1));
        return true;
    }

    /// <summary>
    /// Give a snippet another title. False if the title is empty or taken
    /// </summary>
    internal static bool Rename(int index, string title)
    {
        title = title.Trim();
        if (index < 0 || index >= List.Count || title.Length == 0 || TitleExists(title, index)) return false;
        var old = List[index].Title;
        List[index] = List[index] with { Title = title };
        if (Used.Remove(old, out var used)) Used[title] = used;
        return true;
    }

    internal static void Delete(int index)
    {
        if (index >= 0 && index < List.Count) List.RemoveAt(index);
    }

    internal static int IndexOf(string title)
    {
        return List.FindIndex(snippet => snippet.Title == title);
    }

    /// <summary>
    /// Remember that a snippet was inserted (for the order "last used")
    /// </summary>
    internal static void MarkUsed(string title)
    {
        Used[title] = ++_useCount;
    }

    /// <summary>
    /// Forget which snippets were used (for testing)
    /// </summary>
    internal static void ForgetUsed()
    {
        Used.Clear();
        _useCount = 0;
    }

    /// <summary>
    /// The snippets to show (their indexes): those whose title has the filter in it, in the order asked for.
    /// The snippets for the kind of text being edited come first
    /// </summary>
    internal static List<int> View(string filter, SortOrder order, SyntaxType syntax)
    {
        var shown = Enumerable.Range(0, List.Count)
            .Where(i => filter.Length == 0 || List[i].Title.Contains(filter, StringComparison.OrdinalIgnoreCase));
        shown = order switch
        {
            SortOrder.Title => shown.OrderBy(i => List[i].Title, StringComparer.OrdinalIgnoreCase),
            SortOrder.Used => shown.OrderByDescending(i => Used.GetValueOrDefault(List[i].Title)),
            _ => shown
        };
        // (OrderBy keeps the order of those that are equal)
        if (syntax != SyntaxType.None) shown = shown.OrderBy(i => Tag(List[i].Title) == syntax ? 0 : 1);
        return shown.ToList();
    }

    /// <summary>
    /// The text to insert: the lines after the first are indented as the line of the cursor (the indent), and
    /// the cursor mark is taken out
    /// </summary>
    /// <returns>The text, and where the cursor goes in it (-1: no mark)</returns>
    internal static (string Text, int Cursor) Prepare(string text, string indent)
    {
        var lines = TextBuffer.Normalize(text).Split('\n');
        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].Length > 0) lines[i] = indent + lines[i];
        }

        var joined = string.Join("\n", lines);
        var mark = joined.IndexOf(CursorMark, StringComparison.Ordinal);
        if (mark >= 0) joined = joined.Remove(mark, CursorMark.Length);
        return (joined, mark);
    }

    /// <summary>
    /// Where a line starts in a text (counted in characters, with one for each line break)
    /// </summary>
    internal static int IndexOfLine(string text, int line)
    {
        var lines = TextBuffer.Normalize(text).Split('\n');
        var index = 0;
        for (var i = 0; i < Math.Min(line, lines.Length - 1); i++)
        {
            index += lines[i].Length + 1;
        }

        return index;
    }

    internal static string SortName(SortOrder order)
    {
        return order switch
        {
            SortOrder.Title => "title",
            SortOrder.Used => "used",
            _ => "file"
        };
    }

    internal static SortOrder ParseSort(string name)
    {
        return name.Trim().ToLowerInvariant() switch
        {
            "title" => SortOrder.Title,
            "used" => SortOrder.Used,
            _ => SortOrder.File
        };
    }
}
