namespace Marbles.Tests;

[Collection("Static state")]
public class FileSearchTests : IDisposable
{
    private readonly string _root;

    public FileSearchTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "marbles_search_" + Guid.NewGuid().ToString("N"));
        Make("notes.md", "# Title\nSome text about cats\nMore");
        Make("Plan.txt", "The plan\nline two has Dogs in it");
        Make("data.mbls", "cats");
        Make(Path.Combine("sub", "deep.md"), "nothing here\n\tindented cats line");
        Make(Path.Combine("sub", "inner", "notes(1).txt"), "x");
        File.WriteAllBytes(Path.Combine(_root, "binary.bin"), new byte[] { 0, 1, 2, 0xFF, 0xFE, 0, 99, 97, 116, 115 });
        // A binary file that starts as UTF-16 does: read as text it has null characters (and "cats")
        File.WriteAllBytes(Path.Combine(_root, "image.dat"),
            new byte[] { 0xFF, 0xFE, 99, 0, 97, 0, 116, 0, 115, 0, 0, 0, 1, 0 });
        Directory.CreateDirectory(Path.Combine(_root, "emptydir"));
    }

    public void Dispose()
    {
        Directory.Delete(_root, true);
    }

    private void Make(string name, string text)
    {
        var path = Path.Combine(_root, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private List<string> Find(string name, string text = "", bool regex = false, bool matchCase = false,
        bool subfolders = true)
    {
        var options = new FileSearch.Options
        {
            Name = name, Text = text, IsRegex = regex, MatchCase = matchCase, Subfolders = subfolders
        };
        Assert.Equal(DialogText.Success, FileSearch.Validate(options));
        return FileSearch.Search(_root, options).Results
            .Select(result => Path.GetRelativePath(_root, result.Path).Replace('\\', '/')).ToList();
    }

    [Fact]
    public void Name_ContainsTheText()
    {
        Assert.Equal(new[] { "notes.md", "sub/inner/notes(1).txt" }, Find("notes"));
        Assert.Equal(new[] { "notes.md", "sub/inner/notes(1).txt" }, Find("NOTES"));
        Assert.Empty(Find("NOTES", matchCase: true));
        // Characters that mean something in regular expressions are just characters
        Assert.Equal(new[] { "sub/inner/notes(1).txt" }, Find("(1)"));
        Assert.Equal(new[] { "notes.md", "sub/deep.md" }, Find(".md"));
        // Directories are found by name too
        Assert.Equal(new[] { "emptydir" }, Find("empty"));
    }

    [Fact]
    public void Name_WithWildcards_MustMatchTheWholeName()
    {
        Assert.Equal(new[] { "notes.md", "sub/deep.md" }, Find("*.md"));
        Assert.Equal(new[] { "Plan.txt", "sub/inner/notes(1).txt" }, Find("*.TXT"));
        Assert.Equal(new[] { "notes.md" }, Find("note?.md"));
        Assert.Empty(Find("note?.m"));
    }

    [Fact]
    public void Name_AsRegularExpression()
    {
        Assert.Equal(new[] { "notes.md", "Plan.txt" }, Find(@"^(notes|plan)\.", regex: true));
        Assert.Equal(new[] { "notes.md" }, Find(@"^(notes|plan)\.", regex: true, matchCase: true));
        Assert.Equal(new[] { "sub/inner/notes(1).txt" }, Find(@"\(\d\)", regex: true));
    }

    [Fact]
    public void Subfolders_AreOptional()
    {
        Assert.Equal(new[] { "notes.md" }, Find("*.md", subfolders: false));
        Assert.Equal(new[] { "inner", "sub/inner" }.Skip(1), Find("inner"));
    }

    [Fact]
    public void Text_IsFoundInTextFiles_WithTheLine()
    {
        // Not in the data file, and not in the file that is not text
        Assert.Equal(new[] { "notes.md", "sub/deep.md" }, Find("", "cats"));
        Assert.Equal(new[] { "Plan.txt" }, Find("", "dogs"));
        Assert.Empty(Find("", "dogs", matchCase: true));
        // With a name: both must match
        Assert.Equal(new[] { "sub/deep.md" }, Find("deep", "cats"));
        Assert.Empty(Find("*.txt", "cats"));

        var options = new FileSearch.Options { Text = "cats" };
        var results = FileSearch.Search(_root, options).Results;
        Assert.Equal(1, results[0].Line);
        Assert.Equal("Some text about cats", results[0].LineText);
        Assert.Equal(1, results[1].Line);
        Assert.Equal("indented cats line", results[1].LineText);
    }

    [Fact]
    public void Text_AsRegularExpression()
    {
        Assert.Equal(new[] { "notes.md", "Plan.txt" }, Find("", @"^(# Title|The plan)$", regex: true));
        Assert.Equal(new[] { "Plan.txt" }, Find("", @"has \w+ in", regex: true));
    }

    [Fact]
    public void WhatIsSearchedFor_IsChecked()
    {
        Assert.Equal(DialogText.SearchNothing, FileSearch.Validate(new FileSearch.Options { Name = "  " }));
        Assert.StartsWith(DialogText.SearchInvalidPattern,
            FileSearch.Validate(new FileSearch.Options { Name = "a(b", IsRegex = true }));
        Assert.StartsWith(DialogText.SearchInvalidPattern,
            FileSearch.Validate(new FileSearch.Options { Text = "[", IsRegex = true }));
        // The same is fine as plain text
        Assert.Equal(DialogText.Success, FileSearch.Validate(new FileSearch.Options { Name = "a(b" }));
    }

    [Fact]
    public void TheSearch_CanBeStopped_AndTellsWhereItIs()
    {
        var directories = new List<string>();
        var outcome = FileSearch.Search(_root, new FileSearch.Options { Name = "*" }, (directory, _) =>
        {
            directories.Add(Path.GetRelativePath(_root, directory).Replace('\\', '/'));
            return true;
        });
        Assert.False(outcome.Stopped);
        Assert.Equal(new[] { ".", "emptydir", "sub", "sub/inner" }, directories);

        outcome = FileSearch.Search(_root, new FileSearch.Options { Name = "*" },
            (directory, _) => directory == _root);
        Assert.True(outcome.Stopped);
        // What was found before it was stopped is kept
        Assert.Contains(outcome.Results, result => result.Path.EndsWith("notes.md"));
        Assert.DoesNotContain(outcome.Results, result => result.Path.EndsWith("deep.md"));
    }

    [Fact]
    public void MissingDirectory_IsNotAnError()
    {
        var outcome = FileSearch.Search(Path.Combine(_root, "gone"), new FileSearch.Options { Name = "x" });
        Assert.Empty(outcome.Results);
        Assert.Equal(1, outcome.Unreadable);
    }
}
