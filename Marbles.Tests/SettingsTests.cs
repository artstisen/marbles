namespace Marbles.Tests;

[Collection("Static state")]
public class SettingsTests : IDisposable
{
    private readonly string _originalDirectory = Directory.GetCurrentDirectory();

    private readonly string _workDirectory =
        Path.Combine(Path.GetTempPath(), "marbles-tests-" + Guid.NewGuid().ToString("N"));

    private string SettingsFile => Path.Combine(_workDirectory, "data", "settings.ini");

    // A settings file as written by v1.0.1.x, including the key mapping that no longer exists
    private const string LegacySettings = @"; Last modified 2024-03-15 09:55:06
[SETTINGS]

; DIMENSIONS
linewidth=100
linecount=30

; KEY MAPPING
kbtrigger=W
kbsave=E
kbsavexit=Q
kbrepeat=K
kbnxtword=L
kbpreword=N
kbselect=O

; OPTIONAL
vignette=false

; STYLING
asciistyle=1
logo=7
logotxt=a=b text
(clpreset)=5
color1=2
color2=4
color3=1
color4=14
color5=12
color6=14
color7=12
color8=0
color9=10
color10=15
color11=4
color12=0

; FILES
subdir=
file=notes.mbls
backup=
";

    public SettingsTests()
    {
        Directory.CreateDirectory(Path.Combine(_workDirectory, "data"));
        Directory.SetCurrentDirectory(_workDirectory);
        FileIo.ResetSubFolder();
        Init.UsingDefaultSettings = false;
    }

    public void Dispose()
    {
        Init.SetDefaultOutputDimensions();
        Logo.ResetLogoIndex();
        Logo.ResetLogoText();
        AsciiStyles.ResetAsciiStyle();
        FileIo.SetDefaultFileName();
        Directory.SetCurrentDirectory(_originalDirectory);
        Directory.Delete(_workDirectory, true);
    }

    [Fact]
    public void SettingsFromEarlierVersions_AreLoaded()
    {
        File.WriteAllText(SettingsFile, LegacySettings);
        File.WriteAllText(Path.Combine(_workDirectory, "data", "notes.mbls"), "x");

        Assert.True(FileIo.LoadSettings());
        Assert.False(Init.UsingDefaultSettings);
        Assert.Equal(100, Init.LineWidth);
        Assert.Equal(30, Init.LineCount);
        Assert.Equal(1, AsciiStyles.GetAsciiStyle());
        Assert.Equal(7, Logo.GetLogoIndex());
        Assert.Equal("a=b text", Logo.GetLogoText());
        Assert.Equal(5, ConsoleColors.GetColorPresetIndex());
        Assert.Equal("notes.mbls", FileIo.GetFileName());
        Assert.False(FileIo.BackupEnabled());
    }

    [Theory]
    [InlineData("startmode=notes", "Notes")]
    [InlineData("startmode= Editor ", "Editor")]
    [InlineData("startmode=select", "Select")]
    [InlineData("startmode=other", "Select")]
    [InlineData("", "Select")]
    public void StartMode_IsLoadedAndSaved(string setting, string expected)
    {
        File.WriteAllText(SettingsFile, LegacySettings + "\n" + setting + "\n");
        File.WriteAllText(Path.Combine(_workDirectory, "data", "notes.mbls"), "x");
        try
        {
            Assert.True(FileIo.LoadSettings());
            Assert.Equal(expected, StartMenu.Mode.ToString());
            FileIo.SaveSettings();
            Assert.Equal(expected.ToLowerInvariant(), FileIo.ParseSettings(File.ReadAllText(SettingsFile))["startmode"]);
        }
        finally
        {
            StartMenu.Mode = StartMenu.StartMode.Select;
        }
    }

    [Fact]
    public void SavedSettings_HaveNoKeyMapping_AndLoadAgain()
    {
        File.WriteAllText(SettingsFile, LegacySettings);
        File.WriteAllText(Path.Combine(_workDirectory, "data", "notes.mbls"), "x");
        FileIo.LoadSettings();
        FileIo.SaveSettings();

        var saved = File.ReadAllText(SettingsFile);
        Assert.DoesNotContain("kb", saved);
        Assert.DoesNotContain("KEY MAPPING", saved);
        var settings = FileIo.ParseSettings(saved);
        Assert.Equal("100", settings["linewidth"]);
        Assert.Equal("a=b text", settings["logotxt"]);
        Assert.Equal("notes.mbls", settings["file"]);
        Assert.Equal(35, settings.Count);
        Assert.Equal("default", settings["background"]);
        Assert.Equal("true", settings["linenumbers"]);
        Assert.Equal("true", settings["browsedual"]);
        Assert.Equal("1", settings["browsepane"]);
        Assert.Equal("true", settings["syntaxcolors"]);
        Assert.Equal("false", settings["monochrome"]);
        Assert.Equal("select", settings["startmode"]);

        Init.SetDefaultOutputDimensions();
        Assert.True(FileIo.LoadSettings());
        Assert.Equal(100, Init.LineWidth);
    }

