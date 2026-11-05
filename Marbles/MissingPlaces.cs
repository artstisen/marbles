namespace Marbles;

/// <summary>
/// The entries of a list (favorites, bookmarks, hotlist, history) whose file or directory is not there any more:
/// ":missing" lists them in a box, and ":prune" takes them out of the list. An entry on a drive that is not
/// connected (a USB drive, a network share) is listed, but not pruned: it is only missing for now
/// </summary>
internal static class MissingPlaces
{
    // The folders drives and network shares are mounted in on Linux and macOS, and how many folders down the drive
    // is: /mnt/usb, /Volumes/usb, /media/usb or /media/user/usb, /run/media/user/usb, the shares of GNOME
    // (/run/user/1000/gvfs/smb-share:server=nas,share=books) and of autofs (/net/nas/books)
    private static readonly (string Folder, int Depth)[] MountFolders =
    {
        ("/media/", 2), ("/run/media/", 2), ("/run/user/", 3), ("/mnt/", 1), ("/Volumes/", 1), ("/net/", 2)
    };

    /// <summary>
    /// Tells for the entries of a list whether they are missing, and whether their drive is there. Each drive (and
    /// folder a drive is mounted in) is looked at once, and an entry on a drive that is not there is not looked for:
    /// a network share that cannot be reached can take many seconds to answer
    /// </summary>
    /// <param name="directoryExists">Tells if a directory is there (for testing; null: the file system)</param>
    internal sealed class Checker(Func<string, bool>? directoryExists = null)
    {
        private readonly Func<string, bool> _directoryExists = directoryExists ?? Directory.Exists;
        private readonly Dictionary<string, bool> _folders = new(StringComparer.Ordinal);

        private bool FolderExists(string folder)
        {
            if (!_folders.TryGetValue(folder, out var exists)) _folders[folder] = exists = _directoryExists(folder);
            return exists;
        }

        /// <summary>
        /// True when the drive of the path is not there: a drive letter or network share that is not connected, or
        /// a drive mounted in one of the mount folders that is not mounted
        /// </summary>
        /// <param name="path">The full path</param>
        /// <param name="isFile">The entry is a file (a file directly in a mount folder is not a drive)</param>
        internal bool OnAbsentDrive(string path, bool isFile = false)
        {
            var root = Path.GetPathRoot(path);
            if (!string.IsNullOrEmpty(root) && root is not ("/" or "\\") && !FolderExists(root)) return true;
            foreach (var (folder, depth) in MountFolders)
            {
                if (!path.StartsWith(folder, StringComparison.Ordinal)) continue;
                var parts = path[folder.Length..].Split('/', StringSplitOptions.RemoveEmptyEntries);
                var place = folder.TrimEnd('/');
                for (var i = 0; i < Math.Min(depth, parts.Length - (isFile ? 1 : 0)); i++)
                {
                    place += "/" + parts[i];
                    if (!FolderExists(place)) return true;
                }

                return false;
            }

            return false;
        }

        /// <summary>
        /// True when there is no file or directory at the path (also when its drive is not there)
        /// </summary>
        internal bool IsMissing(string path, bool isFile = false) =>
            OnAbsentDrive(path, isFile) || !File.Exists(path) && !Directory.Exists(path);
    }

    /// <summary>
    /// True when the drive of the path is not there (see Checker.OnAbsentDrive)
    /// </summary>
    internal static bool OnAbsentDrive(string path, Func<string, bool>? directoryExists = null, bool isFile = false) =>
        new Checker(directoryExists).OnAbsentDrive(path, isFile);

    /// <summary>
    /// The missing entries that are pruned (their drive is there), and those kept (on a drive that is not there)
    /// </summary>
    internal static (List<T> Prune, List<T> Kept) Sort<T>(IEnumerable<T> missing, Func<T, bool> onAbsentDrive)
    {
        var prune = new List<T>();
        var kept = new List<T>();
        foreach (var entry in missing)
        {
            (onAbsentDrive(entry) ? kept : prune).Add(entry);
        }

        return (prune, kept);
    }

    /// <summary>
    /// ":missing": the missing entries in a box, to pick one to show in the list
    /// </summary>
    /// <param name="missing">The name and full path of each missing entry, in the order of the list, and whether
    /// its drive is not there</param>
    /// <returns>The number of the entry picked, or -1</returns>
    internal static int Pick(IReadOnlyList<(string Name, string Path, bool AbsentDrive)> missing)
    {
        var rows = missing.Select(entry => entry.Name).ToList();
        return PickListBox.Show(Lang.F("Missing ({0})", missing.Count), rows,
            i => missing[i].Path + "\n" + (missing[i].AbsentDrive
                ? Lang.T("Its drive is not connected: :prune keeps it")
                : Lang.T(":prune removes it from the list")), 0,
            Lang.T("j/k Select | Enter Show in list | / Filter | Esc Close"));
    }

    /// <summary>
    /// ":prune": ask before the missing entries are taken out of a list
    /// </summary>
    /// <param name="caption">The title of the box (the name of the list)</param>
    /// <param name="count">How many are removed</param>
    /// <param name="kept">How many missing are kept (on a drive that is not connected)</param>
    /// <param name="undo">True when u puts them back</param>
    internal static bool Confirm(string caption, int count, int kept, bool undo)
    {
        var note = Lang.T("Their files and directories are not there any more.");
        if (kept > 0) note += " " + Lang.F("Kept, on drives that are not connected: {0}.", kept);
        if (undo) note += " " + Lang.T("u puts them back.");
        ChoiceBox.Forget();
        return ChoiceBox.Confirm(caption, Lang.F("Remove the missing from the list ({0})?", count), note, false);
    }

    /// <summary>
    /// The message when nothing is missing (":missing")
    /// </summary>
    internal static string NothingMissing => Lang.T("Nothing is missing: all the files and directories are there");

    /// <summary>
    /// The message when nothing is pruned: nothing is missing, or only on drives that are not connected
    /// </summary>
    internal static string NothingToPrune(int kept) => kept == 0
        ? NothingMissing
        : Lang.F("Nothing removed (on drives not connected: {0})", kept);
}
