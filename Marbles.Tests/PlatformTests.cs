namespace Marbles.Tests;

/// <summary>
/// What Marbles does for Linux and macOS terminals, tested where it can be on any system: the bright colors on a
/// terminal of 8 colors (the text console of a Raspberry Pi), Esc and a key that come as one, and the key of a page
/// (a key handed back, and the key that has a page drawn again)
/// </summary>
[Collection("Static state")]
public class PlatformTests : IDisposable
{
    public void Dispose()
    {
        Keyboard.TakePending();
    }

    [Theory]
    [InlineData("linux", true)]
    [InlineData("linux-16color", true)]
    [InlineData("xterm", true)]
    [InlineData("screen", true)]
    [InlineData("xterm-256color", false)]
    [InlineData("tmux-256color", false)]
    [InlineData(null, false)]
    public void TheTerminalsOfEightColors_AreKnownByTheirName(string? term, bool expected)
    {
        Assert.Equal(expected, ConsoleColors.HasEightColors(term));
    }

    [Theory]
    [InlineData(ConsoleColor.DarkGray, false, "\u001b[90m")]
    [InlineData(ConsoleColor.White, false, "\u001b[97m")]
    [InlineData(ConsoleColor.Red, false, "\u001b[91m")]
    [InlineData(ConsoleColor.Blue, true, "\u001b[104m")]
    [InlineData(ConsoleColor.Yellow, true, "\u001b[103m")]
    public void ABrightColor_IsSetWithItsOwnCode(ConsoleColor color, bool background, string expected)
    {
        Assert.Equal(expected, ConsoleColors.BrightCode(color, background));
    }

    [Theory]
    [InlineData(ConsoleColor.Black)]
    [InlineData(ConsoleColor.Gray)]
    [InlineData(ConsoleColor.DarkBlue)]
    public void ADarkColor_IsLeftToDotNet(ConsoleColor color)
    {
        Assert.Null(ConsoleColors.BrightCode(color, false));
    }

    [Fact]
    public void EscAndAKeyThatCameAsOne_AreSplit_ButNotAltGrOrControlKeys()
    {
        Assert.True(TextArea.IsEscapeAndKey(new ConsoleKeyInfo('j', ConsoleKey.J, false, true, false)));
        Assert.True(TextArea.IsEscapeAndKey(new ConsoleKeyInfo('U', ConsoleKey.U, true, true, false)));
        // AltGr (Ctrl+Alt) types a character; Alt with a control character (Alt+Enter) is no text
        Assert.False(TextArea.IsEscapeAndKey(new ConsoleKeyInfo('@', ConsoleKey.D2, false, true, true)));
        Assert.False(TextArea.IsEscapeAndKey(new ConsoleKeyInfo('\r', ConsoleKey.Enter, false, true, false)));
        Assert.False(TextArea.IsEscapeAndKey(new ConsoleKeyInfo('j', ConsoleKey.J, false, false, false)));
    }

    [Fact]
    public void AKeyHandedBack_IsReadNext_Once()
    {
        var j = new ConsoleKeyInfo('j', ConsoleKey.J, false, false, false);
        Keyboard.PushBack(j);
        // (read without the console: the key handed back comes first)
        Assert.Equal(j, Keyboard.ReadKey());
        Assert.Null(Keyboard.TakePending());
    }

    [Fact]
    public void TheKeyThatHasAPageDrawnAgain_IsNoKeyOfAPage()
    {
        Assert.True(Keyboard.IsRedraw(Keyboard.RedrawKey));
        Assert.False(Keyboard.IsRedraw(new ConsoleKeyInfo('j', ConsoleKey.J, false, false, false)));
        // F24 typed (with a character) is not taken for it
        Assert.False(Keyboard.IsRedraw(new ConsoleKeyInfo('x', ConsoleKey.F24, false, false, false)));
        Assert.Equal('\0', Keyboard.RedrawKey.KeyChar);
    }

    [Fact]
    public void TheSmallestScreen_FitsAWindowOf80By24()
    {
        // One column and one row of the window are not used
        Assert.Equal(79, Init.MinLineWidth);
        Assert.Equal(23, Init.MinLineCount);
    }
}
