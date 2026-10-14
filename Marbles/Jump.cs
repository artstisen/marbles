using System.Globalization;
using System.Text;

namespace Marbles;

/// <summary>
/// What a jump goes to: a file (opened in the editor), a directory (shown in the file browser), a page of
/// Marbles, or a data file (selected on the login page)
/// </summary>
internal enum JumpKind
{
    File,
    Directory,
    Page,
    DataFile
}

/// <summary>
/// A place to jump to. For a file and a directory the target is the full path, for a page the name of the page
/// (see JumpPages), and for a data file its path in the data directory ("work.mbls" or "notes/work.mbls")
/// </summary>
internal sealed record JumpTarget(JumpKind Kind, string Target)
{
    /// <summary>
    /// The name shown in the Jump menu: the name of a file or directory (not its path), the name of a page, or
    /// the path of a data file in the data directory
    /// </summary>
    internal string Name => Kind switch
    {
        JumpKind.File or JumpKind.Directory => System.IO.Path.GetFileName(
            System.IO.Path.TrimEndingDirectorySeparator(Target)) is { Length: > 0 } name ? name : Target,
        JumpKind.Page => JumpPages.Label(Target),
        _ => Target
    };

    /// <summary>
    /// The full path of a file, directory or data file (empty for a page)
    /// </summary>
    internal string FullPath => Kind switch
    {
        JumpKind.File or JumpKind.Directory => Target,
        JumpKind.DataFile => System.IO.Path.GetFullPath(System.IO.Path.Combine(FileIo.GetDataDirectoryName(),
            Target.Replace('/', System.IO.Path.DirectorySeparatorChar))),
        _ => string.Empty
    };

    /// <summary>
    /// True if what it goes to is there (a page always is)
    /// </summary>
    internal bool Exists => Kind switch
    {
        JumpKind.File or JumpKind.DataFile => File.Exists(FullPath),
        JumpKind.Directory => Directory.Exists(FullPath),
        _ => JumpPages.Ids.Contains(Target)
    };

    /// <summary>
    /// The same place as another (paths are compared as the file system does)
    /// </summary>
    internal bool SameAs(JumpTarget other) =>
        Kind == other.Kind && (Kind == JumpKind.Page
            ? Target.Equals(other.Target, StringComparison.OrdinalIgnoreCase)
            : FileOperations.SamePath(FullPath, other.FullPath));
}

/// <summary>
/// The pages of Marbles that can be jumped to, by their names in the jump history (they do not change when the
/// menus do). The pages of the Configuration tab are shown on the Select mode page; the others are opened on
/// top of where the jump was made from
/// </summary>
internal static class JumpPages
{
    internal static readonly IReadOnlyList<string> Ids = new[]
    {
        "config/general", "config/layout", "config/editor", "config/system", "schemes", "logos", "clipboard",
        "snippets", "help", "about"
    };

    /// <summary>
    /// The page of the Configuration tab (0 to 3) of a page, or -1 if it is not one
    /// </summary>
    internal static int ConfigurationPage(string id) => id switch
    {
        "config/general" => 0,
        "config/layout" => 1,
        "config/editor" => 2,
        "config/system" => 3,
        _ => -1
    };

    /// <summary>
    /// The name of a page in the language in use
    /// </summary>
    internal static string Label(string id)
    {
        return id switch
        {
            "config/general" => Lang.F("{0}: {1}", DialogText.StartConfiguration, Lang.T("General")),
            "config/layout" => Lang.F("{0}: {1}", DialogText.StartConfiguration, Lang.T("Layout")),
            "config/editor" => Lang.F("{0}: {1}", DialogText.StartConfiguration, Lang.T("Editor")),
            "config/system" => Lang.F("{0}: {1}", DialogText.StartConfiguration, Lang.T("System")),
            "schemes" => DialogText.CaptionsColorScheme,
            "logos" => DialogText.CaptionsLogos,
            "clipboard" => Lang.T("Clipboard"),
            "snippets" => Lang.T("Snippet editor"),
            "help" => Lang.T("Help"),
            "about" => Lang.T("About"),
            _ => id
        };
    }
}

