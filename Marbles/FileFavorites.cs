using System.Text;

namespace Marbles;

/// <summary>
/// The favorites of the file browser: files and directories marked by the user, in an order and with text
/// colors set by the user. Favorites can be grouped in folders, which only exist in the list (not on disk).
///
/// The list is kept in the file "favorites.txt" in the data directory, with one line for each favorite:
/// the color (or nothing), a tab character and the path. A folder has its name in brackets instead of a
/// path ("[Work]", followed by a tab character and "collapsed" if it is closed), and the favorites in a
/// folder are the lines after it that start with a blank space.
/// </summary>
internal static class FileFavorites
{
    internal const string FileName = "favorites.txt";
    private const string CollapsedMark = "collapsed";

    internal sealed class Favorite
    {
        internal string Path = string.Empty;
        internal ConsoleColor? Color;
        internal bool IsFolder;
        internal string Name = string.Empty; // The name of a folder
        internal bool Collapsed; // A folder that is closed: the favorites in it are not shown
        internal bool InFolder; // A favorite in the folder above it in the list
        internal int Line; // Where the cursor was when the file was closed in the editor (from 1; 0: not known)
        internal int Column;
    }

    private static readonly List<Favorite> List = new();

    /// <summary>
    /// The favorites and folders in the order set by the user. The favorites of a folder follow it
    /// </summary>
    internal static IReadOnlyList<Favorite> Items => List;

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
        _removed = null;
        var hasFolder = false;
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            var inFolder = hasFolder && line.StartsWith(' ');
            line = line.TrimStart(' ');
            var tab = line.IndexOf('\t');
            var written = tab < 0 ? line : line[(tab + 1)..];
            var rest = written.Trim();
            if (rest.Length == 0) continue;
            ConsoleColor? color = null;
            if (tab > 0 && Enum.TryParse<ConsoleColor>(line[..tab].Trim(), true, out var parsed) &&
                Enum.IsDefined(parsed))
            {
                color = parsed;
            }

            var close = rest.IndexOf(']');
            if (rest[0] == '[' && close > 1)
            {
                // A folder
                List.Add(new Favorite
                {
                    IsFolder = true, Name = rest[1..close].Trim(), Color = color,
                    Collapsed = rest[(close + 1)..].Contains(CollapsedMark, StringComparison.OrdinalIgnoreCase)
                });
                hasFolder = true;
                continue;
            }

