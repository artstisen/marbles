using System.Xml;
using System.Text;

namespace Marbles;

internal static class FileIo
{
    private const string DefaultFileName = "data.mbls"; // The data file name -DO NOT CHANGE
    private const string DefaultFileNameExtension = ".mbls"; // The data file name -DO NOT CHANGE
    private const string ExportFileName = "export";
    private const string ExportFileNameExt = ".txt";

    private const string DataDirectoryName = "data"; // The name of the data directory -DO NOT CHANGE

    /// <summary>
    /// The data directory: "data" in the directory Marbles is started in, if there is one there (as Marbles has
    /// always done), else "data" next to the program, so the same data is used wherever Marbles is started from (a
    /// terminal in another directory, a file opened with Marbles). If the program directory cannot be written to (a
    /// program installed for all users), "Marbles/data" in the user's application data is used
    /// </summary>
    private static string DataDirectory => Directory.Exists(DataDirectoryName) ? DataDirectoryName : ProgramDataDirectory;

    private static string? _programDataDirectory;

    private static string ProgramDataDirectory => _programDataDirectory ??= FindProgramDataDirectory();

    private static string FindProgramDataDirectory()
    {
        var nextToProgram = Path.Combine(AppContext.BaseDirectory, DataDirectoryName);
        if (Directory.Exists(nextToProgram) || CanWriteIn(AppContext.BaseDirectory)) return nextToProgram;
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.DoNotVerify), "Marbles", DataDirectoryName);
    }

    // True if a file can be made in a directory (it is made and deleted again)
    private static bool CanWriteIn(string directory)
    {
        try
        {
            var test = Path.Combine(directory, ".marbles-" + Guid.NewGuid().ToString("N"));
            using (File.Create(test, 1, FileOptions.DeleteOnClose))
            {
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// The directory the data directory is in (where the directory "spell" is too)
    /// </summary>
    internal static string DataParentDirectory =>
        Path.GetDirectoryName(Path.GetFullPath(DataDirectory)) ?? AppContext.BaseDirectory;

    internal const string SettingsFileName = "settings.ini";
    private static bool _saveBackup;
    private static string _backupPath = string.Empty;
    private static string _fileName = string.Empty;
    private static string _tempContent = string.Empty;
    private static string _currentSubFolder = string.Empty;

    internal static string GetSubFolder()
    {
        return _currentSubFolder;
    }

    internal static void ResetSubFolder()
    {
        _currentSubFolder = string.Empty;
    }

    internal static void SetSubFolder(string value)
    {
        if (!string.IsNullOrEmpty(value) && value.Length > 0)
            _currentSubFolder = value;
    }

    internal static void SetFileName(string name)
    {
        if (!FileActions.IsLockedForMove() && !string.IsNullOrEmpty(name))
        {
            _fileName = name;
        }
    }

    internal static void SetNoFileName()
    {
        if (!FileActions.IsLockedForMove())
        {
            _fileName = string.Empty;
        }
    }

    internal static void SetDefaultFileName()
    {
        _fileName = DefaultFileName;
    }

    internal static string GetFileName()
    {
        return _fileName;
    }

    internal static string GetFileNameExtension()
    {
        return DefaultFileNameExtension;
    }

    internal static string GetDataDirectoryName()
    {
        return DataDirectory;
    }

    internal static string GetDataAndSubFolder()
    {
        if (Tools.IsValid(_currentSubFolder) && Directory.Exists(Path.Combine(DataDirectory, _currentSubFolder)))
        {
            return Path.Combine(DataDirectory, _currentSubFolder);
        }
        else
        {
            return DataDirectory;
        }
    }


    /// <summary>
    /// The directory of the data files as shown on the login page: "data", or "data/notes" for a directory in it
    /// (the name of the data directory, not a "~", which stands for the home directory on Linux and macOS)
    /// </summary>
    internal static string GetDataAndSubFolderForDisplay()
    {
        if (Tools.IsValid(_currentSubFolder) && Directory.Exists(Path.Combine(DataDirectory, _currentSubFolder)))
        {
            return Path.Combine(DataDirectory, _currentSubFolder);
        }

        return DataDirectory;
    }

    internal static string GetFileNameWithoutExtension()
    {
        // Only the extension at the end ("a.mblsx.mbls" is "a.mblsx"), in any letter case ("WORK.MBLS")
        return _fileName.EndsWith(DefaultFileNameExtension, StringComparison.OrdinalIgnoreCase)
            ? _fileName[..^DefaultFileNameExtension.Length]
            : _fileName;
    }

    internal static string GetAbsolutePath()
    {
        try
        {
            if (IsCurrentFileValid())
            {
                var info = new FileInfo(GetCurrentFolderAndFileName());
                var absolutePath = info.FullName;
                return absolutePath.Length > 0 ? absolutePath : DialogText.NotAvailable;
            }
            else if (Directory.Exists(GetDataAndSubFolder()))
            {
                var info = new FileInfo(GetDataAndSubFolder());
                var absolutePath = info.FullName;
                return absolutePath.Length > 0 ? absolutePath : DialogText.NotAvailable;
            }
            else
            {
                return DialogText.FileNotFound;
            }
        }
        catch
        {
            return DialogText.ErrorReadingFile;
        }
    }

    internal static string GetCurrentFolderAndFileName()
    {
        // Insert subfolder if valid
        if (Tools.IsValid(_currentSubFolder) && Directory.Exists(Path.Combine(DataDirectory, _currentSubFolder)))
        {
            return Path.Combine(Path.Combine(DataDirectory, _currentSubFolder),
                _fileName);
        }

        // Else return data directory
        return
            Path.Combine(DataDirectory,
                _fileName); // Will insert the correct path separator char according to OS/platform (like using the Path.DirectorySeparatorChar)
    }

    internal static string GetFolderAndFileNameWithSizeForDisplay()
    {
        return GetCurrentFolderAndFileName() + " (" + GetFileSize(false) + ")";
    }

    internal static string DirectoryCheckErrorMessage = string.Empty;


    internal static bool CheckDataDirectory()
    {
        try
        {
            DirectoryCheckErrorMessage = string.Empty;
            if (!Directory.Exists(DataDirectory))
            {
                // Reset sub directory
                _currentSubFolder = string.Empty;
                // Create data folder
                Init.DataDirectoryCreated = true;
                Directory.CreateDirectory(DataDirectory);
                // Exit as data is the only directory
            }

            return true;
        }
        catch (Exception e)
        {
            DirectoryCheckErrorMessage = e.Message;
            return false;
        }
    }

    // The basic method that will check the current directory and create the default
    // data folder if it doesn't exist
    internal static bool CheckCurrentDirectory()
    {
        try
        {
            DirectoryCheckErrorMessage = string.Empty;
            // Check that the data directory exits and if not
            // create it and return true because this means that we cant have
            // any sub directories
            if (!Directory.Exists(DataDirectory))
            {
                // Reset sub directory
                _currentSubFolder = string.Empty;
                // Create data folder
                Init.DataDirectoryCreated = true;
                Directory.CreateDirectory(DataDirectory);
                // Exit as data is the only directory
                return true;
            }

            // Else the data already exists check the sub directory
            // If subFolder is valid but does not exist
            if (Tools.IsValid(_currentSubFolder) &&
                !Directory.Exists(Path.Combine(DataDirectory, _currentSubFolder)))
            {
                return false;
            }

            // All passed
            return true;
        }
        catch (Exception e)
        {
            DirectoryCheckErrorMessage = e.Message;
            return false;
        }
    }

    internal static bool IsCurrentFileValid()
    {
        try
        {
            return File.Exists(GetCurrentFolderAndFileName());
        }
        catch
        {
            return false;
        }
    }


    #region Load entries and return load status

    private static LoadStatus _loadStatus = new();

    // The data file as it was when its entries were loaded, or last saved (its path, time and size): when it has
    // changed since then, another window (or program) has saved it
    private static (string Path, DateTime Time, long Length)? _knownVersion;

    private static (string Path, DateTime Time, long Length)? CurrentVersion()
    {
        try
        {
            var info = new FileInfo(LinkedFile(GetCurrentFolderAndFileName()));
            return info.Exists ? (info.FullName, info.LastWriteTimeUtc, info.Length) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// True if the data file was saved by another window (or program) since its entries were loaded here, or last
    /// saved here: saving would replace those changes. A data file that is not there (a new one) has not changed
    /// </summary>
    internal static bool ChangedElsewhere()
    {
        var current = CurrentVersion();
        // Another path (the data file renamed or moved since): nothing is known about it
        if (current == null || _knownVersion == null ||
            !FileOperations.SamePath(current.Value.Path, _knownVersion.Value.Path)) return false;
        return current.Value.Time != _knownVersion.Value.Time || current.Value.Length != _knownVersion.Value.Length;
    }

    /// <summary>
    /// Forget the data file that was loaded (at logout)
    /// </summary>
    internal static void ForgetKnownVersion()
    {
        _knownVersion = null;
    }

    internal static LoadStatus LoadEntries()
    {
        _loadStatus = new LoadStatus();
        // Before reading: a save made by another window while reading is found at the next save here
        var version = CurrentVersion();
        var fileContent = ReadFile(out var readStatus);
        if (fileContent != null)
        {
            // Decrypt
            _loadStatus.ProcessString = Encryptor.DecryptData(fileContent);
            if (_loadStatus.ProcessString != DialogText.InvalidData &&
                _loadStatus.ProcessString != DialogText.NoData && _loadStatus.ProcessString.Length > 0)
            {
                // Get entries from XMl
                var entries = GetEntriesFromXml(_loadStatus.ProcessString);
                if (entries == null)
                {
                    // The password is right but the content is damaged. Never continue with an empty
                    // collection, as saving it would overwrite the content of the file
                    _loadStatus.Success = false;
                    _loadStatus.StatusMessage = DialogText.ErrorDataDamaged;
                    _loadStatus.ProcessString = "";
                    return _loadStatus;
                }

                _loadStatus.LoadedEntries = entries;
            }

            _loadStatus.IsLegacyFormat = Encryptor.LegacyFormatDetected;

            // Validate load
            if (_loadStatus.ProcessString != DialogText.InvalidData && _loadStatus.LoadedEntries.Count >= 0)
            {
                _loadStatus.Success = true;
                _loadStatus.StatusMessage = DialogText.LoadComplete;
                _loadStatus.ProcessString = "";
                // Set entries
                Entries.SetAllEntries(_loadStatus.LoadedEntries);
                _knownVersion = version;
                return _loadStatus;
            }
            else
            {
                // reject load
                _loadStatus.Success = false;
                if (_loadStatus.ProcessString == DialogText.InvalidData)
                {
                    _loadStatus.StatusMessage = DialogText.InvalidPassword + ". " + DialogText.CapsLockNotice;
                }
                else
                {
                    _loadStatus.StatusMessage = DialogText.NoEntriesFound;
                }

                _loadStatus.ProcessString = "";
                return _loadStatus;
            }
        }
        else
        {
            _loadStatus.Success = false;
            _loadStatus.StatusMessage = readStatus;
            return _loadStatus;
        }
    }

    #endregion

    #region Save entries

    /// <summary>
    /// What happened when the entries were saved: Saved is true when the data file was written (else Error tells
    /// why); BackupError tells why the backup copy could not be written (empty when it was, or is not used)
    /// </summary>
    internal readonly record struct SaveResult(bool Saved, string Error, string BackupError);

    internal static SaveResult SaveEntries()
    {
        var encrypted = Encryptor.EncryptData(GetEntriesAsXml(Entries.GetEntries()).ToString());
        // Never touch the data file if the encryption failed
        if (encrypted == null)
        {
            return new SaveResult(false, DialogText.StatusUnableToSave, string.Empty);
        }

        var error = WriteFile(encrypted);
        // The backup is only written when the data file was
        return error.Length > 0
            ? new SaveResult(false, error, string.Empty)
            : new SaveResult(true, string.Empty, WriteBackupFile(encrypted));
    }

    #endregion

    #region Legacy file conversion

    private const string LegacyCopyFileNameExtension = ".legacy";

    /// <summary>
    /// The file name of the copy made of the original file by the last call to ConvertLegacyFile
    /// </summary>
    internal static string LegacyCopyFileName { get; private set; } = string.Empty;

    /// <summary>
    /// Save the current (just loaded) legacy data file in the current file format.
    /// A copy of the original file is kept alongside it, with ".legacy" added to the file name.
    /// The dates of entries written with the day first by an early version ("17-11-2021 12:21:46") are turned
    /// around ("2021-11-17 12:21:46"), so the entries sort by their dates.
    /// </summary>
    /// <returns>What happened when the file was saved (not saved when the copy could not be made)</returns>
    internal static SaveResult ConvertLegacyFile()
    {
        LegacyCopyFileName = string.Empty;
        try
        {
            var original = GetCurrentFolderAndFileName();
            var copy = original + LegacyCopyFileNameExtension;
            // Never overwrite an earlier copy
            if (File.Exists(copy))
            {
                copy = original + "-" + IsoDateTime.GetDateTimeNowCompressed("-") + LegacyCopyFileNameExtension;
            }

            File.Copy(original, copy, false);
            LegacyCopyFileName = Path.GetFileName(copy);
        }
        catch (Exception e)
        {
            // Without the copy the original is left untouched
            return new SaveResult(false, e.Message, string.Empty);
        }

        var corrected = CorrectTimestamps(Entries.GetEntries());
        if (corrected > 0) Init.Log("Timestamps corrected: " + corrected, false);
        return SaveEntries();
    }

    /// <summary>
    /// Turn the dates of entries that are written with the day first around (see IsoDateTime.CorrectDayFirst)
    /// </summary>
    /// <returns>The number of dates that were corrected</returns>
    internal static int CorrectTimestamps(IEnumerable<Entry> entries)
    {
        var count = 0;
        foreach (var entry in entries)
        {
            entry.DateCreated = IsoDateTime.CorrectDayFirst(entry.DateCreated, out var created);
            entry.DateChanged = IsoDateTime.CorrectDayFirst(entry.DateChanged, out var changed);
            count += (created ? 1 : 0) + (changed ? 1 : 0);
        }

        return count;
    }

    #endregion

    #region Read file

    internal static string GetFileSize(bool verbose)
    {
        try
        {
            if (IsCurrentFileValid())
            {
                var info = new FileInfo(GetCurrentFolderAndFileName());
                var byteSize = info.Length;
                if (byteSize > 0)
                {
                    if (verbose)
                    {
                        if (byteSize >= 1024)
                        {
                            return Lang.F("{0} bytes ({1})", byteSize, FileSizeFormatter.FormatSize(byteSize));
                        }
                        else
                        {
                            return Lang.F("{0} bytes", byteSize);
                        }
                    }
                    else
                    {
                        return FileSizeFormatter.FormatSize(byteSize);
                    }
                }
                else
                {
                    return DialogText.NotAvailable;
                }
            }
            else
            {
                return DialogText.FileNotFound;
            }
        }
        catch
        {
            return DialogText.ErrorReadingFile;
        }
    }


    /// <summary>
    /// Read file from default data directory
    /// </summary>
    /// <param name="status">DialogText.Success or the reason why the file could not be read</param>
    /// <returns>The file content or null if the file could not be read</returns>
    private static byte[]? ReadFile(out string status)
    {
        if (CheckCurrentDirectory() && IsCurrentFileValid())
        {
            try
            {
                status = DialogText.Success;
                return File.ReadAllBytes(GetCurrentFolderAndFileName());
            }
            catch
            {
                status = DialogText.ErrorReadingFile;
                return null;
            }
        }

        status = DialogText.FileNotFound;
        return null;
    }

    #endregion

    #region Write file

    private const string TempFileNameExtension = ".tmp";

    /// <summary>
    /// Write the content to a temporary file and then replace the target file with it,
    /// so an interrupted save can never leave a half written data file behind.
    /// The file keeps the time it was created (where the system has one that can be set), its permissions (Linux,
    /// macOS) or attributes (Windows). A link is followed: the file it points to is written, and the link stays.
    /// </summary>
    internal static void WriteAllBytesSafe(string path, byte[] content)
    {
        path = LinkedFile(path);
        // A name of its own (in the same directory, so the file is replaced in one step): never a file of the user
        var tempPath = path + "." + Guid.NewGuid().ToString("N")[..8] + TempFileNameExtension;
        // The file that replaces the target is a new file: remember when the target was created, and its permissions
        DateTime? created = null;
        UnixFileMode? mode = null;
        FileAttributes? attributes = null;
        try
        {
            if (File.Exists(path))
            {
                created = File.GetCreationTimeUtc(path);
                if (OperatingSystem.IsWindows())
                {
                    // Read-only stays on the target, so the replacing fails as it should
                    attributes = File.GetAttributes(path) & (FileAttributes.Hidden | FileAttributes.System);
                }
                else
                {
                    mode = File.GetUnixFileMode(path);
                }
            }
        }
        catch
        {
            // Saving matters more than the time and permissions
        }

        try
        {
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None
            };
            if (mode is { } createMode && !OperatingSystem.IsWindows()) options.UnixCreateMode = createMode;
            using (var stream = new FileStream(tempPath, options))
            {
                stream.Write(content, 0, content.Length);
                stream.Flush(true); // Make sure the content is on the disk before replacing the target file
            }

            // (The umask can have taken permissions away when it was created)
            if (mode is { } unixMode && !OperatingSystem.IsWindows()) File.SetUnixFileMode(tempPath, unixMode);
            File.Move(tempPath, path, true);
        }
        catch
        {
            try
            {
                File.Delete(tempPath);
            }
            catch
            {
                // Nothing more to do. The original error is the one to report
            }

            throw;
        }

        // The file is saved: the attributes and the time are kept where they can be
        try
        {
            if (attributes is { } kept && kept != 0) File.SetAttributes(path, File.GetAttributes(path) | kept);
        }
        catch
        {
            // Saving matters more than the attributes
        }

        RestoreCreationTime(path, created);
    }

    // The file a link points to (through all links), or the path itself when it is not a link (or the link is broken)
    private static string LinkedFile(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (info.LinkTarget == null) return path;
            var target = info.ResolveLinkTarget(true);
            return target is { Exists: true } ? target.FullName : path;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return path;
        }
    }

    // Give a file that has replaced another the time the other was created. Only where the time can be set:
    // on Linux there is none to set (and setting it would change the time the file was last written)
    private static void RestoreCreationTime(string path, DateTime? created)
    {
        if (created == null || !(OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())) return;
        try
        {
            File.SetCreationTimeUtc(path, created.Value);
        }
        catch
        {
            // The file is saved: the time is not worth failing for
        }
    }

    /// <summary>
    /// Write file to default data directory.
    /// </summary>
    private static string WriteFile(byte[] content)
    {
        try
        {
            if (CheckCurrentDirectory())
            {
                WriteAllBytesSafe(GetCurrentFolderAndFileName(), content);
                _knownVersion = CurrentVersion();
                return string.Empty;
            }
            else
            {
                return DialogText.StatusUnableToSave;
            }
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }

    /// <summary>
    /// The file name suggested when exporting an entry: its title (without the characters that cannot be used
    /// in file names) and ".txt"
    /// </summary>
    internal static string GetExportFileName(string title)
    {
        const string invalid = "<>:\"/\\|?*";
        var name = new string(title.Select(c => invalid.Contains(c) || char.IsControl(c) ? '_' : c).ToArray())
            .Trim(' ', '.');
        if (name.Length > 48) name = name[..48].TrimEnd(' ', '.');
        return (name.Length > 0 ? name : ExportFileName) + ExportFileNameExt;
    }

    internal static string ExportCurrentEntryAsTxt(string path)
    {
        try
        {
            if (Entries.CurrentEntryExists())
            {
                File.WriteAllText(path, Entries.CurrentEntry.Text, Encoding.UTF8);
                return DialogText.Success;
            }
            else
            {
                return DialogText.StatusUnableToSave;
            }
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }

    #endregion

    #region Convert to XML

    // Before v1.5.3 "]]>" in a title or text was written as this word (and every such word read as "]]>"). Now the
    // text is split into two CDATA sections there, which XML reads back as it was; the entries element tells so
    private const string XmlCdataTagReplacer = "cmdncdatareplace";
    private const string XmlCdataTagToReplace = "]]>";
    private const string XmlCdataSplit = "]]]]><![CDATA[>";
    private const string XmlCdataAttribute = "cdata";
    private const string XmlCdataAttributeSplit = "split";
    private const string XmlChanged = "changed";
    private const string XmlCreated = "created";
    internal const string XmlDoctype = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>";
    private const string XmlId = "id";
    private const string XmlItem = "entry";
    private const string XmlOptions = "options";
    private const string XmlParent = "entries";
    private const string XmlText = "text";
    private const string XmlCursorIndex = "cindex";
    private const string XmlSplit = "split"; // The other pane of a split (Entry.Split); only written when split
    private const string XmlTitle = "title";
    private static StringBuilder _xmlDocument = new();

    private static StringBuilder GetEntriesAsXml(IReadOnlyList<Entry> entries)
    {
        _xmlDocument = new StringBuilder();
        if (entries.Count > 0)
        {
            // Write doctype and parent start (telling how "]]>" is written)
            _xmlDocument.Append(XmlDoctype);
            _xmlDocument.Append("<" + XmlParent + " " + XmlCdataAttribute + "=\"" + XmlCdataAttributeSplit + "\">");
            // Write all items
            foreach (var t in entries)
            {
                _xmlDocument.Append(MakeElement(XmlItem, false, false));
                _xmlDocument.Append(MakeElement(XmlId, false, false) + t.Id + MakeElement(XmlId, true, false));
                _xmlDocument.Append(MakeElement(XmlCreated, false, false) + t.DateCreated +
                                    MakeElement(XmlCreated, true, false));
                _xmlDocument.Append(MakeElement(XmlChanged, false, false) + t.DateChanged +
                                    MakeElement(XmlChanged, true, false));
                _xmlDocument.Append(MakeElement(XmlTitle, false, true) + ReplaceCdataTag(t.Title) +
                                    MakeElement(XmlTitle, true, true));
                _xmlDocument.Append(MakeElement(XmlText, false, true) + ReplaceCdataTag(t.Text) +
                                    MakeElement(XmlText, true, true));
                _xmlDocument.Append(MakeElement(XmlCursorIndex, false, false) + t.CursorIndex +
                                    MakeElement(XmlCursorIndex, true, false));
                // Only an entry closed in a split has it (earlier versions read a file without it, and skip it)
                if (IsSplitText(t.Split))
                {
                    _xmlDocument.Append(MakeElement(XmlSplit, false, false) + t.Split + MakeElement(XmlSplit, true, false));
                }

                _xmlDocument.Append(MakeElement(XmlOptions, false, false) + t.Options +
                                    MakeElement(XmlOptions, true, false));
                _xmlDocument.Append(MakeElement(XmlItem, true, false));
            }

            // Write parent end
            _xmlDocument.Append(MakeElement(XmlParent, true, false));
            // Return compiled document
            return _xmlDocument;
        }
        // Return nodata
        return _xmlDocument.Append(DialogText.NoData);
    }

    internal static StringBuilder GetEntriesAsXmlForExport(List<Entry> entries)
    {
        _xmlDocument = new StringBuilder();
        if (entries.Count > 0)
        {
            // Write doctype and parent start
            _xmlDocument.Append(XmlDoctype + Environment.NewLine);
            _xmlDocument.Append("<" + XmlParent + " " + XmlCdataAttribute + "=\"" + XmlCdataAttributeSplit + "\">" +
                                Environment.NewLine);
            // Write all items
            foreach (var t in entries)
            {
                _xmlDocument.Append(new String(' ', 4) + MakeElement(XmlItem, false, false) + Environment.NewLine);
                // ID has been removed from export
                _xmlDocument.Append(new String(' ', 8) + MakeElement(XmlCreated, false, false) + t.DateCreated +
                                    MakeElement(XmlCreated, true, false) + Environment.NewLine);
                _xmlDocument.Append(new String(' ', 8) + MakeElement(XmlChanged, false, false) + t.DateChanged +
                                    MakeElement(XmlChanged, true, false) + Environment.NewLine);
                _xmlDocument.Append(new String(' ', 8) + MakeElement(XmlTitle, false, true) + ReplaceCdataTag(t.Title) +
                                    MakeElement(XmlTitle, true, true) + Environment.NewLine);
                _xmlDocument.Append(new String(' ', 8) + MakeElement(XmlText, false, true) + ReplaceCdataTag(t.Text) +
                                    MakeElement(XmlText, true, true) + Environment.NewLine);
                // Options and cursor index has been removed from export
                _xmlDocument.Append(new String(' ', 4) + MakeElement(XmlItem, true, false) + Environment.NewLine);
            }

            // Write parent end
            _xmlDocument.Append(MakeElement(XmlParent, true, false));
            // Return compiled document
            return _xmlDocument;
        }
        // Return nodata
        return _xmlDocument.Append(DialogText.NoData);
    }


    // A text in a CDATA section: "]]>" would end it, so the section is ended after "]]" and a new one starts
    private static string ReplaceCdataTag(string s)
    {
        s = RemoveInvalidXmlChars(s);
        if (!string.IsNullOrEmpty(s) && s.Contains(XmlCdataTagToReplace))
        {
            s = s.Replace(XmlCdataTagToReplace, XmlCdataSplit);
        }

        return s;
    }

    /// <summary>
    /// Remove the characters that are not allowed in an XML document (most control characters and
    /// incomplete surrogate pairs). A document containing them cannot be loaded again.
    /// </summary>
    internal static string RemoveInvalidXmlChars(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        StringBuilder? cleaned = null;
        for (var i = 0; i < s.Length; i++)
        {
            var valid = XmlConvert.IsXmlChar(s[i]);
            if (!valid && i + 1 < s.Length && XmlConvert.IsXmlSurrogatePair(s[i + 1], s[i]))
            {
                // A complete surrogate pair is one valid character
                cleaned?.Append(s[i]).Append(s[i + 1]);
                i++;
                continue;
            }

            if (valid)
            {
                cleaned?.Append(s[i]);
            }
            else
            {
                // First invalid character found: start building a cleaned copy
                cleaned ??= new StringBuilder(s, 0, i, s.Length);
            }
        }

        return cleaned?.ToString() ?? s;
    }

    private const string CdataStart = "<![CDATA[";
    private const string CdataEnd = "]]>";

    private static string MakeElement(string name, bool isEndTag, bool includeCdataTag)
    {
        switch (isEndTag)
        {
            case true:
            {
                name = "</" + name + ">";
                if (includeCdataTag)
                {
                    name = CdataEnd + name;
                }

                break;
            }
            case false:
            {
                name = "<" + name + ">";
                if (includeCdataTag)
                {
                    name = name + CdataStart;
                }

                break;
            }
        }

        return name;
    }

    #endregion

    #region Convert from XML

    /// <summary>
    /// Read the entries of a decrypted document
    /// </summary>
    /// <returns>The entries, or null if the document cannot be read</returns>
    private static List<Entry>? GetEntriesFromXml(string xml)
    {
        if (!Tools.IsValidXml(xml))
        {
            return null;
        }

        var doc = new XmlDocument();
        try
        {
            doc.LoadXml(xml);
        }
        catch (XmlException)
        {
            // Documents saved by earlier versions can hold characters that are not allowed in XML.
            // Try again without them
            try
            {
                doc.LoadXml(RemoveInvalidXmlChars(xml));
            }
            catch (XmlException)
            {
                return null;
            }
        }

        var entries = new List<Entry>();
        // Written before "]]>" was split: the word it was written as is "]]>"
        var oldCdata = doc.DocumentElement?.GetAttribute(XmlCdataAttribute) != XmlCdataAttributeSplit;
        var nodes = doc.DocumentElement?.SelectNodes(XmlItem);
        if (nodes == null) return entries;
        foreach (XmlNode node in nodes)
        {
            // Elements missing in files from earlier versions get their default value
            var entry = new Entry(string.Empty, string.Empty);
            var id = NodeText(node, XmlId);
            if (id.Length > 0) entry.Id = id;
            entry.DateCreated = NodeText(node, XmlCreated, entry.DateCreated);
            entry.DateChanged = NodeText(node, XmlChanged, entry.DateCreated);
            entry.Title = oldCdata ? RestoreCdataTag(NodeText(node, XmlTitle)) : NodeText(node, XmlTitle);
            entry.Text = oldCdata ? RestoreCdataTag(NodeText(node, XmlText)) : NodeText(node, XmlText);
            entry.CursorIndex = int.TryParse(NodeText(node, XmlCursorIndex), out var cursorIndex) && cursorIndex > 0
                ? cursorIndex
                : 0;
            var split = NodeText(node, XmlSplit);
            entry.Split = IsSplitText(split) ? split : string.Empty;
            entry.Options = NodeText(node, XmlOptions);
            entries.Add(entry);
        }

        return entries;
    }

    // The other pane of a split as kept for an entry: "1234", or "L1234" when it was at the left
    internal static bool IsSplitText(string split) =>
        System.Text.RegularExpressions.Regex.IsMatch(split, @"^L?[0-9]{1,9}\z");

    private static string NodeText(XmlNode entryNode, string name, string defaultValue = "")
    {
        var text = entryNode.SelectSingleNode(name)?.InnerText;
        return string.IsNullOrEmpty(text) ? defaultValue : text;
    }

    private static string RestoreCdataTag(string s)
    {
        if (!string.IsNullOrEmpty(s) && s.Contains(XmlCdataTagReplacer))
        {
            s = s.Replace(XmlCdataTagReplacer, XmlCdataTagToReplace);
        }

        return s;
    }

    #endregion

    #region Backup

    internal static bool CheckBackupDirectory(string path)
    {
        try
        {
            DirectoryCheckErrorMessage = "";
            // The backup directory must exist, and be outside the data directory: there a backup copy could be
            // written on top of another data file (or the data file itself)
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                var backup = Path.GetFullPath(path);
                var data = Path.GetFullPath(DataDirectory);
                return !FileOperations.SamePath(backup, data) && !FileOperations.IsInside(backup, data);
            }

            return false;
        }
        catch (Exception e)
        {
            DirectoryCheckErrorMessage = e.Message;
            return false;
        }
    }

    internal static void SetBackup(bool enabled, string path, bool saveSettings)
    {
        _saveBackup = enabled;
        _backupPath = path;
        _backupFailureTold = false;
        if (saveSettings)
        {
            SaveSettings();
        }
    }

    public static bool BackupEnabled()
    {
        return _saveBackup;
    }

    public static string GetBackupStatus()
    {
        if (_saveBackup)
        {
            return DialogText.CheckedSetting;
        }
        else
        {
            return DialogText.UnCheckedSetting;
        }
    }

    // The backup copy of the current data file: in the same subfolder of the backup directory as the data file is
    // in the data directory, so data files with the same name in different subfolders get backups of their own
    private static string BackupFileName(string backupPath)
    {
        return Tools.IsValid(_currentSubFolder)
            ? Path.Combine(backupPath, _currentSubFolder, _fileName)
            : Path.Combine(backupPath, _fileName);
    }

    internal static string GetBackupPathForDisplay(bool verboseOutput)
    {
        if (!string.IsNullOrEmpty(_backupPath))
        {
            if (verboseOutput)
            {
                return DialogText.CurrentBackupPathColon + _backupPath;
            }
            else
            {
                return _backupPath;
            }
        }
        else
        {
            if (verboseOutput)
            {
                return DialogText.CurrentBackupPathColon + DialogText.NotAvailable;
            }
            else
            {
                return DialogText.NotAvailable;
            }
        }
    }

    // Note regarding Create directory: If you give it a full path c:... it will create the last part as a folder which is nice.

    private static string WriteBackupFile(byte[] content)
    {
        if (!_saveBackup)
        {
            return string.Empty;
        }

        try
        {
            // The backup stays on when it cannot be written (a drive that is not there right now): the next
            // save tries again
            if (CheckBackupDirectory(_backupPath))
            {
                var backupFile = BackupFileName(_backupPath);
                Directory.CreateDirectory(Path.GetDirectoryName(backupFile)!);
                WriteAllBytesSafe(backupFile, content);
                _backupFailureTold = false;
                return string.Empty;
            }
            else
            {
                return BackupFailure(DialogText.StatusUnableToSaveBackup);
            }
        }
        catch (Exception e)
        {
            return BackupFailure(Lang.F("Error: Unable to save the backup: {0}", e.Message));
        }
    }

    /// <summary>
    /// A backup that cannot be written is told about again (at a new login)
    /// </summary>
    internal static void ResetBackupFailureNotice()
    {
        _backupFailureTold = false;
    }

    // True when it has been told that the backup could not be written: not again until a backup is written
    private static bool _backupFailureTold;

    // The message of a backup that could not be written: told once, until a backup is written again
    private static string BackupFailure(string message)
    {
        if (_backupFailureTold) return string.Empty;
        _backupFailureTold = true;
        return message;
    }

    #endregion

    #region Settings

    private static string GetSettingsFolderAndFileName()
    {
        return
            Path.Combine(DataDirectory,
                SettingsFileName); // Will insert the correct path separator char according to OS/platform (like using the Path.DirectorySeparatorChar)
    }


    private static string _tempSettings = string.Empty;

    internal static void SaveSettings()
    {
        // Collect settings data and save it
        // Section start
        _tempSettings = "; Last modified " + IsoDateTime.GetDateTimeNow() + Entries.NewLineChar;
        _tempSettings += "[SETTINGS]" + Entries.NewLineChar;
        // Comment
        _tempSettings += Entries.NewLineChar;
        _tempSettings += "; DIMENSIONS" + Entries.NewLineChar;
        // Output line width (columns)
        _tempSettings += "linewidth=" + Init.LineWidth + Entries.NewLineChar;
        // Output line count (rows)
        _tempSettings += "linecount=" + Init.LineCount + Entries.NewLineChar;
        // Follow the size of the terminal window
        _tempSettings += "autosize=" + (Init.AutoSize ? DialogText.BTrue : DialogText.BFalse) + Entries.NewLineChar;
        // Comment
        _tempSettings += Entries.NewLineChar;
        _tempSettings += "; OPTIONAL" + Entries.NewLineChar;
        // What to show at startup: select (the choices), notes (the login page) or editor (the file browser)
        _tempSettings += "startmode=" + StartMenu.SettingName(StartMenu.Mode) + Entries.NewLineChar;
        // Starting in the Text Editor: open the favorites list first
        _tempSettings += "startfavorites=" + (StartMenu.StartInFavorites ? DialogText.BTrue : DialogText.BFalse) +
                         Entries.NewLineChar;
        // Use Marbles for one mode only: none (both), editor (no Note Management, data files are not opened) or
        // notes (no Text Editor on the start page)
        _tempSettings += "onlymode=" + StartMenu.SettingName(StartMenu.Only) + Entries.NewLineChar;
        // The clipboard: yanks to the system clipboard (OSC 52) and/or the paste bin, every yank ("all") or only
        // with "+ in front ("plus"), also from the notes of a data file, and the paste bin file
        _tempSettings += "osc52=" + (SystemClipboard.Osc52 ? DialogText.BTrue : DialogText.BFalse) + Entries.NewLineChar;
        _tempSettings += "pastebin=" + (SystemClipboard.PasteBin ? DialogText.BTrue : DialogText.BFalse) +
                         Entries.NewLineChar;
        _tempSettings += "clipboardyank=" + (SystemClipboard.EveryYank ? "all" : "plus") + Entries.NewLineChar;
        _tempSettings += "clipboardnotes=" + (SystemClipboard.FromNotes ? DialogText.BTrue : DialogText.BFalse) +
                         Entries.NewLineChar;
        _tempSettings += "pastebinappend=" + (SystemClipboard.PasteBinAppend ? DialogText.BTrue : DialogText.BFalse) +
                         Entries.NewLineChar;
        _tempSettings += "pastebinfile=" + SystemClipboard.PasteBinFile + Entries.NewLineChar;
        // The order of the snippets in the editor (file, title or used)
        _tempSettings += "snipsort=" + Snippets.SortName(Snippets.Sort) + Entries.NewLineChar;
        // Comment
        _tempSettings += Entries.NewLineChar;
        _tempSettings += "; STYLING" + Entries.NewLineChar;
        // ASCII style
        _tempSettings += "asciistyle=" + AsciiStyles.GetAsciiStyle() + Entries.NewLineChar;
        // Logo animation on the login page
        _tempSettings += "logoanim=" + Logo.SettingName(Logo.Animation) + Entries.NewLineChar;
        // The logo: the built-in one (Marbles), or the name of one in the directory "logos"
        _tempSettings += "logo=" + Logo.Current.Name + Entries.NewLineChar;
        // The color scheme: a built-in one, or the name of one in the directory "colorschemes"
        _tempSettings += "colorscheme=" + ConsoleColors.Current.Name + Entries.NewLineChar;
        // Black and white: the colors of the scheme are kept, but not used
        _tempSettings += "monochrome=" + (ConsoleColors.Monochrome ? DialogText.BTrue : DialogText.BFalse) + Entries.NewLineChar;
        // Comment
        _tempSettings += Entries.NewLineChar;
        _tempSettings += "; FILES" + Entries.NewLineChar;
        // Sub directory:
        _tempSettings += "subdir=" + GetSubFolder()+ Entries.NewLineChar;
        // File name:
        _tempSettings += "file=" + GetFileName()+ Entries.NewLineChar;
        // Backup path:
        _tempSettings += "backup=" + _backupPath+ Entries.NewLineChar;
        // Comment
        _tempSettings += Entries.NewLineChar;
        _tempSettings += "; FILE BROWSER AND EDITOR" + Entries.NewLineChar;
        // The directories last shown in the two panes of the file browser, the names selected in them, and the pane
        // that had the focus (1 or 2)
        _tempSettings += "browsedir1=" + FileExplorer.StartDirectory + Entries.NewLineChar;
        _tempSettings += "browsedir2=" + FileExplorer.StartDirectory2 + Entries.NewLineChar;
        _tempSettings += "browsesel1=" + FileExplorer.StartSelected + Entries.NewLineChar;
        _tempSettings += "browsesel2=" + FileExplorer.StartSelected2 + Entries.NewLineChar;
        _tempSettings += "browsepane=" + (FileExplorer.StartPane + 1) + Entries.NewLineChar;
        // Two panes side by side in the file browser (false: one pane)
        _tempSettings += "browsedual=" + (FileExplorer.DualPane ? DialogText.BTrue : DialogText.BFalse) + Entries.NewLineChar;
        // Stay logged into the data file while files are edited and pages shown, until logging out
        _tempSettings += "stayloggedin=" + (DataSession.StayLoggedIn ? DialogText.BTrue : DialogText.BFalse) + Entries.NewLineChar;
        // Complete words while typing in the editor (the lists are in completions.txt)
        _tempSettings += "autocomplete=" + (Completions.Enabled ? DialogText.BTrue : DialogText.BFalse) + Entries.NewLineChar;
        // Color Markdown, HTML, CSS, JavaScript and JSON files in the editor (the editor command :syntax turns
        // the colors on and off)
        // The language of the texts (en, da, de, nb, sv, es, fr)
        // The language set, also when its file is missing (English is used until it is there again)
        _tempSettings += "language=" + (Lang.Missing ?? Lang.Current) + Entries.NewLineChar;
        _tempSettings += "syntaxcolors=" + (SyntaxHighlighter.ColorFiles ? DialogText.BTrue : DialogText.BFalse) + Entries.NewLineChar;
        // Line numbers in the editor (the editor command :number turns them on and off)
        _tempSettings += "linenumbers=" + (TextArea.LineNumbers ? DialogText.BTrue : DialogText.BFalse) + Entries.NewLineChar;
        // Long lines in the editor wrapped (on unless turned off)
        _tempSettings += "wrap=" + (TextArea.WordWrap ? DialogText.BTrue : DialogText.BFalse) + Entries.NewLineChar;
        // Lines kept above and below the cursor when the editor scrolls (on or off, and how many)
        _tempSettings += "scrolloff=" + (TextArea.ScrollOff ? DialogText.BTrue : DialogText.BFalse) +
                         Entries.NewLineChar;
        _tempSettings += "scrolllines=" + TextArea.ScrollOffLines + Entries.NewLineChar;
        // Comment
        _tempSettings += Entries.NewLineChar;
        _tempSettings += "; SPELL CHECKING" + Entries.NewLineChar;
        // The languages used by the editor command :spell (spell files in the directory "spell"):
        _tempSettings += "spelllang=" + SpellChecker.Languages + Entries.NewLineChar;

        // Write to file
        var status = WriteSettingsFile(_tempSettings);
        if (status != DialogText.Success)
        {
            Menu.StatusMessage(status, true, true, true, DialogText.CaptionsSettings, string.Empty, false);
        }
    }

    /// <summary>
    /// The extension of the copy kept of a settings file that could not be read ("settings.ini.bad")
    /// </summary>
    internal const string UnreadSettingsExtension = ".bad";

    // True when the settings file could not be read at startup: before it is written for the first time, a copy of
    // it is kept (settings.ini.bad), so the settings in it are not lost
    private static bool _keepUnreadSettings;

    internal static string WriteSettingsFile(string content)
    {
        try
        {
            if (CheckDataDirectory())
            {
                var path = GetSettingsFolderAndFileName();
                if (_keepUnreadSettings)
                {
                    // Not written at all when the copy cannot be made
                    if (File.Exists(path)) File.Copy(path, path + UnreadSettingsExtension, true);
                    _keepUnreadSettings = false;
                }

                WriteAllBytesSafe(path, new UTF8Encoding(false).GetBytes(content));
                return DialogText.Success;
            }
            else
            {
                return DialogText.StatusUnableToSave;
            }
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }


    internal static string LoadSettingsErrorMessage = "";

    /// <summary>
    /// Load the settings file. The settings are read by name, so they can be in any order.
    /// Missing or invalid settings get their default value and unknown settings (as those
    /// of earlier versions) are ignored.
    /// </summary>
    /// <returns>False if the settings file could not be read</returns>
    internal static bool LoadSettings()
    {
        _keepUnreadSettings = false;
        if (CheckDataDirectory())
        {
            try
            {
                var content = ReadSettingsFile();
                if (content == DialogText.ErrorReadingFile)
                {
                    LoadSettingsErrorMessage = content;
                    _keepUnreadSettings = true;
                    return false;
                }

                var settings = ParseSettings(content == DialogText.FileNotFound ? string.Empty : content);
                if (settings.Count > 0)
                {
                    ApplySettings(settings);
                    return true;
                }
            }
            catch (Exception e)
            {
                LoadSettingsErrorMessage = e.Message;
                _keepUnreadSettings = true;
                return false;
            }
        }

        Init.UsingDefaultSettings = true;
        return true; // Must be true because a new settings file will be created if none exist
    }

    /// <summary>
    /// Get the "name=value" pairs of a settings file. Sections, comments and blank lines are skipped.
    /// </summary>
    internal static Dictionary<string, string> ParseSettings(string content)
    {
        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawLine in Tools.SplitStringOnNewLine(content, false))
        {
            var line = Tools.RemoveNewLineChars(rawLine).Trim();
            if (line.Length == 0 || line[0] is '[' or ';' or '#') continue;
            // Split on the first "=" only, as values (paths, logo text) may contain the character
            var separator = line.IndexOf('=');
            if (separator <= 0) continue;
            settings[line[..separator].Trim()] = line[(separator + 1)..];
        }

        return settings;
    }

    private static void ApplySettings(Dictionary<string, string> settings)
    {
        string Get(string name) => settings.TryGetValue(name, out var value) ? value : string.Empty;
        int? GetNumber(string name)
        {
            var value = Get(name).Trim();
            return value.Length is > 0 and < 9 && Tools.IsNumeric(value) ? Convert.ToInt32(value) : null;
        }

        // Dimensions
        Init.SetLineWidth((byte)Math.Clamp(GetNumber("linewidth") ?? Init.MinLineWidth, 0, byte.MaxValue));
        Init.SetLineCount((byte)Math.Clamp(GetNumber("linecount") ?? Init.MinLineCount, 0, byte.MaxValue));

        // Automatic dimensions (off unless turned on)
        Init.AutoSize = Tools.TrimToLower(Get("autosize")) == DialogText.BTrue;
        StartMenu.Mode = StartMenu.ParseSetting(Get("startmode"));
        StartMenu.StartInFavorites = Tools.TrimToLower(Get("startfavorites")) == DialogText.BTrue;
        StartMenu.Only = StartMenu.ParseOnly(Get("onlymode"));
        // The clipboard (OSC 52 on unless turned off, the rest off unless turned on)
        SystemClipboard.Osc52 = Tools.TrimToLower(Get("osc52")) != DialogText.BFalse;
        SystemClipboard.PasteBin = Tools.TrimToLower(Get("pastebin")) == DialogText.BTrue;
        // Every yank unless only the ones with "+ in front are asked for
        SystemClipboard.EveryYank = Tools.TrimToLower(Get("clipboardyank")) != "plus";
        SystemClipboard.FromNotes = Tools.TrimToLower(Get("clipboardnotes")) == DialogText.BTrue;
        SystemClipboard.PasteBinAppend = Tools.TrimToLower(Get("pastebinappend")) == DialogText.BTrue;
        var pasteBinFile = Get("pastebinfile").Trim();
        SystemClipboard.PasteBinFile = pasteBinFile.Length > 0 ? pasteBinFile : SystemClipboard.DefaultPasteBinFile;
        Snippets.Sort = Snippets.ParseSort(Get("snipsort"));
        var language = Tools.TrimToLower(Get("language"));
        Lang.Missing = null;
        if (!Lang.Set(language) && language.Length > 0) Lang.Missing = language;
        // On unless turned off
        // (named "mdcolors" in an earlier version)
        SyntaxHighlighter.ColorFiles = Tools.TrimToLower(Get("syntaxcolors")) != DialogText.BFalse &&
                                       Tools.TrimToLower(Get("mdcolors")) != DialogText.BFalse;
        // On unless turned off
        TextArea.LineNumbers = Tools.TrimToLower(Get("linenumbers")) == DialogText.BTrue;
        TextArea.WordWrap = Tools.TrimToLower(Get("wrap")) != DialogText.BFalse;
        // On unless turned off
        TextArea.ScrollOff = Tools.TrimToLower(Get("scrolloff")) != DialogText.BFalse;
        var scrollLines = GetNumber("scrolllines");
        TextArea.ScrollOffLines = scrollLines is > 0 and <= 999 ? scrollLines.Value : TextArea.DefaultScrollOffLines;
        ConsoleColors.Monochrome = Tools.TrimToLower(Get("monochrome")) == DialogText.BTrue;

        // ASCII style
        var asciiStyle = GetNumber("asciistyle");
        if (asciiStyle.HasValue)
        {
            AsciiStyles.SetAsciiStyle(asciiStyle.Value);
        }
        else
        {
            AsciiStyles.ResetAsciiStyle();
        }

        // Logo animation (on unless turned off)
        Logo.Animation = Logo.ParseAnimation(Get("logoanim"));
        // The logo by its name (the built-in one if there is none with the name)
        Logo.Current = Logos.Load(Get("logo")) ?? LogoArt.BuiltIn;

        // The color scheme by its name (the default if there is none with the name)
        ConsoleColors.Use(ColorSchemes.Load(Get("colorscheme")) ?? ColorScheme.Default);

        // Sub directory
        var subFolder = Get("subdir");
        if (subFolder.Length > 0 && Tools.IsValidPath(subFolder))
        {
            SetSubFolder(subFolder);
        }
        else
        {
            SetSubFolder(string.Empty);
        }

        // Data file
        var fileName = Get("file");
        if (fileName.Length > 0 && Tools.IsValidFileName(fileName))
        {
            SetFileName(fileName);
            if (!IsCurrentFileValid())
            {
                SetDefaultFileName();
            }
        }
        else
        {
            SetDefaultFileName();
        }

        // File browser directory
        // (a settings file from an earlier version has one directory: "browsedir")
        var browseDirectory = Get("browsedir1").Length > 0 ? Get("browsedir1") : Get("browsedir");
        FileExplorer.StartDirectory = Tools.IsValidPath(browseDirectory) ? browseDirectory : string.Empty;
        FileExplorer.StartDirectory2 = Tools.IsValidPath(Get("browsedir2")) ? Get("browsedir2") : string.Empty;
        // The names selected in the panes (one that is gone selects the first row)
        FileExplorer.StartSelected = Get("browsesel1").Trim();
        FileExplorer.StartSelected2 = Get("browsesel2").Trim();
        FileExplorer.StartPane = Get("browsepane").Trim() == "2" ? 1 : 0;
        // Two panes unless turned off
        FileExplorer.DualPane = Tools.TrimToLower(Get("browsedual")) != DialogText.BFalse;
        // Off unless turned on
        DataSession.StayLoggedIn = Tools.TrimToLower(Get("stayloggedin")) == DialogText.BTrue;
        // Completing words while typing: off unless turned on
        Completions.Enabled = Tools.TrimToLower(Get("autocomplete")) == DialogText.BTrue;

        // Spell checking languages
        var spellLanguages = Get("spelllang").Replace(" ", string.Empty);
        SpellChecker.Languages = spellLanguages.Length > 0 && spellLanguages.Split(',').All(Tools.IsValidFileName)
            ? spellLanguages
            : SpellChecker.DefaultLanguages;

        // Backup path
        var backupPath = Get("backup");
        if (backupPath.Length > 0)
        {
            SetBackup(true, backupPath, false);
        }
        else
        {
            SetBackup(false, string.Empty, false);
        }
    }

    internal static string ReadSettingsFile()
    {
        _tempSettings = "";
        if (File.Exists(GetSettingsFolderAndFileName()))
        {
            try
            {
                var lines = File.ReadAllLines(GetSettingsFolderAndFileName());
                _tempSettings = string.Join(Entries.NewLineCharString, lines);
            }
            catch
            {
                _tempSettings = DialogText.ErrorReadingFile;
            }
        }
        else
        {
            _tempSettings = DialogText.FileNotFound;
        }

        return _tempSettings;
    }

    #endregion
}

#region LoadStatus object

internal class LoadStatus
{
    internal bool Success { get; set; }
    internal string StatusMessage { get; set; }
    internal List<Entry> LoadedEntries { get; set; }
    internal string ProcessString { get; set; }
    internal bool IsLegacyFormat { get; set; } // The file was written in an older file format and should be converted

    public LoadStatus()
    {
        Success = false;
        StatusMessage = string.Empty;
        LoadedEntries = new List<Entry>();
        ProcessString = string.Empty;
    }
}

#endregion

#region File size

public static class FileSizeFormatter
{
    // Load all suffixes in an array  
    private static string[] Suffixes => new[] { " " + Lang.T("bytes"), " kB", " MB", " GB", " TB", " PB" };

    public static string FormatSize(long bytes)
    {
        try
        {
            var counter = 0;
            var number = (decimal) bytes;
            // 1023 bytes stay bytes ("1 kB" from 513 bytes up was rounded)
            var suffixes = Suffixes;
            while (number >= 1024 && counter < suffixes.Length - 1)
            {
                number = number / 1024;
                counter++;
            }

            // Shown with one decimal: 1023.97 kB would be "1024.0 kB"
            if (counter > 0 && Math.Round(number, 1) >= 1024 && counter < suffixes.Length - 1)
            {
                number = number / 1024;
                counter++;
            }

            return bytes < 1024 ? $"{number:n0}{suffixes[counter]}" : $"{number:n1}{suffixes[counter]}";
        }
        catch
        {
            return DialogText.NotAvailable;
        }
    }
}

#endregion