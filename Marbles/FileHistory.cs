using System.Globalization;
using System.Text;

namespace Marbles;

/// <summary>
/// The history of the file browser: the files opened in the editor from the file browser, the last opened
/// first. A file opened again moves to the top. The list holds at most MaxCount files: when it is full, the
/// file opened longest ago is dropped.
///
/// The list is kept in the file "history.txt" in the data directory, with one line for each file: the time it
/// was last opened (yyyy-MM-dd HH:mm), a tab character and the path. When the file was closed in the editor, the
/// line and column of the cursor (as "12:5") and a tab character come before the path: the file is opened there
/// the next time. A file closed in a split (":split") also has the place of the cursor of the other pane after it
/// ("12:5|40:1", or "12:5|<40:1" when that pane was at the left), and is opened split again.
/// </summary>
internal static class FileHistory
{
    internal const string FileName = "history.txt";

    /// <summary>
    /// The most files in the list
    /// </summary>
    internal const int MaxCount = 100;

    private const string TimeFormat = "yyyy-MM-dd HH:mm";

    /// <summary>
    /// A file of the list: its path, when it was opened, and where the cursor was when it was closed (the line
    /// and column, from 1; 0 when it is not known)
    /// </summary>
    internal sealed record Entry(string Path, DateTime Opened, int Line = 0, int Column = 0, string Split = "");

    private static readonly List<Entry> List = new();

    /// <summary>
    /// The files, the last opened first
    /// </summary>
    internal static IReadOnlyList<Entry> Items => List;

    /// <summary>
    /// The file with the list. It can be set to another file than the one in the data directory (for testing)
    /// </summary>
    internal static string? FilePathOverride { get; set; }

    internal static string FilePath =>
        FilePathOverride ?? Path.Combine(FileIo.GetDataDirectoryName(), FileName);

    /// <summary>
    /// Read the list from the file
    /// </summary>
    internal static void Load()
    {
        List.Clear();
        _unreadable = false;
        try
        {
            if (File.Exists(FilePath)) Parse(File.ReadAllText(FilePath, Encoding.UTF8));
        }
        catch
        {
            // A list that cannot be read is empty, and the file is not written over until it is read (it may only be
            // locked for a moment, as by a virus scanner)
            _unreadable = true;
        }
    }

    // The file was there, but could not be read
    private static bool _unreadable;

