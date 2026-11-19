// Copied from Blyant Validator 0.6.2 (src/Blyant.Validator.Css/CssFormatter.cs) by tools/formatters.py: not changed by hand in Marbles.
// Change it in Blyant Validator, and copy it again. English: its messages are not translated
using System.Text;
using Marbles.Format.Core;
using Marbles.Format.Css.Syntax;

namespace Marbles.Format.Css;

/// <summary>
/// CSS formatter. Puts each selector and declaration on its own line, indents blocks, and keeps
/// comments and single empty lines. Only whitespace changes (and a missing last <c>;</c> is added):
/// the tokens of the style sheet stay the same.
/// </summary>
public sealed class CssFormatter : IFormatter
{
    public bool CanFormat(DocumentKind kind) => kind == DocumentKind.Css;

    public string Format(SourceDocument document, FormatOptions options)
    {
        var newline = options.NewLine ?? document.DetectNewLine();
        var text = FormatStylesheet(document.Content, options, newline);
        return text.Length == 0 ? "" : text + newline;
    }

    /// <summary>
    /// Formats a style sheet and returns it without a final line break. Every line is indented by
    /// <paramref name="baseIndent"/> (used for &lt;style&gt; elements in HTML).
    /// </summary>
    /// <exception cref="Core.FormatException">A block is never closed, or a <c>}</c> has no block.</exception>
    public static string FormatStylesheet(string css, FormatOptions options, string newline, string baseIndent = "")
    {
        var sheet = CssParser.Parse(css);
        if (sheet.Tokens.FirstOrDefault(t => t.Type == CssTokenType.BadString) is { } badString)
        {
            // The line break that ends it would become a space, and the string would then take in what follows
            throw new Core.FormatException(
                $"A string is not closed before the end of the line (line {new LineMap(css)[badString.Start].Line}).");
        }

        if (sheet.HasStructuralErrors)
        {
            var lines = new LineMap(css);
            var error = sheet.Errors.First(e => e.Code is "css.parse.unclosed-block" or "css.parse.unexpected-close" or "css.parse.unclosed-parenthesis"
                or "css.parse.too-deep");
            throw new Core.FormatException($"{error.Message} (line {lines[error.Offset].Line})");
        }

        var writer = new Writer(css, options.IndentUnit, newline, baseIndent);
        writer.WriteItems(sheet.Items, 0);
        return writer.ToString();
    }

    private sealed class Writer(string source, string indentUnit, string newline, string baseIndent)
    {
        private readonly List<string> _lines = [];

        public override string ToString() => string.Join(newline, _lines);

        private void Line(int depth, string text)
        {
            var indent = new StringBuilder(baseIndent);
            for (var i = 0; i < depth; i++) indent.Append(indentUnit);
            _lines.Add(indent + text);
        }

        private void AppendToLast(string text) => _lines[^1] += text;

        public void WriteItems(List<CssItem> items, int depth)
        {
            for (var k = 0; k < items.Count; k++)
            {
                var item = items[k];
                if (k > 0 && item is CssComment && item.SameLineAsPrevious)
                {
                    AppendToLast(" " + NormalizeNewlines(item is CssComment c ? c.Token.Text(source) : ""));
                    continue;
                }
                if (k > 0 && item.BlankLineBefore) _lines.Add("");

                switch (item)
                {
                    case CssComment comment:
                        Line(depth, NormalizeNewlines(comment.Token.Text(source)));
                        break;
                    case CssDeclaration declaration:
                        WriteDeclaration(declaration, depth);
                        break;
                    case CssStyleRule rule:
                        WriteSelector(rule.Prelude, depth);
                        WriteBlock(rule.Block, depth);
                        break;
                    case CssAtRule at:
                        WriteAtRule(at, depth);
                        break;
                    case CssBogus bogus:
                        Line(depth, Join(bogus.Tokens) + (bogus.HadSemicolon ? ";" : ""));
                        break;
                }
            }
        }

        // The tokens of a declaration as written (the name with its escapes, comments, "!important" in its case), with
        // only the whitespace changed: none before the ":" and one space after it
        private void WriteDeclaration(CssDeclaration declaration, int depth)
        {
            var tokens = declaration.Tokens;
            var colon = declaration.ColonIndex;
            var rest = tokens.Skip(colon + 1).SkipWhile(t => t.Type == CssTokenType.Whitespace).ToList();
            string value;
            if (declaration.IsCustomProperty)
            {
                // The value of a custom property is kept exactly as written.
                value = rest.Count == 0 ? "" : NormalizeNewlines(source[rest[0].Start..rest[^1].End]);
            }
            else if (declaration.Important && rest.FindLastIndex(t => t.IsDelim('!')) is var bang and > 0)
            {
                // One space before "!important" (only whitespace is added)
                value = (Join(rest.Take(bang)) + " " + Join(rest.Skip(bang))).Trim(' ');
            }
            else
            {
                value = Join(rest);
            }

            var text = new StringBuilder(Join(tokens.Take(colon))).Append(':');
            if (value.Length > 0) text.Append(' ').Append(value);
            text.Append(';');
            Line(depth, text.ToString());
        }

        private void WriteSelector(List<CssToken> prelude, int depth)
        {
            var parts = SplitAtTopLevelCommas(prelude);
            for (var i = 0; i < parts.Count; i++)
            {
                var text = Join(parts[i]);
                Line(depth, i < parts.Count - 1 ? text + "," : text + " {");
            }
        }

        private void WriteAtRule(CssAtRule at, int depth)
        {
            var prelude = Join(at.Prelude);
            // (the name as written, with its escapes)
            var head = at.NameToken.Text(source) + (prelude.Length > 0 ? " " + prelude : "");
            if (at.Block is null)
            {
                Line(depth, head + ";");
                return;
            }
            Line(depth, head + " {");
            WriteBlock(at.Block, depth);
        }

        /// <summary>Writes the contents of a block and its closing brace; the opening line is already written.</summary>
        private void WriteBlock(CssBlock block, int depth)
        {
            if (block.Items.Count == 0)
            {
                AppendToLast("}");
                return;
            }
            WriteItems(block.Items, depth + 1);
            Line(depth, "}");
        }

        /// <summary>Joins tokens, with each run of whitespace made a single space, and trimmed.</summary>
        private string Join(IEnumerable<CssToken> tokens)
        {
            var sb = new StringBuilder();
            foreach (var t in tokens)
            {
                if (t.Type == CssTokenType.Whitespace)
                {
                    if (sb.Length > 0 && sb[^1] != ' ') sb.Append(' ');
                }
                else
                {
                    sb.Append(NormalizeNewlines(t.Text(source)));
                }
            }
            return sb.ToString().TrimEnd(' ');
        }

        private string NormalizeNewlines(string text) =>
            text.Contains('\n') || text.Contains('\r')
                ? text.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", newline)
                : text;

        private static List<List<CssToken>> SplitAtTopLevelCommas(List<CssToken> tokens)
        {
            var parts = new List<List<CssToken>> { new() };
            var depth = 0;
            foreach (var t in tokens)
            {
                if (t.Type is CssTokenType.Function or CssTokenType.LeftParen or CssTokenType.LeftSquare) depth++;
                else if (t.Type is CssTokenType.RightParen or CssTokenType.RightSquare && depth > 0) depth--;

                if (t.Type == CssTokenType.Comma && depth == 0) parts.Add([]);
                else parts[^1].Add(t);
            }
            return parts.Select(p => p.SkipWhile(t => t.Type == CssTokenType.Whitespace).ToList()).ToList();
        }
    }
}
