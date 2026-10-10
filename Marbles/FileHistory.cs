using System.Globalization;
using System.Text;

namespace Marbles;

/// <summary>
/// The history of the file browser: the files opened in the editor from the file browser, the last opened
/// first. A file opened again moves to the top. The list holds at most MaxCount files: when it is full, the
/// file opened longest ago is dropped.
///
/// The list is kept in the file "history.txt" in the data directory, with one line for each file: the time it
/// was last opened (yyyy-MM-dd HH:mm), a tab character and the path.
/// </summary>
internal static class FileHistory
{
    internal const string FileName = "history.txt";

    /// <summary>
    /// The most files in the list
    /// </summary>
    internal const int MaxCount = 100;

    private const string TimeFormat = "yyyy-MM-dd HH:mm";

    internal sealed record Entry(string Path, DateTime Opened);

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
        try
        {
            if (File.Exists(FilePath)) Parse(File.ReadAllText(FilePath, Encoding.UTF8));
        }
        catch
        {
            // A list that cannot be read is empty
        }
    }

    internal static void Parse(string text)
    {
        List.Clear();
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            var tab = line.IndexOf('\t');
            if (tab < 0) continue;
            var path = line[(tab + 1)..].Trim();
            if (path.Length == 0 || List.Any(entry => FileOperations.SamePath(entry.Path, path))) continue;
            if (!DateTime.TryParseExact(line[..tab].Trim(), TimeFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var opened))
            {
                opened = DateTime.MinValue;
            }

            List.Add(new Entry(path, opened));
            if (List.Count == MaxCount) break;
        }
    }

    internal static string Serialize()
    {
        var text = new StringBuilder();
        foreach (var entry in List)
        {
            text.Append(entry.Opened.ToString(TimeFormat, CultureInfo.InvariantCulture)).Append('\t')
                .Append(entry.Path).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>
    /// Write the list to the file
    /// </summary>
    /// <returns>DialogText.Success or an error message</returns>
    internal static string Save()
    {
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
        path = Path.GetFullPath(path);
        List.RemoveAll(entry => FileOperations.SamePath(entry.Path, path));
        List.Insert(0, new Entry(path, opened));
        if (List.Count > MaxCount) List.RemoveRange(MaxCount, List.Count - MaxCount);
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