    internal static void Parse(string text)
    {
        List.Clear();
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            var tab = line.IndexOf('\t');
            if (tab < 0) continue;
            var path = line[(tab + 1)..];
            // Where the cursor was, before the path
            var (caretLine, caretColumn, split) = CaretPosition.Take(ref path);
            path = FileOperations.ListedPath(path);
            if (path.Length == 0 || List.Any(entry => FileOperations.SamePath(entry.Path, path))) continue;
            if (!DateTime.TryParseExact(line[..tab].Trim(), TimeFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var opened))
            {
                opened = DateTime.MinValue;
            }

            List.Add(new Entry(path, opened, caretLine, caretColumn, split));
            if (List.Count == MaxCount) break;
        }
    }

    internal static string Serialize()
    {
        var text = new StringBuilder();
        foreach (var entry in List)
        {
            text.Append(entry.Opened.ToString(TimeFormat, CultureInfo.InvariantCulture)).Append('\t')
                .Append(CaretPosition.Write(entry.Line, entry.Column, entry.Split)).Append(entry.Path).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>
    /// Write the list to the file
    /// </summary>
    /// <returns>DialogText.Success or an error message</returns>
    internal static string Save()
    {
        if (_unreadable) return DialogText.ListNotRead;
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(FilePath));
            if (directory != null) Directory.CreateDirectory(directory);
            FileIo.WriteAllBytesSafe(FilePath, new UTF8Encoding(false).GetBytes(Serialize()));
            return DialogText.Success;
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }

    /// <summary>
    /// Put a file at the top of the list (moving it there if it is in the list). The oldest is dropped if the
    /// list is full
    /// </summary>
    internal static void Add(string path, DateTime opened)
    {
        // A path with a line break would break the lines of the file
        if (path.IndexOfAny(new[] { '\r', '\n' }) >= 0) return;
        path = Path.GetFullPath(path);
        // Where the cursor was is kept
        var before = List.FirstOrDefault(entry => FileOperations.SamePath(entry.Path, path));
        List.RemoveAll(entry => FileOperations.SamePath(entry.Path, path));
        List.Insert(0, new Entry(path, opened, before?.Line ?? 0, before?.Column ?? 0, before?.Split ?? ""));
        if (List.Count > MaxCount) List.RemoveRange(MaxCount, List.Count - MaxCount);
    }

    /// <summary>
    /// Where the cursor was in a file when it was closed (line and column from 1), or null if not known
    /// </summary>
    internal static (int Line, int Column, string Split)? CaretOf(string path)
    {
        var entry = List.FirstOrDefault(item => FileOperations.SamePath(item.Path, path));
        return entry is { Line: > 0 } ? (entry.Line, entry.Column, entry.Split) : null;
    }

    /// <summary>
    /// Keep where the cursor was in a file of the list when it was closed (and in the other pane of a split, as
    /// CaretPosition.SplitText writes it; empty: not split)
    /// </summary>
    /// <returns>True if the file is in the list</returns>
    internal static bool SetCaret(string path, int line, int column, string split = "")
    {
        var index = List.FindIndex(item => FileOperations.SamePath(item.Path, path));
        if (index < 0) return false;
        List[index] = List[index] with { Line = Math.Max(1, line), Column = Math.Max(1, column), Split = split };
        return true;
    }

    /// <summary>
    /// A file or directory has a new path (renamed or moved): the files of the list follow it
    /// </summary>
    /// <returns>True if the list was changed</returns>
    internal static bool PathChanged(string oldPath, string newPath)
    {
        var changed = false;
        for (var i = 0; i < List.Count; i++)
        {
            if (CaretPosition.NewPath(List[i].Path, oldPath, newPath) is not { } path) continue;
            List[i] = List[i] with { Path = path };
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// Take a file out of the list (the file itself is not changed)
    /// </summary>
    internal static void RemoveAt(int index)
    {
        if (index >= 0 && index < List.Count) List.RemoveAt(index);
    }

    /// <summary>
    /// Empty the list
    /// </summary>
    internal static void Clear()
    {
        List.Clear();
    }
}

/// <summary>
/// Where the cursor is in a text, as a line and column (from 1), kept in the lists of the file browser (history
/// and favorites) as "12:5" and a tab character before a path, so a file is opened where it was left
/// </summary>
internal static class CaretPosition
{
    // "12:5" and a tab; in a split also the other pane: "12:5|40:1" ("|<40:1": that pane at the left)
    private static readonly System.Text.RegularExpressions.Regex Written =
        new(@"^([0-9]{1,9}):([0-9]{1,9})(\|<?[0-9]{1,9}:[0-9]{1,9})?\t");

    private static readonly System.Text.RegularExpressions.Regex SplitWritten = new(@"^\|(<?)([0-9]{1,9}):([0-9]{1,9})\z");

    /// <summary>
    /// The line and column at the start of a text from a list ("12:5" and a tab), taken out of it, and the place in
    /// the other pane of a split ("|40:1", empty if it was not split)
    /// </summary>
    /// <returns>The line and column, or (0, 0) if the text does not start with one</returns>
    internal static (int Line, int Column, string Split) Take(ref string text)
    {
        var match = Written.Match(text);
        if (!match.Success) return (0, 0, string.Empty);
        text = text[match.Length..];
        return (int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), match.Groups[3].Value);
    }

    /// <summary>
    /// The place in the other pane of a split as written after the line and column ("|40:1", "|<40:1" when that
    /// pane is at the left)
    /// </summary>
    internal static string SplitText(int line, int column, bool otherLeft) =>
        "|" + (otherLeft ? "<" : string.Empty) + Math.Max(1, line) + ":" + Math.Max(1, column);

    /// <summary>
    /// The other pane of a split, from its place as written ("|40:1"): the index of its cursor in a text, and
    /// whether it is at the left. Null if not split
    /// </summary>
    internal static (int Index, bool OtherLeft)? SplitOf(string split, string text)
    {
        var match = SplitWritten.Match(split);
        if (!match.Success) return null;
        return (IndexOf(text, int.Parse(match.Groups[2].Value), int.Parse(match.Groups[3].Value)),
            match.Groups[1].Value.Length > 0);
    }

    /// <summary>
    /// The new path of a path when a file or directory has a new path (the path itself, or one in the directory),
    /// or null if it is not affected
    /// </summary>
    internal static string? NewPath(string path, string oldPath, string newPath)
    {
        if (FileOperations.SamePath(path, oldPath)) return Path.GetFullPath(newPath);
        if (!FileOperations.IsInside(path, oldPath)) return null;
        var rest = Path.GetRelativePath(Path.GetFullPath(oldPath), Path.GetFullPath(path));
        return Path.GetFullPath(Path.Combine(newPath, rest));
    }

    /// <summary>
    /// A line and column as written before a path ("12:5" and a tab), or nothing if it is not known
    /// </summary>
    internal static string Write(int line, int column, string split = "") =>
        line > 0 ? line + ":" + Math.Max(1, column) + split + "\t" : string.Empty;

    /// <summary>
    /// The line and column (from 1) of a place in a text, where lines end with a line break of one character
    /// </summary>
    internal static (int Line, int Column) Of(string text, int index)
    {
        index = Math.Clamp(index, 0, text.Length);
        var line = 1;
        var start = 0;
        for (var i = 0; i < index; i++)
        {
            if (text[i] != '\n') continue;
            line++;
            start = i + 1;
        }

        return (line, index - start + 1);
    }

    /// <summary>
    /// The place in a text of a line and column (from 1). A line or column past the end is the end of the text or
    /// of the line (the text may have changed since)
    /// </summary>
    internal static int IndexOf(string text, int line, int column)
    {
        var index = 0;
        for (var current = 1; current < line; current++)
        {
            var next = text.IndexOf('\n', index);
            if (next < 0) return text.Length;
            index = next + 1;
        }

        var end = text.IndexOf('\n', index);
        if (end < 0) end = text.Length;
        return Math.Min(index + Math.Max(0, column - 1), end);
    }
}
