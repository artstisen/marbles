using System.Text.RegularExpressions;

namespace Marbles.Tests;

/// <summary>
/// What Marbles does for Linux and macOS terminals, tested where it can be on any system: the bright colors on a
/// terminal of 8 colors (the text console of a Raspberry Pi), Esc and a key that come as one, and the key of a page
/// (a key handed back, and the key that has a page drawn again), and the terminals that drop the clipboard sequence
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

    [Theory]
    [InlineData("TERM=linux", true)]
    [InlineData("TERM=xterm-256color VTE_VERSION=7600", true)]
    [InlineData("TERM=xterm-256color TERM_PROGRAM=Apple_Terminal", true)]
    [InlineData("TERM=xterm-256color TERM_PROGRAM=iTerm.app", false)]
    [InlineData("TERM=xterm-kitty", false)]
    // VTE_VERSION passed on from GNOME Terminal to another terminal, or to tmux, that takes the sequence
    [InlineData("TERM=xterm-kitty VTE_VERSION=7600", false)]
    [InlineData("TERM=xterm-256color VTE_VERSION=7600 KITTY_WINDOW_ID=1", false)]
    [InlineData("TERM=wezterm VTE_VERSION=7600", false)]
    [InlineData("TERM=xterm-256color VTE_VERSION=7600 WEZTERM_EXECUTABLE=/usr/bin/wezterm", false)]
    [InlineData("TERM=alacritty VTE_VERSION=7600", false)]
    [InlineData("TERM=tmux-256color VTE_VERSION=7600 TMUX=/tmp/tmux-1000/default", false)]
    [InlineData("TERM=xterm-256color VTE_VERSION=7600 TERM_PROGRAM=vscode", false)]
    public void TheTerminalsThatDropTheClipboard_AreKnown(string environment, bool expected)
    {
        var variables = environment.Split(' ').Select(pair => pair.Split('=', 2))
            .ToDictionary(pair => pair[0], pair => pair[1]);
        Assert.Equal(expected, SystemClipboard.WithoutClipboard(name => variables.GetValueOrDefault(name)));
    }

    // A static field or property set by a call to a method of its own class: the static initializers run in the
    // order they are written, so the method can see the fields below it still unset (null). That stopped Blyant
    // (from which this comes) at startup on Linux, where SystemClipboard.TerminalWithoutClipboard was set so; on
    // Windows the call was skipped, so only reading the source finds it here. Every kind: a field (readonly or not)
    // and a property with an initializer (get, set, init), not a property computed each time ("=>")
    private static readonly Regex StaticInitializer = new(
        @"\bstatic\b[^=;(){}]*?(\{\s*get;[^}]*\}\s*=|\s=)(?![=>])([^;]*);",
        RegexOptions.Singleline);

    private static readonly Regex OwnCall =
        new(@"(?<![\.\w]|new\s)\b([A-Z]\w*)\s*\(");

    // The strings and comments of the code, which are not looked in for calls
    private static readonly Regex StringOrComment =
        new(@"@?""(?:\\.|""""|[^""\\])*""|'(?:\\.|[^'\\])'|//[^\n]*");

    [Fact]
    public void StaticInitializers_DoNotCallAMethodOfTheirOwnClass()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Marbles.sln"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var root = Path.Combine(directory!.FullName, "Marbles");
        var found = new List<string>();
        foreach (var path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) ||
                path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)) continue;
            foreach (Match match in StaticInitializer.Matches(File.ReadAllText(path)))
            {
                if (OwnCall.Match(StringOrComment.Replace(match.Groups[2].Value, "\"\"")) is { Success: true } call)
                {
                    found.Add(Path.GetRelativePath(root, path) + ": " + call.Groups[1].Value + "(...) in " +
                              match.Value.Split('\n')[0].Trim());
                }
            }
        }

        Assert.True(found.Count == 0, "Find the value when it is first asked for (a property with ??=):\n" +
                                      string.Join("\n", found));
    }
}
