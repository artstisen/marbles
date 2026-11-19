// Copied from Blyant Validator 0.6.2 (src/Blyant.Validator.Css/Syntax/CssTokenizer.cs) by tools/formatters.py: not changed by hand in Marbles.
// Change it in Blyant Validator, and copy it again. English: its messages are not translated
using System.Globalization;
using System.Text;

namespace Marbles.Format.Css.Syntax;

/// <summary>A problem found while tokenizing or parsing.</summary>
public sealed record CssSyntaxError(int Offset, string Code, string Message);

/// <summary>
/// Tokenizer following CSS Syntax Level 3, section 4. Unlike the specification it keeps comments as
/// tokens (the formatter needs them) and records the offsets of every token.
/// </summary>
public sealed class CssTokenizer
{
    private readonly string _s;
    private int _pos;

    public List<CssSyntaxError> Errors { get; } = [];

    private CssTokenizer(string source) => _s = source;

    /// <summary>Tokenizes <paramref name="source"/>. The list always ends with an EndOfFile token.</summary>
    public static List<CssToken> Tokenize(string source, out List<CssSyntaxError> errors)
    {
        var tokenizer = new CssTokenizer(source);
        var tokens = new List<CssToken>();
        while (true)
        {
            var token = tokenizer.Next();
            tokens.Add(token);
            if (token.Type == CssTokenType.EndOfFile) break;
        }
        errors = tokenizer.Errors;
        return tokens;
    }

    private char Peek(int ahead = 0) => _pos + ahead < _s.Length ? _s[_pos + ahead] : '\0';

    private bool AtEnd(int ahead = 0) => _pos + ahead >= _s.Length;

    private CssToken Make(CssTokenType type, int start, string value = "") =>
        new(type, start, _pos, value);

    private void Error(int offset, string code, string message) => Errors.Add(new CssSyntaxError(offset, code, message));

    private CssToken Next()
    {
        var start = _pos;
        if (AtEnd()) return Make(CssTokenType.EndOfFile, start);

        var c = Peek();

        if (c == '/' && Peek(1) == '*')
        {
            var end = _s.IndexOf("*/", _pos + 2, StringComparison.Ordinal);
            if (end < 0)
            {
                Error(start, "css.parse.unclosed-comment", "Comment is never closed (missing “*/”).");
                _pos = _s.Length;
            }
            else
            {
                _pos = end + 2;
            }
            return Make(CssTokenType.Comment, start);
        }

        if (IsWhitespace(c))
        {
            while (!AtEnd() && IsWhitespace(Peek())) _pos++;
            return Make(CssTokenType.Whitespace, start);
        }

        switch (c)
        {
            case '"':
            case '\'':
                return ConsumeString(c);
            case '#':
                if (IsNameChar(Peek(1)) || IsValidEscape(Peek(1), Peek(2)))
                {
                    _pos++;
                    var isId = StartsIdent(Peek(), Peek(1), Peek(2));
                    var name = ConsumeName();
                    return Make(CssTokenType.Hash, start, name) with { IsIdHash = isId };
                }
                _pos++;
                return Make(CssTokenType.Delim, start, "#");
            case '(': _pos++; return Make(CssTokenType.LeftParen, start);
            case ')': _pos++; return Make(CssTokenType.RightParen, start);
            case '[': _pos++; return Make(CssTokenType.LeftSquare, start);
            case ']': _pos++; return Make(CssTokenType.RightSquare, start);
            case '{': _pos++; return Make(CssTokenType.LeftCurly, start);
            case '}': _pos++; return Make(CssTokenType.RightCurly, start);
            case ',': _pos++; return Make(CssTokenType.Comma, start);
            case ':': _pos++; return Make(CssTokenType.Colon, start);
            case ';': _pos++; return Make(CssTokenType.Semicolon, start);
            case '+':
            case '.':
                if (StartsNumber(c, Peek(1), Peek(2))) return ConsumeNumeric();
                _pos++;
                return Make(CssTokenType.Delim, start, c.ToString());
            case '-':
                if (StartsNumber(c, Peek(1), Peek(2))) return ConsumeNumeric();
                if (Peek(1) == '-' && Peek(2) == '>')
                {
                    _pos += 3;
                    return Make(CssTokenType.Cdc, start);
                }
                if (StartsIdent(c, Peek(1), Peek(2))) return ConsumeIdentLike();
                _pos++;
                return Make(CssTokenType.Delim, start, "-");
            case '<':
                if (Peek(1) == '!' && Peek(2) == '-' && Peek(3) == '-')
                {
                    _pos += 4;
                    return Make(CssTokenType.Cdo, start);
                }
                _pos++;
                return Make(CssTokenType.Delim, start, "<");
            case '@':
                if (StartsIdent(Peek(1), Peek(2), Peek(3)))
                {
                    _pos++;
                    return Make(CssTokenType.AtKeyword, start, ConsumeName());
                }
                _pos++;
                return Make(CssTokenType.Delim, start, "@");
            case '\\':
                if (IsValidEscape(c, Peek(1))) return ConsumeIdentLike();
                Error(start, "css.parse.bad-escape", "Invalid escape: a “\\” must not be followed by a line break.");
                _pos++;
                return Make(CssTokenType.Delim, start, "\\");
        }

        if (char.IsAsciiDigit(c)) return ConsumeNumeric();
        if (IsNameStart(c)) return ConsumeIdentLike();

        // (an ASCII character: all others start a name)
        _pos++;
        return Make(CssTokenType.Delim, start, _s[start.._pos]);
    }

