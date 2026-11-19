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
    /// The line and column (from 1) a bookmark of a file was made at in the editor (":bookmark"): the file is opened
    /// there. Null: where the cursor was left. Only a file has one: a place in a note is never kept (it would be
    /// written to bookmarks.txt unencrypted)
    /// </summary>
    internal (int Line, int Column)? At { get; init; }

    /// <summary>
    /// The file selected in a directory shown in the file browser (":browse" in the editor: the file edited). Null:
    /// the first entry
    /// </summary>
    internal string? Select { get; init; }

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
    /// The place after a file or directory got a new path (renamed or moved), or null if it is not affected. A
    /// data file stays a data file while it is in the data directory
    /// </summary>
    internal JumpTarget? Followed(string oldPath, string newPath)
    {
        switch (Kind)
        {
            case JumpKind.File or JumpKind.Directory:
                return CaretPosition.NewPath(Target, oldPath, newPath) is { } path ? this with { Target = path } : null;
            case JumpKind.DataFile:
                // (with links followed: the data directory can be written in two ways)
                return CaretPosition.NewPath(FileOperations.RealPath(FullPath), FileOperations.RealPath(oldPath),
                           FileOperations.RealPath(newPath)) is { } moved &&
                       StartArgument.DataFileInDataDirectory(moved) is { } inData
                    ? this with { Target = inData }
                    : null;
            default:
                return null;
        }
    }

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
        "config/general", "config/layout", "config/editor", "config/lists", "config/system", "favorites", "history",
        "hotlist", "browse", "schemes", "logos", "clipboard", "snippets", "completions", "log", "help", "about"
    };

    /// <summary>
    /// The page of the file browser a command goes to: ":favorites" (":favs"), ":history" (":hist"), ":hotlist"
    /// (":hot") or ":browse", or null for another command
    /// </summary>
    internal static string? ListPageOf(string command) => command switch
    {
        "favorites" or "favs" => "favorites",
        "history" or "hist" => "history",
        "hotlist" or "hot" => "hotlist",
        "browse" => "browse",
        _ => null
    };

    /// <summary>
    /// True for the file browser and its lists (a file opened from them opens the editor, which cannot be on top of
    /// another page)
    /// </summary>
    internal static bool IsFileBrowser(string id) => id is "favorites" or "history" or "hotlist" or "browse";

    /// <summary>
    /// True for the pages that leave the page the jump was made from (and the notes, unless staying logged in): the
    /// pages of the Configuration tab, and the file browser and its lists
    /// </summary>
    internal static bool Leaves(string id) => ConfigurationPage(id) >= 0 || IsFileBrowser(id);

    /// <summary>
    /// True for the pages opened on top that change settings (and save them): color schemes, logos, the clipboard,
    /// and the editors of snippets and completions. Not opened while the settings file is edited
    /// </summary>
    internal static bool ChangesSettings(string id) =>
        id is "schemes" or "logos" or "clipboard" or "snippets" or "completions";

    // The name of a page in ":jump general"
    private static string NameOf(string id) => id == "schemes" ? "colors" : id[(id.IndexOf('/') + 1)..];

    /// <summary>
    /// The names of the pages in ":jump general" (English in all languages, as commands)
    /// </summary>
    internal static IEnumerable<string> Names => Ids.Select(NameOf);

    /// <summary>
    /// The page with a name given to ":jump" (upper and lower case alike), or null if there is none
    /// </summary>
    internal static string? IdOf(string name) =>
        Ids.FirstOrDefault(id => NameOf(id).Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The page of the Configuration tab (0 to 4) of a page, or -1 if it is not one
    /// </summary>
    internal static int ConfigurationPage(string id) => id switch
    {
        "config/general" => 0,
        "config/layout" => 1,
        "config/editor" => 2,
        "config/lists" => 3,
        "config/system" => 4,
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
            "config/lists" => Lang.F("{0}: {1}", DialogText.StartConfiguration, Lang.T("Lists")),
            "config/system" => Lang.F("{0}: {1}", DialogText.StartConfiguration, Lang.T("System")),
            "favorites" => DialogText.HelpSectionFavorites,
            "history" => DialogText.HelpSectionHistory,
            "hotlist" => DialogText.HelpSectionHotlist,
            "browse" => DialogText.HelpSectionFiles,
            "schemes" => DialogText.CaptionsColorScheme,
            "logos" => DialogText.CaptionsLogos,
            "clipboard" => Lang.T("Clipboard"),
            "snippets" => Lang.T("Snippet editor"),
            "completions" => Lang.T("Completions editor"),
            "log" => DialogText.LogReview,
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

    internal static string KindName(JumpKind kind) => kind switch
    {
        JumpKind.Directory => "dir",
        JumpKind.Page => "page",
        JumpKind.DataFile => "data",
        _ => "file"
    };

    internal static JumpKind? ParseKind(string name) => name.Trim().ToLowerInvariant() switch
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

    internal static void Parse(string text)
    {
        List.Clear();
        foreach (var rawLine in text.Split('\n'))
        {
            var parts = rawLine.TrimEnd('\r').Split('\t', 3);
            if (parts.Length < 3 || ParseKind(parts[1]) is not { } kind) continue;
            var place = kind is JumpKind.File or JumpKind.Directory ? FileOperations.ListedPath(parts[2]) : parts[2].Trim();
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
    /// A file or directory has a new path (renamed or moved): the files, directories and data files of the list
    /// follow it
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
    /// Go to a place when Marbles has started (a file or directory given when it is started, see StartArgument)
    /// </summary>
    internal static void OpenAtStart(JumpTarget target)
    {
        Pending = target;
    }

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
        target.Kind == JumpKind.Page && !JumpPages.Leaves(target.Target);

    /// <summary>
    /// Show the Jump menu and go to the place picked. Returns when nothing was picked, or the place was opened
    /// on top of the page (which must then be drawn again). When the page is left, it does not return
    /// </summary>
    /// <param name="beforeLeaving">Asked before the page is left (as about changes that are not saved): false
    /// to stay</param>
    /// <param name="here">Shows the place on the page itself, without leaving it (the editor, at a bookmarked place
    /// in the file it has): true when it did</param>
    internal static void Open(Func<bool>? beforeLeaving = null, Func<JumpTarget, bool>? here = null)
    {
        var target = JumpMenu.Pick();
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        if (target == null) return;
        if (here != null && here(target))
        {
            Remember(target);
            return;
        }

        if (OpensInPlace(target))
        {
            Remember(target);
            OpenInPlace(target.Target);
            return;
        }

        Leave(target, beforeLeaving, true);
    }

    // A jump to the data file that is logged in to
    private static bool IsOpenDataFile(JumpTarget target)
    {
        if (!DataSession.IsLoggedIn) return false;
        var open = Path.Combine(FileIo.GetDataDirectoryName(), FileIo.GetSubFolder(), FileIo.GetFileName());
        return FileOperations.SamePath(Path.GetFullPath(open), target.FullPath);
    }

    /// <summary>
    /// ":book 3": jump to bookmark 3 (its number on the Bookmarks tab of the Jump menu), or ":book chapter3" to the
    /// bookmark with that name. ":book" alone shows the Jump menu on the Bookmarks tab. A number that is not a bookmark is told about in a box. Returns when nothing
    /// was jumped to, or the place was opened on top of the page (which must then be drawn again)
    /// </summary>
    /// <param name="argument">What follows ":book"</param>
    /// <param name="beforeLeaving">Asked before the page is left (the editor asks about changes that are not saved);
    /// false stays</param>
    /// <param name="here">As for Open</param>
    internal static void ToBookmark(string argument, Func<bool>? beforeLeaving = null,
        Func<JumpTarget, bool>? here = null)
    {
        argument = argument.Trim();
        if (argument.Length == 0)
        {
            JumpMenu.ShowNextOn(JumpMenu.Tab.Bookmarks);
            Open(beforeLeaving, here);
            return;
        }

        Bookmarks.Load();
        ChoiceBox.Forget();
        // The bookmarks are numbered without the folders (also those in a closed folder)
        var places = Bookmarks.Places;
        Bookmarks.Entry? bookmark;
        if (!int.TryParse(argument, out var number))
        {
            // A name (upper and lower case alike)
            bookmark = Bookmarks.FindByName(argument);
            if (bookmark == null)
            {
                ChoiceBox.Message(Lang.T("Bookmarks"), Lang.F("No bookmark is named {0}", argument),
                    DialogText.BookNumbersNote);
                return;
            }
        }
        else if (number < 1 || number > places.Count)
        {
            ChoiceBox.Message(Lang.T("Bookmarks"), places.Count == 0
                ? DialogText.BookmarksEmpty
                : Lang.F("No bookmark {0} (the bookmarks have 1 to {1})", argument, places.Count),
                DialogText.BookNumbersNote);
            return;
        }
        else
        {
            bookmark = places[number - 1];
        }

        var target = bookmark.Target;
        if (!JumpMenu.Usable(target))
        {
            ChoiceBox.Message(Lang.T("Bookmarks"), DialogText.BookNotInThisMode, target.Name);
            return;
        }

        if (!target.Exists)
        {
            ChoiceBox.Message(Lang.T("Bookmarks"), DialogText.FileNotFound, target.FullPath);
            return;
        }

        if (here != null && here(target))
        {
            Remember(target);
            return;
        }

        if (OpensInPlace(target))
        {
            Remember(target);
            OpenInPlace(target.Target);
            return;
        }

        Leave(target, beforeLeaving, true);
    }

    /// <summary>
    /// ":jump general": go to a page of Marbles by its name (as if picked in the Jump menu): a page of Configuration
    /// leaves the page the jump is made from (and the notes, unless staying logged in), the others are opened on top
    /// of it. ":jump" alone opens the Jump menu. A name that is not known is told about in a box, with the names
    /// </summary>
    /// <param name="argument">What follows ":jump"</param>
    /// <param name="beforeLeaving">As for Open</param>
    /// <param name="here">As for Open</param>
    internal static void ToPage(string argument, Func<bool>? beforeLeaving = null, Func<JumpTarget, bool>? here = null)
    {
        argument = argument.Trim();
        if (argument.Length == 0)
        {
            Open(beforeLeaving, here);
            return;
        }

        if (JumpPages.IdOf(argument) is not { } page)
        {
            ChoiceBox.Forget();
            ChoiceBox.Message(DialogText.CaptionsJump, Lang.F("No page named {0}", argument),
                string.Join(", ", JumpPages.Names));
            return;
        }

        var target = new JumpTarget(JumpKind.Page, page);
        if (!JumpMenu.Usable(target))
        {
            // The file browser and its lists, with Note Management only
            ChoiceBox.Forget();
            ChoiceBox.Message(DialogText.CaptionsJump, DialogText.EditorNotUsed, string.Empty);
            return;
        }

        if (here != null && here(target))
        {
            Remember(target);
            return;
        }

        if (OpensInPlace(target))
        {
            Remember(target);
            OpenInPlace(target.Target);
            return;
        }

        Leave(target, beforeLeaving, true);
    }

    /// <summary>
    /// Go to a place picked elsewhere than in the Jump menu (the Recent files box). It is not kept in the jump
    /// history. The page is left as for a jump from the menu (a data file that is logged in to is logged out of, unless
    /// staying logged in). Returns when the page is not left (beforeLeaving says to stay)
    /// </summary>
    /// <param name="target">The place</param>
    /// <param name="beforeLeaving">Asked before the page is left (false: stay)</param>
    internal static void ToPlace(JumpTarget target, Func<bool>? beforeLeaving = null)
    {
        Leave(target, beforeLeaving, false);
    }

    // Leave the page for a place: not while a page below has changes that are not saved (it is told); else the page
    // asks about its own changes (beforeLeaving, false: stay). The place is kept in the jump history (remember), and
    // gone to by the main loop. Every jump that leaves the page goes through here
    private static void Leave(JumpTarget target, Func<bool>? beforeLeaving, bool remember)
    {
        if (!CanLeave()) return;
        if (beforeLeaving != null && !beforeLeaving()) return;
        if (remember) Remember(target);
        Pending = target;
        Navigation.Wait();
    }

    // False (and told) while a page that a page was opened on top of has changes that are not saved: leaving would
    // throw them away without a question (the page below cannot be asked to save from here: what it saves to, as
    // the file of the editor, can be another while the page on top is shown)
    private static bool CanLeave()
    {
        if (!PageBelowHasChanges) return true;
        ChoiceBox.Forget();
        ChoiceBox.Message(DialogText.CaptionsJump, PageBelowChanged, PageBelowNote);
        return false;
    }

    private static string PageBelowChanged => Lang.T("A page below this one has changes that are not saved");
    private static string PageBelowNote => Lang.T("Close this page, then save or discard changes there.");

    /// <summary>
    /// What a command that goes to a page goes to, for ToPage: ":favorites", ":history", ":hotlist" and ":browse" the
    /// file browser and its lists, and ":jump name" the page named (or the Jump menu)
    /// </summary>
    internal static string PageOf(string command) =>
        JumpPages.ListPageOf(CommandLine.Name(command)) ?? CommandLine.Argument(command);

    /// <summary>
    /// ":log": the log, on top of the page (which must then be drawn again). Not a jump: as the help, it is not kept
    /// in the jump history (":jump log" and the Jump menu are)
    /// </summary>
    internal static void ShowLog() => OpenInPlace("log");

    // The pages that pages were opened on top of (the editor below the Jump menu), each with what tells if it has
    // changes that are not saved. A page below cannot be asked to save from the page on top
    private static readonly List<Func<bool>> PagesBelow = new();

    // How many of the pages below (from the first) have a page opened on top of them (OpenInPlace: the snippet editor
    // on top of the editor). The last of the others is the page that is making the jump: it is asked about its own
    // changes (beforeLeaving)
    private static int _covered;

    /// <summary>
    /// True while a page that a page was opened on top of has changes that are not saved: no jump leaves then
    /// </summary>
    internal static bool PageBelowHasChanges => PagesBelow.Take(_covered).Any(hasChanges => hasChanges());

    /// <summary>
    /// Show something on top of a page (the Jump menu from the editor): the page is one of the pages below while it is
    /// shown, so ":exit" there does not leave its changes that are not saved, and no jump from a page opened on top of
    /// it (Covering) leaves them
    /// </summary>
    internal static void OnTop(Func<bool>? hasChanges, Action show)
    {
        PagesBelow.Add(hasChanges ?? (() => false));
        try
        {
            show();
        }
        finally
        {
            PagesBelow.RemoveAt(PagesBelow.Count - 1);
        }
    }

    /// <summary>
    /// Show a page on top of the pages below (OpenInPlace): a jump from it does not leave while one of them has changes
    /// that are not saved
    /// </summary>
    internal static void Covering(Action show)
    {
        var before = _covered;
        _covered = PagesBelow.Count;
        try
        {
            show();
        }
        finally
        {
            _covered = before;
        }
    }

    /// <summary>
    /// ":exit" (":qa", ":quitall") on a page: leave Marbles. Not while a page below has changes that are not saved (it
    /// is told); else the page asks about its own changes (beforeLeaving: the editor asks to save them, or the usual
    /// question), or the usual question is asked. Returns when Marbles is not left
    /// </summary>
    internal static void Exit(Func<bool>? beforeLeaving = null)
    {
        ChoiceBox.Forget();
        if (PagesBelow.Any(hasChanges => hasChanges()))
        {
            ChoiceBox.Message(DialogText.Exit, PageBelowChanged, PageBelowNote);
            return;
        }

        var leave = beforeLeaving?.Invoke() ??
                    ChoiceBox.Confirm(DialogText.Exit, DialogText.ExitQuestion, string.Empty, false);
        if (leave) Tools.ExitApplication();
    }

    // Keep a jump in the jump history
    private static void Remember(JumpTarget target)
    {
        JumpHistory.Load();
        JumpHistory.Add(target with { At = null }, DateTime.Now);
        var status = JumpHistory.Save();
        if (status != DialogText.Success) EventLog.Add(DialogText.LogFile + "Jumps: " + status);
    }

    // A page shown on top of the page the jump was made from. Not a page that changes the settings while the
    // settings file is edited (they would be saved over it)
    private static void OpenInPlace(string page)
    {
        ChoiceBox.Forget();
        if (FileIo.EditingSettingsFile && JumpPages.ChangesSettings(page))
        {
            ChoiceBox.Message(JumpPages.Label(page), DialogText.SnippetsNotHere, string.Empty);
            return;
        }

        Covering(() => ShowInPlace(page));
        ConsoleColors.SetColors(ConsoleColors.ColorPreset.Normal);
        Tools.ClearScreen();
    }

    private static void ShowInPlace(string page)
    {
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
            case "completions":
                FileExplorer.EditCompletions();
                break;
            case "log":
                EventLog.Show();
                break;
            case "help":
                Tools.WriteEditorHelp(false);
                break;
            case "about":
                Actions.ShowAboutPages();
                break;
        }
    }

    /// <summary>
    /// Make the pending jump (called by the main loop). It ends with the screen it goes to (see Navigation)
    /// </summary>
    internal static void RunPending()
    {
        var target = Pending;
        Pending = null;
        if (target == null) return;
        // A data file that is logged in to is logged out of, unless staying logged in (and not going to another
        // data file). Staying: the data file is open in the main menu's mode while the place is shown
        var keep = DataSession.Keep && !(target.Kind == JumpKind.DataFile && !IsOpenDataFile(target));
        if (keep)
        {
            Mode.SetMode(Mode.SelectedMode.Manage);
        }
        else if (Mode.CurrentMode != Mode.SelectedMode.Login || DataSession.IsLoggedIn)
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
                    if (keep) DataSession.ReturnToMainMenu();
                    StartMenu.Open();
                    break;
                }

                // The file browser, in the directory of the file (which is opened in the editor), or in the
                // directory. Closing it shows the Select mode page, as when it is closed there (staying logged in:
                // the main menu of the data file)
                if (target.Kind == JumpKind.File) FileExplorer.RunAtFile(target.FullPath, target.At);
                else FileExplorer.RunAtDirectory(target.FullPath, target.Select);
                if (FileExplorer.DataFileSelected) break;
                if (DataSession.Keep) DataSession.ReturnToMainMenu();
                StartMenu.Open();
                break;
            case JumpKind.Page when JumpPages.IsFileBrowser(target.Target):
                // The file browser, or one of its lists alone: closing it shows the Select mode page, as when the
                // browser is closed there (staying logged in: the main menu of the data file)
                if (target.Target == "browse") FileExplorer.Run();
                else FileExplorer.RunList(target.Target);
                if (FileExplorer.DataFileSelected) break;
                if (DataSession.Keep) DataSession.ReturnToMainMenu();
                StartMenu.Open();
                break;
            case JumpKind.DataFile:
                if (!target.Exists)
                {
                    NotFound(target);
                    break;
                }

                // The data file that is open (staying logged in): its main menu
                if (keep) DataSession.ReturnToMainMenu();

                // The data file is selected on the login page
                var relative = target.Target.Replace('\\', '/');
                var slash = relative.LastIndexOf('/');
                FileIo.ResetSubFolder();
                FileIo.SetSubFolder(slash < 0 ? string.Empty : relative[..slash]);
                FileIo.SetFileName(slash < 0 ? relative : relative[(slash + 1)..]);
                FileBrowser.ResolveSubFolderIndex();
                break;
            default:
                // A page of the Configuration tab of the Select mode page (staying logged in: the main menu of the
                // data file comes back afterwards)
                if (keep)
                {
                    DataSession.ShowStartPage(JumpPages.ConfigurationPage(target.Target));
                    break;
                }

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
