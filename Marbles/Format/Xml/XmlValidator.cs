// Copied from Blyant Validator 0.6.2 (src/Blyant.Validator.Xml/XmlValidator.cs) by tools/formatters.py: not changed by hand in Marbles.
// Change it in Blyant Validator, and copy it again. English: its messages are not translated
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Marbles.Format.Core;

namespace Marbles.Format.Xml;

/// <summary>
/// Native XML validator: well-formedness (XML 1.0), with .NET's own XML reader, and the rules of the sitemap protocol
/// (https://www.sitemaps.org/protocol.html) for sitemaps. The internal DTD subset of a file is read, so the entities
/// it declares are known (Adobe Illustrator's SVG uses them), but nothing outside the file is ever loaded: there is
/// no resolver, so a file cannot make the validator read other files or the network (XML external entities), and
/// entities may expand to at most <see cref="MaxEntityCharacters"/> characters (no "billion laughs").
/// </summary>
public sealed partial class XmlValidator : IValidator
{
    public string Engine => "native";

    public bool CanValidate(DocumentKind kind) => kind == DocumentKind.Xml;

    /// <summary>How many characters the entities of a file may expand to, in all.</summary>
    public const long MaxEntityCharacters = 10_000_000;

    public Task<IReadOnlyList<Diagnostic>> ValidateAsync(SourceDocument document, CancellationToken cancellationToken = default) =>
        Task.FromResult(Validate(document.Path, document.Content));

    /// <summary>Validates XML given as text.</summary>
    public static IReadOnlyList<Diagnostic> Validate(string path, string xml)
    {
        var diagnostics = new List<Diagnostic>();
        var document = Parse(path, xml, diagnostics);
        // An entity in another file is never read (there is no resolver): its content is not checked
        foreach (Match external in ExternalEntity().Matches(xml))
        {
            var line = 1 + xml.AsSpan(0, external.Index).Count('\n');
            diagnostics.Add(new Diagnostic(path, Severity.Warning, "xml.external-entity",
                $"The entity “{external.Groups[1].Value}” is in another file, which is never read: its content is not checked.",
                new SourcePosition(line, 1), new SourcePosition(line, 1)));
        }

        if (document?.Root is { } root)
        {
            if (Sitemap.IsSitemap(root)) Sitemap.Check(path, xml, root, diagnostics);
            else if (Feeds.IsRss(root)) Feeds.CheckRss(path, root, diagnostics);
            else if (Feeds.IsAtom(root)) Feeds.CheckAtom(path, root, diagnostics);
        }
        return [.. diagnostics.OrderBy(d => d.Start.Line).ThenBy(d => d.Start.Column)];
    }

    /// <summary>
    /// Reads the XML, keeping the line and column of every element. A file that is not well-formed gives one error, at
    /// the first place that is wrong (XML stops there, as browsers do), and null
    /// </summary>
    private static XDocument? Parse(string path, string xml, List<Diagnostic> diagnostics)
    {
        var settings = new XmlReaderSettings
        {
            // The internal subset only: without a resolver, an external DTD or entity is never loaded
            DtdProcessing = DtdProcessing.Parse,
            XmlResolver = null,
            MaxCharactersFromEntities = MaxEntityCharacters,
            IgnoreWhitespace = false,
        };

        try
        {
            using var reader = XmlReader.Create(new StringReader(xml), settings);
            return XDocument.Load(reader, LoadOptions.SetLineInfo);
        }
        catch (XmlException e)
        {
            var at = new SourcePosition(Math.Max(1, e.LineNumber), Math.Max(1, e.LinePosition));
            // With an external DTD (not read), an entity may be declared there: an undeclared one is then not a
            // well-formedness error (XML 1.0, 4.1), but it cannot be checked either
            if (UndeclaredEntity().IsMatch(e.Message) && ExternalDtd().IsMatch(xml))
            {
                var name = UndeclaredEntity().Match(e.Message).Groups[1].Value;
                diagnostics.Add(new Diagnostic(path, Severity.Warning, "xml.undeclared-entity",
                    $"Entity “&{name};” is not declared in the file: it may be declared in the external DTD, which is not " +
                    "read, so the rest of the file is not checked. A number (as &#160; for a no-break space) or the " +
                    "character itself works without a DTD.", at, at));
                return null;
            }

            diagnostics.Add(new Diagnostic(path, Severity.Error, "xml.not-well-formed", Explain(e.Message), at, at));
            return null;
        }
    }

    // .NET's message without its own " Line 3, position 7." (the place is told by the diagnostic), and the most
    // common mistakes told in plain words
    private static string Explain(string message)
    {
        message = LinePosition().Replace(message, string.Empty).Trim();
        if (message.Contains("MaxCharactersFromEntities", StringComparison.Ordinal))
            return $"The entities of the file expand to more than {MaxEntityCharacters:N0} characters: it is not read further.";
        if (message.Contains("parsing EntityName", StringComparison.Ordinal))
            return "“&” does not start a reference here: write “&amp;” for an ampersand.";
        if (UndeclaredEntity().Match(message) is { Success: true } entity)
            return $"Reference to undeclared entity “&{entity.Groups[1].Value};”. XML only knows &amp; &lt; &gt; &quot; and &apos;: " +
                   "use a number instead (as &#160; for a no-break space), or the character itself.";
        return message;
    }

    [GeneratedRegex(@"undeclared entity '([^']+)'")]
    private static partial Regex UndeclaredEntity();

    // The declaration of an external entity: <!ENTITY name SYSTEM "…"> (not a parameter entity, "%")
    [GeneratedRegex(@"<!ENTITY\s+([^\s%>]+)\s+(?:SYSTEM|PUBLIC)\s")]
    private static partial Regex ExternalEntity();

    // A doctype with a public or system identifier: an external DTD
    [GeneratedRegex(@"<!DOCTYPE\s+[^\s>\[]+\s+(PUBLIC|SYSTEM)\s", RegexOptions.IgnoreCase)]
    private static partial Regex ExternalDtd();

    [GeneratedRegex(@"\s*Line [0-9]+, position [0-9]+\.?")]
    private static partial Regex LinePosition();

    /// <summary>The place of an element or attribute (1:1 when it is not known).</summary>
    internal static SourcePosition PositionOf(XObject node) =>
        node is IXmlLineInfo { } info && info.HasLineInfo()
            ? new SourcePosition(info.LineNumber, info.LinePosition)
            : new SourcePosition(1, 1);
}
