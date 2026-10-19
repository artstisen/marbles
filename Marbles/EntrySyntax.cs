namespace Marbles;

/// <summary>
/// The syntax highlighting of an entry (Markdown, HTML ...), kept as a tag (one letter) in the options of the entry,
/// as the favorite, pinned and color tags: "m" Markdown, "w" HTML, "c" CSS, "k" JavaScript, "n" JSON, "x" XML,
/// "d" Diff, "s" C#, "q" PHP.
/// The letters are not used by the other tags, so earlier versions of Marbles leave them alone.
/// </summary>
internal static class EntrySyntax
{
    private static readonly (char Tag, SyntaxType Type, string Short)[] Kinds =
    {
        ('m', SyntaxType.Markdown, "MD"),
        ('w', SyntaxType.Html, "HTML"),
        ('c', SyntaxType.Css, "CSS"),
        ('k', SyntaxType.JavaScript, "JS"),
        ('n', SyntaxType.Json, "JSON"),
        ('x', SyntaxType.Xml, "XML"),
        ('d', SyntaxType.Diff, "DIFF"),
        ('s', SyntaxType.CSharp, "C#"),
        ('q', SyntaxType.Php, "PHP")
    };

    /// <summary>
    /// The syntax set in the options of an entry (SyntaxType.None if none is set)
    /// </summary>
    internal static SyntaxType Get(string? options)
    {
        if (string.IsNullOrEmpty(options)) return SyntaxType.None;
        foreach (var kind in Kinds)
        {
            if (options.Contains(kind.Tag)) return kind.Type;
        }

        return SyntaxType.None;
    }

    /// <summary>
    /// Set the syntax in the options of an entry
    /// </summary>
    /// <param name="options">The options</param>
    /// <param name="type">The syntax, or SyntaxType.None to remove it</param>
    /// <returns>The new options</returns>
    internal static string Set(string? options, SyntaxType type)
    {
        var result = new string((options ?? string.Empty).Where(c => Kinds.All(kind => kind.Tag != c)).ToArray());
        foreach (var kind in Kinds)
        {
            if (kind.Type == type) return result + kind.Tag;
        }

        return result;
    }

    /// <summary>
    /// The short name of a syntax for the lists of entries ("MD", "HTML" ...), empty for none
    /// </summary>
    internal static string ShortName(SyntaxType type)
    {
        foreach (var kind in Kinds)
        {
            if (kind.Type == type) return kind.Short;
        }

        return string.Empty;
    }
}
