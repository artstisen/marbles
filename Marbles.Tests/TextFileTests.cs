using System.Text;

namespace Marbles.Tests;

public class TextFileTests
{
    private static readonly byte[] Utf8Bom = { 0xEF, 0xBB, 0xBF };

    [Theory]
    [InlineData("one\ntwo\n", "\n")]
    [InlineData("one\r\ntwo\r\n", "\r\n")]
    [InlineData("one\rtwo\r", "\r")]
    [InlineData("æøå 日本語 \U0001F600\r\nlast line without ending", "\r\n")]
    public void Utf8File_IsReadAndWrittenBackUnchanged(string content, string lineEnding)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var file = TextFile.Read(bytes);
        Assert.True(file.IsUtf8);
        Assert.Equal(lineEnding, file.LineEnding);
        Assert.DoesNotContain('\r', file.Text);
        Assert.Equal(content.Replace("\r\n", "\n").Replace('\r', '\n'), file.Text);
        Assert.Equal(bytes, file.GetBytes(file.Text));
    }

    [Fact]
    public void Utf8ByteOrderMark_IsKept()
    {
        var bytes = Utf8Bom.Concat(Encoding.UTF8.GetBytes("text\r\n")).ToArray();
        var file = TextFile.Read(bytes);
        Assert.True(file.IsUtf8);
        Assert.Equal("text\n", file.Text);
        Assert.Equal(bytes, file.GetBytes(file.Text));
    }

    [Fact]
    public void EmptyFile_IsUtf8WithTheLineEndingOfTheSystem()
    {
        var file = TextFile.Read(Array.Empty<byte>());
        Assert.True(file.IsUtf8);
        Assert.Equal("", file.Text);
        Assert.Equal(Environment.NewLine, file.LineEnding);
        Assert.Equal(Encoding.UTF8.GetBytes("a" + Environment.NewLine + "b"), file.GetBytes("a\nb"));
    }

    [Fact]
    public void MixedLineEndings_UseTheMostCommon()
    {
        var file = TextFile.Read(Encoding.UTF8.GetBytes("a\r\nb\r\nc\nd"));
        Assert.Equal("\r\n", file.LineEnding);
        Assert.Equal("a\nb\nc\nd", file.Text);
        Assert.Equal("a\r\nb\r\nc\r\nd", Encoding.UTF8.GetString(file.GetBytes(file.Text)));
    }

    [Fact]
    public void WesternEuropeanFile_IsDetectedAndConvertedToUtf8()
    {
        // "Blåbærgrød på café" + line break in Windows-1252/ISO-8859-1: not valid as UTF-8
        var bytes = new byte[]
        {
            0x42, 0x6C, 0xE5, 0x62, 0xE6, 0x72, 0x67, 0x72, 0xF8, 0x64, 0x20, 0x70, 0xE5, 0x20, 0x63, 0x61, 0x66,
            0xE9, 0x0D, 0x0A
        };
        var file = TextFile.Read(bytes);
        Assert.False(file.IsUtf8);
        Assert.Equal("Windows-1252", file.EncodingName);
        Assert.Equal("Blåbærgrød på café\n", file.Text);
        Assert.Equal(Encoding.UTF8.GetBytes("Blåbærgrød på café\r\n"), file.GetBytes(file.Text));
    }

    [Fact]
    public void Windows1252Characters_AreRead()
    {
        // Euro sign and curly quotes only exist in Windows-1252, not in ISO-8859-1
        var file = TextFile.Read(new byte[] { 0x80, 0x20, 0x93, 0x78, 0x94 });
        Assert.Equal("€ “x”", file.Text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Utf16File_IsDetectedAndConvertedToUtf8(bool bigEndian)
    {
        var encoding = new UnicodeEncoding(bigEndian, true);
        var bytes = encoding.GetPreamble().Concat(encoding.GetBytes("æøå\r\nline 2")).ToArray();
        var file = TextFile.Read(bytes);
        Assert.False(file.IsUtf8);
        Assert.Equal("UTF-16", file.EncodingName);
        Assert.Equal("æøå\nline 2", file.Text);
        Assert.Equal(Encoding.UTF8.GetBytes("æøå\r\nline 2"), file.GetBytes(file.Text));
    }

    [Fact]
    public void BinaryFile_IsRefused()
    {
        Assert.Throws<InvalidDataException>(() => TextFile.Read(new byte[] { 0x4D, 0x5A, 0x00, 0x03, 0x00, 0x00 }));
    }

    [Fact]
    public void LoadAndSave_OnDisk()
    {
        var path = Path.Combine(Path.GetTempPath(), "marbles-test-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            File.WriteAllBytes(path, new byte[] { 0x63, 0x61, 0x66, 0xE9, 0x0A }); // "café" + line break, not UTF-8
            var file = TextFile.Load(path);
            Assert.False(file.IsUtf8);
            Assert.Equal(DialogText.Success, file.Save(file.Text + "more"));
            Assert.True(file.IsUtf8);
            Assert.Equal(Encoding.UTF8.GetBytes("café\nmore"), File.ReadAllBytes(path));
            Assert.False(File.Exists(path + ".tmp"));

            var again = TextFile.Load(path);
            Assert.True(again.IsUtf8);
            Assert.Equal("café\nmore", again.Text);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void EncodingLabel_TellsTheEncoding()
    {
        Assert.Equal("UTF-8", TextFile.Read(System.Text.Encoding.UTF8.GetBytes("text æøå")).EncodingLabel);
        Assert.Equal("UTF-8 (BOM)",
            TextFile.Read(new byte[] { 0xEF, 0xBB, 0xBF }.Concat(System.Text.Encoding.UTF8.GetBytes("text")).ToArray())
                .EncodingLabel);
        Assert.Equal("UTF-16",
            TextFile.Read(new byte[] { 0xFF, 0xFE }.Concat(System.Text.Encoding.Unicode.GetBytes("text")).ToArray())
                .EncodingLabel);
    }
}
