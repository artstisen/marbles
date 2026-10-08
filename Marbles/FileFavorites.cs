using System.Text;

namespace Marbles;

/// <summary>
/// The favorites of the file browser: files and directories marked by the user, in an order and with text
/// colors set by the user. The list is kept in the file "favorites.txt" in the data directory, with one
/// favorite per line: the color (or nothing), a tab character and the path.
/// </summary>
internal static class FileFavorites
{
    internal const string FileName = "favorites.txt";

    internal sealed class Favorite
    {
        internal string Path = string.Empty;
        internal ConsoleColor? Color;
    }

    private static readonly List<Favorite> List = new();

    /// <summary>
    /// The favorites in the order set by the user
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
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            var tab = line.IndexOf('\t');
            var path = (tab < 0 ? line : line[(tab + 1)..]).Trim();
            if (path.Length == 0 || Contains(path)) continue;
            ConsoleColor? color = null;
            if (tab > 0 && Enum.TryParse<ConsoleColor>(line[..tab].Trim(), true, out var parsed) &&
                Enum.IsDefined(parsed))
            {
                color = parsed;
            }

            List.Add(new Favorite { Path = path, Color = color });
        }
    }

    internal static string Serialize()
    {
        var text = new StringBuilder();
        foreach (var favorite in List)
        {
            text.Append(favorite.Color?.ToString() ?? string.Empty).Append('\t').Append(favorite.Path).Append('\n');
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
        return List.FindIndex(favorite => FileOperations.SamePath(favorite.Path, path));
    }

    internal static bool Contains(string path) => IndexOf(path) >= 0;

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

    internal static void RemoveAt(int index)
    {
        if (index >= 0 && index < List.Count) List.RemoveAt(index);
    }

    /// <summary>
    /// Move a favorite one place up or down in the list
    /// </summary>
    /// <returns>False if it is at the end of the list</returns>
    internal static bool Move(int index, bool up)
    {
        var target = index + (up ? -1 : 1);
        if (index < 0 || index >= List.Count || target < 0 || target >= List.Count) return false;
        (List[index], List[target]) = (List[target], List[index]);
        return true;
    }

    internal static void SetColor(int index, ConsoleColor? color)
    {
        if (index >= 0 && index < List.Count) List[index].Color = color;
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
        return List.RemoveAll(favorite => FileOperations.SamePath(favorite.Path, path) ||
                                          FileOperations.IsInside(favorite.Path, path)) > 0;
    }
}
