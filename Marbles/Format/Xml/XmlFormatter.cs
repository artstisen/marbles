// Copied from Blyant Validator 0.6.2 (src/Blyant.Validator.Xml/XmlFormatter.cs) by tools/formatters.py: not changed by hand in Marbles.
// Change it in Blyant Validator, and copy it again. English: its messages are not translated
using System.Text;
using Marbles.Format.Core;

namespace Marbles.Format.Xml;

/// <summary>
/// XML formatter. Elements whose content is only elements (and comments and processing instructions) get their
/// children on lines of their own, indented: only whitespace between elements changes. Kept exactly as written: an
/// element with text in it (a "loc", a "title", mixed content) or with only whitespace in it, everything under
/// xml:space="preserve", SVG text ("text", "tspan", "textPath"), and XHTML (an element that declares the XHTML
/// namespace, as Atom's xhtml content), where whitespace between elements shows. Tags are kept as written, apart from
/// runs of whitespace between attributes made one space. Comments, CDATA sections, the XML declaration and the
/// doctype are kept. A file that is not well-formed, or nested deeper than <see cref="MaxDepth"/>, is not formatted.
/// </summary>
public sealed class XmlFormatter : IFormatter
{
    public bool CanFormat(DocumentKind kind) => kind == DocumentKind.Xml;

    /// <summary>How deep elements may be nested to be formatted (the writing recurses).</summary>
    public const int MaxDepth = 512;

    public string Format(SourceDocument document, FormatOptions options)
    {
        var errors = XmlValidator.Validate(document.Path, document.Content)
            .FirstOrDefault(d => d.Code == "xml.not-well-formed");
        if (errors != null)
            throw new Core.FormatException($"{errors.Message} (line {errors.Start.Line})");

        var newline = options.NewLine ?? document.DetectNewLine();
        var root = XmlSourceReader.Read(document.Content, out var depth);
        if (depth > MaxDepth)
            throw new Core.FormatException($"Elements are nested more than {MaxDepth} deep.");
        var lines = new List<string>();
        WriteChildren(document.Content, root.Children, 0, options.IndentUnit, lines, preserveBlankLines: true);
        return lines.Count == 0 ? string.Empty : string.Join(newline, lines.Select(l => Newlines(l, newline))) + newline;
    }

    private static string Newlines(string text, string newline) =>
        text.Contains('\n') || text.Contains('\r') ? text.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", newline) : text;

    private static void WriteChildren(string source, List<SourceNode> children, int depth, string indentUnit, List<string> lines,
        bool preserveBlankLines)
    {
        var indent = string.Concat(Enumerable.Repeat(indentUnit, depth));
        var blank = false;
        var first = true;
        foreach (var child in children)
        {
            if (child is SourceText { IsWhitespace: true } space)
            {
                // One empty line in the source between two children is kept
                if (preserveBlankLines && !first && CountNewlines(space.Raw) >= 2) blank = true;
                continue;
            }

            if (blank) lines.Add(string.Empty);
            blank = false;
            first = false;

            switch (child)
            {
                case SourceElement element:
                    WriteElement(source, element, depth, indentUnit, lines);
                    break;
                default:
                    lines.Add(indent + child.Raw);
                    break;
            }
        }
    }

    private static void WriteElement(string source, SourceElement element, int depth, string indentUnit, List<string> lines)
    {
        var indent = string.Concat(Enumerable.Repeat(indentUnit, depth));
        var start = NormalizeTag(element.StartTag);
        if (element.SelfClosing)
        {
            lines.Add(indent + start);
            return;
        }

        var elementOnly = !element.KeptAsWritten &&
                          element.Children.Any(c => c is SourceElement) &&
                          element.Children.All(c => c is SourceElement or SourceComment or SourceInstruction || c is SourceText { IsWhitespace: true });
        if (!elementOnly)
        {
            // Text in it (or only whitespace, or text where whitespace between elements shows): kept as written
            lines.Add(indent + start + source[element.ContentStart..element.ContentEnd] + element.EndTag);
            return;
        }

        lines.Add(indent + start);
        WriteChildren(source, element.Children, depth + 1, indentUnit, lines, preserveBlankLines: true);
        lines.Add(indent + element.EndTag);
    }

    // A tag with runs of whitespace outside the attribute values made one space (and none before ">" or "/>" but the
    // one kept before "/>")
    private static string NormalizeTag(string tag)
    {
        var result = new StringBuilder(tag.Length);
        char? quote = null;
        var space = false;
        foreach (var c in tag)
        {
            if (quote != null)
            {
                result.Append(c);
                if (c == quote) quote = null;
                continue;
            }

            if (c is ' ' or '\t' or '\n' or '\r')
            {
                space = true;
                continue;
            }

            // (no space is kept before ">"; one before "/>" is kept)
            if (space && c != '>') result.Append(' ');
            space = false;
            if (c is '"' or '\'') quote = c;
            result.Append(c);
        }

        return result.ToString();
    }

    private static int CountNewlines(string text) => text.Replace("\r\n", "\n").Count(c => c is '\n' or '\r');
}

// A lightweight tree of the XML source, which keeps the source text of every part (as the HTML formatter's)
internal abstract class SourceNode
{
    public string Raw { get; init; } = string.Empty;
}

