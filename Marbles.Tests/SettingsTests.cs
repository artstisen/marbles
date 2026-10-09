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
        Assert.Equal(34, settings.Count);
        Assert.Equal("true", settings["linenumbers"]);
        Assert.Equal("true", settings["browsedual"]);
        Assert.Equal("1", settings["browsepane"]);
        Assert.Equal("true", settings["mdcolors"]);
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

    [Fact]
    public void MissingSettingsFile_UsesDefaults()
    {
        Assert.True(FileIo.LoadSettings());
        Assert.True(Init.UsingDefaultSettings);
    }
}
