namespace Marbles;

/// <summary>
/// What Marbles opens when it is started with a file or directory, as "marbles notes.md" in a terminal (a path that
/// is not a full path is taken from the directory Marbles is started in):
///   a text file      opened in the editor, with the file browser in its directory behind it
///   a directory      shown in the file browser
///   a data file      selected on the login page (one outside the data directory is shown in the file browser,
///                    which offers to copy it there)
///   a file that is not there, in a directory that is: Marbles asks to make it
/// The place is opened as a jump of the Jump menu (Jump.Pending). This class does not touch the console.
/// </summary>
internal static class StartArgument
{
    /// <summary>
    /// What a path given at startup leads to: a place to open, or a file to make first (when the user wants that),
    /// or a message (the path cannot be used)
    /// </summary>
    internal sealed record Result(JumpTarget? Target, string Path, bool AskToCreate = false, string Message = "");

    /// <summary>
    /// True if the argument is an option ("--help", "-v" ...): it starts with "-" and is not a file or directory
    /// that is there (a file can be named "-notes.md")
    /// </summary>
    internal static bool IsOption(string argument, string currentDirectory)
    {
        if (!argument.StartsWith('-') || argument == "-") return false;
        try
        {
            var path = System.IO.Path.GetFullPath(argument, currentDirectory);
            return !File.Exists(path) && !Directory.Exists(path);
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or IOException)
        {
            return true;
        }
    }

    /// <summary>
    /// Answer an option: the usage or the version, written to the terminal. Returns the exit code (1 for an option
    /// that is not known). English: written before the settings (and the language) are read
    /// </summary>
    internal static int AnswerOption(string option, TextWriter output)
    {
        switch (option.ToLowerInvariant())
        {
            case "--version" or "-v" or "-version":
                output.WriteLine(DialogText.ApplicationName + " " + DialogText.ApplicationVersion);
                return 0;
            case "--help" or "-h" or "-?" or "-help":
                output.WriteLine(Usage);
                return 0;
            default:
                output.WriteLine("Unknown option: " + option);
                output.WriteLine(Usage);
                return 1;
        }
    }

    // English: see AnswerOption
    private const string Usage =
        "Usage: marbles [file, directory or data file]\n" +
        "  A file is opened in the editor, a directory in the file browser, and a data\n" +
        "  file (.mbls) is selected on the login page. A file that is not there (in a\n" +
        "  directory that is) can be made. Without a path, Marbles starts as it is\n" +
        "  set to.\n" +
        "  --help, -h    Show this text\n" +
        "  --version, -v Show the version";

    /// <summary>
    /// Find what a path given at startup leads to
    /// </summary>
    /// <param name="argument">The path, as given</param>
    /// <param name="currentDirectory">The directory Marbles is started in</param>
    /// <param name="only">The one mode Marbles is set to, if any</param>
    internal static Result Resolve(string argument, string currentDirectory, StartMenu.OnlyMode only)
    {
        string path;
        try
        {
            path = System.IO.Path.GetFullPath(argument.Trim().Trim('"'), currentDirectory);
        }
        catch (Exception e)
        {
            return new Result(null, argument, Message: e.Message);
        }

        var dataFile = path.EndsWith(FileIo.GetFileNameExtension(), StringComparison.OrdinalIgnoreCase);
        if (Directory.Exists(path))
        {
            return only == StartMenu.OnlyMode.Notes
                ? new Result(null, path, Message: DialogText.EditorNotUsed)
                : new Result(new JumpTarget(JumpKind.Directory, path), path);
        }

        if (File.Exists(path))
        {
            if (dataFile)
            {
                if (only == StartMenu.OnlyMode.Editor) return new Result(null, path, Message: DialogText.NotesNotUsed);
                // A data file in the data directory (or a directory in it) is selected on the login page
                if (DataFileInDataDirectory(path) is { } inData)
                {
                    return new Result(new JumpTarget(JumpKind.DataFile, inData), path);
                }
            }
            else if (only == StartMenu.OnlyMode.Notes)
            {
                return new Result(null, path, Message: DialogText.EditorNotUsed);
            }

            return new Result(new JumpTarget(JumpKind.File, path), path);
        }

        // Not there: a text file can be made (in a directory that is there)
        var parent = System.IO.Path.GetDirectoryName(path);
        if (!dataFile && only != StartMenu.OnlyMode.Notes && parent != null && Directory.Exists(parent))
        {
            return new Result(new JumpTarget(JumpKind.File, path), path, AskToCreate: true);
        }

        return new Result(null, path, Message: Lang.F("Not found: {0}", path));
    }

    // The path of a data file in the data directory as the login page knows it ("work.mbls", "notes/work.mbls"), or
    // null if it is not in the data directory or a directory in it
    internal static string? DataFileInDataDirectory(string path)
    {
        // (with links followed: the same directory can be written in two ways)
        var data = FileOperations.RealPath(FileIo.GetDataDirectoryName());
        path = FileOperations.RealPath(path);
        if (!FileOperations.IsInside(path, data)) return null;
        var relative = System.IO.Path.GetRelativePath(data, path).Replace('\\', '/');
        return relative.Count(c => c == '/') <= 1 ? relative : null;
    }
}
