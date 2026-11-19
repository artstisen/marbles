// Copied from Blyant Validator 0.6.2 (src/Blyant.Validator.Xml/Feeds.cs) by tools/formatters.py: not changed by hand in Marbles.
// Change it in Blyant Validator, and copy it again. English: its messages are not translated
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Marbles.Format.Core;

namespace Marbles.Format.Xml;

/// <summary>
/// The rules of news feeds: RSS 2.0 (https://www.rssboard.org/rss-specification) and Atom (RFC 4287). Elements of
/// other namespaces (atom:link, content:encoded, dc:creator, itunes:*, media:*) are extensions, and allowed.
/// </summary>
internal static partial class Feeds
{
    internal const string AtomNamespace = "http://www.w3.org/2005/Atom";

    public static bool IsRss(XElement root) => root.Name == "rss";

    public static bool IsAtom(XElement root) => root.Name == XName.Get("feed", AtomNamespace);

    private delegate void Reporter(XObject at, Severity severity, string code, string message);

    private static Reporter MakeReporter(string path, List<Diagnostic> diagnostics) => (at, severity, code, message) =>
    {
        var position = XmlValidator.PositionOf(at);
        diagnostics.Add(new Diagnostic(path, severity, code, message, position, position));
    };

    #region RSS 2.0

    private static readonly HashSet<string> ChannelElements =
    [
        "title", "link", "description", "language", "copyright", "managingEditor", "webMaster", "pubDate",
        "lastBuildDate", "category", "generator", "docs", "cloud", "ttl", "image", "rating", "textInput", "skipHours",
        "skipDays", "item",
    ];

    private static readonly HashSet<string> ItemElements =
        ["title", "link", "description", "author", "category", "comments", "enclosure", "guid", "pubDate", "source"];