    [Fact]
    public void SettingsCanBeInAnyOrder_AndMissingOnesGetDefaults()
    {
        File.WriteAllText(SettingsFile, "# comment\nlogo=3\n\nnot a setting\nLINECOUNT=40\nlinewidth=nonsense\n");
        Assert.True(FileIo.LoadSettings());
        Assert.Equal(3, Logo.GetLogoIndex());
        Assert.Equal(40, Init.LineCount);
        Assert.Equal(Init.MinLineWidth, Init.LineWidth);
        Assert.Equal("data.mbls", FileIo.GetFileName());
    }

    [Theory]
    [InlineData("background=5", "DarkBlue", "5")]
    [InlineData("background=default", null, "default")]
    [InlineData("background=nonsense", null, "default")]
    [InlineData("background=16", null, "default")]
    [InlineData("", null, "default")]
    public void Background_IsLoadedAndSaved(string setting, string? expected, string saved)
    {
        File.WriteAllText(SettingsFile, LegacySettings + "\n" + setting + "\n");
        File.WriteAllText(Path.Combine(_workDirectory, "data", "notes.mbls"), "x");
        try
        {
            Assert.True(FileIo.LoadSettings());
            Assert.Equal(expected, ConsoleColors.Background?.ToString());
            FileIo.SaveSettings();
            Assert.Equal(saved, FileIo.ParseSettings(File.ReadAllText(SettingsFile))["background"]);
            // A color preset uses the background of the terminal
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.IceWarp);
            Assert.Null(ConsoleColors.Background);
        }
        finally
        {
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Reset);
        }
    }

    [Fact]
    public void TheDeepBluePreset_IsTheSecond_AndHasABackground()
    {
        try
        {
            Assert.Equal(8, Actions.ColorPresetCount);
            Assert.Equal("Deep Blue", Actions.ColorPresetName(1));
            Assert.Equal("Marbles Quest", Actions.ColorPresetName(2));
            Actions.SetColorPreset(1);
            Assert.Equal(ConsoleColor.DarkBlue, ConsoleColors.Background);
            FileIo.SaveSettings();
            var saved = FileIo.ParseSettings(File.ReadAllText(SettingsFile));
            var expected = new[] { "2", "4", "4", "14", "8", "15", "12", "0", "10", "15", "6", "4" };
            for (var i = 1; i <= expected.Length; i++)
            {
                Assert.Equal(expected[i - 1], saved["color" + i]);
            }

            Assert.Equal("5", saved["background"]);
            Assert.Equal("1", saved["(clpreset)"]);
            // The others use the background of the terminal
            Actions.SetColorPreset(2);
            Assert.Null(ConsoleColors.Background);
        }
        finally
        {
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Reset);
            ConsoleColors.DefaultColorPresetIndex();
        }
    }

    [Fact]
    public void TheBackground_CannotHaveTheColorOfTheText()
    {
        try
        {
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Reset);
            ConsoleColors.InitializePreviewColors();
            // The default text is gray, the logo cyan
            Assert.False(ConsoleColors.IsPreviewColorAllowed(0, ConsoleColor.Gray));
            Assert.False(ConsoleColors.IsPreviewColorAllowed(0, ConsoleColor.Cyan));
            Assert.True(ConsoleColors.IsPreviewColorAllowed(0, ConsoleColor.DarkBlue));
            ConsoleColors.PreviewBackground = ConsoleColor.DarkBlue;
            ConsoleColors.SavePreviewToCurrentColors();
            Assert.Equal(ConsoleColor.DarkBlue, ConsoleColors.Background);
            // Text on it cannot have its color
            Assert.True(ConsoleColors.IsBackgroundColor(ConsoleColor.DarkBlue));
        }
        finally
        {
            ConsoleColors.SetColors(ConsoleColors.ColorPreset.Reset);
        }
    }

    [Fact]
    public void MissingSettingsFile_UsesDefaults()
    {
        Assert.True(FileIo.LoadSettings());
        Assert.True(Init.UsingDefaultSettings);
    }
}
