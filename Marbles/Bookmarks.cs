using System.Text;

namespace Marbles;

/// <summary>
/// The bookmarks of the Jump menu: places kept to jump to (a file, a directory, a page of Marbles or a data file),
/// added with B on any list of the Jump menu, or with ":bookmark" in the editor and the file browser. They are in
/// the order they were added, which can be changed on the Bookmarks tab. A bookmark can be given a name and a text
/// color of its own, and bookmarks can be grouped in folders (as the favorites of the file browser). The list holds
/// at most MaxCount places.
///
/// The bookmarks are numbered 1, 2, 3 ... in the order of the list (folders are not counted, and the bookmarks in a
/// closed folder are): ":book 3" jumps to the third.
///
/// The list is kept in the file "bookmarks.txt" in the data directory, one bookmark on each line: the kind ("file",
/// "dir", "page" or "data", as in jump.txt), a tab character, the place, and after other tab characters the name it
/// was given and its color (if any). A folder has its name in brackets ("[Work]"), its color and "collapsed" if it is
/// closed; the bookmarks in a folder are the lines after it that start with a blank space. No titles or text of notes
/// are kept: a data file is kept by its path in the data directory.
///
/// This class does not touch the console: the list is shown by JumpMenu.
/// </summary>
internal static class Bookmarks
{
    internal const string FileName = "bookmarks.txt";
    private const string CollapsedMark = "collapsed";

    /// <summary>
    /// The most places in the list (the folders are not counted)
    /// </summary>
    internal const int MaxCount = 100;

    /// <summary>
    /// A bookmark (a place, the name it was given and its color), or a folder (its name and color)
    /// </summary>
    internal sealed class Entry : IFolderItem
    {
        // The place a folder does not have
        private static readonly JumpTarget NoPlace = new(JumpKind.Page, string.Empty);

        internal Entry(JumpTarget target, string name = "")
        {
            Target = target;
            Name = name;
        }

        /// <summary>
        /// A folder with a name
        /// </summary>
        internal static Entry Folder(string name) => new(NoPlace, name) { IsFolder = true };

        internal JumpTarget Target { get; set; }

        /// <summary>
        /// The name given to a bookmark (empty: the name of the place is shown), or the name of a folder
        /// </summary>
        internal string Name { get; set; }

        /// <summary>
        /// The text color of the bookmark or folder (null: none of its own)
        /// </summary>
        internal ConsoleColor? Color { get; set; }

        public bool IsFolder { get; init; }
        public bool InFolder { get; set; }
        public bool Collapsed { get; set; }

        /// <summary>
        /// The name shown: the one given, or the name of the place (a folder: its name)
        /// </summary>
        internal string Shown => IsFolder || Name.Length > 0 ? Name : Target.Name;
    }

    // The list, with its folders (moving, removing and putting back are done by it)
    private static readonly FolderList<Entry> Folders = new();
    private static List<Entry> List => Folders.Items;

    /// <summary>
    /// The bookmarks and folders, in the order of the list. The bookmarks of a folder follow it
    /// </summary>
    internal static IReadOnlyList<Entry> Items => List;

    /// <summary>
    /// The bookmarks without the folders, in the order of the list: bookmark 1 is the first (":book 1")
    /// </summary>
    internal static List<Entry> Places => List.Where(entry => !entry.IsFolder).ToList();

    /// <summary>
    /// The number of a bookmark (from 1; 0 if it is not a bookmark of the list)
    /// </summary>
    internal static int NumberOf(Entry entry) => Places.IndexOf(entry) + 1;

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

    private static ConsoleColor? ParseColor(string text) =>
        Enum.TryParse<ConsoleColor>(text.Trim(), true, out var color) && Enum.IsDefined(color) ? color : null;

