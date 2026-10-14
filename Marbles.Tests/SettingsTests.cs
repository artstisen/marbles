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
        Logo.Animation = Logo.AnimationSpeed.Fast;
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
        Assert.Equal("notes.mbls", FileIo.GetFileName());
        Assert.False(FileIo.BackupEnabled());
    }

    [Theory]
    [InlineData("onlymode=editor", "Editor")]
    [InlineData("onlymode= Notes ", "Notes")]
    [InlineData("onlymode=none", "None")]
    [InlineData("onlymode=other", "None")]
    [InlineData("", "None")]
    public void OnlyMode_IsLoadedAndSaved_AndResetWithTheSettings(string setting, string expected)
    {
        File.WriteAllText(SettingsFile, LegacySettings + "\n" + setting + "\n");
        File.WriteAllText(Path.Combine(_workDirectory, "data", "notes.mbls"), "x");
        try
        {
            Assert.True(FileIo.LoadSettings());
            Assert.Equal(expected, StartMenu.Only.ToString());
            FileIo.SaveSettings();
            Assert.Equal(expected.ToLowerInvariant(), FileIo.ParseSettings(File.ReadAllText(SettingsFile))["onlymode"]);
            // Resetting all settings uses both modes again
            Actions.ApplyDefaultSettings();
            Assert.Equal(StartMenu.OnlyMode.None, StartMenu.Only);
            Assert.Equal("none", FileIo.ParseSettings(File.ReadAllText(SettingsFile))["onlymode"]);
        }
        finally
        {
            StartMenu.Only = StartMenu.OnlyMode.None;
        }
    }

    [Theory]
    [InlineData("logoanim=slow", "Slow", "slow")]
    [InlineData("logoanim= VeryFast ", "VeryFast", "veryfast")]
    [InlineData("logoanim=off", "Off", "off")]
    [InlineData("logoanim=false", "Off", "off")] // As it was written before
    [InlineData("logoanim=true", "Fast", "fast")]
    [InlineData("", "Fast", "fast")]
    public void TheSpeedOfTheLogoAnimation_IsLoadedAndSaved_AndResetWithTheSettings(string setting, string expected,
        string saved)
    {
        File.WriteAllText(SettingsFile, LegacySettings + "\n" + setting + "\n");
        File.WriteAllText(Path.Combine(_workDirectory, "data", "notes.mbls"), "x");
        try
        {
            Assert.True(FileIo.LoadSettings());
            Assert.Equal(expected, Logo.Animation.ToString());
            Assert.Equal(expected != "Off", Logo.AnimationEnabled);
            FileIo.SaveSettings();
            Assert.Equal(saved, FileIo.ParseSettings(File.ReadAllText(SettingsFile))["logoanim"]);
            Actions.ApplyDefaultSettings();
            Assert.Equal(Logo.AnimationSpeed.Fast, Logo.Animation);
        }
        finally
        {
            Logo.Animation = Logo.AnimationSpeed.Fast;
        }
    }

    [Fact]
    public void TheLogoAnimation_TakesLongerTheSlowerItIs()
    {
        Assert.True(Logo.AnimationDuration(Logo.AnimationSpeed.Slow) > Logo.AnimationDuration(Logo.AnimationSpeed.Fast));
        Assert.True(Logo.AnimationDuration(Logo.AnimationSpeed.Fast) > Logo.AnimationDuration(Logo.AnimationSpeed.VeryFast));
        Assert.True(Logo.AnimationDuration(Logo.AnimationSpeed.VeryFast) > 0);
        // The columns are done evenly over the time, the last one at the end
        Assert.Equal(800, Logo.ColumnTime(LogoArt.Width - 1, 800));
        Assert.Equal(12, Logo.ColumnTime(0, 800));
        Assert.True(Logo.ColumnTime(31, 800) is > 390 and < 420);
    }

    [Fact]
    public void WithOneModeOnly_MarblesDoesNotStartInTheOther()
    {
        try
        {
            StartMenu.Mode = StartMenu.StartMode.Notes;
            StartMenu.Only = StartMenu.OnlyMode.Editor; // As read from a settings file
            Assert.Equal(StartMenu.StartMode.Select, StartMenu.EffectiveMode);
            StartMenu.Only = StartMenu.OnlyMode.Notes;
            Assert.Equal(StartMenu.StartMode.Notes, StartMenu.EffectiveMode);

            // Set on the Configuration page: the start mode goes back to showing the choices
            StartMenu.Mode = StartMenu.StartMode.Editor;
            StartMenu.SetOnly(StartMenu.OnlyMode.Notes);
            Assert.Equal(StartMenu.StartMode.Select, StartMenu.Mode);
            StartMenu.Mode = StartMenu.StartMode.Editor;
            StartMenu.SetOnly(StartMenu.OnlyMode.Editor);
            Assert.Equal(StartMenu.StartMode.Editor, StartMenu.Mode);
            StartMenu.SetOnly(StartMenu.OnlyMode.None);
            Assert.Equal(StartMenu.StartMode.Editor, StartMenu.Mode);
        }
        finally
        {
            StartMenu.Mode = StartMenu.StartMode.Select;
            StartMenu.Only = StartMenu.OnlyMode.None;
        }
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
        // The logo presets are gone: the logo in use is a setting by its name
        Assert.Equal("Marbles", settings["logo"]);
        Assert.False(settings.ContainsKey("logotxt"));
        Assert.Equal("notes.mbls", settings["file"]);
        Assert.Equal(34, settings.Count);
        Assert.Equal("none", settings["onlymode"]);
        Assert.Equal("fast", settings["logoanim"]);
        Assert.Equal("true", settings["wrap"]);
        Assert.Equal("file", settings["snipsort"]);
        Assert.Equal("true", settings["scrolloff"]);
        Assert.Equal("12", settings["scrolllines"]);
        // The colors are kept in the color schemes: only the name of the one in use is a setting
        Assert.Equal("Default", settings["colorscheme"]);
        Assert.False(settings.ContainsKey("color1"));
        Assert.False(settings.ContainsKey("background"));
        Assert.Equal("false", settings["startfavorites"]);
        Assert.Equal("true", settings["osc52"]);
        Assert.Equal("false", settings["pastebin"]);
        Assert.Equal("all", settings["clipboardyank"]);
        Assert.Equal("false", settings["clipboardnotes"]);
        Assert.Equal("false", settings["pastebinappend"]);
        Assert.Equal("paste-bin.txt", settings["pastebinfile"]);
        Assert.Equal("en", settings["language"]);
        Assert.Equal("false", settings["linenumbers"]);
        Assert.Equal("true", settings["browsedual"]);
        Assert.Equal("1", settings["browsepane"]);
        Assert.Equal(string.Empty, settings["browsesel1"]);
        Assert.Equal(string.Empty, settings["browsesel2"]);
        Assert.Equal("true", settings["syntaxcolors"]);
        Assert.Equal("false", settings["monochrome"]);
        Assert.Equal("select", settings["startmode"]);

        Init.SetDefaultOutputDimensions();
        Assert.True(FileIo.LoadSettings());
        Assert.Equal(100, Init.LineWidth);
    }

    [Fact]
    public void TheNamesSelectedInTheFileBrowser_AreLoadedAndSaved()
    {
        File.WriteAllText(SettingsFile, "browsesel1=notes.md\nbrowsesel2=My folder\n");
        Assert.True(FileIo.LoadSettings());
        Assert.Equal("notes.md", FileExplorer.StartSelected);
        Assert.Equal("My folder", FileExplorer.StartSelected2);
        FileIo.SaveSettings();
        var settings = FileIo.ParseSettings(File.ReadAllText(SettingsFile));
        Assert.Equal("notes.md", settings["browsesel1"]);
        Assert.Equal("My folder", settings["browsesel2"]);

        // Missing: the first row
        File.WriteAllText(SettingsFile, "linecount=40\n");
        Assert.True(FileIo.LoadSettings());
        Assert.Equal(string.Empty, FileExplorer.StartSelected);
        Assert.Equal(string.Empty, FileExplorer.StartSelected2);
    }

    [Fact]
    public void SettingsCanBeInAnyOrder_AndMissingOnesGetDefaults()
    {
        File.WriteAllText(SettingsFile, "# comment\nlogoanim=false\n\nnot a setting\nLINECOUNT=40\nlinewidth=nonsense\n");
        Assert.True(FileIo.LoadSettings());
        Assert.False(Logo.AnimationEnabled);
        Assert.Equal(40, Init.LineCount);
        Assert.Equal(Init.MinLineWidth, Init.LineWidth);
        Assert.Equal("data.mbls", FileIo.GetFileName());
    }

    [Theory]
    [InlineData("colorscheme=Deep Blue", "Deep Blue")]
    [InlineData("colorscheme=ice warp", "Ice Warp")]
    [InlineData("colorscheme=Mine", "Mine")]
    [InlineData("colorscheme=Missing", "Default")]
    [InlineData("", "Default")]
    public void TheColorScheme_IsLoadedAndSavedByName(string setting, string expected)
    {
        ColorSchemes.DirectoryOverride = Path.Combine(_workDirectory, "data", ColorSchemes.DirectoryName);
        try
        {
            var mine = ColorScheme.Default;
            mine.Name = "Mine";
            mine.Text = ConsoleColor.White;
            Assert.Equal(DialogText.Success, ColorSchemes.Save(mine));
            File.WriteAllText(SettingsFile, setting + "\n");
            File.WriteAllText(Path.Combine(_workDirectory, "data", "notes.mbls"), "x");
            Assert.True(FileIo.LoadSettings());
            Assert.Equal(expected, ConsoleColors.Current.Name);
            if (expected == "Mine") Assert.Equal(ConsoleColor.White, ConsoleColors.Current.Text);
            FileIo.SaveSettings();
            Assert.Equal(expected, FileIo.ParseSettings(File.ReadAllText(SettingsFile))["colorscheme"]);
        }
        finally
        {
            ColorSchemes.DirectoryOverride = null;
            ConsoleColors.Use(ColorScheme.Default);
        }
    }

    [Fact]
    public void MissingSettingsFile_UsesDefaults()
    {
        Assert.True(FileIo.LoadSettings());
        Assert.True(Init.UsingDefaultSettings);
    }
}