internal sealed class SourceText : SourceNode
{
    public bool IsWhitespace => Raw.All(c => c is ' ' or '\t' or '\n' or '\r');
}

internal sealed class SourceComment : SourceNode;

/// <summary>A processing instruction or the XML declaration.</summary>
internal sealed class SourceInstruction : SourceNode;

/// <summary>A CDATA section or a doctype.</summary>
internal sealed class SourceVerbatim : SourceNode;

internal sealed class SourceElement : SourceNode
{
    public string StartTag { get; init; } = string.Empty;
    public string EndTag { get; set; } = string.Empty;
    public bool SelfClosing { get; init; }
    /// <summary>The content is kept exactly as written: xml:space="preserve", SVG text, XHTML (and inside them).</summary>
    public bool KeptAsWritten { get; set; }
    public List<SourceNode> Children { get; } = [];
    public int ContentStart { get; init; }
    public int ContentEnd { get; set; }
}

/// <summary>Reads well-formed XML into an <see cref="SourceElement"/> tree with the source text of every part.</summary>
internal static class XmlSourceReader
{
    /// <param name="depth">How deep elements are nested at most.</param>
    public static SourceElement Read(string s, out int depth)
    {
        depth = 0;
        var root = new SourceElement();
        var stack = new Stack<SourceElement>();
        stack.Push(root);
        var i = 0;
        while (i < s.Length)
        {
            var current = stack.Peek();
            if (s[i] != '<')
            {
                var end = s.IndexOf('<', i);
                if (end < 0) end = s.Length;
                current.Children.Add(new SourceText { Raw = s[i..end] });
                i = end;
                continue;
            }

            int close;
            if (At(s, i, "<!--"))
            {
                close = s.IndexOf("-->", i + 4, StringComparison.Ordinal) + 3;
                current.Children.Add(new SourceComment { Raw = s[i..close] });
            }
            else if (At(s, i, "<![CDATA["))
            {
                close = s.IndexOf("]]>", i, StringComparison.Ordinal) + 3;
                current.Children.Add(new SourceVerbatim { Raw = s[i..close] });
            }
            else if (At(s, i, "<?"))
            {
                close = s.IndexOf("?>", i, StringComparison.Ordinal) + 2;
                current.Children.Add(new SourceInstruction { Raw = s[i..close] });
            }
            else if (At(s, i, "<!"))
            {
                // A doctype, with an internal subset in [ ] if it has one
                var bracket = s.IndexOf('[', i);
                var gt = s.IndexOf('>', i);
                close = (bracket >= 0 && bracket < gt ? s.IndexOf('>', s.IndexOf(']', bracket)) : gt) + 1;
                current.Children.Add(new SourceVerbatim { Raw = s[i..close] });
            }
            else if (At(s, i, "</"))
            {
                close = s.IndexOf('>', i) + 1;
                current.EndTag = s[i..close];
                current.ContentEnd = i;
                stack.Pop();
            }
            else
            {
                close = TagEnd(s, i) + 1;
                var tag = s[i..close];
                var selfClosing = tag.EndsWith("/>", StringComparison.Ordinal);
                var element = new SourceElement { StartTag = tag, SelfClosing = selfClosing, ContentStart = close };
                // (xml:space="default" ends a preserve of an element around it)
                var spaceDefault = tag.Contains("xml:space", StringComparison.Ordinal) && !tag.Contains("preserve", StringComparison.Ordinal);
                element.KeptAsWritten = IsKeptAsWritten(tag) || current.KeptAsWritten && !spaceDefault;
                current.Children.Add(element);
                if (!selfClosing) stack.Push(element);
                depth = Math.Max(depth, stack.Count - 1);
            }

            i = close;
        }

        return root;
    }

    private static readonly string[] SvgText = ["text", "tspan", "textPath"];

    // An element whose content is kept exactly as written: xml:space="preserve", SVG text, or one that declares the
    // XHTML namespace (whitespace between elements is shown there)
    private static bool IsKeptAsWritten(string tag)
    {
        if (tag.Contains("xml:space", StringComparison.Ordinal) && tag.Contains("preserve", StringComparison.Ordinal)) return true;
        if (tag.Contains("http://www.w3.org/1999/xhtml", StringComparison.Ordinal)) return true;
        var end = 1;
        while (end < tag.Length && tag[end] is not (' ' or '\t' or '\n' or '\r' or '/' or '>')) end++;
        var name = tag[1..end];
        var colon = name.IndexOf(':');
        return SvgText.Contains(colon < 0 ? name : name[(colon + 1)..], StringComparer.Ordinal);
    }

    private static bool At(string s, int i, string text) => string.CompareOrdinal(s, i, text, 0, text.Length) == 0;

    // The ">" that ends a tag (not one inside a quoted attribute value)
    private static int TagEnd(string s, int start)
    {
        char? quote = null;
        for (var i = start + 1; i < s.Length; i++)
        {
            var c = s[i];
            if (quote != null)
            {
                if (c == quote) quote = null;
            }
            else if (c is '"' or '\'')
            {
                quote = c;
            }
            else if (c == '>')
            {
                return i;
            }
        }

        return s.Length - 1;
    }
}
