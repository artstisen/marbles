// Copied from Blyant Validator 0.6.2 (src/Blyant.Validator.Core/Abstractions.cs) by tools/formatters.py: not changed by hand in Marbles.
// Change it in Blyant Validator, and copy it again. English: its messages are not translated
namespace Marbles.Format.Core;

/// <summary>The kinds of documents Blyant Validator understands.</summary>
public enum DocumentKind
{
    Unknown,
    Html,
    Css,

    /// <summary>XML files (.xml), sitemaps among them, and SVG files (.svg), which are XML.</summary>
    Xml,

    /// <summary>Markdown files (.md, .markdown), and llms.txt and llms-full.txt (https://llmstxt.org/).</summary>
    Markdown,

    /// <summary>Typst files (.typ, https://typst.app/).</summary>
    Typst,
}

/// <summary>A document to validate or format.</summary>
/// <param name="Path">File path (used for reporting only).</param>
/// <param name="Content">The full text of the document.</param>
/// <param name="Kind">What kind of document it is.</param>
public sealed record SourceDocument(string Path, string Content, DocumentKind Kind)
{
    /// <summary>True when the file started with a UTF-8 byte order mark (kept when it is written back).</summary>
    public bool HasUtf8Bom { get; init; }

    public static DocumentKind DetectKind(string path) =>
        System.IO.Path.GetFileName(path).ToLowerInvariant() is "llms.txt" or "llms-full.txt"
            ? DocumentKind.Markdown
            : System.IO.Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".md" or ".markdown" => DocumentKind.Markdown,
            ".typ" => DocumentKind.Typst,
            ".html" or ".htm" => DocumentKind.Html,
            ".css" => DocumentKind.Css,
            // (XHTML is XML: checked for well-formedness, not by the rules of HTML's syntax)
            ".xml" or ".svg" or ".rss" or ".atom" or ".xhtml" => DocumentKind.Xml,
            _ => DocumentKind.Unknown,
        };

    /// <summary>
    /// The line ending the document mostly uses: CRLF, LF or CR (old Mac files). A tie goes to CRLF, then LF; a
    /// document without line breaks gets LF
    /// </summary>
    public string DetectNewLine()
    {
        int crlf = 0, lf = 0, cr = 0;
        for (var i = 0; i < Content.Length; i++)
        {
            if (Content[i] == '\n')
            {
                lf++;
            }
            else if (Content[i] == '\r')
            {
                if (i + 1 < Content.Length && Content[i + 1] == '\n')
                {
                    crlf++;
                    i++;
                }
                else
                {
                    cr++;
                }
            }
        }

        if (crlf > 0 && crlf >= lf && crlf >= cr) return "\r\n";
        return cr > lf ? "\r" : "\n";
    }
}

/// <summary>An engine that runs another program (vnu with Java, the Typst CLI) and could not be run.</summary>
public class EngineNotAvailableException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Checks a document and reports diagnostics.</summary>
public interface IValidator
{
    /// <summary>Short engine name shown in output, e.g. <c>native</c> or <c>vnu</c>.</summary>
    string Engine { get; }

    bool CanValidate(DocumentKind kind);

    Task<IReadOnlyList<Diagnostic>> ValidateAsync(SourceDocument document, CancellationToken cancellationToken = default);
}

/// <summary>Options shared by all formatters.</summary>
public sealed record FormatOptions
{
    public int IndentSize { get; init; } = 2;
    public bool UseTabs { get; init; }

    /// <summary>Line ending to write. Null keeps the line ending the document already uses.</summary>
    public string? NewLine { get; init; }

    /// <summary>Elements whose content fits within this width are kept on one line (HTML).</summary>
    public int LineWidth { get; init; } = 100;

    public string IndentUnit => UseTabs ? "\t" : new string(' ', IndentSize);
}

/// <summary>Thrown when a document cannot be formatted safely, e.g. because a block is never closed.</summary>
public sealed class FormatException(string message) : Exception(message);

/// <summary>Re-indents and normalises the layout of a document without changing its meaning.</summary>
public interface IFormatter
{
    bool CanFormat(DocumentKind kind);

    /// <exception cref="FormatException">The document has errors that make formatting unsafe.</exception>
    string Format(SourceDocument document, FormatOptions options);
}