    private CssToken ConsumeString(char quote)
    {
        var start = _pos++;
        var value = new StringBuilder();
        while (true)
        {
            if (AtEnd())
            {
                Error(start, "css.parse.unclosed-string", "String is never closed.");
                return Make(CssTokenType.String, start, value.ToString());
            }
            var c = Peek();
            if (c == quote)
            {
                _pos++;
                return Make(CssTokenType.String, start, value.ToString());
            }
            if (IsNewline(c))
            {
                Error(start, "css.parse.unclosed-string", "String is not closed before the end of the line.");
                return Make(CssTokenType.BadString, start, value.ToString());
            }
            if (c == '\\')
            {
                if (AtEnd(1)) { _pos++; continue; }
                if (IsNewline(Peek(1)))
                {
                    _pos += Peek(1) == '\r' && Peek(2) == '\n' ? 3 : 2;
                    continue;
                }
                _pos++;
                value.Append(ConsumeEscape());
                continue;
            }
            value.Append(c);
            _pos++;
        }
    }

    private CssToken ConsumeNumeric()
    {
        var start = _pos;
        var (number, isInteger) = ConsumeNumber();
        if (StartsIdent(Peek(), Peek(1), Peek(2)))
        {
            var unit = ConsumeName();
            return Make(CssTokenType.Dimension, start, unit) with { Number = number, IsInteger = isInteger };
        }
        if (Peek() == '%')
        {
            _pos++;
            return Make(CssTokenType.Percentage, start) with { Number = number, IsInteger = isInteger };
        }
        return Make(CssTokenType.Number, start) with { Number = number, IsInteger = isInteger };
    }

