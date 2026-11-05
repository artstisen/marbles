using System.Text;

namespace Marbles;

/// <summary>
/// The bookmarks of the Jump menu: places kept to jump to (a file, a directory, a page of Marbles or a data file),
/// added with B on any list of the Jump menu, or with ":bookmark" in the editor and the file browser. They are in
/// the order they were added, which can be changed on the Bookmarks tab. A bookmark can be given a name and a text
/// color of its own, and bookmarks can be grouped in folders (as the favorites of the file browser). The list has
/// no limit.
///
/// The bookmarks are numbered 1, 2, 3 ... in the order of the list (folders are not counted, and the bookmarks in a
/// closed folder are): ":book 3" jumps to the third. A bookmark with a name is also jumped to by it (":book chapter3"):
/// a name is used by one bookmark only, and is not only digits.
///
/// The list is kept in the file "bookmarks.txt" in the data directory, one bookmark on each line: the kind ("file",
/// "dir", "page" or "data", as in jump.txt), a tab character, the place, and after other tab characters the name it
/// was given and its color (if any), and the line and column of a bookmark of a file made in the editor ("120:5").
/// A folder has its name in brackets ("[Work]"), its color and "collapsed" if it is closed; the bookmarks in a folder
/// are the lines after it that start with a blank space. No titles or text of notes, and no places in notes, are
/// kept: the file is not encrypted, so a data file is kept by its path in the data directory only (a line and
/// column are kept for a file only).
///
/// This class does not touch the console: the list is shown by JumpMenu.
/// </summary>
internal static class Bookmarks
{
    internal const string FileName = "bookmarks.txt";
    private const string CollapsedMark = "collapsed";

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
        /// The name shown: the one given, or the name of the place, with the line of a bookmark made in the editor
        /// ("chapter.md:120"; a folder: its name)
        /// </summary>
        internal string Shown => IsFolder || Name.Length > 0 ? Name : PlaceName;

        /// <summary>
        /// The name of the place, with the line of a bookmark made in the editor ("chapter.md:120")
        /// </summary>
        internal string PlaceName => Target.At is { } at ? Target.Name + ":" + at.Line : Target.Name;
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
        Folders.ForgetRemoved();
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

    private static ConsoleColor? ParseColor(string text) =>
        Enum.TryParse<ConsoleColor>(text.Trim(), true, out var color) && Enum.IsDefined(color) ? color : null;

    // The line and column of a bookmark made in the editor ("120:5"), or null
    private static (int Line, int Column)? ParseAt(string text)
    {
        var parts = text.Trim().Split(':');
        return parts.Length == 2 && int.TryParse(parts[0], out var line) && int.TryParse(parts[1], out var column) &&
               line > 0 && column > 0
            ? (line, column)
            : null;
    }

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
            // (a line and column only for a file: never for a data file, whose notes are not to be told about)
            var target = new JumpTarget(kind, place)
            {
                At = kind == JumpKind.File && parts.Length > 4 ? ParseAt(parts[4]) : null
            };
            if (Contains(target)) continue;
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
                var at = entry.Target is { Kind: JumpKind.File, At: { } place } ? place.Line + ":" + place.Column
                    : string.Empty;
                if (entry.Name.Length > 0 || color.Length > 0 || at.Length > 0) text.Append('\t').Append(entry.Name);
                if (color.Length > 0 || at.Length > 0) text.Append('\t').Append(color);
                if (at.Length > 0) text.Append('\t').Append(at);
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

    internal static bool Contains(JumpTarget target) =>
        List.Exists(entry => !entry.IsFolder && SameBookmark(entry.Target, target));

    // The same bookmark: the same place, and the same line in a file (a bookmark of a file, and bookmarks at other
    // lines of it, are others)
    private static bool SameBookmark(JumpTarget one, JumpTarget other) =>
        one.SameAs(other) && one.At?.Line == other.At?.Line;

    // The bookmark of a place, or null
    private static Entry? Find(JumpTarget target) =>
        List.Find(entry => !entry.IsFolder && SameBookmark(entry.Target, target));

