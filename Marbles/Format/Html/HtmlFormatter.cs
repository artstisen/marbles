// Copied from Blyant Validator 0.6.2 (src/Blyant.Validator.Html/HtmlFormatter.cs) by tools/formatters.py: not changed by hand in Marbles.
// Change it in Blyant Validator, and copy it again. English: its messages are not translated
using System.Text;
using Marbles.Format.Core;
using Marbles.Format.Css;
using Marbles.Format.Html.Parsing;

namespace Marbles.Format.Html;

/// <summary>
/// HTML formatter. Block-level elements get lines of their own and are indented; text and inline
/// elements stay together on a line, with runs of whitespace made one space. Whitespace is only
/// added or removed where it does not change how the page looks: around block-level elements, and
/// inside elements like &lt;svg&gt; and &lt;select&gt; whose children are not text.
/// The contents of &lt;pre&gt;, &lt;textarea&gt; and &lt;script&gt; are kept exactly as written;
/// &lt;style&gt; elements are formatted with the CSS formatter.
/// </summary>
public sealed class HtmlFormatter : IFormatter
{
    public bool CanFormat(DocumentKind kind) => kind == DocumentKind.Html;

    public string Format(SourceDocument document, FormatOptions options)
    {
        var newline = options.NewLine ?? document.DetectNewLine();
        var result = HtmlReader.Read(document.Content);
        // A file that ends inside a tag: what the tag is cannot be known, and writing it would add a ">"
        if (result.Errors.FirstOrDefault(e => e.Code is "html.parse.eof-in-tag" or "html.parse.eof-in-attribute") is { } eof)
            throw new Core.FormatException($"{eof.Message} (line {new LineMap(document.Content)[eof.Offset].Line})");
        if (result.TooDeepAt >= 0)
            throw new Core.FormatException(
                $"Elements are nested more than {HtmlReader.MaxDepth} deep (line {new LineMap(document.Content)[result.TooDeepAt].Line}): are end tags missing?");
        var root = result.Root;
        var writer = new Writer(document.Content, options, newline);
        writer.FormatChildren(root.Children, 0, container: false, foreign: false);
        var text = writer.ToString();
        return text.Length == 0 ? "" : text + newline;
    }

    private sealed class Writer(string source, FormatOptions options, string newline)
    {
        private readonly List<string> _lines = [];
        private StringBuilder? _line;
        private int _lineDepth;

        public override string ToString() => string.Join(newline, _lines);

