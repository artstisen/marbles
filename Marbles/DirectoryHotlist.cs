using System.Text;

namespace Marbles;

/// <summary>
/// The directory hotlist of the file browser: directories kept to go back to (B adds the directory shown, W shows
/// the list). They are in the order they were added, which can be changed on the page of the list. The list
/// holds at most MaxCount directories.
///
/// The list is kept in the file "hotlist.txt" in the data directory, with the full path of a directory on each
/// line (so it can be edited by hand). Blank lines and a directory that is there twice are not used.
///
/// This class does not touch the console: the list is shown by FileExplorer (FileExplorer.Hotlist.cs).
/// </summary>
internal static class DirectoryHotlist
{
    internal const string FileName = "hotlist.txt";

    /// <summary>
    /// The most directories in the list
    /// </summary>
    internal const int MaxCount = 100;

    private static readonly List<string> List = new();

    /// <summary>
    /// The directories, in the order of the list
    /// </summary>
    internal static IReadOnlyList<string> Items => List;

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
            var path = rawLine.Trim();
            if (path.Length == 0 || Contains(path)) continue;
            List.Add(path);
            if (List.Count == MaxCount) break;
        }
    }

    internal static string Serialize()
    {
        var text = new StringBuilder();
        foreach (var path in List) text.Append(path).Append('\n');
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

    internal static bool Contains(string path) => List.Exists(item => FileOperations.SamePath(item, path));

    /// <summary>
    /// What adding a directory did
    /// </summary>
    internal enum AddResult
    {
        Added,
        AlreadyThere,
        Full
    }

    /// <summary>
    /// Add a directory at the end of the list (a directory that is there already stays where it is)
    /// </summary>
    internal static AddResult Add(string path)
    {
        path = Path.GetFullPath(path);
        if (Contains(path)) return AddResult.AlreadyThere;
        if (List.Count >= MaxCount) return AddResult.Full;
        List.Add(path);
        return AddResult.Added;
    }

    /// <summary>
    /// Put a directory back at a place in the list (undo)
    /// </summary>
    internal static void Insert(int index, string path)
    {
        if (Contains(path) || List.Count >= MaxCount) return;
        List.Insert(Math.Clamp(index, 0, List.Count), path);
    }

    /// <summary>
    /// Take a directory out of the list (the directory itself is not changed)
    /// </summary>
    internal static void RemoveAt(int index)
    {
        if (index >= 0 && index < List.Count) List.RemoveAt(index);
    }

    /// <summary>
    /// Move a directory to another place in the list
    /// </summary>
    /// <returns>Where it is now</returns>
    internal static int Move(int from, int to)
    {
        if (from < 0 || from >= List.Count) return from;
        to = Math.Clamp(to, 0, List.Count - 1);
        var path = List[from];
        List.RemoveAt(from);
        List.Insert(to, path);
        return to;
    }

    /// <summary>
    /// Empty the list
    /// </summary>
    internal static void Clear()
    {
        List.Clear();
    }

    /// <summary>
    /// A directory has a new path (renamed or moved): the directories of the list follow it (also those in it)
    /// </summary>
    /// <returns>True if the list was changed</returns>
    internal static bool PathChanged(string oldPath, string newPath)
    {
        var changed = false;
        for (var i = 0; i < List.Count; i++)
        {
            if (CaretPosition.NewPath(List[i], oldPath, newPath) is not { } path) continue;
            List[i] = path;
            changed = true;
        }

        return changed;
    }
}
