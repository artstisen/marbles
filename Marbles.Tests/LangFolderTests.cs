namespace Marbles.Tests;

/// <summary>
/// Language files in a folder "lang" (next to the program, or in the data directory): they are found by
/// themselves, named by their first line, replace a built-in language, and the data directory is used first
/// </summary>
[Collection("Static state")]
public class LangFolderTests : IDisposable
{
    private readonly string _data;
    private readonly string _program;

    public LangFolderTests()
    {
        var root = Path.Combine(Path.GetTempPath(), "marbles-lang-" + Guid.NewGuid().ToString("N"));
        _data = Path.Combine(root, "data", Lang.FolderName);
        _program = Path.Combine(root, "program", Lang.FolderName);
        Directory.CreateDirectory(_data);
        Directory.CreateDirectory(_program);
        Lang.FoldersOverride = new[] { _data, _program };
        Lang.Refresh();
    }

    public void Dispose()
    {
        Lang.FoldersOverride = null;
        Lang.Refresh();
        Lang.Set(Lang.English);
        Lang.Missing = null;
        var root = Path.GetDirectoryName(Path.GetDirectoryName(_data))!;
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    private static string File(string code, string name, params (string English, string Text)[] texts)
    {
        var lines = new List<string> { "# Marbles: " + name + " (" + code + ")" };
        foreach (var (english, text) in texts)
        {
            lines.Add("en: " + Lang.Quote(english));
            lines.Add(code + ": " + Lang.Quote(text));
        }

        return string.Join("\n", lines) + "\n";
    }

    [Fact]
    public void AFileInTheFolder_IsALanguage_NamedByItsFirstLine()
    {
        System.IO.File.WriteAllText(Path.Combine(_program, "pl.txt"),
            File("pl", "Polski", ("File deleted", "Plik usunięty"), ("Not translated", "")));
        Lang.Refresh();
        var polish = Assert.Single(Lang.Available, language => language.Code == "pl");
        Assert.Equal("Polski", polish.Name);
        Assert.Equal(Lang.Source.ProgramFolder, polish.From);
        Assert.False(polish.Replaced);
        Assert.InRange(polish.Percent ?? -1, 0, 5);
        // English and the built-in languages first
        Assert.Equal(new[] { "en", "da", "de" }, Lang.Available.Take(3).Select(language => language.Code));

        Assert.True(Lang.Set("pl"));
        Assert.Equal("Plik usunięty", Lang.T("File deleted"));
        Assert.Equal("Polski", Lang.CurrentName);
        // Not translated: English
        Assert.Equal("Not translated", Lang.T("Not translated"));
    }

    [Fact]
    public void TheDataDirectory_IsUsedFirst_AndAFileReplacesABuiltInLanguage()
    {
        System.IO.File.WriteAllText(Path.Combine(_program, "pl.txt"), File("pl", "Polski", ("File deleted", "Program")));
        System.IO.File.WriteAllText(Path.Combine(_data, "pl.txt"), File("pl", "Polski", ("File deleted", "Data")));
        System.IO.File.WriteAllText(Path.Combine(_data, "da.txt"), File("da", "Dansk", ("File deleted", "Min fil er slettet")));
        Lang.Refresh();
        Assert.Equal(Lang.Source.DataFolder, Lang.Available.Single(language => language.Code == "pl").From);
        Assert.True(Lang.Set("pl"));
        Assert.Equal("Data", Lang.T("File deleted"));

        var danish = Lang.Available.Single(language => language.Code == "da");
        Assert.True(danish.Replaced);
        Assert.Equal(Lang.Source.DataFolder, danish.From);
        Assert.True(Lang.Set("da"));
        Assert.Equal("Min fil er slettet", Lang.T("File deleted"));
        // The built-in file is still there to compare with
        Assert.NotNull(Lang.ReadBuiltIn("da"));
    }

    [Fact]
    public void FilesThatAreNotLanguages_AreLeftOut()
    {
        System.IO.File.WriteAllText(Path.Combine(_program, "readme.txt"), File("readme", "Readme", ("Save", "x")));
        System.IO.File.WriteAllText(Path.Combine(_program, "fi.txt"), File("fi", "Suomi", ("Save", "")));
        System.IO.File.WriteAllText(Path.Combine(_program, "pt-br.txt"), File("pt-br", "Português (Brasil)", ("Save", "Salvar")));
        System.IO.File.WriteAllText(Path.Combine(_program, "nl.txt"), "en: \"Save\"\nnl: \"Opslaan\"\n");
        Lang.Refresh();
        var codes = Lang.Available.Select(language => language.Code).ToList();
        Assert.DoesNotContain("readme", codes);
        // Without translations
        Assert.DoesNotContain("fi", codes);
        Assert.Contains("pt-br", codes);
        // Without a first line naming it: its code
        Assert.Equal("nl", Lang.Available.Single(language => language.Code == "nl").Name);
    }

    [Fact]
    public void AMissingLanguage_GivesEnglish_AndIsKeptInTheSettings()
    {
        Assert.False(Lang.Set("pl"));
        Assert.Equal(Lang.English, Lang.Current);
        Lang.Missing = "pl";
        // When the file is there and the language is used, it is no longer missing
        System.IO.File.WriteAllText(Path.Combine(_program, "pl.txt"), File("pl", "Polski", ("Save", "Zapisz")));
        Lang.Refresh();
        Assert.True(Lang.Set("pl"));
        Assert.Null(Lang.Missing);
    }
}
