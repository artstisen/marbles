// Copied from Blyant Validator 0.6.2 (src/Blyant.Validator.Html/Parsing/HtmlTree.cs) by tools/formatters.py: not changed by hand in Marbles.
// Change it in Blyant Validator, and copy it again. English: its messages are not translated
using System.Collections.Frozen;

namespace Marbles.Format.Html.Parsing;

/// <summary>
/// A node of the lightweight tree made by <see cref="HtmlReader"/>, used by both the formatter and the
/// validator. It keeps the source text of every part.
/// </summary>
public abstract class HNode
{
    /// <summary>Offset of the first character in the source.</summary>
    public int Start { get; init; }
}

public sealed class HText(string raw) : HNode
{
    public string Raw { get; } = raw;
}

/// <summary>A comment, CDATA section, processing instruction or doctype, kept exactly as written.</summary>
public sealed class HVerbatim(string raw, bool isDoctype) : HNode
{
    public string Raw { get; } = raw;
    public bool IsDoctype { get; } = isDoctype;
}

/// <summary>An end tag with no open element to close. Kept as written.</summary>
public sealed class HStrayEndTag(string raw) : HNode
{
    public string Raw { get; } = raw;
}

/// <param name="Name">The name as written.</param>
/// <param name="RawValue">The value as written, with its quotes; null for an attribute without a value.</param>
/// <param name="Start">Offset of the name in the source.</param>
/// <param name="ValueStart">Offset of the value as written (at its quote, if it has one); -1 without a value.</param>
public sealed record HAttribute(string Name, string? RawValue, int Start, int ValueStart = -1)
{
    /// <summary>Offset of the first character of <see cref="Value"/> (after the quote); -1 without a value.</summary>
    public int ValueContentStart => ValueStart < 0 ? -1 : RawValue is ['"' or '\'', ..] ? ValueStart + 1 : ValueStart;

    /// <summary>The value without quotes ("" for an attribute without a value). Character references are not decoded.</summary>
    public string Value => RawValue switch
    {
        null => "",
        ['"', .. var inner, '"'] => inner,
        ['\'', .. var inner, '\''] => inner,
        _ => RawValue,
    };
}

public sealed class HElement(string name) : HNode
{
    /// <summary>The name as written.</summary>
    public string Name { get; } = name;

    public string Lower { get; } = name.ToLowerInvariant();

    public List<HAttribute> Attributes { get; } = [];
    public bool SelfClosing { get; set; }
    public bool SpaceBeforeSlash { get; set; }
    public List<HNode> Children { get; } = [];

    /// <summary>The end tag as written, or null when the source has none.</summary>
    public string? EndTag { get; set; }

    /// <summary>Offsets of the content between the start tag and the end tag.</summary>
    public int ContentStart { get; set; }
    public int ContentEnd { get; set; }

    /// <summary>For script, style, textarea, title and the like: the text content, exactly as written.</summary>
    public string? RawContent { get; set; }

    /// <summary>True inside &lt;svg&gt; or &lt;math&gt; (and for those elements themselves).</summary>
    public bool IsForeign { get; init; }

    public bool IsVoid => HtmlNames.Void.Contains(Lower);

    public HAttribute? GetAttribute(string name) =>
        Attributes.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public bool HasAttribute(string name) => GetAttribute(name) is not null;

    /// <summary>All elements below this one, in document order.</summary>
    public IEnumerable<HElement> Descendants()
    {
        foreach (var child in Children)
        {
            if (child is not HElement e) continue;
            yield return e;
            foreach (var d in e.Descendants()) yield return d;
        }
    }

    /// <summary>The text of the element and its descendants, as written (character references not decoded).</summary>
    public string TextContent =>
        RawContent ?? string.Concat(Children.Select(c => c switch
        {
            HText t => t.Raw,
            HElement e => e.TextContent,
            _ => "",
        }));
}

public static class HtmlNames
{
    private static readonly StringComparer Ci = StringComparer.OrdinalIgnoreCase;

    public static readonly FrozenSet<string> Void = new[]
    {
        "area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "source", "track", "wbr",
        "param", "keygen", "basefont", "bgsound", "frame",
    }.ToFrozenSet(Ci);

    /// <summary>Elements whose content is text up to the matching end tag.</summary>
    public static readonly FrozenSet<string> RawText = new[]
    {
        "script", "style", "xmp", "iframe", "noembed", "noframes", "textarea", "title",
    }.ToFrozenSet(Ci);

    /// <summary>Elements whose end tag may be left out (HTML standard, "Optional tags").</summary>
    public static readonly FrozenSet<string> OptionalEndTag = new[]
    {
        "html", "head", "body", "p", "li", "dt", "dd", "option", "optgroup", "tr", "td", "th", "thead",
        "tbody", "tfoot", "colgroup", "caption", "rb", "rt", "rtc", "rp",
    }.ToFrozenSet(Ci);

    /// <summary>Block-level elements: whitespace around them does not matter, so they get lines of their own.</summary>
    public static readonly FrozenSet<string> Block = new[]
    {
        "address", "article", "aside", "base", "blockquote", "body", "caption", "col", "colgroup", "dd",
        "details", "dialog", "div", "dl", "dt", "fieldset", "figcaption", "figure", "footer", "form", "h1",
        "h2", "h3", "h4", "h5", "h6", "head", "header", "hgroup", "hr", "html", "legend", "li", "link",
        "main", "menu", "meta", "nav", "noscript", "ol", "p", "pre", "script", "search", "section", "style",
        "summary", "table", "tbody", "td", "template", "tfoot", "th", "thead", "title", "tr", "ul",
        "listing", "xmp", "plaintext", "center", "dir", "frameset", "frame", "noframes", "optgroup", "option",
    }.ToFrozenSet(Ci);

    /// <summary>
    /// Inline elements in whose content whitespace does not matter (their children are laid out by
    /// the element, not as text). Their children get lines of their own; the whitespace around the
    /// element itself is kept as it is.
    /// </summary>
    public static readonly FrozenSet<string> Container = new[]
    {
        "svg", "math", "select", "datalist", "picture", "video", "audio", "object", "map",
    }.ToFrozenSet(Ci);

    /// <summary>
    /// An HTML integration point: an element of SVG or MathML whose content is HTML again (svg foreignObject, desc and
    /// title; MathML annotation-xml)
    /// </summary>
    public static bool IsIntegrationPoint(HElement e) =>
        e.IsForeign && e.Lower is "foreignobject" or "desc" or "title" or "annotation-xml";

    /// <summary>
    /// Elements that show nothing themselves (script, link …). They get lines of their own only where that adds no
    /// whitespace between text: in the middle of a sentence they stay in the line
    /// </summary>
    public static readonly FrozenSet<string> Hidden = new[]
    {
        "script", "noscript", "template", "link", "meta", "style", "base",
    }.ToFrozenSet(Ci);

    /// <summary>Elements inside SVG or MathML whose content is text.</summary>
    public static readonly FrozenSet<string> ForeignText = new[]
    {
        "text", "tspan", "textpath", "title", "desc", "mi", "mn", "mo", "ms", "mtext", "annotation",
        "style", "script",
    }.ToFrozenSet(Ci);

    /// <summary>Elements whose content is kept exactly as written.</summary>
    public static readonly FrozenSet<string> Preformatted = new[]
    {
        "pre", "textarea", "listing", "xmp", "plaintext", "script", "iframe", "noembed", "noframes",
    }.ToFrozenSet(Ci);
}