        private string Indent(int depth)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < depth; i++) sb.Append(options.IndentUnit);
            return sb.ToString();
        }

        private void Line(int depth, string text) => _lines.Add(Indent(depth) + NormalizeNewlines(text));

        private void BeginLine(int depth)
        {
            _line = new StringBuilder();
            _lineDepth = depth;
        }

        private void Append(string text) => _line!.Append(text);

        private void EndLine()
        {
            var text = _line!.ToString().Trim(' ');
            _line = null;
            if (text.Length > 0) Line(_lineDepth, text);
        }

        private string NormalizeNewlines(string text) =>
            text.Contains('\n') || text.Contains('\r')
                ? text.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", newline)
                : text;

        /// <param name="container">The parent lays out its children itself (svg, select …): every element child gets a line.</param>
        /// <param name="foreign">Inside SVG or MathML.</param>
        public void FormatChildren(List<HNode> children, int depth, bool container, bool foreign)
        {
            var run = new List<HNode>();
            var emitted = false;
            var newlinesSinceLast = 0;

            // One empty line in the source between two things at this level is kept.
            void BlankIfWanted()
            {
                if (emitted && newlinesSinceLast >= 2) _lines.Add("");
            }

            void FlushRun()
            {
                if (run.Count == 0) return;
                if (run.All(n => n is HText t && IsWhitespace(t.Raw)))
                {
                    newlinesSinceLast += run.Sum(n => CountNewlines(((HText)n).Raw));
                    run.Clear();
                    return;
                }

                newlinesSinceLast += EdgeNewlines(run, leading: true);
                BlankIfWanted();
                var before = _lines.Count;
                BeginLine(depth);
                RenderInline(run, depth, foreign);
                EndLine();
                if (_lines.Count > before) emitted = true;
                newlinesSinceLast = EdgeNewlines(run, leading: false);
                run.Clear();
            }

            for (var index = 0; index < children.Count; index++)
            {
                var child = children[index];
                if (container && child is HText t && IsWhitespace(t.Raw))
                {
                    newlinesSinceLast += CountNewlines(t.Raw);
                    continue;
                }

                var ownLine = child switch
                {
                    HVerbatim v => v.IsDoctype,
                    HElement e => container || IsBlock(e, foreign) && !(HtmlNames.Hidden.Contains(e.Lower) && TouchesText(children, index, foreign)),
                    _ => false,
                };

                if (!ownLine)
                {
                    run.Add(child);
                    continue;
                }

                FlushRun();
                BlankIfWanted();
                if (child is HVerbatim doctype) Line(depth, doctype.Raw);
                else FormatElement((HElement)child, depth, foreign);
                emitted = true;
                newlinesSinceLast = 0;
            }

            FlushRun();
        }

        /// <summary>
        /// True when something on a side of a child is not separated from it by whitespace or a block: text, an inline
        /// element, a comment. A line of its own would then put whitespace there, which can show (a script in the
        /// middle of a sentence)
        /// </summary>
        private static bool TouchesText(List<HNode> children, int index, bool foreign)
        {
            bool Separated(int at, bool before) => at < 0 || at >= children.Count || children[at] switch
            {
                HText text => text.Raw.Length == 0 || IsHtmlSpace(before ? text.Raw[^1] : text.Raw[0]),
                HElement e => IsBlock(e, foreign),
                HVerbatim v => v.IsDoctype,
                _ => false,
            };

            return !Separated(index - 1, before: true) || !Separated(index + 1, before: false);
        }

        /// <summary>Line breaks in the whitespace at the start (or end) of a run of inline content.</summary>
        private static int EdgeNewlines(List<HNode> run, bool leading)
        {
            var count = 0;
            var nodes = leading ? run : Enumerable.Reverse(run);
            foreach (var node in nodes)
            {
                if (node is not HText t) break;
                var raw = t.Raw;
                if (IsWhitespace(raw))
                {
                    count += CountNewlines(raw);
                    continue;
                }
                var edge = leading
                    ? raw[..raw.TakeWhile(IsHtmlSpace).Count()]
                    : raw[(raw.Length - raw.Reverse().TakeWhile(IsHtmlSpace).Count())..];
                count += CountNewlines(edge);
                break;
            }
            return count;
        }

        /// <summary>
        /// Block-level elements get lines of their own. Inside SVG and MathML that is decided by the
        /// parent (the container flag) instead.
        /// </summary>
        private static bool IsBlock(HElement e, bool foreign) =>
            !foreign && (HtmlNames.Block.Contains(e.Lower) || ContainsBlock(e));

        /// <summary>An inline element holding a block (e.g. &lt;a&gt; around a &lt;div&gt;) is laid out as a block.</summary>
        private static bool ContainsBlock(HElement e)
        {
            if (HtmlNames.Container.Contains(e.Lower) || HtmlNames.Preformatted.Contains(e.Lower)) return false;
            foreach (var child in e.Children)
                if (child is HElement c && (HtmlNames.Block.Contains(c.Lower) || ContainsBlock(c)))
                    return true;
            return false;
        }

        /// <summary>Writes an element that has lines of its own.</summary>
        private void FormatElement(HElement e, int depth, bool foreign)
        {
            var open = OpenTag(e);
            var childForeign = (foreign || e.Lower is "svg" or "math") && !HtmlNames.IsIntegrationPoint(e);

            if (e.SelfClosing || e.IsVoid)
            {
                Line(depth, open);
                return;
            }

            if (e.Lower == "style" && e.RawContent is { } css)
            {
                FormatStyle(e, open, css, depth);
                return;
            }

            if (HtmlNames.Preformatted.Contains(e.Lower))
            {
                Line(depth, open + source[e.ContentStart..e.ContentEnd] + (e.EndTag ?? ""));
                return;
            }

            if (e.RawContent is { } rawText)
            {
                // title (and the like): text only; whitespace runs do not matter.
                var content = CollapseWhitespace(rawText).Trim(' ');
                Line(depth, open + content + (e.EndTag ?? ""));
                return;
            }

            var container = HtmlNames.Container.Contains(e.Lower) ||
                            (childForeign && !HtmlNames.ForeignText.Contains(e.Lower));

            if (e.Children.Count == 0 || e.Children.All(c => c is HText t && IsWhitespace(t.Raw)))
            {
                Line(depth, open + (e.EndTag ?? ""));
                return;
            }

            // Keep it on one line when the content is inline, has no line breaks and fits.
            if (!container && e.Children.All(c => c is not HElement child || !IsBlock(child, childForeign)))
            {
                var single = TryRenderOneLine(e.Children, childForeign);
                var line = open + single + (e.EndTag ?? "");
                if (single is not null && Indent(depth).Length + line.Length <= options.LineWidth)
                {
                    Line(depth, line);
                    return;
                }
            }

            Line(depth, open);
            FormatChildren(e.Children, depth + 1, container, childForeign);
            if (e.EndTag is not null) Line(depth, e.EndTag);
        }

        private void FormatStyle(HElement e, string open, string css, int depth)
        {
            if (IsWhitespace(css))
            {
                Line(depth, open + (e.EndTag ?? ""));
                return;
            }
            try
            {
                var formatted = CssFormatter.FormatStylesheet(css, options, newline, Indent(depth + 1));
                Line(depth, open);
                _lines.Add(formatted);
                if (e.EndTag is not null) Line(depth, e.EndTag);
            }
            catch (Core.FormatException)
            {
                Line(depth, open + css + (e.EndTag ?? ""));
            }
        }

        /// <summary>Renders inline content as one line, or returns null when it needs more than one.</summary>
        private string? TryRenderOneLine(List<HNode> nodes, bool foreign)
        {
            var saveLines = _lines.Count;
            BeginLine(0);
            RenderInline(nodes, 0, foreign);
            EndLine();
            var produced = _lines.Count - saveLines;
            string? result = produced switch
            {
                0 => "",
                1 => _lines[^1],
                _ => null,
            };
            if (result is not null && (result.Contains('\n') || result.Contains('\r'))) result = null;
            _lines.RemoveRange(saveLines, produced);
            return result;
        }

        /// <summary>Appends inline content to the current line. Line breaks are made after &lt;br&gt;, and around the children of svg, select …</summary>
        private void RenderInline(List<HNode> nodes, int depth, bool foreign)
        {
            foreach (var node in nodes)
            {
                switch (node)
                {
                    case HText text:
                        AppendText(text.Raw, depth);
                        break;
                    case HVerbatim verbatim:
                        Append(verbatim.Raw);
                        break;
                    case HStrayEndTag stray:
                        Append(stray.Raw);
                        break;
                    case HElement e:
                        RenderInlineElement(e, depth, foreign);
                        break;
                }
            }
        }

        /// <summary>
        /// Appends text with each run of whitespace made one space, except that a run holding a line
        /// break starts a new line (both are the same whitespace to the browser).
        /// </summary>
        private void AppendText(string raw, int depth)
        {
            var i = 0;
            while (i < raw.Length)
            {
                if (!IsHtmlSpace(raw[i]))
                {
                    var start = i;
                    while (i < raw.Length && !IsHtmlSpace(raw[i])) i++;
                    Append(raw[start..i]);
                    continue;
                }

                var hasBreak = false;
                while (i < raw.Length && IsHtmlSpace(raw[i]))
                {
                    hasBreak |= raw[i] is '\n' or '\r';
                    i++;
                }
                if (hasBreak)
                {
                    EndLine();
                    BeginLine(depth);
                }
                else
                {
                    Append(" ");
                }
            }
        }

        private void RenderInlineElement(HElement e, int depth, bool foreign)
        {
            var open = OpenTag(e);
            Append(open);
            if (e.SelfClosing || e.IsVoid)
            {
                if (e.Lower == "br")
                {
                    EndLine();
                    BeginLine(depth);
                }
                return;
            }

            if (HtmlNames.Preformatted.Contains(e.Lower) || e.RawContent is not null)
            {
                Append(source[e.ContentStart..e.ContentEnd] + (e.EndTag ?? ""));
                return;
            }

            var childForeign = (foreign || e.Lower is "svg" or "math") && !HtmlNames.IsIntegrationPoint(e);
            var container = HtmlNames.Container.Contains(e.Lower) ||
                            (childForeign && !HtmlNames.ForeignText.Contains(e.Lower));

            if (container && e.Children.Any(c => c is HElement))
            {
                // Whitespace inside does not matter: the children get lines of their own,
                // and the end tag continues the line, so the whitespace around the element is unchanged.
                EndLine();
                FormatChildren(e.Children, depth + 1, container: true, childForeign);
                BeginLine(depth);
                Append(e.EndTag ?? "");
                return;
            }

            RenderInline(e.Children, depth, childForeign);
            Append(e.EndTag ?? "");
        }

        private static string OpenTag(HElement e)
        {
            var sb = new StringBuilder("<").Append(e.Name);
            foreach (var a in e.Attributes)
            {
                sb.Append(' ').Append(a.Name);
                if (a.RawValue is not null) sb.Append('=').Append(a.RawValue);
            }
            // "/>" keeps the space before it if the source had one (after an unquoted value it always has).
            sb.Append(e.SelfClosing ? (e.SpaceBeforeSlash ? " />" : "/>") : ">");
            return sb.ToString();
        }

        /// <summary>Makes each run of HTML whitespace (not no-break spaces) a single space.</summary>
        private static string CollapseWhitespace(string text)
        {
            var sb = new StringBuilder(text.Length);
            var space = false;
            foreach (var c in text)
            {
                if (IsHtmlSpace(c))
                {
                    if (!space) sb.Append(' ');
                    space = true;
                    continue;
                }
                space = false;
                sb.Append(c);
            }
            return sb.ToString();
        }

        private static bool IsHtmlSpace(char c) => c is ' ' or '\t' or '\n' or '\r' or '\f';

        private static bool IsWhitespace(string text) => text.All(IsHtmlSpace);

        private static int CountNewlines(string text) => text.Replace("\r\n", "\n").Count(c => c is '\n' or '\r');
    }
}
