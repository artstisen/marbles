namespace Marbles.Tests;

/// <summary>
/// The logos: the built-in one, the files of the user's own, and their size of 4 rows of 63 characters
/// </summary>
[Collection("Static state")]
public class LogoTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "marbles-logos-" + Guid.NewGuid());

    public LogoTests()
    {
        Logos.DirectoryOverride = _directory;
    }

    public void Dispose()
    {
        Logos.DirectoryOverride = null;
        Logo.Current = LogoArt.BuiltIn;
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    [Fact]
    public void TheBuiltInLogo_IsMarbles_FourRowsOf63()
    {
        var logo = LogoArt.BuiltIn;
        Assert.Equal("Marbles", logo.Name);
        Assert.Equal(4, logo.AllRows.Count);
        Assert.All(logo.AllRows, row => Assert.Equal(63, row.Length));
        Assert.StartsWith("M_     _M \"\"\"\"\"MM_", logo.Row(0));
        Assert.EndsWith("MMM__MM\"", logo.Row(3));
    }

    [Fact]
    public void ALogo_IsCutAndFilled_ToItsSize()
    {
        var logo = LogoArt.Parse("short\n" + new string('x', 80) + "\r\nthird\nfourth\nfifth\n", "Mine");
        Assert.Equal("short".PadRight(63), logo.Row(0));
        Assert.Equal(new string('x', 63), logo.Row(1));
        Assert.Equal("fourth".PadRight(63), logo.Row(3));
        // Written without the blank space at the ends of the rows, and read again the same
        Assert.Equal("short\n" + new string('x', 63) + "\nthird\nfourth\n", logo.Serialize());
        Assert.Equal(logo.AllRows, LogoArt.Parse(logo.Serialize(), "Mine").AllRows);
    }

    [Fact]
    public void ACharacterTakesOneCell()
    {
        // Wide characters and control characters are blank space
        var logo = LogoArt.Parse("a汉b\tc", "X");
        Assert.Equal("a b c", logo.Row(0)[..5]);
        logo[1, 0] = 'Q';
        Assert.Equal('Q', logo[1, 0]);
        Assert.True(LogoArt.Typeable('#'));
        Assert.False(LogoArt.Typeable('æ'));
    }

    [Fact]
    public void LogosOfYourOwn_AreFiles()
    {
        Assert.Equal(new[] { "Marbles" }, Logos.AllNames());
        var mine = new LogoArt("My logo", new[] { "Hello" });
        Assert.Equal(DialogText.Success, Logos.Save(mine));
        Assert.True(File.Exists(Path.Combine(_directory, "My logo.txt")));
        Assert.Equal(new[] { "Marbles", "My logo" }, Logos.AllNames());
        Assert.Equal("Hello".PadRight(63), Logos.Load("my logo")!.Row(0));
        Assert.Equal(DialogText.Success, Logos.Delete("My logo"));
        Assert.Null(Logos.Load("My logo"));
        Assert.Equal("Marbles", Logos.Load("marbles")!.Name);
    }

    [Theory]
    [InlineData("Mine", true)]
    [InlineData("Marbles", false)]
    [InlineData(" ", false)]
    [InlineData("a/b", false)]
    public void TheNameOfALogoOfYourOwn(string name, bool valid)
    {
        Assert.Equal(valid, Logos.IsValidName(name));
    }
}