            // Where the cursor was, before the path
            var (caretLine, caretColumn) = CaretPosition.Take(ref written);
            rest = FileOperations.ListedPath(written);
            if (rest.Length == 0 || Contains(rest)) continue;
            // The favorites of a folder follow it directly
            if (!inFolder) hasFolder = false;
            List.Add(new Favorite
            {
                Path = rest, Color = color, InFolder = inFolder, Line = caretLine, Column = caretColumn
            });
        }
    }

    internal static string Serialize()
    {
        var text = new StringBuilder();
        foreach (var favorite in List)
        {
            if (favorite.InFolder) text.Append(' ');
            text.Append(favorite.Color?.ToString() ?? string.Empty).Append('\t');
            if (favorite.IsFolder)
            {
                text.Append('[').Append(favorite.Name).Append(']');
                if (favorite.Collapsed) text.Append('\t').Append(CollapsedMark);
            }
            else
            {
                text.Append(CaretPosition.Write(favorite.Line, favorite.Column)).Append(favorite.Path);
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

    private static int IndexOf(string path)
    {
        return List.FindIndex(favorite => !favorite.IsFolder && FileOperations.SamePath(favorite.Path, path));
    }

    internal static bool Contains(string path) => IndexOf(path) >= 0;

    /// <summary>
    /// The text color of a favorite file or directory (null: none, or not a favorite)
    /// </summary>
    internal static ConsoleColor? ColorOf(string path)
    {
        var index = IndexOf(path);
        return index >= 0 ? List[index].Color : null;
    }

    /// <summary>
    /// Where the cursor was in a favorite file when it was closed (line and column from 1), or null if not known
    /// </summary>
    internal static (int Line, int Column)? CaretOf(string path)
    {
        var index = IndexOf(path);
        return index >= 0 && List[index].Line > 0 ? (List[index].Line, List[index].Column) : null;
    }

    /// <summary>
    /// Keep where the cursor was in a favorite file when it was closed
    /// </summary>
    /// <returns>True if the file is a favorite</returns>
    internal static bool SetCaret(string path, int line, int column)
    {
        var index = IndexOf(path);
        if (index < 0) return false;
        List[index].Line = Math.Max(1, line);
        List[index].Column = Math.Max(1, column);
        return true;
    }

    /// <summary>
    /// Add a file or directory to the favorites, or remove it if it is one
    /// </summary>
    /// <returns>True if it is a favorite now</returns>
    internal static bool Toggle(string path)
    {
        var index = IndexOf(path);
        if (index >= 0)
        {
            List.RemoveAt(index);
            return false;
        }

        List.Add(new Favorite { Path = Path.GetFullPath(path) });
        return true;
    }

    /// <summary>
    /// Remove a favorite, or a folder with the favorites in it
    /// </summary>
    internal static void RemoveAt(int index)
    {
        if (index < 0 || index >= List.Count) return;
        var count = FolderEnd(index) - index + 1;
        // Kept so that it can be put back
        _removed = new List<(int, List<Favorite>)> { (index, List.GetRange(index, count)) };
        List.RemoveRange(index, count);
    }

    /// <summary>
    /// Remove several favorites (folders are not removed this way)
    /// </summary>
    /// <returns>The number removed</returns>
    internal static int RemoveMany(ICollection<Favorite> items)
    {
        var removed = new List<(int, List<Favorite>)>();
        for (var i = 0; i < List.Count; i++)
        {
            if (!List[i].IsFolder && items.Contains(List[i])) removed.Add((i, new List<Favorite> { List[i] }));
        }

        if (removed.Count == 0) return 0;
        List.RemoveAll(item => !item.IsFolder && items.Contains(item));
        _removed = removed;
        return removed.Count;
    }

    // What was removed last, with the places it had in the list: a favorite, a folder with its favorites,
    // or several favorites
    private static List<(int Index, List<Favorite> Items)>? _removed;

    /// <summary>
    /// Put back what was removed last, where it was
    /// </summary>
    /// <returns>The place in the list of the first that was put back, or -1 if there is nothing to put back</returns>
    internal static int UndoRemove()
    {
        if (_removed == null) return -1;
        var first = -1;
        // From the top, so that each gets the place it had
        foreach (var (place, removed) in _removed)
        {
            var items = removed.Where(item => item.IsFolder || !Contains(item.Path)).ToList();
            if (items.Count == 0) continue;
            var index = Math.Min(place, List.Count);
            // Not into the middle of a folder that it was not in
            if (!items[0].InFolder) index = index < List.Count ? FolderStart(index) : index;
            // A favorite that was in a folder goes back into it only if the folder is still there
            if (items[0].InFolder && (index == 0 || !(List[index - 1].IsFolder || List[index - 1].InFolder)))
            {
                items[0].InFolder = false;
                index = index < List.Count ? FolderStart(index) : index;
            }

            List.InsertRange(index, items);
            if (first < 0) first = index;
        }

        _removed = null;
        return first;
    }

    /// <summary>
    /// Move favorites into a folder (last in it, in the order they have in the list), or out of their folders
    /// </summary>
    /// <param name="items">The favorites</param>
    /// <param name="folder">The folder, or null to place them last in the list, outside the folders</param>
    /// <returns>The place in the list of the first of them, or -1 if nothing was moved</returns>
    internal static int MoveToFolder(ICollection<Favorite> items, Favorite? folder)
    {
        if (folder != null && (!folder.IsFolder || !List.Contains(folder))) return -1;
        var moving = List.Where(item => !item.IsFolder && items.Contains(item)).ToList();
        if (moving.Count == 0) return -1;
        List.RemoveAll(item => moving.Contains(item));
        var index = List.Count;
        if (folder != null)
        {
            index = FolderEnd(List.IndexOf(folder)) + 1;
            // Opened, so that what was moved into it can be seen
            folder.Collapsed = false;
        }

        foreach (var item in moving) item.InFolder = folder != null;
        List.InsertRange(index, moving);
        return index;
    }

    /// <summary>
    /// Move favorites (or folders, with the favorites in them) to the top or the end: a favorite in a folder to
    /// the top or the end of its folder, the others to the top or the end of the list. Their order is kept
    /// </summary>
    /// <returns>The number moved</returns>
    internal static int MoveToEnd(ICollection<Favorite> items, bool top)
    {
        var moving = List.Where(items.Contains).ToList();
        // By where they are: the folder they are in (null: the list)
        var groups = moving.GroupBy(item => item.InFolder ? List[FolderStart(List.IndexOf(item))] : null).ToList();
        foreach (var group in groups)
        {
            var blocks = new List<Favorite>();
            foreach (var item in group)
            {
                var index = List.IndexOf(item);
                var count = item.IsFolder ? FolderEnd(index) - index + 1 : 1;
                blocks.AddRange(List.GetRange(index, count));
                List.RemoveRange(index, count);
            }

            int at;
            if (group.Key == null)
            {
                at = top ? 0 : List.Count;
            }
            else
            {
                var folder = List.IndexOf(group.Key);
                at = top ? folder + 1 : FolderEnd(folder) + 1;
            }

            List.InsertRange(at, blocks);
        }

        return moving.Count;
    }

    internal static void SetColor(int index, ConsoleColor? color)
    {
        if (index >= 0 && index < List.Count) List[index].Color = color;
    }

    #region Folders

    /// <summary>
    /// A name that can be used for a folder (the brackets mark a folder in the file)
    /// </summary>
    internal static bool IsValidFolderName(string name)
    {
        name = name.Trim();
        return name.Length > 0 && name.IndexOfAny(new[] { '[', ']', '\t', '\n', '\r' }) < 0;
    }

    /// <summary>
    /// Add a folder in front of an item of the list (in front of its folder, if it is in one)
    /// </summary>
    /// <returns>The place of the folder in the list, or -1 if the name cannot be used</returns>
    internal static int AddFolder(string name, int before)
    {
        if (!IsValidFolderName(name)) return -1;
        var index = before < 0 || before >= List.Count ? List.Count : FolderStart(before);
        List.Insert(index, new Favorite { IsFolder = true, Name = name.Trim() });
        return index;
    }

    internal static bool RenameFolder(int index, string name)
    {
        if (index < 0 || index >= List.Count || !List[index].IsFolder || !IsValidFolderName(name)) return false;
        List[index].Name = name.Trim();
        return true;
    }

    /// <summary>
    /// Open or close all the folders
    /// </summary>
    /// <returns>The number of folders in the list</returns>
    internal static int SetAllCollapsed(bool collapsed)
    {
        var folders = 0;
        foreach (var item in List)
        {
            if (!item.IsFolder) continue;
            item.Collapsed = collapsed;
            folders++;
        }

        return folders;
    }

    /// <summary>
    /// The folder a favorite is in (the item itself if it is not in a folder)
    /// </summary>
    internal static int FolderOf(int index)
    {
        return index >= 0 && index < List.Count ? FolderStart(index) : index;
    }

    /// <summary>
    /// The number of favorites in a folder
    /// </summary>
    internal static int FolderCount(int index)
    {
        return index >= 0 && index < List.Count && List[index].IsFolder ? FolderEnd(index) - index : 0;
    }

    // The last item of a folder (the folder itself if it is empty, or the item if it is not a folder)
    private static int FolderEnd(int index)
    {
        if (!List[index].IsFolder) return index;
        var end = index;
        while (end + 1 < List.Count && List[end + 1].InFolder) end++;
        return end;
    }

    // Where the folder of an item starts (the item itself if it is not in a folder)
    private static int FolderStart(int index)
    {
        while (index > 0 && List[index].InFolder) index--;
        return index;
    }

    #endregion

    /// <summary>
    /// Move a favorite or a folder one place up or down in the list.
    /// A favorite moved onto a folder goes into it, and one moved past the first or last favorite of its
    /// folder leaves the folder. A folder is closed and moves with its favorites, past the favorite or
    /// folder next to it.
    /// </summary>
    /// <returns>False if it is at the end of the list</returns>
    internal static bool Move(int index, bool up)
    {
        if (index < 0 || index >= List.Count) return false;
        var item = List[index];
        if (item.IsFolder)
        {
            item.Collapsed = true;
            var end = FolderEnd(index);
            var count = end - index + 1;
            int target;
            if (up)
            {
                if (index == 0) return false;
                target = FolderStart(index - 1);
            }
            else
            {
                if (end == List.Count - 1) return false;
                // After the favorite or folder that follows
                target = FolderEnd(end + 1) + 1 - count;
            }

            var block = List.GetRange(index, count);
            List.RemoveRange(index, count);
            List.InsertRange(target, block);
            return true;
        }

        if (up)
        {
            if (index == 0) return false;
            var previous = List[index - 1];
            if (item.InFolder)
            {
                if (previous.IsFolder)
                {
                    // The first of the folder: out of it, above it
                    Swap(index - 1, index);
                    item.InFolder = false;
                }
                else
                {
                    Swap(index - 1, index);
                }
            }
            else if (previous.IsFolder || previous.InFolder)
            {
                // Into the folder above, as the last in it
                item.InFolder = true;
                List[FolderStart(index - 1)].Collapsed = false;
            }
            else
            {
                Swap(index - 1, index);
            }

            return true;
        }

        if (item.InFolder)
        {
            if (index + 1 < List.Count && List[index + 1].InFolder)
            {
                Swap(index, index + 1);
            }
            else
            {
                // The last of the folder: out of it, below it
                item.InFolder = false;
            }

            return true;
        }

        if (index + 1 >= List.Count) return false;
        var next = List[index + 1];
        Swap(index, index + 1);
        if (next.IsFolder)
        {
            // Into the folder below, as the first in it
            item.InFolder = true;
            next.Collapsed = false;
        }

        return true;
    }

    private static void Swap(int a, int b)
    {
        (List[a], List[b]) = (List[b], List[a]);
    }

    /// <summary>
    /// A file or directory was renamed or moved: the favorites follow (also the ones inside a directory)
    /// </summary>
    /// <returns>True if the list was changed</returns>
    internal static bool PathChanged(string oldPath, string newPath)
    {
        var changed = false;
        foreach (var favorite in List)
        {
            if (favorite.IsFolder) continue;
            if (FileOperations.SamePath(favorite.Path, oldPath))
            {
                favorite.Path = Path.GetFullPath(newPath);
                changed = true;
            }
            else if (FileOperations.IsInside(favorite.Path, oldPath))
            {
                var rest = Path.GetRelativePath(Path.GetFullPath(oldPath), Path.GetFullPath(favorite.Path));
                favorite.Path = Path.GetFullPath(Path.Combine(newPath, rest));
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    /// A file or directory was deleted: it is removed from the favorites (with what was inside a directory)
    /// </summary>
    /// <returns>True if the list was changed</returns>
    internal static bool PathDeleted(string path)
    {
        return List.RemoveAll(favorite => !favorite.IsFolder &&
                                          (FileOperations.SamePath(favorite.Path, path) ||
                                           FileOperations.IsInside(favorite.Path, path))) > 0;
    }
}
