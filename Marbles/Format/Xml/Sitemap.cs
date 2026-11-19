// Copied from Blyant Validator 0.6.2 (src/Blyant.Validator.Xml/Sitemap.cs) by tools/formatters.py: not changed by hand in Marbles.
// Change it in Blyant Validator, and copy it again. English: its messages are not translated
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Marbles.Format.Core;

namespace Marbles.Format.Xml;

/// <summary>
/// The rules of the sitemap protocol (https://www.sitemaps.org/protocol.html) for a sitemap (urlset) and a sitemap
/// index (sitemapindex). Elements of other namespaces (image:, video:, news:, xhtml:link) are extensions, and allowed.
/// </summary>
internal static partial class Sitemap
{
    internal const string Namespace = "http://www.sitemaps.org/schemas/sitemap/0.9";
    private const int MaxEntries = 50_000;
    private const int MaxBytes = 50 * 1024 * 1024;
    private const int MaxLocLength = 2048;

    private static readonly HashSet<string> ChangeFrequencies =
        ["always", "hourly", "daily", "weekly", "monthly", "yearly", "never"];

    public static bool IsSitemap(XElement root) => root.Name.LocalName is "urlset" or "sitemapindex";

    public static void Check(string path, string xml, XElement root, List<Diagnostic> diagnostics)
    {
        void Report(XObject at, Severity severity, string code, string message)
        {
            var position = XmlValidator.PositionOf(at);
            diagnostics.Add(new Diagnostic(path, severity, code, message, position, position));
        }

        var rootName = root.Name.LocalName;
        var isIndex = rootName == "sitemapindex";
        var entryName = isIndex ? "sitemap" : "url";

        if (root.Name.NamespaceName != Namespace)
        {
            Report(root, Severity.Error, "sitemap.namespace",
                $"The “{rootName}” element must be in the namespace “{Namespace}” (xmlns=\"{Namespace}\").");
            return;
        }

        if (Declaration().Match(xml) is { Success: true } declaration &&
            !declaration.Groups[1].Value.Equals("utf-8", StringComparison.OrdinalIgnoreCase))
            Report(root, Severity.Error, "sitemap.encoding",
                $"A sitemap must be encoded in UTF-8, not “{declaration.Groups[1].Value}”.");

        if (Encoding.UTF8.GetByteCount(xml) > MaxBytes)
            Report(root, Severity.Error, "sitemap.too-large", "A sitemap must not be larger than 50 MB (uncompressed).");

        var entries = new List<XElement>();
        foreach (var child in root.Elements())
        {
            if (child.Name.NamespaceName != Namespace) continue;
            if (child.Name.LocalName == entryName) entries.Add(child);
            else Report(child, Severity.Error, "sitemap.unknown-element",
                $"Element “{child.Name.LocalName}” not allowed as child of element “{rootName}”. Expected “{entryName}”.");
        }

        if (entries.Count > MaxEntries)
            Report(root, Severity.Error, "sitemap.too-many-entries",
                $"A sitemap must not have more than {MaxEntries:N0} “{entryName}” entries; this one has {entries.Count:N0}. Split it, and list the parts in a sitemap index.");
        if (entries.Count == 0)
            Report(root, Severity.Warning, "sitemap.empty", $"The sitemap has no “{entryName}” entries.");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        string? firstHost = null;
        var otherHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            var parts = new Dictionary<string, XElement>();
            foreach (var part in entry.Elements())
            {
                if (part.Name.NamespaceName != Namespace) continue;
                var name = part.Name.LocalName;
                var allowed = name is "loc" or "lastmod" || !isIndex && name is "changefreq" or "priority";
                if (!allowed)
                    Report(part, Severity.Error, "sitemap.unknown-element",
                        $"Element “{name}” not allowed as child of element “{entryName}”.");
                else if (!parts.TryAdd(name, part))
                    Report(part, Severity.Error, "sitemap.duplicate-element",
                        $"Element “{name}” must not appear more than once in a “{entryName}” entry.");
            }

            if (!parts.TryGetValue("loc", out var loc))
            {
                Report(entry, Severity.Error, "sitemap.missing-loc", $"Element “{entryName}” is missing required child element “loc”.");
            }
            else
            {
                var url = loc.Value.Trim();
                if (CheckLoc(url) is { } problem)
                {
                    Report(loc, Severity.Error, "sitemap.bad-loc", problem);
                }
                else
                {
                    if (!seen.Add(url))
                        Report(loc, Severity.Warning, "sitemap.duplicate-loc", $"“{Shorten(url)}” is listed more than once.");
                    var host = new Uri(url).Host;
                    firstHost ??= host;
                    if (!host.Equals(firstHost, StringComparison.OrdinalIgnoreCase) && otherHosts.Add(host))
                        Report(loc, Severity.Warning, "sitemap.mixed-hosts",
                            $"“{host}” is not the host of the first URL (“{firstHost}”): a sitemap is for the URLs of one host.");
                }
            }

            if (parts.TryGetValue("lastmod", out var lastmod) && !IsW3cDate(lastmod.Value.Trim()))
                Report(lastmod, Severity.Error, "sitemap.bad-lastmod",
                    $"“{Shorten(lastmod.Value.Trim())}” is not a W3C date: use, for example, “2026-11-10” or “2026-11-10T14:30:00+01:00”.");

            if (parts.TryGetValue("changefreq", out var changefreq) && !ChangeFrequencies.Contains(changefreq.Value.Trim()))
                Report(changefreq, Severity.Error, "sitemap.bad-changefreq",
                    $"“{Shorten(changefreq.Value.Trim())}” is not a change frequency: use always, hourly, daily, weekly, monthly, yearly or never.");

            if (parts.TryGetValue("priority", out var priority) && !IsPriority(priority.Value.Trim()))
                Report(priority, Severity.Error, "sitemap.bad-priority",
                    $"“{Shorten(priority.Value.Trim())}” is not a priority: use a number from 0.0 to 1.0.");
        }
    }

    // Why a URL cannot be the "loc" of a sitemap, or null
    private static string? CheckLoc(string url)
    {
        if (url.Length == 0) return "The “loc” element must not be empty.";
        if (url.Length > MaxLocLength) return $"A URL in a sitemap must not be longer than {MaxLocLength} characters.";
        if (url.Any(char.IsWhiteSpace)) return $"“{Shorten(url)}” contains a space: use “%20” instead.";
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") ||
            uri.Host.Length == 0)
            return $"“{Shorten(url)}” is not an absolute http or https URL (with the host, as “https://example.org/page.html”).";
        return null;
    }

    // W3C Datetime (https://www.w3.org/TR/NOTE-datetime): YYYY, YYYY-MM, YYYY-MM-DD, or a date with a time and a
    // time zone (hh:mm, hh:mm:ss or hh:mm:ss.s, then Z or +hh:mm / -hh:mm)
    private static bool IsW3cDate(string value)
    {
        var match = W3cDate().Match(value);
        if (!match.Success) return false;
        int Part(string name, int otherwise) => match.Groups[name].Success
            ? int.Parse(match.Groups[name].Value, CultureInfo.InvariantCulture) : otherwise;

        var (year, month, day) = (Part("y", 1), Part("m", 1), Part("d", 1));
        if (month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(Math.Max(1, year), month)) return false;
        return Part("hh", 0) <= 23 && Part("mm", 0) <= 59 && Part("ss", 0) <= 59 &&
               Part("tzh", 0) <= 14 && Part("tzm", 0) <= 59;
    }

    private static bool IsPriority(string value) => Priority().IsMatch(value);

    private static string Shorten(string text) => text.Length > 60 ? text[..60] + "…" : text;

    [GeneratedRegex(@"^﻿?\s*<\?xml[^>]*?\sencoding\s*=\s*[""']([^""']+)[""']")]
    private static partial Regex Declaration();

    [GeneratedRegex(@"^(?<y>[0-9]{4})(-(?<m>[0-9]{2})(-(?<d>[0-9]{2})(T(?<hh>[0-9]{2}):(?<mm>[0-9]{2})(:(?<ss>[0-9]{2})(\.[0-9]+)?)?(Z|[+-](?<tzh>[0-9]{2}):(?<tzm>[0-9]{2})))?)?)?$")]
    private static partial Regex W3cDate();

    [GeneratedRegex(@"^(0(\.[0-9]+)?|1(\.0+)?|\.[0-9]+)$")]
    private static partial Regex Priority();
}
