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
    public void TheCells_CanHaveColorsOfTheirOwn_InsteadOfTheColorOfTheirRow()
    {
        var logo = new LogoArt("Mine", new[] { "abc", "b", "c", "d" });
        Assert.False(logo.HasCellColors);
        Assert.DoesNotContain("cells=", logo.Serialize());
        logo.SetRowColor(0, ConsoleColor.Cyan);
        logo.SetCellColor(0, 1, ConsoleColor.Red);
        logo.SetCellColor(3, 62, ConsoleColor.Black);
        Assert.True(logo.HasCellColors);
        Assert.Equal(ConsoleColor.Red, logo.ColorAt(0, 1));
        Assert.Equal(ConsoleColor.Cyan, logo.ColorAt(0, 0)); // The color of the row
        Assert.Null(logo.ColorAt(1, 0)); // The color of the logo
        Assert.Null(logo.CellColor(0, 0));

        // A line for each row after the colors of the rows: a hex digit for a color, "." for none
        Assert.EndsWith("d\ncolors=Cyan,default,default,default\ncells=.C\ncells=\ncells=\ncells=" +
                        new string('.', 62) + "0\n", logo.Serialize());
        var read = LogoArt.Parse(logo.Serialize(), "Mine");
        Assert.Equal(ConsoleColor.Red, read.CellColor(0, 1));
        Assert.Equal(ConsoleColor.Black, read.CellColor(3, 62));
        Assert.Null(read.CellColor(1, 0));
        Assert.Equal(ConsoleColor.Cyan, read.RowColor(0));
        Assert.Equal(logo.Serialize(), read.Serialize());
        Assert.Equal(ConsoleColor.Red, read.Clone().CellColor(0, 1));

        // A new color for the row leaves the cells with a color of their own
        read.SetRowColor(0, ConsoleColor.Green);
        Assert.Equal(ConsoleColor.Red, read.ColorAt(0, 1));
        Assert.Equal(ConsoleColor.Green, read.ColorAt(0, 2));
        // ... and so does a new character
        read[0, 1] = ' ';
        Assert.Equal(ConsoleColor.Red, read.CellColor(0, 1));

        read.ClearCellColors();
        Assert.False(read.HasCellColors);
        Assert.Equal(ConsoleColor.Green, read.ColorAt(0, 1));
        Assert.DoesNotContain("cells=", read.Serialize());
    }

    [Fact]
    public void TheColorsOfTheCells_AreReadWithoutTheColorsOfTheRows_AndWithSmallLetters()
    {
        var logo = LogoArt.Parse("a\nb\nc\nd\ncells=e?\ncells=.b\n", "Mine");
        Assert.Equal(ConsoleColor.Yellow, logo.CellColor(0, 0));
        Assert.Null(logo.CellColor(0, 1)); // Not a color
        Assert.Equal(ConsoleColor.Cyan, logo.CellColor(1, 1));
        Assert.Null(logo.RowColor(0));
        // A row cannot be read as the colors of the cells
        Assert.False(LogoArt.Parse("cells=EEE\n", "X").HasCellColors);
    }

    [Fact]
    public void ARow_IsDrawnAsItsPartsOfOneColor()
    {
        var logo = new LogoArt("Mine", new[] { "  ab cd", "", "x", "" });
        // One color: one part, the whole row
        var whole = Assert.Single(logo.Runs(0));
        Assert.Equal((0, "  ab cd".PadRight(63), (ConsoleColor?)null), whole);
        Assert.Equal(63, Assert.Single(logo.Runs(1)).Text.Length);

        logo.SetRowColor(0, ConsoleColor.Cyan);
        logo.SetCellColor(0, 3, ConsoleColor.Red); // "b"
        logo.SetCellColor(0, 4, ConsoleColor.Blue); // Blank space: its color cannot be seen
        var runs = logo.Runs(0);
        Assert.Equal(3, runs.Count);
        Assert.Equal((0, "  a", (ConsoleColor?)ConsoleColor.Cyan), runs[0]);
        Assert.Equal((3, "b ", (ConsoleColor?)ConsoleColor.Red), runs[1]);
        Assert.Equal((5, "cd".PadRight(58), (ConsoleColor?)ConsoleColor.Cyan), runs[2]);
        // The parts are the row
        Assert.Equal(logo.Row(0), string.Concat(runs.Select(run => run.Text)));
    }

    [Fact]
    public void TheRows_CanHaveColorsOfTheirOwn()
    {
        var logo = new LogoArt("Mine", new[] { "a", "b", "c", "d" });
        Assert.DoesNotContain("colors=", logo.Serialize());
        logo.SetRowColor(0, ConsoleColor.Cyan);
        logo.SetRowColor(2, ConsoleColor.Red);
        Assert.EndsWith("d\ncolors=Cyan,default,Red,default\n", logo.Serialize());
        var read = LogoArt.Parse(logo.Serialize(), "Mine");
        Assert.Equal(ConsoleColor.Cyan, read.RowColor(0));
        Assert.Null(read.RowColor(1));
        Assert.Equal(ConsoleColor.Red, read.RowColor(2));
        // A copy has the colors too
        Assert.Equal(ConsoleColor.Red, read.Clone().RowColor(2));
        // A row cannot be read as the colors
        Assert.Equal("colors=Red".PadRight(63), LogoArt.Parse("colors=Red\n", "X").Row(0));
        Assert.Null(LogoArt.Parse("colors=Red\n", "X").RowColor(0));
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
        Assert.Equal(new[] { "Marbles", "Reflections", "Islands" }, Logos.AllNames());
        var mine = new LogoArt("My logo", new[] { "Hello" });
        Assert.Equal(DialogText.Success, Logos.Save(mine));
        Assert.True(File.Exists(Path.Combine(_directory, "My logo.txt")));
        Assert.Equal(new[] { "Marbles", "Reflections", "Islands", "My logo" }, Logos.AllNames());
        Assert.Equal("Hello".PadRight(63), Logos.Load("my logo")!.Row(0));
        Assert.Equal(DialogText.Success, Logos.Delete("My logo"));
        Assert.Null(Logos.Load("My logo"));
        Assert.Equal("Marbles", Logos.Load("marbles")!.Name);
    }

    [Fact]
    public void TheBuiltInLogos_AreMarbles_ReflectionsWithRowColors_AndIslandsWithCellColors()
    {
        var reflections = Logos.Load("reflections")!;
        Assert.Equal("Reflections", reflections.Name);
        Assert.Equal(LogoArt.BuiltIn.AllRows, reflections.AllRows);
        Assert.Equal(new ConsoleColor?[] { ConsoleColor.Blue, ConsoleColor.Cyan, ConsoleColor.Green, ConsoleColor.DarkGreen },
            Enumerable.Range(0, LogoArt.Rows).Select(reflections.RowColor));
        Assert.False(reflections.HasCellColors);
        // The logo of Marbles has no colors of its own
        Assert.Null(LogoArt.BuiltIn.RowColor(0));

        var islands = Logos.Load("Islands")!;
        Assert.Equal("Islands", islands.Name);
        Assert.True(islands.HasCellColors);
        // Every character that can be seen has a color: the stars in the sky are cyan, the water blue and the
        // sun yellow
        var stars = new[] { (0, 1), (0, 20), (0, 40), (1, 13) };
        for (var row = 0; row < LogoArt.Rows; row++)
        {
            for (var column = 0; column < LogoArt.Width; column++)
            {
                Assert.True(LogoArt.Typeable(islands[row, column]));
                if (islands[row, column] == ' ') continue;
                Assert.NotNull(islands.CellColor(row, column));
            }
        }

        Assert.All(stars, star => Assert.Equal('.', islands[star.Item1, star.Item2]));
        Assert.All(stars, star => Assert.Equal(ConsoleColor.Cyan, islands.CellColor(star.Item1, star.Item2)));
        Assert.Equal('~', islands[3, 1]);
        Assert.Equal(ConsoleColor.Blue, islands.CellColor(3, 1));
        Assert.Equal('0', islands[1, 30]);
        Assert.Equal(ConsoleColor.Yellow, islands.CellColor(1, 30));
        Assert.Equal(" . ,-\\|/-,".PadRight(20) + ".", islands.Row(0)[..21]);
        Assert.StartsWith("~~~/..;...;", islands.Row(3));
        Assert.Null(islands.RowColor(0));

        // They cannot be saved over, renamed or deleted
        Assert.True(Logos.IsBuiltIn("ISLANDS"));
        Assert.NotEqual(DialogText.Success, Logos.Save(islands));
        Assert.NotEqual(DialogText.Success, Logos.Delete("Reflections"));
        Assert.NotEqual(DialogText.Success, Logos.Rename("Islands", "Mine"));
    }

    [Fact]
    public void ALogoOfYourOwn_CanBeRenamed()
    {
        Assert.Equal(DialogText.Success, Logos.Save(new LogoArt("First", new[] { "One" })));
        Assert.Equal(DialogText.Success, Logos.Save(new LogoArt("Second", new[] { "Two" })));
        Assert.Equal(DialogText.Success, Logos.Rename("first", " Third "));
        Assert.Equal(new[] { "Second", "Third" }, Logos.CustomNames());
        Assert.Equal("One".PadRight(63), Logos.Load("Third")!.Row(0));
        // Not to the name of another logo, a built-in name or a name that cannot be used
        Assert.Equal(DialogText.LogoNameTaken, Logos.Rename("Third", "second"));
        Assert.Equal(DialogText.LogoNameBuiltIn, Logos.Rename("Third", "Islands"));
        Assert.Equal(DialogText.LogoNameInvalid, Logos.Rename("Third", "a/b"));
        Assert.Equal(DialogText.LogoNameInvalid, Logos.Rename("Missing", "Fourth"));
        Assert.Equal("Two".PadRight(63), Logos.Load("Second")!.Row(0));
        // Other capital letters are a new name too
        Assert.Equal(DialogText.Success, Logos.Rename("Third", "THIRD"));
        Assert.Equal(new[] { "Second", "THIRD" }, Logos.CustomNames());
    }

    [Theory]
    [InlineData("Mine", true)]
    [InlineData("Marbles", false)]
    [InlineData("Islands", false)]
    [InlineData(" ", false)]
    [InlineData("a/b", false)]
    public void TheNameOfALogoOfYourOwn(string name, bool valid)
    {
        Assert.Equal(valid, Logos.IsValidName(name));
    }
}
