namespace Marbles;

/// <summary>
/// The file management of the file browser: creating directories, and renaming, copying, moving and deleting
/// files and directories. The methods return DialogText.Success or an error message, and never touch the console.
/// Nothing is overwritten: an operation is refused when the target exists. Deleted files cannot be recovered.
/// </summary>
internal static class FileOperations
{
    private const string CopySuffix = "-copy";

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private static string Trimmed(string path)
    {
        var full = Path.GetFullPath(path);
        // The root ("C:\" or "/") keeps its separator
        return full.Length > (Path.GetPathRoot(full)?.Length ?? 0)
            ? full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            : full;
    }

    /// <summary>
    /// True if two paths are the same file or directory
    /// </summary>
    internal static bool SamePath(string a, string b)
    {
        return a.Length > 0 && b.Length > 0 && string.Equals(Trimmed(a), Trimmed(b), PathComparison);
    }

    /// <summary>
    /// True if a path is somewhere inside a directory
    /// </summary>
    internal static bool IsInside(string path, string directory)
    {
        if (path.Length == 0 || directory.Length == 0) return false;
        var parent = Trimmed(directory);
        if (!parent.EndsWith(Path.DirectorySeparatorChar)) parent += Path.DirectorySeparatorChar;
        return Trimmed(path).StartsWith(parent, PathComparison);
    }

    private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    /// <summary>
    /// A path with the links in it followed, so the same place is written the same way: on Linux and macOS a
    /// directory can be a link to another (on macOS "/var" and "/tmp" are links into "/private"), and the current
    /// directory is told with the links followed. The parts that are not there are kept as they are. On Windows,
    /// the full path
    /// </summary>
    internal static string RealPath(string path)
    {
        var full = Path.GetFullPath(path);
        if (OperatingSystem.IsWindows()) return full;
        try
        {
            var root = Path.GetPathRoot(full) ?? string.Empty;
            var current = root;
            var parts = full[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < parts.Length; i++)
            {
                var next = Path.Combine(current, parts[i]);
                FileSystemInfo item = Directory.Exists(next) ? new DirectoryInfo(next) : new FileInfo(next);
                if (!item.Exists) return Path.Combine(new[] { current }.Concat(parts[i..]).ToArray());
                current = item.LinkTarget != null && item.ResolveLinkTarget(true) is { } target
                    ? Path.GetFullPath(target.FullName)
                    : next;
            }

            return current;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return full;
        }
    }

    /// <summary>
    /// A path as written in a list file (favorites, history, bookmarks, jumps): without blank space at its ends,
    /// unless a file or directory has those in its name (Linux and macOS allow it) and is there
    /// </summary>
    internal static string ListedPath(string written)
    {
        var path = written.Trim();
        // (Windows has no names ending with blank space, and finds "notes.txt " as "notes.txt")
        if (OperatingSystem.IsWindows()) return path;
        return path.Length > 0 && written != path && Exists(written) ? written : path;
    }

    // A link to a file or directory (the link is deleted or moved, never what it points to)
    private static bool IsLink(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// The number of files and directories in a directory (all levels), as far as they can be read
    /// </summary>
    internal static (int Files, int Directories) Count(string directory)
    {
        var files = 0;
        var directories = 0;
        if (IsLink(directory)) return (0, 0);
        try
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.None
            };
            foreach (var entry in new DirectoryInfo(directory).EnumerateFileSystemInfos("*", options))
            {
                if ((entry.Attributes & FileAttributes.Directory) != 0)
                {
                    directories++;
                }
                else
                {
                    files++;
                }
            }
        }
        catch
        {
            // The number is only for information
        }

