using System.Text;

namespace Marbles;

/// <summary>
/// The bookmarks of the Jump menu: places kept to jump to (a file, a directory, a page of Marbles or a data file),
/// added with B on any list of the Jump menu, or with ":bookmark" in the editor and the file browser. They are in
/// the order they were added, which can be changed on the Bookmarks tab, and a bookmark can be given a name of its
/// own. The list holds at most MaxCount places.
///
/// The list is kept in the file "bookmarks.txt" in the data directory, one bookmark on each line: the kind ("file",
/// "dir", "page" or "data", as in jump.txt), a tab character, the place, and the name it was given (if any) after
/// another tab character. No titles or text of notes are kept: a data file is kept by its path in the data
/// directory.
///
/// This class does not touch the console: the list is shown by JumpMenu.
/// </summary>
internal static class Bookmarks
{
    internal const string FileName = "bookmarks.txt";

    /// <summary>
    /// The most places in the list
    /// </summary>
    internal const int MaxCount = 100;

    /// <summary>
    /// A bookmark: the place, and the name it was given (empty: the name of the place is shown)
    /// </summary>
    internal sealed record Entry(JumpTarget Target, string Name = "")
    {
        /// <summary>
        /// The name shown: the one given, or the name of the place
        /// </summary>
        internal string Shown => Name.Length > 0 ? Name : Target.Name;
    }

    private static readonly List<Entry> List = new();

    /// <summary>
    /// The bookmarks, in the order of the list
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
            var parts = rawLine.TrimEnd('\r').Split('\t', 3);
            if (parts.Length < 2 || JumpHistory.ParseKind(parts[0]) is not { } kind) continue;
            var place = kind is JumpKind.File or JumpKind.Directory ? FileOperations.ListedPath(parts[1]) : parts[1].Trim();
            if (place.Length == 0) continue;
            var target = new JumpTarget(kind, place);
            if (Contains(target)) continue;
            List.Add(new Entry(target, parts.Length > 2 ? parts[2].Trim() : string.Empty));
            if (List.Count == MaxCount) break;
        }
    }

    internal static string Serialize()
    {
        var text = new StringBuilder();
        foreach (var entry in List)
        {
            text.Append(JumpHistory.KindName(entry.Target.Kind)).Append('\t').Append(entry.Target.Target);
            if (entry.Name.Length > 0) text.Append('\t').Append(entry.Name);
            text.Append('\n');
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

    internal static bool Contains(JumpTarget target) => List.Exists(entry => entry.Target.SameAs(target));

    /// <summary>
    /// What adding a place did
    /// </summary>
    internal enum AddResult
    {
        Added,
        AlreadyThere,
        Full
    }

    /// <summary>
    /// Add a place at the end of the list (a place that is there already stays where it is)
    /// </summary>
    internal static AddResult Add(JumpTarget target)
    {
        if (target.Kind is JumpKind.File or JumpKind.Directory)
        {
            target = target with { Target = Path.GetFullPath(target.Target) };
        }

        if (Contains(target)) return AddResult.AlreadyThere;
        if (List.Count >= MaxCount) return AddResult.Full;
        List.Add(new Entry(target));
        return AddResult.Added;
    }

    /// <summary>
    /// Put a bookmark back at a place in the list (undo)
    /// </summary>
    internal static void Insert(int index, Entry entry)
    {
        if (Contains(entry.Target) || List.Count >= MaxCount) return;
        List.Insert(Math.Clamp(index, 0, List.Count), entry);
    }

    /// <summary>
    /// Take a bookmark out of the list (the place itself is not changed)
    /// </summary>
    internal static void RemoveAt(int index)
    {
        if (index >= 0 && index < List.Count) List.RemoveAt(index);
    }

    /// <summary>
    /// Move a bookmark to another place in the list
    /// </summary>
    /// <returns>Where it is now</returns>
    internal static int Move(int from, int to)
    {
        if (from < 0 || from >= List.Count) return from;
        to = Math.Clamp(to, 0, List.Count - 1);
        var entry = List[from];
        List.RemoveAt(from);
        List.Insert(to, entry);
        return to;
    }

    /// <summary>
    /// Give a bookmark the name it is shown with (empty: the name of the place)
    /// </summary>
    internal static void Rename(int index, string name)
    {
        if (index < 0 || index >= List.Count) return;
        // A tab would end the name in the file
        name = name.Replace('\t', ' ').Trim();
        if (name == List[index].Target.Name) name = string.Empty;
        List[index] = List[index] with { Name = name };
    }

    /// <summary>
    /// Empty the list
    /// </summary>
    internal static void Clear()
    {
        List.Clear();
    }

    /// <summary>
    /// A file or directory has a new path (renamed or moved): the bookmarks of it (and of what is in it) follow it
    /// </summary>
    /// <returns>True if the list was changed</returns>
    internal static bool PathChanged(string oldPath, string newPath)
    {
        var changed = false;
        for (var i = 0; i < List.Count; i++)
        {
            if (List[i].Target.Followed(oldPath, newPath) is not { } target) continue;
            List[i] = List[i] with { Target = target };
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// Add a place to the bookmarks (as they are in the file) and save them
    /// </summary>
    /// <returns>The message that tells what happened</returns>
    internal static string AddWithMessage(JumpTarget target)
    {
        Load();
        var result = Add(target);
        if (result == AddResult.AlreadyThere) return Lang.F("Already bookmarked: {0}", target.Name);
        if (result == AddResult.Full) return Lang.F("The bookmarks are full ({0})", MaxCount);
        var status = Save();
        return status == DialogText.Success ? Lang.F("Bookmarked: {0}", target.Name) : status;
    }

    /// <summary>
    /// Load the list, follow a file or directory with a new path, and save it if it changed
    /// </summary>
    internal static void Follow(string oldPath, string newPath)
    {
        Load();
        if (PathChanged(oldPath, newPath)) Save();
    }
}
