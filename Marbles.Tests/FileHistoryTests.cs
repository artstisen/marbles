namespace Marbles.Tests;

[Collection("Static state")]
public class FileHistoryTests : IDisposable
{
    private readonly string _root;
    private readonly string? _savedPath = FileHistory.FilePathOverride;

    public FileHistoryTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "marbles_history_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        FileHistory.FilePathOverride = Path.Combine(_root, FileHistory.FileName);
        FileHistory.Clear();
    }

    public void Dispose()
    {
        FileHistory.FilePathOverride = _savedPath;
        FileHistory.Clear();
        Directory.Delete(_root, true);
    }

    private string In(string name) => Path.Combine(_root, name);

    private static string Names() =>
        string.Join(" ", FileHistory.Items.Select(entry => Path.GetFileName(entry.Path)));

    [Fact]
    public void ThePlaceOfTheCursor_IsALineAndColumn()
    {
        const string text = "one\ntwo three\n\nlast";
        Assert.Equal((1, 1), CaretPosition.Of(text, 0));
        Assert.Equal((2, 5), CaretPosition.Of(text, 8));
        Assert.Equal((3, 1), CaretPosition.Of(text, 14));
        Assert.Equal((4, 5), CaretPosition.Of(text, text.Length));
        foreach (var index in new[] { 0, 3, 4, 8, 13, 14, 15, text.Length })
        {
            var (line, column) = CaretPosition.Of(text, index);
            Assert.Equal(index, CaretPosition.IndexOf(text, line, column));
        }

        // A text that has changed since: the end of the line, or of the text
        Assert.Equal(3, CaretPosition.IndexOf(text, 1, 40));
        Assert.Equal(text.Length, CaretPosition.IndexOf(text, 99, 1));
        Assert.Equal(0, CaretPosition.IndexOf(string.Empty, 5, 5));

        // As written in the lists
        Assert.Equal("12:5\t", CaretPosition.Write(12, 5));
        Assert.Equal(string.Empty, CaretPosition.Write(0, 0));
        var written = "12:5\tC:\\notes\\a.md";
        Assert.Equal((12, 5), CaretPosition.Take(ref written));
        Assert.Equal("C:\\notes\\a.md", written);
        var path = "/home/notes/1:2 a.md";
        Assert.Equal((0, 0), CaretPosition.Take(ref path));
        Assert.Equal("/home/notes/1:2 a.md", path);
    }

    [Fact]
    public void WhereTheCursorWas_IsKeptInTheHistory_AndReadFromAnOlderList()
    {
        var time = new DateTime(2026, 10, 14, 9, 30, 0);
        FileHistory.Add(In("a.txt"), time);
        FileHistory.Add(In("b.txt"), time);
        Assert.Null(FileHistory.CaretOf(In("a.txt")));
        Assert.True(FileHistory.SetCaret(In("a.txt"), 12, 5));
        Assert.False(FileHistory.SetCaret(In("other.txt"), 1, 1));
        Assert.Equal((12, 5), FileHistory.CaretOf(In("a.txt")));
        // Opened again: moved to the top, with the place kept
        FileHistory.Add(In("a.txt"), time.AddMinutes(1));
        Assert.Equal((12, 5), FileHistory.CaretOf(In("a.txt")));

        var text = FileHistory.Serialize();
        Assert.Contains("2026-10-14 09:31\t12:5\t" + In("a.txt") + "\n", text);
        Assert.Contains("2026-10-14 09:30\t" + In("b.txt") + "\n", text);
        FileHistory.Parse(text);
        Assert.Equal((12, 5), FileHistory.CaretOf(In("a.txt")));
        Assert.Null(FileHistory.CaretOf(In("b.txt")));
        Assert.Equal(new[] { "a.txt", "b.txt" }, FileHistory.Items.Select(entry => Path.GetFileName(entry.Path)));
    }

    [Fact]
    public void Add_PutsTheFileFirst_AndMovesAFileOpenedAgain()
    {
        var time = new DateTime(2026, 10, 10, 20, 45, 0);
        FileHistory.Add(In("a.txt"), time);
        FileHistory.Add(In("b.txt"), time.AddMinutes(1));
        FileHistory.Add(In("c.txt"), time.AddMinutes(2));
        Assert.Equal("c.txt b.txt a.txt", Names());
        FileHistory.Add(In("a.txt"), time.AddMinutes(3));
        Assert.Equal("a.txt c.txt b.txt", Names());
        Assert.Equal(time.AddMinutes(3), FileHistory.Items[0].Opened);
    }

    [Fact]
    public void Add_DropsTheOldest_WhenTheListIsFull()
    {
        for (var i = 0; i < FileHistory.MaxCount + 5; i++)
        {
            FileHistory.Add(In(i + ".txt"), DateTime.Now);
        }

        Assert.Equal(FileHistory.MaxCount, FileHistory.Items.Count);
        Assert.Equal((FileHistory.MaxCount + 4) + ".txt", Path.GetFileName(FileHistory.Items[0].Path));
        Assert.Equal("5.txt", Path.GetFileName(FileHistory.Items[^1].Path));
    }

    [Fact]
    public void TheList_IsSavedAndReadAgain()
    {
        var time = new DateTime(2026, 10, 10, 20, 45, 0);
        FileHistory.Add(In("a.txt"), time);
        FileHistory.Add(In("my notes.md"), time.AddDays(1));
        Assert.Equal(DialogText.Success, FileHistory.Save());
        FileHistory.Clear();
        FileHistory.Load();
        Assert.Equal("my notes.md a.txt", Names());
        Assert.Equal(time, FileHistory.Items[1].Opened);
        Assert.Equal("2026-10-11 20:45\t" + In("my notes.md") + "\n2026-10-10 20:45\t" + In("a.txt") + "\n",
            FileHistory.Serialize());
    }

    [Fact]
    public void Parse_SkipsLinesThatAreNotFiles_AndKeepsAFileWithoutATime()
    {
        FileHistory.Parse("no tab\n\t\nnot a time\t" + In("a.txt") + "\r\n2026-10-10 20:45\t" + In("a.txt") + "\n");
        Assert.Equal("a.txt", Names());
        Assert.Equal(DateTime.MinValue, FileHistory.Items[0].Opened);
    }

    [Fact]
    public void RemoveAndClear()
    {
        FileHistory.Add(In("a.txt"), DateTime.Now);
        FileHistory.Add(In("b.txt"), DateTime.Now);
        FileHistory.RemoveAt(0);
        FileHistory.RemoveAt(5);
        Assert.Equal("a.txt", Names());
        FileHistory.Clear();
        Assert.Empty(FileHistory.Items);
    }
}