    private (double Number, bool IsInteger) ConsumeNumber()
    {
        var start = _pos;
        var isInteger = true;
        if (Peek() is '+' or '-') _pos++;
        while (char.IsAsciiDigit(Peek())) _pos++;
        if (Peek() == '.' && char.IsAsciiDigit(Peek(1)))
        {
            isInteger = false;
            _pos++;
            while (char.IsAsciiDigit(Peek())) _pos++;
        }
        if (Peek() is 'e' or 'E' &&
            (char.IsAsciiDigit(Peek(1)) || (Peek(1) is '+' or '-' && char.IsAsciiDigit(Peek(2)))))
        {
            isInteger = false;
            _pos += Peek(1) is '+' or '-' ? 2 : 1;
            while (char.IsAsciiDigit(Peek())) _pos++;
        }
        var text = _s[start.._pos];
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number);
        return (number, isInteger);
    }

    private CssToken ConsumeIdentLike()
    {
        var start = _pos;
        var name = ConsumeName();

        if (name.Equals("url", StringComparison.OrdinalIgnoreCase) && Peek() == '(')
        {
            _pos++;
            var save = _pos;
            while (IsWhitespace(Peek()) && IsWhitespace(Peek(1))) _pos++;
            var next = IsWhitespace(Peek()) ? Peek(1) : Peek();
            if (next is '"' or '\'')
            {
                _pos = save;
                return Make(CssTokenType.Function, start, name);
            }
            return ConsumeUrl(start);
        }

        if (Peek() == '(')
        {
            _pos++;
            return Make(CssTokenType.Function, start, name);
        }

        return Make(CssTokenType.Ident, start, name);
    }

    private CssToken ConsumeUrl(int start)
    {
        var value = new StringBuilder();
        while (IsWhitespace(Peek())) _pos++;
        while (true)
        {
            if (AtEnd())
            {
                Error(start, "css.parse.unclosed-url", "“url(” is never closed.");
                return Make(CssTokenType.Url, start, value.ToString());
            }
            var c = Peek();
            if (c == ')')
            {
                _pos++;
                return Make(CssTokenType.Url, start, value.ToString());
            }
            if (IsWhitespace(c))
            {
                while (IsWhitespace(Peek())) _pos++;
                if (Peek() == ')' || AtEnd()) continue;
                return ConsumeBadUrl(start);
            }
            if (c is '"' or '\'' or '(' || IsNonPrintable(c))
                return ConsumeBadUrl(start);
            if (c == '\\')
            {
                if (IsValidEscape(c, Peek(1)))
                {
                    _pos++;
                    value.Append(ConsumeEscape());
                    continue;
                }
                return ConsumeBadUrl(start);
            }
            value.Append(c);
            _pos++;
        }
    }

    private CssToken ConsumeBadUrl(int start)
    {
        Error(start, "css.parse.bad-url",
            "Invalid “url()”: an unquoted URL must not contain spaces, quotes or “(”. Put the URL in quotes.");
        while (!AtEnd())
        {
            var c = Peek();
            if (c == ')') { _pos++; break; }
            if (c == '\\' && IsValidEscape(c, Peek(1))) { _pos++; ConsumeEscape(); continue; }
            _pos++;
        }
        return Make(CssTokenType.BadUrl, start);
    }

    private string ConsumeName()
    {
        var name = new StringBuilder();
        while (!AtEnd())
        {
            var c = Peek();
            if (IsNameChar(c))
            {
                name.Append(c);
                _pos++;
            }
            else if (IsValidEscape(c, Peek(1)))
            {
                _pos++;
                name.Append(ConsumeEscape());
            }
            else
            {
                break;
            }
        }
        return name.ToString();
    }

    /// <summary>Consumes an escape; the backslash has already been consumed.</summary>
    private string ConsumeEscape()
    {
        if (AtEnd()) return "�";
        if (char.IsAsciiHexDigit(Peek()))
        {
            var start = _pos;
            while (_pos - start < 6 && char.IsAsciiHexDigit(Peek())) _pos++;
            var code = int.Parse(_s.AsSpan(start, _pos - start), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            if (Peek() == '\r' && Peek(1) == '\n') _pos += 2;
            else if (IsWhitespace(Peek())) _pos++;
            if (code == 0 || code > 0x10FFFF || code is >= 0xD800 and <= 0xDFFF) return "�";
            return char.ConvertFromUtf32(code);
        }
        var c = _s[_pos++];
        return c.ToString();
    }

    private static bool IsNewline(char c) => c is '\n' or '\r' or '\f';

    private static bool IsWhitespace(char c) => c is ' ' or '\t' || IsNewline(c);

    private static bool IsNameStart(char c) => char.IsAsciiLetter(c) || c == '_' || c >= 0x80;

    private static bool IsNameChar(char c) => IsNameStart(c) || char.IsAsciiDigit(c) || c == '-';

    private static bool IsNonPrintable(char c) => c is <= '\u0008' or '\u000B' or (>= '\u000E' and <= '\u001F') or '\u007F';

    // Peek returns '\0' past the end, so a backslash at the very end is not taken as an escape.
    private static bool IsValidEscape(char first, char second) => first == '\\' && !IsNewline(second) && second != '\0';

    private static bool StartsIdent(char a, char b, char c)
    {
        if (a == '-') return IsNameStart(b) || b == '-' || IsValidEscape(b, c);
        if (IsNameStart(a)) return true;
        return a == '\\' && IsValidEscape(a, b);
    }

    private static bool StartsNumber(char a, char b, char c)
    {
        if (a is '+' or '-') return char.IsAsciiDigit(b) || (b == '.' && char.IsAsciiDigit(c));
        if (a == '.') return char.IsAsciiDigit(b);
        return char.IsAsciiDigit(a);
    }
}