    internal static void Parse(string text)
    {
        List.Clear();
        // What was removed before is not put back into a list read again
        Folders.ForgetRemoved();
        var hasFolder = false;
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            var inFolder = hasFolder && line.StartsWith(' ');
            line = line.TrimStart(' ');
            var parts = line.Split('\t');
            var close = parts[0].IndexOf(']');
            if (parts[0].StartsWith('[') && close > 1)
            {
                // A folder: its name, color, and if it is closed
                var name = parts[0][1..close].Trim();
                if (!IsValidFolderName(name)) continue;
                var folder = Entry.Folder(name);
                folder.Color = parts.Length > 1 ? ParseColor(parts[1]) : null;
                folder.Collapsed = parts.Skip(2)
                    .Any(part => part.Trim().Equals(CollapsedMark, StringComparison.OrdinalIgnoreCase));
                List.Add(folder);
                hasFolder = true;
                continue;
            }

            if (parts.Length < 2 || JumpHistory.ParseKind(parts[0]) is not { } kind) continue;
            var place = kind is JumpKind.File or JumpKind.Directory ? FileOperations.ListedPath(parts[1]) : parts[1].Trim();
            if (place.Length == 0) continue;
            var target = new JumpTarget(kind, place);
            if (Contains(target) || Places.Count >= MaxCount) continue;
            // The bookmarks of a folder follow it directly
            if (!inFolder) hasFolder = false;
            List.Add(new Entry(target, parts.Length > 2 ? parts[2].Trim() : string.Empty)
            {
                Color = parts.Length > 3 ? ParseColor(parts[3]) : null,
                InFolder = inFolder
            });
        }
    }

    internal static string Serialize()
    {
        var text = new StringBuilder();
        foreach (var entry in List)
        {
            if (entry.InFolder) text.Append(' ');
            var color = entry.Color?.ToString() ?? string.Empty;
            if (entry.IsFolder)
            {
                text.Append('[').Append(entry.Name).Append(']').Append('\t').Append(color);
                if (entry.Collapsed) text.Append('\t').Append(CollapsedMark);
            }
            else
            {
                text.Append(JumpHistory.KindName(entry.Target.Kind)).Append('\t').Append(entry.Target.Target);
                if (entry.Name.Length > 0 || color.Length > 0) text.Append('\t').Append(entry.Name);
                if (color.Length > 0) text.Append('\t').Append(color);
            }

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

    internal static bool Contains(JumpTarget target) =>
        List.Exists(entry => !entry.IsFolder && entry.Target.SameAs(target));

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
    /// Add a place at the end of the list, outside the folders (a place that is there already stays where it is)
    /// </summary>
    internal static AddResult Add(JumpTarget target)
    {
        if (target.Kind is JumpKind.File or JumpKind.Directory)
        {
            target = target with { Target = Path.GetFullPath(target.Target) };
        }

        if (Contains(target)) return AddResult.AlreadyThere;
        if (Places.Count >= MaxCount) return AddResult.Full;
        List.Add(new Entry(target));
        return AddResult.Added;
    }

    /// <summary>
    /// Remove a bookmark, or a folder with the bookmarks in it (UndoRemove puts it back)
    /// </summary>
    internal static void RemoveAt(int index) => Folders.RemoveAt(index);

    /// <summary>
    /// Remove several bookmarks (folders are not removed this way)
    /// </summary>
    /// <returns>The number removed</returns>
    internal static int RemoveMany(ICollection<Entry> items) => Folders.RemoveMany(items);

    /// <summary>
    /// Put back what was removed last, where it was (a place that is a bookmark again is not)
    /// </summary>
    /// <returns>The place in the list of the first that was put back, or -1 if there is nothing to put back</returns>
    internal static int UndoRemove() => Folders.UndoRemove(entry => !Contains(entry.Target));

    /// <summary>
    /// Move a bookmark or a folder one place up or down (into and out of folders, see FolderList.Move)
    /// </summary>
    /// <returns>False if it is at the end of the list</returns>
    internal static bool Move(int index, bool up) => Folders.Move(index, up);

    /// <summary>
    /// Move bookmarks (or folders) to the top or the end of the list (a bookmark in a folder: of its folder)
    /// </summary>
    internal static int MoveToEnd(ICollection<Entry> items, bool top) => Folders.MoveToEnd(items, top);

    /// <summary>
    /// Move bookmarks into a folder (last in it), or out of their folders (null: last in the list)
    /// </summary>
    /// <returns>The place in the list of the first of them, or -1 if nothing was moved</returns>
    internal static int MoveToFolder(ICollection<Entry> items, Entry? folder) => Folders.MoveToFolder(items, folder);

    /// <summary>
    /// Give a bookmark the name it is shown with (empty: the name of the place), or a folder a new name
    /// </summary>
    /// <returns>False if a folder cannot have the name</returns>
    internal static bool Rename(int index, string name)
    {
        if (index < 0 || index >= List.Count) return false;
        // A tab would end the name in the file
        name = name.Replace('\t', ' ').Trim();
        var entry = List[index];
        if (entry.IsFolder)
        {
            if (!IsValidFolderName(name)) return false;
            entry.Name = name;
            return true;
        }

        entry.Name = name == entry.Target.Name ? string.Empty : name;
        return true;
    }

    internal static void SetColor(int index, ConsoleColor? color)
    {
        if (index >= 0 && index < List.Count) List[index].Color = color;
    }

    #region Folders

    /// <summary>
    /// A name that can be used for a folder (the brackets mark a folder in the file)
    /// </summary>
    internal static bool IsValidFolderName(string name) => FileFavorites.IsValidFolderName(name);

    /// <summary>
    /// Add a folder in front of an item of the list (in front of its folder, if it is in one)
    /// </summary>
    /// <returns>The place of the folder in the list, or -1 if the name cannot be used</returns>
    internal static int AddFolder(string name, int before)
    {
        if (!IsValidFolderName(name)) return -1;
        return Folders.AddFolder(Entry.Folder(name.Trim()), before);
    }

    /// <summary>
    /// Open or close all the folders
    /// </summary>
    /// <returns>The number of folders</returns>
    internal static int SetAllCollapsed(bool collapsed) => Folders.SetAllCollapsed(collapsed);

    /// <summary>
    /// The number of bookmarks in a folder
    /// </summary>
    internal static int FolderCount(int index) => Folders.FolderCount(index);

    #endregion

    /// <summary>
    /// Empty the list (the folders too)
    /// </summary>
    internal static void Clear()
    {
        List.Clear();
        Folders.ForgetRemoved();
    }

    /// <summary>
    /// A file or directory has a new path (renamed or moved): the bookmarks of it (and of what is in it) follow it
    /// </summary>
    /// <returns>True if the list was changed</returns>
    internal static bool PathChanged(string oldPath, string newPath)
    {
        var changed = false;
        foreach (var entry in List)
        {
            if (entry.IsFolder || entry.Target.Followed(oldPath, newPath) is not { } target) continue;
            entry.Target = target;
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