        return (files, directories);
    }

    /// <summary>
    /// Create a directory in a directory
    /// </summary>
    internal static string CreateDirectory(string parent, string name, out string newPath)
    {
        newPath = string.Empty;
        name = name.Trim();
        var problem = Tools.FileNameProblem(name);
        if (problem != null) return problem;
        var path = Path.Combine(parent, name);
        if (Exists(path)) return DialogText.NameExists;
        try
        {
            Directory.CreateDirectory(path);
        }
        catch (Exception e)
        {
            return e.Message;
        }

        newPath = path;
        return DialogText.Success;
    }

    /// <summary>
    /// Give a file or directory a new name
    /// </summary>
    internal static string Rename(string path, string newName, out string newPath)
    {
        newPath = string.Empty;
        newName = newName.Trim();
        var problem = Tools.FileNameProblem(newName);
        if (problem != null) return problem;
        var directory = Path.GetDirectoryName(Trimmed(path));
        if (directory == null) return DialogText.InvalidFileName;
        var target = Path.Combine(directory, newName);
        var oldName = Path.GetFileName(Trimmed(path));
        if (oldName == newName)
        {
            newPath = target;
            return DialogText.Success;
        }

        // Only the case of the name can differ ("notes.txt" to "Notes.txt") where names are not case sensitive
        var caseOnly = SamePath(path, target) && string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase);
        if (!caseOnly && Exists(target)) return DialogText.NameExists;
        try
        {
            if (Directory.Exists(path) && !File.Exists(path))
            {
                if (caseOnly)
                {
                    // A directory cannot be moved to itself: go by another name
                    var temporary = UniquePath(directory, newName + "-" + Guid.NewGuid().ToString("N")[..8]);
                    Directory.Move(path, temporary);
                    Directory.Move(temporary, target);
                }
                else
                {
                    Directory.Move(path, target);
                }
            }
            else
            {
                File.Move(path, target);
            }
        }
        catch (Exception e)
        {
            return e.Message;
        }

        newPath = target;
        return DialogText.Success;
    }

    /// <summary>
    /// A path in a directory that is not in use: the name itself, or "name-copy", "name-copy2" ...
    /// </summary>
    internal static string UniquePath(string directory, string name)
    {
        var path = Path.Combine(directory, name);
        if (!Exists(path)) return path;
        var isDirectory = Directory.Exists(path);
        var stem = isDirectory ? name : Path.GetFileNameWithoutExtension(name);
        var extension = isDirectory ? string.Empty : Path.GetExtension(name);
        for (var number = 1;; number++)
        {
            path = Path.Combine(directory, stem + CopySuffix + (number == 1 ? string.Empty : number.ToString()) +
                                           extension);
            if (!Exists(path)) return path;
        }
    }

    /// <summary>
    /// Copy a file or directory to a directory. A copy made in the directory where the original is
    /// (a duplicate) gets a new name.
    /// </summary>
    /// <param name="source">The file or directory</param>
    /// <param name="targetDirectory">The directory to copy it to</param>
    /// <param name="newPath">The path of the copy</param>
    /// <param name="progress">Called with the name of each file before it is copied. Returns false to cancel</param>
    internal static string Copy(string source, string targetDirectory, out string newPath,
        Func<string, bool>? progress = null)
    {
        newPath = string.Empty;
        var name = Path.GetFileName(Trimmed(source));
        var isDirectory = Directory.Exists(source) && !File.Exists(source);
        if (!isDirectory && !File.Exists(source)) return DialogText.FileNotFound;
        if (!Directory.Exists(targetDirectory)) return DialogText.DirectoryNotFound;
        if (isDirectory && (SamePath(targetDirectory, source) || IsInside(targetDirectory, source)))
        {
            return DialogText.FileIntoItself;
        }

        var sourceDirectory = Path.GetDirectoryName(Trimmed(source)) ?? string.Empty;
        var target = SamePath(sourceDirectory, targetDirectory)
            ? UniquePath(targetDirectory, name)
            : Path.Combine(targetDirectory, name);
        if (Exists(target)) return DialogText.ErrorFileAlreadyExist;
        try
        {
            if (!isDirectory)
            {
                File.Copy(source, target, false);
            }
            else if (!CopyDirectory(source, target, progress))
            {
                // Canceled: remove what was copied
                Directory.Delete(target, true);
                return DialogText.FileCanceled;
            }
        }
        catch (Exception e)
        {
            // Do not leave half a copy of a directory
            try
            {
                if (isDirectory && Directory.Exists(target)) Directory.Delete(target, true);
            }
            catch
            {
                // The error of the copy is the one to show
            }

            return e.Message;
        }

        newPath = target;
        return DialogText.Success;
    }

    private static bool CopyDirectory(string source, string target, Func<string, bool>? progress)
    {
        Directory.CreateDirectory(target);
        var info = new DirectoryInfo(source);
        foreach (var file in info.EnumerateFiles())
        {
            if (progress != null && !progress(file.Name)) return false;
            file.CopyTo(Path.Combine(target, file.Name), false);
        }

        foreach (var directory in info.EnumerateDirectories())
        {
            var copy = Path.Combine(target, directory.Name);
            if (directory.LinkTarget != null)
            {
                // A link is copied as a link: what it points to is not followed
                Directory.CreateSymbolicLink(copy, directory.LinkTarget);
            }
            else if (!CopyDirectory(directory.FullName, copy, progress))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Move a file or directory to another directory
    /// </summary>
    /// <param name="source">The file or directory</param>
    /// <param name="targetDirectory">The directory to move it to</param>
    /// <param name="newPath">The new path</param>
    /// <param name="progress">Called with the name of each file when the files have to be copied (to another
    /// drive). Returns false to cancel</param>
    internal static string Move(string source, string targetDirectory, out string newPath,
        Func<string, bool>? progress = null)
    {
        newPath = string.Empty;
        var name = Path.GetFileName(Trimmed(source));
        var isDirectory = Directory.Exists(source) && !File.Exists(source);
        if (!isDirectory && !File.Exists(source)) return DialogText.FileNotFound;
        if (!Directory.Exists(targetDirectory)) return DialogText.DirectoryNotFound;
        var sourceDirectory = Path.GetDirectoryName(Trimmed(source)) ?? string.Empty;
        if (SamePath(sourceDirectory, targetDirectory)) return DialogText.FileAlreadyThere;
        if (isDirectory && (SamePath(targetDirectory, source) || IsInside(targetDirectory, source)))
        {
            return DialogText.FileIntoItself;
        }

        var target = Path.Combine(targetDirectory, name);
        if (Exists(target)) return DialogText.ErrorFileAlreadyExist;
        try
        {
            if (!isDirectory)
            {
                File.Move(source, target, false);
            }
            else
            {
                try
                {
                    Directory.Move(source, target);
                }
                catch (IOException e) when (!IsLink(source) && !Directory.Exists(target) &&
                                            IsOtherDrive(e, source, targetDirectory))
                {
                    // To another drive: copy the directory, and remove the original when all of it is copied.
                    // Only then: a directory that cannot be moved for another reason (a file in it is open) is
                    // left as it is, and the error is told
                    var result = Copy(source, targetDirectory, out _, progress);
                    if (result != DialogText.Success) return result;
                    Directory.Delete(source, true);
                }
            }
        }
        catch (Exception e)
        {
            return e.Message;
        }

        newPath = target;
        return DialogText.Success;
    }

    // A directory move that failed because the target is on another drive: Windows checks the drives first (or
    // tells ERROR_NOT_SAME_DEVICE), Linux and macOS tell EXDEV (18)
    private static bool IsOtherDrive(IOException error, string source, string targetDirectory)
    {
        if (!OperatingSystem.IsWindows()) return error.HResult == 18 || (error.HResult & 0xFFFF) == 18;
        const int notSameDevice = unchecked((int)0x80070011);
        return error.HResult == notSameDevice || !string.Equals(Path.GetPathRoot(Path.GetFullPath(source)),
            Path.GetPathRoot(Path.GetFullPath(targetDirectory)), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The trash: the directory "trash" in the data directory, for all that is deleted in the file browser (and the
    /// data files deleted on the login page). It goes with the data directory when Marbles is copied or backed up. A
    /// data file stays encrypted in it; Marbles writes no other file with notes in it
    /// </summary>
    internal const string TrashName = "trash"; // English: the name of a directory

    /// <summary>
    /// The trash directory, for tests
    /// </summary>
    internal static string? TrashOverride { get; set; }

    /// <summary>
    /// The trash directory (it is made when the first file is moved to it)
    /// </summary>
    internal static string TrashDirectory =>
        Path.GetFullPath(TrashOverride ?? Path.Combine(FileIo.GetDataDirectoryName(), TrashName));

    /// <summary>
    /// Deleting in the file browser and on the login page moves to the trash (setting "deletetotrash", on unless
    /// turned off). Off: the files are deleted at once and cannot be recovered
    /// </summary>
    internal static bool DeleteToTrash { get; set; } = true;

    /// <summary>
    /// The list in the trash of what was moved there: one line for each, with its name in the trash, when it was
    /// deleted and where it was
    /// </summary>
    internal const string TrashListName = "trash.txt"; // English: the name of a file

    /// <summary>
    /// True if a file or directory can be moved to the trash: not the trash itself, something in it (deleting
    /// there is for good), or a directory the trash is in
    /// </summary>
    internal static bool CanTrash(string path, string trashDirectory)
    {
        return !SamePath(path, trashDirectory) && !IsInside(path, trashDirectory) && !IsInside(trashDirectory, path);
    }

    // The part of a directory copied before the copying stopped (it can be deleted in the trash if it stays)
    private static void RemovePartialCopy(string target)
    {
        if (!Directory.Exists(target)) return;
        try
        {
            Directory.Delete(target, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Left in the trash: it can be deleted there
        }
    }

    /// <summary>
    /// A name for a file in the trash: one that can be made on every file system, also FAT and exFAT on a memory
    /// stick (no : ? * " &lt; &gt; | \ / or control characters, no blank space or dot at the end), and short enough to
    /// have the time added: at most maxBytes in UTF-8, as Linux and macOS count a name (255 bytes; a letter like "ø"
    /// takes two), never splitting a character. The real name is kept in the list of the trash
    /// </summary>
    internal static string StoredName(string name, int maxBytes = 180)
    {
        var chars = name.Select(c => c is ':' or '?' or '*' or '"' or '<' or '>' or '|' or '\\' or '/' ||
                                     char.IsControl(c)
            ? '_'
            : c).ToArray();
        var stored = new string(chars);
        var end = 0;
        var bytes = 0;
        while (end < stored.Length)
        {
            var length = char.IsSurrogatePair(stored, end) ? 2 : 1;
            var size = System.Text.Encoding.UTF8.GetByteCount(stored.AsSpan(end, length));
            if (bytes + size > maxBytes) break;
            bytes += size;
            end += length;
        }

        stored = stored[..end].TrimEnd(' ', '.');
        return stored.Length > 0 ? stored : "_";
    }

    /// <summary>
    /// Move a file or directory to the trash, under a name of its own there that starts with the time
    /// ("2026-10-22 14.03.11 chapter 1.md"), and add where it was to the list in the trash. The trash is made if
    /// it is not there. A data file keeps its extension, so it is still a data file in the trash
    /// </summary>
    /// <param name="path">The file or directory</param>
    /// <param name="trashDirectory">The trash</param>
    /// <param name="trashedPath">Where it is in the trash</param>
    /// <param name="progress">Called with the name of each file when the files have to be copied (the trash is
    /// on another drive). Returns false to cancel</param>
    internal static string MoveToTrash(string path, string trashDirectory, out string trashedPath,
        Func<string, bool>? progress = null)
    {
        trashedPath = string.Empty;
        var isDirectory = Directory.Exists(path) && !File.Exists(path);
        if (!isDirectory && !File.Exists(path)) return DialogText.FileNotFound;
        if (!CanTrash(path, trashDirectory)) return DialogText.TrashNotPossible;
        var deleted = DateTime.Now;
        var name = deleted.ToString("yyyy-MM-dd HH.mm.ss ", System.Globalization.CultureInfo.InvariantCulture) +
                   StoredName(Path.GetFileName(Trimmed(path)));
        try
        {
            Directory.CreateDirectory(trashDirectory);
            var target = UniquePath(trashDirectory, name);
            if (!isDirectory)
            {
                // (to another drive the file is copied, and the original removed)
                File.Move(path, target, false);
            }
            else
            {
                try
                {
                    Directory.Move(path, target);
                }
                catch (IOException e) when (!IsLink(path) && !Directory.Exists(target) &&
                                            IsOtherDrive(e, path, trashDirectory))
                {
                    // As when it is moved to another drive: the original is removed when all of it is copied. A
                    // copy that stops (a file that cannot be read, a full disk) is removed again: the original stays
                    bool copied;
                    try
                    {
                        copied = CopyDirectory(path, target, progress);
                    }
                    catch
                    {
                        RemovePartialCopy(target);
                        throw;
                    }

                    if (!copied)
                    {
                        RemovePartialCopy(target);
                        return DialogText.FileCanceled;
                    }

                    Directory.Delete(path, true);
                }
            }

            trashedPath = target;
        }
        catch (Exception e)
        {
            return e.Message;
        }

        try
        {
            // LF on all systems. The list is only for finding where it was (added to: never emptied)
            var list = Path.Combine(trashDirectory, TrashListName);
            var line = Path.GetFileName(trashedPath) + "\t" +
                       deleted.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture) +
                       "\t" + Path.GetFullPath(path) + "\n";
            if (!File.Exists(list))
            {
                // English: the list is read by people, in any language
                line = "# Marbles: what was moved to the trash (name in the trash, when, where it was)\n" + line;
            }

            File.AppendAllText(list, line, new System.Text.UTF8Encoding(false));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // It is in the trash: the list is only a help to find where it came from
        }

        return DialogText.Success;
    }

    /// <summary>
    /// Delete a file, or a directory with all its content. This cannot be undone
    /// </summary>
    internal static string Delete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            else if (Directory.Exists(path))
            {
                // Only the link is removed if it is a link to a directory
                Directory.Delete(path, !IsLink(path));
            }
            else
            {
                return DialogText.FileNotFound;
            }
        }
        catch (Exception e)
        {
            return e.Message;
        }

        return DialogText.Success;
    }
}