/// <summary>
/// The jump history: the places jumped to with the Jump menu, the latest first. A place jumped to again moves
/// to the top, and the list holds at most MaxCount places.
///
/// The list is kept in the file "jump.txt" in the data directory, with one line for each place: the time of
/// the jump (yyyy-MM-dd HH:mm), a tab character, the kind of place (file, dir, page or data), a tab character
/// and the place: the path of a file or directory, the name of a page, or the path of a data file in the data
/// directory.
/// </summary>
internal static class JumpHistory
{
    internal const string FileName = "jump.txt";
    internal const int MaxCount = 100;
    private const string TimeFormat = "yyyy-MM-dd HH:mm";

    internal sealed record Entry(JumpTarget Target, DateTime Jumped);

    private static readonly List<Entry> List = new();

    /// <summary>
    /// The places, the latest jumped to first
    /// </summary>
    internal static IReadOnlyList<Entry> Items => List;

    /// <summary>
    /// The file with the list. It can be set to another file than the one in the data directory (for testing)
    /// </summary>
    internal static string? FilePathOverride { get; set; }

    internal static string FilePath => FilePathOverride ?? Path.Combine(FileIo.GetDataDirectoryName(), FileName);

    private static string KindName(JumpKind kind) => kind switch
    {
        JumpKind.Directory => "dir",
        JumpKind.Page => "page",
        JumpKind.DataFile => "data",
        _ => "file"
    };

    private static JumpKind? ParseKind(string name) => name.Trim().ToLowerInvariant() switch
    {
        "file" => JumpKind.File,
        "dir" => JumpKind.Directory,
        "page" => JumpKind.Page,
        "data" => JumpKind.DataFile,
        _ => null
    };

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
            if (parts.Length < 3 || ParseKind(parts[1]) is not { } kind) continue;
            var place = parts[2].Trim();
            if (place.Length == 0) continue;
            var target = new JumpTarget(kind, place);
            if (List.Any(entry => entry.Target.SameAs(target))) continue;
            if (!DateTime.TryParseExact(parts[0].Trim(), TimeFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var jumped))
            {
                jumped = DateTime.MinValue;
            }

