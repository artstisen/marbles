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
                catch (IOException) when (!IsLink(source) && !Directory.Exists(target))
                {
                    // To another drive: copy the directory, and remove the original when all of it is copied
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
