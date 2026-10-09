using System.Text;

namespace Marbles;

/// <summary>
/// A plain text file opened from the file browser. It remembers how the file was written (line endings
/// and byte order mark) so the file is saved the same way, and how its text was read if it is not UTF-8.
/// Text files are always saved as UTF-8.
/// </summary>
internal sealed class TextFile
{
    internal const long MaxSize = 10 * 1024 * 1024; // Larger files are not opened
    private static readonly byte[] Utf8Bom = { 0xEF, 0xBB, 0xBF };

    /// <summary>
    /// The full path the text is saved to
    /// </summary>
    internal string Path { get; set; } = string.Empty;

    /// <summary>
    /// The text of the file, with all line endings as a single newline character
    /// </summary>
    internal string Text { get; private set; } = string.Empty;

    /// <summary>
    /// False if the file was read using another encoding than UTF-8 (it is converted to UTF-8 when saved)
    /// </summary>
    internal bool IsUtf8 { get; private set; } = true;

    /// <summary>
    /// The name of the encoding the file was read with
    /// </summary>
    internal string EncodingName { get; private set; } = "UTF-8";

    internal string LineEnding { get; private set; } = Environment.NewLine;

    /// <summary>
    /// The encoding of the file as shown to the user: "UTF-8", "UTF-8 (BOM)", "UTF-16", "Windows-1252" ...
    /// </summary>
    internal string EncodingLabel => EncodingName + (_hasUtf8Bom ? " (BOM)" : string.Empty);
    private bool _hasUtf8Bom;

    /// <summary>
    /// A new, empty file
    /// </summary>
    internal static TextFile Create(string path)
    {
        return new TextFile { Path = path };
    }

    /// <summary>
    /// Read a text file
    /// </summary>
    /// <exception cref="InvalidDataException">The file is too large or is not a text file</exception>
    internal static TextFile Load(string path)
    {
        if (new FileInfo(path).Length > MaxSize)
        {
            throw new InvalidDataException(DialogText.FileTooLarge);
        }

        var file = Read(File.ReadAllBytes(path));
        file.Path = path;
        return file;
    }

    /// <summary>
    /// Get the text from the content of a file, detecting its encoding
    /// </summary>
    internal static TextFile Read(byte[] bytes)
    {
        var file = new TextFile();
        string text;
        if (StartsWith(bytes, Utf8Bom))
        {
            file._hasUtf8Bom = true;
            text = DecodeUtf8(bytes, Utf8Bom.Length) ?? throw new InvalidDataException(DialogText.FileNotText);
        }
        else if (StartsWith(bytes, 0xFF, 0xFE, 0x00, 0x00))
        {
            text = file.Decode(new UTF32Encoding(false, false), "UTF-32", bytes, 4);
        }
        else if (StartsWith(bytes, 0x00, 0x00, 0xFE, 0xFF))
        {
            text = file.Decode(new UTF32Encoding(true, false), "UTF-32", bytes, 4);
        }
        else if (StartsWith(bytes, 0xFF, 0xFE))
        {
            text = file.Decode(Encoding.Unicode, "UTF-16", bytes, 2);
        }
        else if (StartsWith(bytes, 0xFE, 0xFF))
        {
            text = file.Decode(Encoding.BigEndianUnicode, "UTF-16", bytes, 2);
        }
        else if (Array.IndexOf(bytes, (byte)0) >= 0)
        {
            // Text files do not contain zero bytes
            throw new InvalidDataException(DialogText.FileNotText);
        }
        else
        {
            // UTF-8 if the content is valid as such, else an 8-bit encoding (where any content is valid)
            text = DecodeUtf8(bytes, 0) ?? file.Decode(GetLegacyEncoding(out var name), name, bytes, 0);
        }

        // Use the line ending found most times in the file
        var windows = Count(text, "\r\n");
        var unix = Count(text, "\n") - windows;
        var mac = Count(text, "\r") - windows;
        if (windows + unix + mac > 0)
        {
            file.LineEnding = windows >= unix && windows >= mac ? "\r\n" : unix >= mac ? "\n" : "\r";
        }

        file.Text = TextBuffer.Normalize(text);
        return file;
    }

    /// <summary>
    /// The content to write to the file for a text: UTF-8 with the line endings (and byte order mark) of the original
    /// </summary>
    internal byte[] GetBytes(string text)
    {
        text = TextBuffer.Normalize(text);
        if (LineEnding != "\n") text = text.Replace("\n", LineEnding);
        var content = new UTF8Encoding(false).GetBytes(text);
        return _hasUtf8Bom ? Utf8Bom.Concat(content).ToArray() : content;
    }

    /// <summary>
    /// Save a text to the file
    /// </summary>
    /// <returns>DialogText.Success or an error message</returns>
    internal string Save(string text)
    {
        try
        {
            FileIo.WriteAllBytesSafe(Path, GetBytes(text));
            Text = TextBuffer.Normalize(text);
            // The file is UTF-8 now, whatever it was read as
            IsUtf8 = true;
            EncodingName = "UTF-8";
            return DialogText.Success;
        }
        catch (Exception e)
        {
            return e.Message;
        }
    }

    private string Decode(Encoding encoding, string name, byte[] bytes, int offset)
    {
        IsUtf8 = false;
        EncodingName = name;
        return encoding.GetString(bytes, offset, bytes.Length - offset);
    }

    private static string? DecodeUtf8(byte[] bytes, int offset)
    {
        try
        {
            return new UTF8Encoding(false, true).GetString(bytes, offset, bytes.Length - offset);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }

    // The encoding assumed for text that is not Unicode: Windows-1252 (Western European), which
    // also reads ISO-8859-1 (Latin-1) correctly
    private static Encoding GetLegacyEncoding(out string name)
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            name = "Windows-1252";
            return Encoding.GetEncoding(1252);
        }
        catch
        {
            name = "ISO-8859-1";
            return Encoding.Latin1;
        }
    }

    private static bool StartsWith(byte[] bytes, params byte[] start)
    {
        return bytes.Length >= start.Length && bytes.AsSpan(0, start.Length).SequenceEqual(start);
    }

    private static int Count(string text, string value)
    {
        var count = 0;
        for (var index = text.IndexOf(value, StringComparison.Ordinal);
             index >= 0;
             index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