            List.Add(new Entry(target, jumped));
            if (List.Count == MaxCount) break;
        }
    }

    internal static string Serialize()
    {
        var text = new StringBuilder();
        foreach (var entry in List)
        {
            text.Append(entry.Jumped.ToString(TimeFormat, CultureInfo.InvariantCulture)).Append('\t')
                .Append(KindName(entry.Target.Kind)).Append('\t').Append(entry.Target.Target).Append('\n');
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

    /// <summary>
    /// Put a place at the top of the list (moving it there if it is in the list). The oldest is dropped if the
    /// list is full
    /// </summary>
    internal static void Add(JumpTarget target, DateTime jumped)
    {
        List.RemoveAll(entry => entry.Target.SameAs(target));
        List.Insert(0, new Entry(target, jumped));
        if (List.Count > MaxCount) List.RemoveRange(MaxCount, List.Count - MaxCount);
    }

    /// <summary>
    /// A file or directory has a new path (renamed or moved): the files and directories of the list follow it
    /// </summary>
    /// <returns>True if the list was changed</returns>
    internal static bool PathChanged(string oldPath, string newPath)
    {
        var changed = false;
        for (var i = 0; i < List.Count; i++)
        {
            var target = List[i].Target;
            if (target.Kind is not (JumpKind.File or JumpKind.Directory)) continue;
            if (CaretPosition.NewPath(target.Target, oldPath, newPath) is not { } path) continue;
            List[i] = List[i] with { Target = target with { Target = path } };
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// Take a place out of the list
    /// </summary>
    internal static void RemoveAt(int index)
    {
        if (index >= 0 && index < List.Count) List.RemoveAt(index);
    }

    internal static void Clear() => List.Clear();
}

/// <summary>
/// Jumping: the Jump menu is opened (":jump", ":ju" or Ctrl+O, and from the options of the login page), and the
/// place picked there is gone to. A page that is shown on top of a page (color schemes, logos, clipboard,
/// snippets, help, about) is opened where the jump was made from. For the other places (a file, a directory,
/// a page of the Configuration tab, a data file) the page the jump was made from is left: the jump is kept as
/// pending and control goes back to the main loop (see Navigation), which makes it. A data file that is logged
/// in to is logged out of first.
/// </summary>
internal static class Jump
{
    /// <summary>
    /// The place to go to when control is back in the main loop (null: none)
    /// </summary>
    internal static JumpTarget? Pending { get; private set; }

    /// <summary>
    /// True for the key that opens the Jump menu: Ctrl+O
    /// </summary>
    internal static bool IsKey(ConsoleKeyInfo key) =>
        key.Key == ConsoleKey.O && (key.Modifiers & ConsoleModifiers.Control) != 0 &&
        (key.Modifiers & ConsoleModifiers.Alt) == 0;

    /// <summary>
    /// True for the places that are opened where the jump was made from (the page is not left)
    /// </summary>
    internal static bool OpensInPlace(JumpTarget target) =>
        target.Kind == JumpKind.Page && JumpPages.ConfigurationPage(target.Target) < 0;

    /// <summary>
    /// Show the Jump menu and go to the place picked. Returns when nothing was picked, or the place was opened
    /// on top of the page (which must then be drawn again). When the page is left, it does not return
    /// </summary>
    /// <param name="beforeLeaving">Asked before the page is left (as about changes that are not saved): false
    /// to stay</param>
    internal static void Open(Func<bool>? beforeLeaving = null)
    {
        var target = JumpMenu.Pick();
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        if (target == null) return;
        if (OpensInPlace(target))
        {
            Remember(target);
            OpenInPlace(target.Target);
            return;
        }

        if (beforeLeaving != null && !beforeLeaving()) return;
        Remember(target);
        Pending = target;
        Navigation.Wait();
    }

    // Keep a jump in the jump history
    private static void Remember(JumpTarget target)
    {
        JumpHistory.Load();
        JumpHistory.Add(target, DateTime.Now);
        var status = JumpHistory.Save();
        if (status != DialogText.Success) EventLog.Add(DialogText.LogFile + "Jumps: " + status);
    }

    // A page shown on top of the page the jump was made from
    private static void OpenInPlace(string page)
    {
        ChoiceBox.Forget();
        switch (page)
        {
            case "schemes":
                SchemeEditor.Show();
                break;
            case "logos":
                LogoEditor.Show();
                break;
            case "clipboard":
                ClipboardPage.Show();
                break;
            case "snippets":
                if (SnippetBox.EnsureFile()) FileExplorer.EditSnippets(0);
                break;
            case "help":
                Tools.WriteEditorHelp(false);
                break;
            case "about":
                Actions.ShowAboutPages();
                break;
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Tools.ClearScreen();
    }

    /// <summary>
    /// Make the pending jump (called by the main loop). It ends with the screen it goes to (see Navigation)
    /// </summary>
    internal static void RunPending()
    {
        var target = Pending;
        Pending = null;
        if (target == null) return;
        // A data file that is logged in to is logged out of
        if (Mode.CurrentMode != Mode.SelectedMode.Login)
        {
            Tools.RemovePrivileges(true);
            Mode.SetMode(Mode.SelectedMode.Login);
        }

        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Tools.ClearScreen();
        switch (target.Kind)
        {
            case JumpKind.File:
            case JumpKind.Directory:
                if (!target.Exists)
                {
                    NotFound(target);
                    StartMenu.Open();
                    break;
                }

                // The file browser, in the directory of the file (which is opened in the editor), or in the
                // directory. Closing it shows the Select mode page, as when it is closed there
                if (target.Kind == JumpKind.File) FileExplorer.RunAtFile(target.FullPath);
                else FileExplorer.RunAtDirectory(target.FullPath);
                if (!FileExplorer.DataFileSelected) StartMenu.Open();
                break;
            case JumpKind.DataFile:
                if (!target.Exists)
                {
                    NotFound(target);
                    break;
                }

                // The data file is selected on the login page
                var relative = target.Target.Replace('\\', '/');
                var slash = relative.LastIndexOf('/');
                FileIo.ResetSubFolder();
                FileIo.SetSubFolder(slash < 0 ? string.Empty : relative[..slash]);
                FileIo.SetFileName(slash < 0 ? relative : relative[(slash + 1)..]);
                FileBrowser.ResolveSubFolderIndex();
                break;
            default:
                // A page of the Configuration tab of the Select mode page
                StartMenu.Open(JumpPages.ConfigurationPage(target.Target));
                break;
        }

        Tools.LoginRoutine(true);
    }

    // The place is not there any more (a file that was deleted or moved)
    private static void NotFound(JumpTarget target)
    {
        ChoiceBox.Forget();
        ChoiceBox.Message(DialogText.CaptionsJump, DialogText.FileNotFound, target.FullPath);
    }
}
