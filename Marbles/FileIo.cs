using System.Xml;
using System.Text;
using System.Text.RegularExpressions;

namespace Marbles;

internal static class FileIo
{
    private const string DefaultFileName = "data.mbls"; // The data file name -DO NOT CHANGE
    private const string DefaultFileNameExtension = ".mbls"; // The data file name -DO NOT CHANGE
    private const string ExportFileName = "export";
    private const string ExportFileNameExt = ".txt";

    private const string
        DataDirectory = "data"; // Always use the data directory in the application folder -DO NOT CHANGE

    private const string ImportFileName = "import.txt";
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


    internal static string GetDataAndSubFolderForDisplay()
    {
        if (Tools.IsValid(_currentSubFolder) && Directory.Exists(Path.Combine(DataDirectory, _currentSubFolder)))
        {
            return Path.Combine(DataDirectory, _currentSubFolder);
        }

        return "~";
    }

    internal static string GetFileNameWithoutExtension()
    {
        return _fileName.Replace(DefaultFileNameExtension, "");
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

    internal static LoadStatus LoadEntries()
    {
        _loadStatus = new LoadStatus();
        var fileContent = ReadFile(out var readStatus);
        if (fileContent != null)
        {
            // Decrypt
            _loadStatus.ProcessString = Encryptor.DecryptData(fileContent);
            if (_loadStatus.ProcessString != DialogText.InvalidData && _loadStatus.ProcessString.Length > 0)
            {
                // Get entries from XMl
                _loadStatus.LoadedEntries = GetEntriesFromXml(_loadStatus.ProcessString);
                // Only flag a legacy file for conversion if its content was fully understood
                _loadStatus.IsLegacyFormat = Encryptor.LegacyFormatDetected &&
                                             (_loadStatus.LoadedEntries.Count > 0 ||
                                              _loadStatus.ProcessString == DialogText.NoData);
            }

            // Validate load
            if (_loadStatus.ProcessString != DialogText.InvalidData && _loadStatus.LoadedEntries.Count >= 0)
            {
                _loadStatus.Success = true;
                _loadStatus.StatusMessage = DialogText.LoadComplete;
                _loadStatus.ProcessString = "";
                // Set entries
                Entries.SetAllEntries(_loadStatus.LoadedEntries);
                return _loadStatus;
            }
            else
            {
                // reject load
                _loadStatus.Success = false;
                if (_loadStatus.ProcessString == DialogText.InvalidData)
                {
                    _loadStatus.StatusMessage = DialogText.InvalidPasswordDot + " " + DialogText.CapsLockNotice;
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

    private static string _tempSaveStatus = string.Empty;

    internal static string SaveEntries()
    {
        var encrypted = Encryptor.EncryptData(GetEntriesAsXml(Entries.GetEntries()).ToString());
        // Never touch the data file if the encryption failed
        if (encrypted == null)
        {
            return DialogText.StatusUnableToSave;
        }

        _tempSaveStatus = WriteFile(encrypted);
        _tempSaveStatus += WriteBackupFile(encrypted);
        return _tempSaveStatus;
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
    /// </summary>
    /// <returns>DialogText.Success or an error message</returns>
    internal static string ConvertLegacyFile()
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
            return e.Message;
        }

        return SaveEntries();
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
                            return byteSize.ToString() + " bytes (" + FileSizeFormatter.FormatSize(byteSize) + ")";
                        }
                        else
                        {
                            return byteSize.ToString() + " bytes";
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

    internal static string GetImportPathAndFile()
    {
        return Path.Combine(DataDirectory, ImportFileName);
    }

    internal static string ImportTxt()
    {
        _tempContent = "";
        if (CheckDataDirectory() && File.Exists(GetImportPathAndFile()))
        {
            try
            {
                var lines = File.ReadAllLines(GetImportPathAndFile());
                _tempContent = string.Join(Entries.NewLineCharString, lines);
            }
            catch
            {
                _tempContent = DialogText.ErrorReadingFile;
            }
        }
        else
        {
            _tempContent = DialogText.FileNotFound;
        }

        return _tempContent;
    }

    #endregion

    #region Write file

    private const string TempFileNameExtension = ".tmp";

    /// <summary>
    /// Write the content to a temporary file and then replace the target file with it,
    /// so an interrupted save can never leave a half written data file behind.
    /// </summary>
    private static void WriteAllBytesSafe(string path, byte[] content)
    {
        var tempPath = path + TempFileNameExtension;
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(content, 0, content.Length);
                stream.Flush(true); // Make sure the content is on the disk before replacing the target file
            }

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

    internal static string GetExportPathAndFile(bool example)
    {
        return example
            ? Path.Combine(GetDataAndSubFolder(), ExportFileName + "-timestamp" + ExportFileNameExt)
            : Path.Combine(GetDataAndSubFolder(),
                ExportFileName + "-" + IsoDateTime.GetDateTimeNowCompressed("-") + ExportFileNameExt);
    }

    internal static string ExportCurrentEntryAsTxt()
    {
        try
        {
            if (CheckCurrentDirectory() && Entries.CurrentEntryExists())
            {
                File.WriteAllText(GetExportPathAndFile(false), Entries.CurrentEntry.Text, Encoding.UTF8);
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

    private const string XmlCdataTagReplacer = "cmdncdatareplace";
    private const string XmlCdataTagToReplace = "]]>";
    private const string XmlChanged = "changed";
    private const string XmlCreated = "created";
    internal const string XmlDoctype = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>";
    private const string XmlId = "id";
    private const string XmlItem = "entry";
    private const string XmlOptions = "options";
    private const string XmlParent = "entries";
    private const string XmlText = "text";
    private const string XmlCursorIndex = "cindex";
    private const string XmlTitle = "title";
    private static StringBuilder _xmlDocument = new();

    private static StringBuilder GetEntriesAsXml(IReadOnlyList<Entry> entries)
    {
        _xmlDocument = new StringBuilder();
        if (entries.Count > 0)
        {
            // Write doctype and parent start
            _xmlDocument.Append(XmlDoctype);
            _xmlDocument.Append(MakeElement(XmlParent, false, false));
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
            _xmlDocument.Append(MakeElement(XmlParent, false, false) + Environment.NewLine);
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


    private static string ReplaceCdataTag(string s)
    {
        if (!string.IsNullOrEmpty(s) && s.Contains(XmlCdataTagToReplace))
        {
            s = s.Replace(XmlCdataTagToReplace, XmlCdataTagReplacer);
        }

        return s;
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

    private static List<Entry> _entriesFromXml = new();
    private static Entry _entryFromXml = new(string.Empty, string.Empty);

    private static List<Entry> GetEntriesFromXml(string xml)
    {
        if (!Tools.IsValidXml(xml))
        {
            return new List<Entry>();
        }

        try
        {
            _entriesFromXml = new List<Entry>();
            var doc = new XmlDocument();
            doc.LoadXml(xml);
            var root = doc.DocumentElement;
            var nodes = root?.SelectNodes(XmlItem);
            if (nodes is not {Count: > 0}) return _entriesFromXml;
            foreach (XmlNode node in nodes)
            {
                _entryFromXml = new Entry("", "")
                {
                    Id = node.SelectSingleNode(XmlId)!.InnerText,
                    DateCreated = node.SelectSingleNode(XmlCreated)!.InnerText,
                    DateChanged = node.SelectSingleNode(XmlChanged)!.InnerText,
                    Title = RestoreCdataTag(node.SelectSingleNode(XmlTitle)!.InnerText),
                    Text = RestoreCdataTag(node.SelectSingleNode(XmlText)!.InnerText),
                    CursorIndex = CheckNodeInt(node.SelectSingleNode(XmlCursorIndex)!, 0),
                    //CursorIndex = 0, // Used for converting existing datafiles from before the CursorIndex
                    Options = CheckNodeString(node.SelectSingleNode(XmlOptions)!, string.Empty)
                };
                // Add the entry to the collection
                _entriesFromXml.Add(_entryFromXml);
            }

            return _entriesFromXml;
        }
        catch
        {
            return new List<Entry>();
        }
    }

    private static string CheckNodeString(XmlNode node, string output)
    {
        return !string.IsNullOrEmpty(node.InnerText) ? node.InnerText : output;
    }

    private static int _tempCheckNodeInt;
    private static int CheckNodeInt(XmlNode node, int output)
    {
        _tempCheckNodeInt = 0;
        if (Tools.IsValid(node.InnerText) && Tools.IsNumeric(node.InnerText))
        {
            try
            {
                _tempCheckNodeInt = Convert.ToInt32(node.InnerText);
                return _tempCheckNodeInt;
            }
            catch 
            {
                return output;
            }
        }
        return output;
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
            // Check directory
            if (!string.IsNullOrEmpty(path) && Path.Combine(path, _fileName) != GetAbsolutePath() &&
                Directory.Exists(path))
            {
                return true;
            }
            else
            {
                return false;
            }
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

    private static string GetBackupFolderAndFileName()
    {
        return
            Path.Combine(_backupPath,
                _fileName); // Will insert the correct path separator char according to OS/platform (like using the Path.DirectorySeparatorChar)
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
            if (CheckBackupDirectory(_backupPath))
            {
                WriteAllBytesSafe(GetBackupFolderAndFileName(), content);
                return string.Empty;
            }
            else
            {
                SetBackup(false, string.Empty, true); // Disable backup
                return DialogText.StatusUnableToSaveBackup;
            }
        }
        catch (Exception e)
        {
            SetBackup(false, string.Empty, true); // Disable backup
            return e.Message;
        }
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
        // Comment
        _tempSettings += Entries.NewLineChar;
        _tempSettings += "; KEY MAPPING" + Entries.NewLineChar;
        // Compound shortcut key
        _tempSettings += "kbtrigger=" +Keyboard.GetTriggerAsString() + Entries.NewLineChar;
        // Additional keys
        _tempSettings += "kbsave=" + Keyboard.KbSave + Entries.NewLineChar;
        _tempSettings += "kbsavexit=" + Keyboard.KbSaveExit + Entries.NewLineChar;
        _tempSettings += "kbrepeat=" + Keyboard.KbRepeat + Entries.NewLineChar;
        _tempSettings += "kbnxtword=" + Keyboard.KbNxtword + Entries.NewLineChar;
        _tempSettings += "kbpreword=" + Keyboard.KbPreword + Entries.NewLineChar;
        _tempSettings += "kbselect=" + Keyboard.KbSelect + Entries.NewLineChar;
        // Comment
        _tempSettings += Entries.NewLineChar;
        _tempSettings += "; OPTIONAL" + Entries.NewLineChar;
        // Vignette
        _tempSettings += "vignette=" + Vignette.GetVignetteState() + Entries.NewLineChar;
        // Comment
        _tempSettings += Entries.NewLineChar;
        _tempSettings += "; STYLING" + Entries.NewLineChar;
        // ASCII style
        _tempSettings += "asciistyle=" + AsciiStyles.GetAsciiStyle() + Entries.NewLineChar;
        // Logo
        _tempSettings += "logo=" + Logo.GetLogoIndex() + Entries.NewLineChar;
        // Logo text
        _tempSettings += "logotxt=" + Logo.GetLogoText() + Entries.NewLineChar;
        // Color scheme (for the color preset selector)
        _tempSettings += "(clpreset)=" + ConsoleColors.GetColorPresetIndex()+ Entries.NewLineChar;
        // Colors
        //tempSettings += Environment.NewLine;
        //tempSettings += "color0=";
        //tempSettings += ConsoleColors.GetIndexFromColor(ConsoleColors.GetCurrentColorFromIndex(0));
        _tempSettings += "color1=" + ConsoleColors.GetIndexFromColor(ConsoleColors.GetCurrentColorFromIndex(1))+ Entries.NewLineChar;
        _tempSettings += "color2=" + ConsoleColors.GetIndexFromColor(ConsoleColors.GetCurrentColorFromIndex(2))+ Entries.NewLineChar;
        _tempSettings += "color3=" + ConsoleColors.GetIndexFromColor(ConsoleColors.GetCurrentColorFromIndex(3))+ Entries.NewLineChar;
        _tempSettings += "color4=" + ConsoleColors.GetIndexFromColor(ConsoleColors.GetCurrentColorFromIndex(4))+ Entries.NewLineChar;
        _tempSettings += "color5=" +ConsoleColors.GetIndexFromColor(ConsoleColors.GetCurrentColorFromIndex(5))+ Entries.NewLineChar;
        _tempSettings += "color6=" + ConsoleColors.GetIndexFromColor(ConsoleColors.GetCurrentColorFromIndex(6))+ Entries.NewLineChar;
        _tempSettings += "color7=" + ConsoleColors.GetIndexFromColor(ConsoleColors.GetCurrentColorFromIndex(7))+ Entries.NewLineChar;
        _tempSettings += "color8=" + ConsoleColors.GetIndexFromColor(ConsoleColors.GetCurrentColorFromIndex(8))+ Entries.NewLineChar;
        _tempSettings += "color9=" + ConsoleColors.GetIndexFromColor(ConsoleColors.GetCurrentColorFromIndex(9))+ Entries.NewLineChar;
        _tempSettings += "color10=" + ConsoleColors.GetIndexFromColor(ConsoleColors.GetCurrentColorFromIndex(10))+ Entries.NewLineChar;
        _tempSettings += "color11=" + ConsoleColors.GetIndexFromColor(ConsoleColors.GetCurrentColorFromIndex(11))+ Entries.NewLineChar;
        _tempSettings += "color12=" + ConsoleColors.GetIndexFromColor(ConsoleColors.GetCurrentColorFromIndex(12))+ Entries.NewLineChar;
        // Comment
        _tempSettings += Entries.NewLineChar;
        _tempSettings += "; FILES" + Entries.NewLineChar;
        // Sub directory:
        _tempSettings += "subdir=" + GetSubFolder()+ Entries.NewLineChar;
        // File name:
        _tempSettings += "file=" + GetFileName()+ Entries.NewLineChar;
        // Backup path:
        _tempSettings += "backup=" + _backupPath+ Entries.NewLineChar;

        // Write to file
        if (WriteSettingsFile(_tempSettings) != DialogText.Success)
        {
            Menu.StatusMessage(LoadSettingsErrorMessage, true, true, true, DialogText.CaptionsSettings, string.Empty,
                false);
        }
    }

    internal static string WriteSettingsFile(string content)
    {
        try
        {
            if (CheckDataDirectory())
            {
                File.WriteAllText(GetSettingsFolderAndFileName(), content);
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
    private static List<string> _settingsList = new();
    private static string _tempLoadSettingsStatus = string.Empty;
    private static string[]? _tempLoadedSettings;

    internal static bool LoadSettings()
    {
        if (CheckDataDirectory())
        {
            try
            {
                // Load the file into memory
                _tempLoadSettingsStatus = ReadSettingsFile();
                // Check for error codes
                if (_tempLoadSettingsStatus != DialogText.ErrorReadingFile &&
                    _tempLoadSettingsStatus != DialogText.FileNotFound)
                {
                    // Extract key/value pairs
                    if (!string.IsNullOrEmpty(_tempLoadSettingsStatus))
                    {
                        _settingsList = new List<string>();
                        _tempLoadedSettings = Tools.SplitStringOnNewLine(_tempLoadSettingsStatus, true);
                        for (int i = 0; i < _tempLoadedSettings.Length; i++)
                        {
                            _tempLoadedSettings[i] = Tools.RemoveNewLineChars(_tempLoadedSettings[i]);
                            // Only add lines that are not sections, comments and line breaks (at the beginning of lines)
                            if (_tempLoadedSettings[i].Length > 0 && !Regex.IsMatch(_tempLoadedSettings[i], @"^\[|^\;|^\#", RegexOptions.Singleline))
                            {
                                // Split on the first "=" only, as values (paths, logo text) may contain the character
                                _settingsList.Add(_tempLoadedSettings[i].Split("=", 2)[1]);
                            }
                        }

                        // Only continue if settings count equals 29
                        if (_settingsList.Count == 29)
                        {
                            // Line width
                            if (_settingsList[0].Length > 0 && Tools.IsNumeric(_settingsList[0]))
                            {
                                Init.SetLineWidth(Convert.ToByte(_settingsList[0]));
                            }
                            else
                            {
                                Init.SetLineWidth(Init.MinLineWidth);
                            }

                            // Line count
                            if (_settingsList[1].Length > 0 && Tools.IsNumeric(_settingsList[1]))
                            {
                                Init.SetLineCount(Convert.ToByte(_settingsList[1]));
                            }
                            else
                            {
                                Init.SetLineCount(Init.MinLineCount);
                            }

                            // Compound trigger key
                            if (_settingsList[2].Length > 0)
                            {
                                Keyboard.SetTriggerKeyFromString(_settingsList[2]);
                            }
                            else
                            {
                                Keyboard.ResetTriggerKey();
                            }

                            // Additional keys (Save key)
                            if (_settingsList[3].Length > 0)
                            {
                                Keyboard.SetAdditionalKeyFromString(_settingsList[3], Keyboard.AdditionalKeys.SaveKey);
                            }
                            else
                            {
                                Keyboard.SetAdditionalKeyDefault(Keyboard.AdditionalKeys.SaveKey);
                            }

                            // Additional keys (Save exit key)
                            if (_settingsList[4].Length > 0)
                            {
                                Keyboard.SetAdditionalKeyFromString(_settingsList[4],
                                    Keyboard.AdditionalKeys.SaveExitKey);
                            }
                            else
                            {
                                Keyboard.SetAdditionalKeyDefault(Keyboard.AdditionalKeys.SaveExitKey);
                            }

                            // Additional keys (Repeat key)
                            if (_settingsList[5].Length > 0)
                            {
                                Keyboard.SetAdditionalKeyFromString(_settingsList[5],
                                    Keyboard.AdditionalKeys.RepeatKey);
                            }
                            else
                            {
                                Keyboard.SetAdditionalKeyDefault(Keyboard.AdditionalKeys.RepeatKey);
                            }

                            // Additional keys (Next word key)
                            if (_settingsList[6].Length > 0)
                            {
                                Keyboard.SetAdditionalKeyFromString(_settingsList[6],
                                    Keyboard.AdditionalKeys.NextWordKey);
                            }
                            else
                            {
                                Keyboard.SetAdditionalKeyDefault(Keyboard.AdditionalKeys.NextWordKey);
                            }

                            // Additional keys (Previous word key)
                            if (_settingsList[7].Length > 0)
                            {
                                Keyboard.SetAdditionalKeyFromString(_settingsList[7],
                                    Keyboard.AdditionalKeys.PreviousWordKey);
                            }
                            else
                            {
                                Keyboard.SetAdditionalKeyDefault(Keyboard.AdditionalKeys.PreviousWordKey);
                            }

                            // Additional keys (Select word key)
                            if (_settingsList[8].Length > 0)
                            {
                                Keyboard.SetAdditionalKeyFromString(_settingsList[8],
                                    Keyboard.AdditionalKeys.SelectWordKey);
                            }
                            else
                            {
                                Keyboard.SetAdditionalKeyDefault(Keyboard.AdditionalKeys.SelectWordKey);
                            }

                            // Vignettes
                            if (_settingsList[9].Length > 0 &&
                                (Tools.TrimToLower(_settingsList[9]) == DialogText.BTrue ||
                                 Tools.TrimToLower(_settingsList[9]) == DialogText.BFalse))
                            {
                                Vignette.SetVignetteSate(Convert.ToBoolean(Tools.TrimToLower(_settingsList[9])));
                            }
                            else
                            {
                                Vignette.SetDefaultVignetteState();
                            }

                            // ASCII style
                            if (_settingsList[10].Length > 0 && Tools.IsNumeric(_settingsList[10]))
                            {
                                AsciiStyles.SetAsciiStyle(Convert.ToInt32(_settingsList[10]));
                            }
                            else
                            {
                                AsciiStyles.ResetAsciiStyle();
                            }

                            // Logo
                            if (_settingsList[11].Length > 0 && Tools.IsNumeric(_settingsList[11]))
                            {
                                Logo.SetLogoIndex(Convert.ToInt32(Tools.TrimToLower(_settingsList[11])));
                            }
                            else
                            {
                                Logo.ResetLogoIndex();
                            }
                            
                            // Logo text
                            if (_settingsList[12].Length > 0 && Tools.IsValid(_settingsList[12]))
                            {
                                Logo.SetLogoText(_settingsList[12]);
                            }
                            else
                            {
                                Logo.ResetLogoText();
                            }

                            // Color scheme preset
                            if (_settingsList[13].Length > 0 && Tools.IsNumeric(_settingsList[13]))
                            {
                                ConsoleColors.SetColorPresetIndex(
                                    Convert.ToInt32(Tools.TrimToLower(_settingsList[13])));
                            }
                            else
                            {
                                ConsoleColors.DefaultColorPresetIndex();
                            }


                            // Colors
                            int settingsOffset = 14;
                            for (int i = 1;
                                 i < 13;
                                 i++) // Start at 1 to match the text color (background is 0 and not used). Colors are 1-12
                            {
                                if (_settingsList[settingsOffset].Length > 0 &&
                                    Tools.IsNumeric(_settingsList[settingsOffset]))
                                {
                                    ConsoleColors.SetColorFromIndex(i, Convert.ToInt32(_settingsList[settingsOffset]));
                                }
                                else
                                {
                                    ConsoleColors.SetColorFromIndex(i, 15);
                                }

                                settingsOffset += 1;
                            }

                            // Sub directory
                            if (_settingsList[26].Length > 0 && Tools.IsValidPath(_settingsList[26]))
                            {
                                SetSubFolder(_settingsList[26]);
                            }
                            else
                            {
                                SetSubFolder(string.Empty);
                            }

                            // Data file
                            if (_settingsList[27].Length > 0 && Tools.IsValidFileName(_settingsList[27]))
                            {
                                SetFileName(_settingsList[27]);
                                if (!IsCurrentFileValid())
                                {
                                    SetDefaultFileName();
                                }
                            }
                            else
                            {
                                SetDefaultFileName();
                            }

                            // Backup path
                            if (_settingsList[28].Length > 0)
                            {
                                SetBackup(true, _settingsList[28], false);
                            }
                            else
                            {
                                SetBackup(false, String.Empty, false);
                            }

                            return true;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LoadSettingsErrorMessage = e.Message;
                return false;
            }
        }

        Init.UsingDefaultSettings = true;
        return true; // Must be true because a new settings file will be created if none exist
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
    private static readonly string[] Suffixes =
        {" bytes", " kB", " MB", " GB", " TB", " PB"};

    public static string FormatSize(long bytes)
    {
        try
        {
            var counter = 0;
            var number = (decimal) bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number = number / 1024;
                counter++;
            }

            return bytes < 1024 ? $"{number:n0}{Suffixes[counter]}" : $"{number:n1}{Suffixes[counter]}";
        }
        catch
        {
            return DialogText.NotAvailable;
        }
    }
}

#endregion