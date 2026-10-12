using System.Runtime.InteropServices;
using System.Text;

namespace Marbles;

/// <summary>
/// Text yanked in the editor sent out of Marbles: to the system clipboard through the terminal (OSC 52, which
/// many terminals support, also over SSH) and/or to a text file, the paste bin (for terminals without OSC 52, as
/// the console of a Raspberry Pi). The paste bin can also be written, read and cleared with commands.
/// The settings are shown on the clipboard settings page (ClipboardPage) and kept in the settings file.
/// </summary>
internal static class SystemClipboard
{
    internal const string DefaultPasteBinFile = "paste-bin.txt";

    /// <summary>
    /// Yanks go to the system clipboard through the terminal (setting "osc52", on unless turned off)
    /// </summary>
    internal static bool Osc52 { get; set; } = true;

    /// <summary>
    /// Yanks are written to the paste bin file (setting "pastebin")
    /// </summary>
    internal static bool PasteBin { get; set; }

    /// <summary>
    /// Every yank goes out, or (false) only the ones with "+ in front, as "+y (setting "clipboardyank":
    /// "all" or "plus")
    /// </summary>
    internal static bool EveryYank { get; set; } = true;

    /// <summary>
    /// Text may be sent out of the notes of a data file too, which are kept encrypted (setting "clipboardnotes")
    /// </summary>
    internal static bool FromNotes { get; set; }

    /// <summary>
    /// Text written to the paste bin is added to the end of it, else it replaces it (setting "pastebinappend")
    /// </summary>
    internal static bool PasteBinAppend { get; set; }

    /// <summary>
    /// The paste bin file (setting "pastebinfile"). A path that is not a full path is in the data directory,
    /// with the settings and the snippets
    /// </summary>
    internal static string PasteBinFile { get; set; } = DefaultPasteBinFile;

    /// <summary>
    /// The full path of the paste bin file
    /// </summary>
    internal static string PasteBinPath => FullPath(PasteBinFile);

    /// <summary>
    /// The full path of a paste bin file: one that is not a full path is in the data directory
    /// </summary>
    internal static string FullPath(string file)
    {
        return Path.GetFullPath(Path.IsPathRooted(file) ? file : Path.Combine(FileIo.GetDataDirectoryName(), file));
    }

    internal static void SetDefaults()
    {
        Osc52 = true;
        PasteBin = false;
        EveryYank = true;
        FromNotes = false;
        PasteBinAppend = false;
        PasteBinFile = DefaultPasteBinFile;
    }

    /// <summary>
    /// The escape sequence that puts text on the system clipboard of the terminal (OSC 52): the text in base64
    /// </summary>
    internal static string Osc52Sequence(string text)
    {
        return "\u001b]52;c;" + Convert.ToBase64String(Encoding.UTF8.GetBytes(text)) + "\u0007";
    }

    /// <summary>
    /// Send yanked text on, as set: to the system clipboard and/or the paste bin
    /// </summary>
    /// <param name="text">The text yanked</param>
    /// <param name="fromNote">The text is from a note of a data file</param>
    /// <param name="asked">The yank had "+ in front (a message is shown if nothing is turned on)</param>
    /// <returns>The message for the status line of the editor (empty: none), and if it is an error</returns>
    internal static (string Message, bool IsError) Copy(string text, bool fromNote, bool asked)
    {
        if (!Osc52 && !PasteBin)
        {
            return asked ? (DialogText.ClipboardAllOff, true) : (string.Empty, false);
        }

        if (fromNote && !FromNotes) return (DialogText.ClipboardNotesOff, true);
        if (Osc52) WriteOsc52(text);
        if (PasteBin)
        {
            var result = WritePasteBin(text, PasteBinAppend);
            if (result != DialogText.Success) return (result, true);
        }

        return (Osc52 && PasteBin ? DialogText.CopiedToBoth : Osc52 ? DialogText.CopiedToClipboard
            : DialogText.CopiedToPasteBin, false);
    }

    #region The paste bin

    /// <summary>
    /// Write text to the paste bin: added to the end of it (on a line of its own), or replacing it
    /// </summary>
    /// <returns>DialogText.Success or an error message</returns>
    internal static string WritePasteBin(string text, bool append)
    {
        try
        {
            var path = PasteBinPath;
            var directory = Path.GetDirectoryName(path);
            if (directory != null && !Directory.Exists(directory)) return DialogText.DirectoryNotFound;
            // The line endings of the system, as other programs expect them
            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n');
            if (!lines.EndsWith('\n')) lines += "\n";
            lines = lines.Replace("\n", Environment.NewLine);
            if (append && File.Exists(path))
            {
                var existing = File.ReadAllText(path, Encoding.UTF8);
                if (existing.Length > 0 && !existing.EndsWith('\n')) lines = Environment.NewLine + lines;
                File.AppendAllText(path, lines, new UTF8Encoding(false));
            }
            else
            {
                File.WriteAllText(path, lines, new UTF8Encoding(false));
            }

            return DialogText.Success;
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }

    /// <summary>
    /// Read the text of the paste bin (with "\n" between lines, and without the line break at its end)
    /// </summary>
    /// <returns>DialogText.Success or a message (the paste bin is empty, or an error)</returns>
    internal static string ReadPasteBin(out string text)
    {
        text = string.Empty;
        try
        {
            var path = PasteBinPath;
            if (File.Exists(path)) text = File.ReadAllText(path, Encoding.UTF8);
        }
        catch (Exception e)
        {
            return e.Message;
        }

        text = text.Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd('\n');
        return text.Length == 0 ? DialogText.PasteBinEmpty : DialogText.Success;
    }

    /// <summary>
    /// Empty the paste bin
    /// </summary>
    /// <returns>DialogText.Success or an error message</returns>
    internal static string ClearPasteBin()
    {
        try
        {
            var path = PasteBinPath;
            if (File.Exists(path)) File.WriteAllText(path, string.Empty);
            return DialogText.Success;
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }

    #endregion

    #region OSC 52

    // Write the escape sequence to the terminal. On Windows the console only reads escape sequences when
    // "virtual terminal processing" is on: it is turned on while the sequence is written, as it was before after
    private static void WriteOsc52(string text)
    {
        var sequence = Osc52Sequence(text);
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var handle = GetStdHandle(StdOutputHandle);
                var hasMode = GetConsoleMode(handle, out var mode);
                if (hasMode) SetConsoleMode(handle, mode | EnableVirtualTerminalProcessing);
                Console.Out.Write(sequence);
                Console.Out.Flush();
                if (hasMode) SetConsoleMode(handle, mode);
            }
            else
            {
                Console.Out.Write(sequence);
                Console.Out.Flush();
            }
        }
        catch
        {
            // Without a terminal there is no clipboard to send to
        }
    }

    private const int StdOutputHandle = -11;
    private const uint EnableVirtualTerminalProcessing = 0x0004;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr handle, out uint mode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(IntPtr handle, uint mode);

    #endregion
}
