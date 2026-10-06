using System.Globalization;
using System.Net;
using System.Xml.Linq;

namespace FFAggerator.Infrastructure.Rss;

public interface INewsFeedReader
{
    Task<IReadOnlyList<RssItem>> FetchAsync(string url, CancellationToken cancellationToken);
}

public sealed class RssFeedReader : INewsFeedReader
{
    public const string HttpClientName = "Rss";

    private static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<RssFeedReader> _logger;

    public RssFeedReader(IHttpClientFactory httpFactory, ILogger<RssFeedReader> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RssItem>> FetchAsync(string url, CancellationToken cancellationToken)
    {
        var client = _httpFactory.CreateClient(HttpClientName);
        await using var stream = await client.GetStreamAsync(url, cancellationToken);
        var doc = await XDocument.LoadAsync(stream, LoadOptions.None, cancellationToken);
        var root = doc.Root;
        if (root is null)
        {
            return [];
        }

        if (root.Name.LocalName == "rss")
        {
            return ParseRss(root, url);
        }

        if (root.Name.LocalName == "feed")
        {
            return ParseAtom(root, url);
        }

        _logger.LogWarning("Unrecognized feed format at {Url}.", url);
        return [];
    }

    private static List<RssItem> ParseRss(XElement root, string url)
    {
        var channel = root.Element("channel");
        var feedSource = Decode(channel?.Element("title")?.Value) ?? new Uri(url).Host;
        var items = new List<RssItem>();
        if (channel is null)
        {
            return items;
        }

        foreach (var item in channel.Elements("item"))
        {
            var title = Decode(item.Element("title")?.Value);
            var link = item.Element("link")?.Value?.Trim()
                       ?? item.Element(Atom + "link")?.Attribute("href")?.Value;
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(link))
            {
                continue;
            }

            var source = Decode(item.Element("source")?.Value) ?? feedSource;
            items.Add(new RssItem(
                title,
                link,
                source,
                ParseDate(item.Element("pubDate")?.Value),
                Decode(item.Element("description")?.Value)));
        }

        return items;
    }

    private static List<RssItem> ParseAtom(XElement root, string url)
    {
        var feedSource = Decode(root.Element(Atom + "title")?.Value) ?? new Uri(url).Host;
        var items = new List<RssItem>();
        foreach (var item in root.Elements(Atom + "entry"))
        {
            var title = Decode(item.Element(Atom + "title")?.Value);
            var link = item.Elements(Atom + "link")
                .Select(l => l.Attribute("href")?.Value)
                .FirstOrDefault(h => !string.IsNullOrWhiteSpace(h));
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(link))
            {
                continue;
            }

            items.Add(new RssItem(
                title,
                link,
                feedSource,
                ParseDate(item.Element(Atom + "updated")?.Value ?? item.Element(Atom + "published")?.Value),
                Decode(item.Element(Atom + "summary")?.Value)));
        }

        return items;
    }

    private static string? Decode(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : WebUtility.HtmlDecode(value.Trim());

    private static DateTimeOffset? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return parsed.ToUniversalTime();
        }

        return null;
    }
}
