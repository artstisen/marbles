﻿namespace Marbles.Tests;

/// <summary>
/// ":surround /bold": a completion with "$0" put around the selection, the lines or a block (and Tab after
/// ":surround ", also after a range)
/// </summary>
[Collection("Static state")]
public class SurroundTests : IDisposable
{
    private const string Lists = "=== Markdown (md)\n" +
                                 "/bold = **$0**\n" +
                                 "/link = [$0]()\n" +
                                 "/code = ```\\n$0\\n```\n" +
                                 "/h2 = ## $0\n" +
                                 "/hr = ---\n" +
                                 "/x = md $0\n" +
                                 "=== All text\n" +
                                 "/todo = TODO: $0\n" +
                                 "/x = all $0\n";

    public SurroundTests()
    {
        Completions.Parse(Lists);
    }

    public void Dispose()
    {
        Completions.Parse(string.Empty);
        CommandCompletion.Arguments.Remove("surround");
    }

    private static EditorEngine Editor(string text) =>
        new(text, 60, 10, false) { CompletionSyntax = SyntaxType.Markdown };

    [Theory]
    // Text selected with v: as it is (also over two lines)
    [InlineData("one two three", "wviw:surround /bold<CR>", "one **two** three")]
    [InlineData("see Marbles here", "wvee:sur /link<CR>", "see [Marbles here]()")]
    [InlineData("one\ntwo", "vj:surround /bold<CR>", "**one\nt**wo")]
    // Lines selected with V: each by itself (empty ones left), or all at once with a completion of several lines
    [InlineData("a\n\nb", "Vjj:surround /h2<CR>", "## a\n\n## b")]
    [InlineData("  a\n  b", "Vj:surround /bold<CR>", "  **a**\n  **b**")]
    [InlineData("x\ny", "Vj:surround /code<CR>", "```\nx\ny\n```")]
    [InlineData("  x\n    y", "Vj:surround /code<CR>", "  ```\n  x\n    y\n  ```")]
    // A block: the part of each line
    [InlineData("ab cd\nef gh", "Qjl:surround /bold<CR>", "**ab** cd\n**ef** gh")]
    // Without a selection: the line, or the lines of a range; the completions for all text work too
    [InlineData("hello", ":surround /bold<CR>", "**hello**")]
    [InlineData("a\nb\nc", ":1,2surround /todo<CR>", "TODO: a\nTODO: b\nc")]
    // The one for the kind of text before the one for all text
    [InlineData("word", ":surround /x<CR>", "md word")]
    public void TheSelection_IsSurrounded(string text, string keys, string expected)
    {
        var editor = Editor(text);
        editor.Type(keys);
        Assert.Equal(expected, editor.Buffer.GetText());
        Assert.False(editor.MessageIsError);
        // One change
        editor.Type("u");
        Assert.Equal(text, editor.Buffer.GetText());
    }

    [Theory]
    // A selection onto an empty line: the line break stays outside the marks
    [InlineData("abc\n\nx", "vj:surround /bold<CR>", "**abc**\n\nx")]
    public void LineBreaksAtTheEndsOfTheSelection_StayOutside(string text, string keys, string expected)
    {
        var editor = Editor(text);
        editor.Type(keys);
        Assert.Equal(expected, editor.Buffer.GetText());
    }

    [Theory]
    [InlineData("a\n\nb", "jv:surround /bold<CR>")]
    [InlineData("a\n\n\nb", "jVj:surround /h2<CR>")]
    public void WithNothingToSurround_ItIsTold(string text, string keys)
    {
        var editor = Editor(text);
        editor.Type(keys);
        Assert.Equal(text, editor.Buffer.GetText());
        Assert.True(editor.MessageIsError);
        Assert.Equal(Lang.T("Nothing to surround"), editor.Message);
    }

    [Theory]
    [InlineData(":surround<CR>")]
    [InlineData(":surround /nothing<CR>")]
    [InlineData(":surround /hr<CR>")]
    [InlineData("Qj:surround /code<CR>")]
    public void WithoutAFittingCompletion_NothingIsChanged_AndItIsTold(string keys)
    {
        var editor = Editor("ab\ncd");
        editor.Type(keys);
        Assert.Equal("ab\ncd", editor.Buffer.GetText());
        Assert.True(editor.MessageIsError);
    }

    [Fact]
    public void AReadOnlyText_IsNotSurrounded()
    {
        var editor = new EditorEngine("hello", 60, 10, false) { CompletionSyntax = SyntaxType.Markdown, ReadOnly = true };
        editor.Type("viw:surround /bold<CR>");
        Assert.Equal("hello", editor.Buffer.GetText());
        Assert.Equal(DialogText.EditorReadOnly, editor.Message);
    }

    [Fact]
    public void Tab_GoesThroughTheCompletionsThatCanSurround_AlsoAfterARange()
    {
        Assert.Equal(new[] { "/bold", "/link", "/code", "/h2", "/x", "/todo" },
            Completions.SurroundWords(SyntaxType.Markdown));
        // Not in another kind of text (only those for all text)
        Assert.Equal(new[] { "/todo", "/x" }, Completions.SurroundWords(SyntaxType.Html));
        CommandCompletion.Arguments["surround"] = () => Completions.SurroundWords(SyntaxType.Markdown);
        var completion = new CommandCompletion(CommandContext.Editor);
        Assert.Equal("'<,'>surround /bold", completion.Next("'<,'>surround /b", true));
        Assert.Equal("surround /h2", new CommandCompletion(CommandContext.Editor).Next("surround /h", true));
    }

    [Fact]
    public void InANoteWithoutColors_TheCompletionsForAllTextSurround_AndWithMarkdownColorsThoseOfMarkdown()
    {
        var note = new EditorEngine("word", 60, 10, false);
        note.Type(":surround /todo<CR>");
        Assert.Equal("TODO: word", note.Buffer.GetText());
        note.Type("u:surround /bold<CR>");
        Assert.True(note.MessageIsError);
        note.Type(":syntax md<CR>:surround /bold<CR>");
        Assert.Equal("**word**", note.Buffer.GetText());
    }
}