    private static readonly HashSet<string> Days = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];

    public static void CheckRss(string path, XElement rss, List<Diagnostic> diagnostics)
    {
        var report = MakeReporter(path, diagnostics);
        var version = rss.Attribute("version")?.Value;
        if (version == null)
            report(rss, Severity.Error, "rss.version", "The “rss” element is missing required attribute “version” (“2.0”).");
        else if (version != "2.0")
            report(rss, Severity.Warning, "rss.version", $"RSS version “{version}”: the rules of RSS 2.0 are used.");

        var channels = rss.Elements("channel").ToList();
        if (channels.Count != 1)
        {
            report(rss, Severity.Error, "rss.channel", channels.Count == 0
                ? "The “rss” element must contain a “channel” element."
                : "The “rss” element must contain only one “channel” element.");
            if (channels.Count == 0) return;
        }

        var channel = channels[0];
        CheckChildren(channel, "channel", ChannelElements, ["title", "link", "description"], report);
        CheckRssCommon(channel, report);

        if (channel.Element("ttl") is { } ttl && !IsNonNegativeInteger(ttl.Value))
            report(ttl, Severity.Error, "rss.bad-value", $"“{Shorten(ttl.Value)}” is not a number of minutes.");
        if (channel.Element("language") is { } language && !LanguageCode().IsMatch(language.Value.Trim()))
            report(language, Severity.Warning, "rss.bad-value", $"“{Shorten(language.Value)}” is not a language code (as “en” or “da-DK”).");
        if (channel.Element("image") is { } image)
        {
            foreach (var required in new[] { "url", "title", "link" })
                if (image.Element(required) == null)
                    report(image, Severity.Error, "rss.missing-element", $"Element “image” is missing required child element “{required}”.");
            CheckUrl(image.Element("url"), report);
            CheckUrl(image.Element("link"), report);
        }
        foreach (var hour in channel.Element("skipHours")?.Elements("hour") ?? [])
            if (!int.TryParse(hour.Value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var h) || h > 23)
                report(hour, Severity.Error, "rss.bad-value", $"“{Shorten(hour.Value)}” is not an hour from 0 to 23.");
        foreach (var day in channel.Element("skipDays")?.Elements("day") ?? [])
            if (!Days.Contains(day.Value.Trim()))
                report(day, Severity.Error, "rss.bad-value", $"“{Shorten(day.Value)}” is not a day (Monday … Sunday).");

        var guids = new Dictionary<string, XElement>(StringComparer.Ordinal);
        foreach (var item in channel.Elements("item"))
        {
            CheckChildren(item, "item", ItemElements, [], report);
            if (item.Element("title") == null && item.Element("description") == null)
                report(item, Severity.Error, "rss.missing-element", "An “item” must have a “title” or a “description”, or both.");
            CheckRssCommon(item, report);

            if (item.Element("guid") is { } guid)
            {
                var permalink = guid.Attribute("isPermaLink")?.Value.Trim();
                if (permalink is not (null or "true" or "false"))
                    report(guid, Severity.Error, "rss.bad-value", "The “isPermaLink” attribute must be “true” or “false”.");
                else if (permalink != "false" && UrlProblem(guid.Value.Trim()) != null)
                    report(guid, Severity.Error, "rss.bad-guid",
                        $"“{Shorten(guid.Value.Trim())}” is not a URL: a “guid” is a link unless it has isPermaLink=\"false\".");
                if (!guids.TryAdd(guid.Value.Trim(), guid))
                    report(guid, Severity.Warning, "rss.duplicate-guid", $"The guid “{Shorten(guid.Value.Trim())}” is used by more than one item.");
            }

            if (item.Element("enclosure") is { } enclosure)
            {
                foreach (var required in new[] { "url", "length", "type" })
                    if (enclosure.Attribute(required) == null)
                        report(enclosure, Severity.Error, "rss.missing-attribute", $"Element “enclosure” is missing required attribute “{required}”.");
                if (enclosure.Attribute("url") is { } url && UrlProblem(url.Value) is { } problem)
                    report(enclosure, Severity.Error, "rss.bad-url", problem);
                if (enclosure.Attribute("length") is { } length && !IsNonNegativeInteger(length.Value))
                    report(enclosure, Severity.Error, "rss.bad-value", $"The enclosure length “{Shorten(length.Value)}” is not a number of bytes.");
            }

            if (item.Element("source") is { } source && source.Attribute("url") == null)
                report(source, Severity.Error, "rss.missing-attribute", "Element “source” is missing required attribute “url”.");
        }
    }

    // The children of a channel or item: known elements, the required ones, and single ones not repeated
    private static void CheckChildren(XElement parent, string name, HashSet<string> known, string[] required, Reporter report)
    {
        foreach (var child in parent.Elements())
        {
            // Elements of a namespace are extensions
            if (child.Name.NamespaceName.Length > 0) continue;
            if (!known.Contains(child.Name.LocalName))
                report(child, Severity.Warning, "rss.unknown-element",
                    $"Element “{child.Name.LocalName}” is not part of RSS 2.0: an extension must be in a namespace (as “dc:creator”).");
        }

        foreach (var element in required)
            if (parent.Element(element) == null)
                report(parent, Severity.Error, "rss.missing-element", $"Element “{name}” is missing required child element “{element}”.");

        foreach (var group in parent.Elements().Where(e => e.Name.NamespaceName.Length == 0 && e.Name.LocalName is not ("item" or "category"))
                     .GroupBy(e => e.Name.LocalName).Where(g => g.Count() > 1))
            foreach (var extra in group.Skip(1))
                report(extra, Severity.Error, "rss.duplicate-element", $"Element “{group.Key}” must not appear more than once in “{name}”.");
    }

    // What a channel and an item have alike: links, dates and e-mail addresses
    private static void CheckRssCommon(XElement element, Reporter report)
    {
        CheckUrl(element.Element("link"), report);
        CheckUrl(element.Element("comments"), report);
        CheckUrl(element.Element("docs"), report);
        foreach (var date in new[] { element.Element("pubDate"), element.Element("lastBuildDate") })
        {
            if (date == null) continue;
            var value = date.Value.Trim();
            switch (Rfc822(value))
            {
                case DateCheck.Invalid:
                    report(date, Severity.Error, "rss.bad-date",
                        $"“{Shorten(value)}” is not an RFC 822 date: use, for example, “Tue, 10 Nov 2026 14:30:00 GMT”.");
                    break;
                case DateCheck.WrongWeekday:
                    report(date, Severity.Warning, "rss.bad-date", $"The weekday of “{Shorten(value)}” is not that of the date.");
                    break;
            }
        }

        foreach (var person in new[] { element.Element("managingEditor"), element.Element("webMaster"), element.Element("author") })
        {
            if (person != null && !person.Value.Contains('@'))
                report(person, Severity.Warning, "rss.bad-value",
                    $"“{Shorten(person.Value.Trim())}” is not an e-mail address: RSS expects one here (use dc:creator for a name).");
        }
    }

    private enum DateCheck
    {
        Valid,
        Invalid,
        WrongWeekday,
    }

    // RFC 822 (with a 2- or 4-digit year, as RSS 2.0 allows): "Tue, 10 Nov 2026 14:30:00 GMT", "10 Nov 2026 14:30 +0100"
    private static DateCheck Rfc822(string value)
    {
        var match = Rfc822Date().Match(value);
        if (!match.Success) return DateCheck.Invalid;
        var month = Array.IndexOf(["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"],
            match.Groups["mon"].Value) + 1;
        var year = int.Parse(match.Groups["y"].Value, CultureInfo.InvariantCulture);
        if (match.Groups["y"].Length == 2) year += year < 50 ? 2000 : 1900;
        if (year < 1) return DateCheck.Invalid;
        var day = int.Parse(match.Groups["d"].Value, CultureInfo.InvariantCulture);
        if (day < 1 || day > DateTime.DaysInMonth(year, month)) return DateCheck.Invalid;
        if (int.Parse(match.Groups["hh"].Value, CultureInfo.InvariantCulture) > 23 ||
            int.Parse(match.Groups["mm"].Value, CultureInfo.InvariantCulture) > 59 ||
            match.Groups["ss"].Success && int.Parse(match.Groups["ss"].Value, CultureInfo.InvariantCulture) > 60)
            return DateCheck.Invalid;
        if (match.Groups["wd"].Success &&
            match.Groups["wd"].Value != new DateTime(year, month, day).DayOfWeek.ToString()[..3])
            return DateCheck.WrongWeekday;
        return DateCheck.Valid;
    }

    #endregion

    #region Atom

    private static readonly HashSet<string> FeedSingles = ["id", "title", "updated", "subtitle", "rights", "icon", "logo", "generator"];
    private static readonly HashSet<string> EntrySingles = ["id", "title", "updated", "summary", "content", "published", "rights", "source"];

    public static void CheckAtom(string path, XElement feed, List<Diagnostic> diagnostics)
    {
        var report = MakeReporter(path, diagnostics);
        static XName Atom(string name) => XName.Get(name, AtomNamespace);

        CheckAtomElement(feed, "feed", FeedSingles, report);
        var feedHasAuthor = feed.Elements(Atom("author")).Any();
        var ids = new Dictionary<string, XElement>(StringComparer.Ordinal);
        if (feed.Element(Atom("id")) is { } feedId) ids[feedId.Value.Trim()] = feedId;

        foreach (var entry in feed.Elements(Atom("entry")))
        {
            CheckAtomElement(entry, "entry", EntrySingles, report);
            if (!feedHasAuthor && !entry.Elements(Atom("author")).Any() &&
                entry.Element(Atom("source"))?.Elements(Atom("author")).Any() != true)
                report(entry, Severity.Error, "atom.missing-author",
                    "An “entry” must have an “author” when the “feed” has none.");

            if (entry.Element(Atom("content")) == null &&
                !entry.Elements(Atom("link")).Any(l => (l.Attribute("rel")?.Value ?? "alternate") == "alternate"))
                report(entry, Severity.Error, "atom.missing-content",
                    "An “entry” must have a “content” element or a link with rel=\"alternate\".");
            if (entry.Element(Atom("content")) is { } content && content.Attribute("src") != null && !content.IsEmpty &&
                content.Value.Trim().Length > 0)
                report(content, Severity.Error, "atom.bad-content", "A “content” element with a “src” attribute must be empty.");

            if (entry.Element(Atom("id")) is { } id && !ids.TryAdd(id.Value.Trim(), id))
                report(id, Severity.Warning, "atom.duplicate-id", $"The id “{Shorten(id.Value.Trim())}” is used more than once.");
        }

        if (!feedHasAuthor && !feed.Elements(Atom("entry")).Any())
            report(feed, Severity.Error, "atom.missing-author", "Element “feed” is missing required child element “author”.");
    }

    // A feed or an entry: the required elements, those that may appear once, ids, dates, links and people
    private static void CheckAtomElement(XElement element, string name, HashSet<string> singles, Reporter report)
    {
        static XName Atom(string local) => XName.Get(local, AtomNamespace);
        foreach (var required in new[] { "id", "title", "updated" })
            if (element.Element(Atom(required)) == null)
                report(element, Severity.Error, "atom.missing-element", $"Element “{name}” is missing required child element “{required}”.");

        foreach (var group in element.Elements().Where(e => e.Name.NamespaceName == AtomNamespace && singles.Contains(e.Name.LocalName))
                     .GroupBy(e => e.Name.LocalName).Where(g => g.Count() > 1))
            foreach (var extra in group.Skip(1))
                report(extra, Severity.Error, "atom.duplicate-element", $"Element “{group.Key}” must not appear more than once in “{name}”.");

        if (element.Element(Atom("id")) is { } id && !IriScheme().IsMatch(id.Value.Trim()))
            report(id, Severity.Error, "atom.bad-id",
                $"“{Shorten(id.Value.Trim())}” is not an absolute IRI: an id has a scheme, as “https://…”, “urn:uuid:…” or “tag:…”.");

        foreach (var date in element.Elements().Where(e => e.Name == Atom("updated") || e.Name == Atom("published")))
            if (!IsRfc3339(date.Value.Trim()))
                report(date, Severity.Error, "atom.bad-date",
                    $"“{Shorten(date.Value.Trim())}” is not an RFC 3339 date: use, for example, “2026-11-10T14:30:00Z”.");

        foreach (var link in element.Elements(Atom("link")))
            if (link.Attribute("href") == null)
                report(link, Severity.Error, "atom.missing-attribute", "Element “link” is missing required attribute “href”.");

        foreach (var person in element.Elements().Where(e => e.Name == Atom("author") || e.Name == Atom("contributor")))
            if (person.Element(Atom("name")) == null)
                report(person, Severity.Error, "atom.missing-element",
                    $"Element “{person.Name.LocalName}” is missing required child element “name”.");
    }

    // RFC 3339: 2026-11-10T14:30:00Z, 2026-11-10T14:30:00.5+01:00. A leap second (":60") is valid there, though .NET
    // does not take it: it is checked as second 59
    private static bool IsRfc3339(string value) =>
        Rfc3339().IsMatch(value) &&
        DateTimeOffset.TryParse(value.Length > 19 && value[17..19] == "60" ? value[..17] + "59" + value[19..] : value,
            CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    #endregion

    private static void CheckUrl(XElement? element, Reporter report)
    {
        if (element != null && UrlProblem(element.Value.Trim()) is { } problem)
            report(element, Severity.Error, "rss.bad-url", problem);
    }

    // Why a value is not an absolute http(s) URL, or null
    private static string? UrlProblem(string url)
    {
        if (url.Length == 0) return "The URL is empty.";
        if (url.Any(char.IsWhiteSpace)) return $"“{Shorten(url)}” contains a space: use “%20” instead.";
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" && uri.Host.Length > 0
            ? null
            : $"“{Shorten(url)}” is not an absolute http or https URL.";
    }

    private static bool IsNonNegativeInteger(string value) =>
        value.Trim().Length is > 0 and < 19 && value.Trim().All(char.IsAsciiDigit);

    private static string Shorten(string text) => text.Length > 60 ? text[..60] + "…" : text;

    [GeneratedRegex(@"^((?<wd>Mon|Tue|Wed|Thu|Fri|Sat|Sun),\s*)?(?<d>[0-9]{1,2})\s+(?<mon>Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)\s+(?<y>[0-9]{2}|[0-9]{4})\s+(?<hh>[0-9]{2}):(?<mm>[0-9]{2})(:(?<ss>[0-9]{2}))?\s+(UT|GMT|EST|EDT|CST|CDT|MST|MDT|PST|PDT|Z|[A-IK-Y]|[+-][0-9]{4})$")]
    private static partial Regex Rfc822Date();

    [GeneratedRegex(@"^[0-9]{4}-[0-9]{2}-[0-9]{2}[Tt][0-9]{2}:[0-9]{2}:[0-9]{2}(\.[0-9]+)?([Zz]|[+-][0-9]{2}:[0-9]{2})$")]
    private static partial Regex Rfc3339();

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9+.-]*:\S+$")]
    private static partial Regex IriScheme();

    [GeneratedRegex(@"^[A-Za-z]{2,3}(-[A-Za-z0-9]{1,8})*$")]
    private static partial Regex LanguageCode();
}