    /// <summary>
    /// The bookmark with a name (upper and lower case alike; not a folder), or null
    /// </summary>
    internal static Entry? FindByName(string name) =>
        name.Length == 0
            ? null
            : List.Find(entry => !entry.IsFolder && entry.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The names of the bookmarks, for Tab after ":book ". From the list as it is (read from the file only when it has
    /// not been read yet): in the Jump menu, reading it again would lose what "u" puts back, and the marks
    /// </summary>
    internal static IEnumerable<string> NamesToComplete()
    {
        if (List.Count == 0) Load();
        return Places.Where(entry => entry.Name.Length > 0 && !entry.Name.Contains(' ')).Select(entry => entry.Name)
            .ToList();
    }

    /// <summary>
    /// Why a bookmark cannot have a name, or null: a name of only digits would be a number of ":book", and a name is
    /// used by one bookmark only (other than the one given)
    /// </summary>
    internal static string? NameProblem(string name, Entry? entry)
    {
        if (name.Length == 0) return null;
        if (name.All(char.IsAsciiDigit)) return Lang.T("A name of only digits is the number of a bookmark: add a letter");
        return FindByName(name) is { } other && other != entry
            ? Lang.F("The name {0} is used by bookmark {1}", other.Name, NumberOf(other))
            : null;
    }

    /// <summary>
    /// The command that jumps to a bookmark: ":book chapter3" by its name, else ":book 14" by its number
    /// </summary>
    internal static string CommandOf(Entry entry) =>
        ":book " + (entry.Name.Length > 0 && !entry.Name.All(char.IsAsciiDigit) && FindByName(entry.Name) == entry
            ? entry.Name
            : NumberOf(entry).ToString()); // English

    /// <summary>
    /// What adding a place did
    /// </summary>
    internal enum AddResult
    {
        Added,
        AlreadyThere,
        NotKept // A tab or a line break in its path
    }

    private static readonly char[] LineBreaking = { '\t', '\r', '\n' };

    /// <summary>
    /// Add a place at the end of the list, outside the folders (a place that is there already stays where it is)
    /// </summary>
    internal static AddResult Add(JumpTarget target)
    {
        if (target.Kind is JumpKind.File or JumpKind.Directory)
        {
            target = target with { Target = Path.GetFullPath(target.Target) };
        }

        // A line and column in a file only (never in a data file: the file of the list is not encrypted)
        if (target.Kind != JumpKind.File) target = target with { At = null };
        if (Contains(target)) return AddResult.AlreadyThere;
        // (a tab or a line break in it would break the line of the file)
        if (target.Target.IndexOfAny(LineBreaking) >= 0 || target.Name.IndexOfAny(LineBreaking) >= 0)
        {
            return AddResult.NotKept;
        }

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
        // A tab or a line break would end the name in the file
        name = name.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();
        var entry = List[index];
        if (entry.IsFolder)
        {
            if (!IsValidFolderName(name)) return false;
            entry.Name = name;
            return true;
        }

        // The name of the place (as offered when renaming), with or without the line: no name of its own. A name of
        // another bookmark, or of only digits, is not taken (NameProblem tells why)
        name = name == entry.Target.Name || name == entry.PlaceName ? string.Empty : name;
        if (NameProblem(name, entry) != null) return false;
        entry.Name = name;
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
    /// <param name="target">The place</param>
    /// <param name="reload">False in the Jump menu, which has read the list already: what can be undone there (a
    /// bookmark removed) is kept</param>
    /// <returns>The message that tells what happened (with the number of the bookmark, for ":book 14")</returns>
    internal static string AddWithMessage(JumpTarget target, bool reload = true) =>
        AddWithMessage(target, string.Empty, false, reload, out _);

    /// <summary>
    /// Add a place to the bookmarks with a name, to jump to it by (":bookmark chapter3", then ":book chapter3"), and
    /// save them (as they are in the file). A place that is bookmarked already gets the name. A name another bookmark
    /// has is not taken, unless "moveName" (":bookmark! name"): that bookmark is moved to the place, keeping its
    /// number, folder and color
    /// </summary>
    /// <param name="target">The place</param>
    /// <param name="name">The name (empty: none)</param>
    /// <param name="moveName">The bookmark with the name is moved here</param>
    /// <param name="reload">As for AddWithMessage</param>
    /// <param name="isError">True when nothing was done for a reason the message tells</param>
    /// <returns>The message that tells what happened (with the number of the bookmark, for ":book 14")</returns>
    internal static string AddWithMessage(JumpTarget target, string name, bool moveName, bool reload,
        out bool isError)
    {
        isError = true;
        if (reload) Load();
        name = name.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (target.Kind is JumpKind.File or JumpKind.Directory)
        {
            target = target with { Target = Path.GetFullPath(target.Target) };
        }

        // A line and column in a file only (never in a data file: bookmarks.txt is not encrypted)
        if (target.Kind != JumpKind.File) target = target with { At = null };
        if (target.Target.IndexOfAny(LineBreaking) >= 0) return Lang.T("A path with a tab or a line break is not kept");
        if (name.Length > 0 && name.All(char.IsAsciiDigit)) return NameProblem(name, null)!;
        var existing = Find(target);
        var named = FindByName(name);
        Entry entry;
        if (named != null && named != existing)
        {
            if (!moveName)
            {
                return Lang.F("{0} is the name of bookmark {1}: :bookmark! moves it here", named.Name,
                    NumberOf(named));
            }

            // (a place has one bookmark: the one it has keeps its own name)
            if (existing != null) return Lang.F("Already bookmarked ({0}): {1}", NumberOf(existing), existing.Shown);
            named.Target = target;
            isError = !Saved(out var moved);
            return isError ? moved : Lang.F("Bookmark {0} moved here: {1}", NumberOf(named), named.Shown);
        }

        if (existing != null)
        {
            isError = false;
            if (name.Length == 0 || existing.Name.Equals(name, StringComparison.Ordinal))
            {
                return Lang.F("Already bookmarked ({0}): {1}", NumberOf(existing), existing.Shown);
            }

            // The bookmark of the place gets the name
            existing.Name = name;
            entry = existing;
        }
        else
        {
            Add(target);
            entry = Find(target)!;
            entry.Name = name;
        }

        isError = !Saved(out var status);
        return isError ? status : Lang.F("Bookmarked ({0}): {1}", NumberOf(entry), entry.Shown);
    }

    // The list saved: true, else false and why
    private static bool Saved(out string status)
    {
        status = Save();
        return status == DialogText.Success;
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
